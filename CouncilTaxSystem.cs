using Colossal.Logging;
using Game;
using Game.Simulation;

namespace CityCouncil
{
    /// <summary>
    /// Système "service" pur, sans état persisté : lit les taux d'imposition résidentiels par
    /// niveau d'éducation (via Game.Simulation.TaxSystem.GetResidentialTaxRate) et en déduit un
    /// bonus de mécontentement pour Populiste + GaucheRadicale, city-wide. Même famille que
    /// CouncilEconomySystem (chômage) : rien n'est sauvegardé, tout est recalculé à la demande.
    ///
    /// Deux mécanismes MUTUELLEMENT EXCLUSIFS (jamais cumulés), avec priorité au premier :
    ///   1) Colère populaire : les 3 tranches basses (SansInstruction/PeuInstruits/Instruits)
    ///      sont TOUTES individuellement > TaxHighThresholdPct -> bonus interpolé sur leur
    ///      MOYENNE, de PopulistBonusAtThreshold (au seuil) à PopulistBonusAtMax (au plafond).
    ///   2) Colère anti-inégalité (seulement si 1) n'est pas déclenché) : la moyenne des 3
    ///      tranches basses dépasse la moyenne des 2 tranches hautes (InstructionAvancee/
    ///      InstructionSuperieure) -> bonus interpolé sur l'écart en points, de
    ///      InequalityBonusAtOnePoint (1 point d'écart) à InequalityBonusAtMaxPoints (10 points
    ///      ou plus, plafond).
    /// </summary>
    public partial class CouncilTaxSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        // --- Job levels : ordre supposé de Game.Citizens.Worker.m_Level (0..4), à vérifier
        // en jeu si les bonus semblent inversés/décalés — même remarque que pour CountHouseholdDataSystem. ---
        private const int JobLevel_SansInstruction = 0;
        private const int JobLevel_PeuInstruits = 1;
        private const int JobLevel_Instruits = 2;
        private const int JobLevel_InstructionAvancee = 3;
        private const int JobLevel_InstructionSuperieure = 4;

        // --- Cas 1 : colère populaire (tranches basses sur-taxées) ---
        public const float TaxHighThresholdPct = 10f;   // seuil déclencheur (%), les 3 tranches basses doivent le dépasser
        public const float TaxHighMaxPct = 30f;          // plafond du panneau Économie du jeu (%)
        public const float PopulistBonusAtThreshold = 0.50f; // bonus au seuil (10%)
        public const float PopulistBonusAtMax = 0.90f;        // bonus au plafond (30%)

        // --- Cas 2 : colère anti-inégalité (riches moins taxés que les pauvres) ---
        public const float InequalityMinDiffPoints = 1f;   // écart minimum (points) pour déclencher
        public const float InequalityMaxDiffPoints = 10f;  // écart au-delà duquel le bonus plafonne
        public const float InequalityBonusGaucheAtOnePoint = 0.20f;
        public const float InequalityBonusGaucheAtMaxPoints = 0.60f;
        public const float InequalityBonusPopulisteAtOnePoint = 0.05f;
        public const float InequalityBonusPopulisteAtMaxPoints = 0.20f;

        private TaxSystem m_TaxSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TaxSystem = World.GetOrCreateSystemManaged<TaxSystem>();
        }

        protected override void OnUpdate() { } // purement passif, lu à la demande

        /// <summary>Taux d'imposition résidentiel courant pour un niveau d'éducation donné (%).</summary>
        private float GetRate(int jobLevel) => m_TaxSystem.GetResidentialTaxRate(jobLevel);

        /// <summary>
        /// Calcule les bonus de mécontentement actifs pour Populiste et GaucheRadicale
        /// séparément (0 pour les deux si aucun mécanisme n'est déclenché). Le cas 1 applique
        /// la MÊME valeur aux deux partis ; le cas 2 applique des réglages indépendants
        /// (GaucheRadicale historiquement plus sensible à l'écart de traitement fiscal).
        /// </summary>
        public (float populisteBonus, float gaucheRadicaleBonus) GetTaxDiscontentBonus()
        {
            float sansInstruction = GetRate(JobLevel_SansInstruction);
            float peuInstruits = GetRate(JobLevel_PeuInstruits);
            float instruits = GetRate(JobLevel_Instruits);
            float instructionAvancee = GetRate(JobLevel_InstructionAvancee);
            float instructionSuperieure = GetRate(JobLevel_InstructionSuperieure);

            // --- Cas 1 : priorité absolue ---
            bool allThreeAboveThreshold = sansInstruction > TaxHighThresholdPct
                && peuInstruits > TaxHighThresholdPct
                && instruits > TaxHighThresholdPct;

            if (allThreeAboveThreshold)
            {
                float avgLow = (sansInstruction + peuInstruits + instruits) / 3f;
                float bonus = InterpolateClamped(
                    avgLow, TaxHighThresholdPct, TaxHighMaxPct,
                    PopulistBonusAtThreshold, PopulistBonusAtMax);

                s_Log.Info($"[CouncilTaxSystem] Colère populaire active : moyenne tranches basses={avgLow:F1}%, bonus={bonus:P0}.");
                return (bonus, bonus);
            }

            // --- Cas 2 : seulement si le cas 1 n'est pas actif ---
            float avgLowAll = (sansInstruction + peuInstruits + instruits) / 3f;
            float avgHigh = (instructionAvancee + instructionSuperieure) / 2f;
            float diff = avgLowAll - avgHigh;

            if (diff >= InequalityMinDiffPoints)
            {
                float gaucheBonus = InterpolateClamped(
                    diff, InequalityMinDiffPoints, InequalityMaxDiffPoints,
                    InequalityBonusGaucheAtOnePoint, InequalityBonusGaucheAtMaxPoints);
                float populisteBonus = InterpolateClamped(
                    diff, InequalityMinDiffPoints, InequalityMaxDiffPoints,
                    InequalityBonusPopulisteAtOnePoint, InequalityBonusPopulisteAtMaxPoints);

                s_Log.Info($"[CouncilTaxSystem] Colère anti-inégalité active : écart={diff:F1} points, bonus Populiste={populisteBonus:P0}, bonus GaucheRadicale={gaucheBonus:P0}.");
                return (populisteBonus, gaucheBonus);
            }

            return (0f, 0f);
        }

        /// <summary>
        /// Interpolation linéaire clampée : value est ramené dans [fromValue, toValue], puis
        /// mappé proportionnellement sur [fromBonus, toBonus]. Si fromValue==toValue (garde-fou),
        /// retourne toBonus pour éviter une division par zéro.
        /// </summary>
        private static float InterpolateClamped(float value, float fromValue, float toValue, float fromBonus, float toBonus)
        {
            if (toValue <= fromValue) return toBonus;

            float clamped = System.Math.Clamp(value, fromValue, toValue);
            float t = (clamped - fromValue) / (toValue - fromValue);
            return fromBonus + t * (toBonus - fromBonus);
        }
    }
}