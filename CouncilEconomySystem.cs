using Colossal.Logging;
using Game;
using Game.Simulation;

namespace CityCouncil
{
    /// <summary>
    /// Système "service" pur, sans état persisté : expose le taux de chômage actuel de la ville,
    /// utilisé par VoteCalculator pour le bonus Populiste au-delà du seuil (cf. UnemploymentThresholdPct).
    /// Aucune donnée sauvegardée ici — la valeur est recalculée à la demande, comme
    /// CouncilBonusSystem.GetCityMajorityParty ou CouncilElectionSystem.GetCityLeadingParty.
    /// </summary>
    public partial class CouncilEconomySystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        // Seuil déclenchant le bonus Populiste, et amplitude du bonus — ajustables librement.
        public const float UnemploymentThresholdPct = 7f; // 7%
        public const float PopulisteUnemploymentBonusPct = 0.30f;

        private CountHouseholdDataSystem m_CountHouseholdDataSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_CountHouseholdDataSystem = World.GetOrCreateSystemManaged<CountHouseholdDataSystem>();
        }

        protected override void OnUpdate() { } // purement passif, lu à la demande

        /// <summary>
        /// Retourne le taux de chômage actuel de la ville, en POURCENTAGE 0..100 (même unité que
        /// CountHouseholdDataSystem.UnemploymentRate, lue directement — c'est le même calcul que
        /// celui affiché dans le panneau "Population" du jeu).
        /// </summary>
        public float GetCurrentUnemploymentRate()
        {

            return m_CountHouseholdDataSystem.UnemploymentRate;
        }

        public bool IsUnemploymentCrisisActive()
        {
            return GetCurrentUnemploymentRate() > UnemploymentThresholdPct;
        }

    }

}