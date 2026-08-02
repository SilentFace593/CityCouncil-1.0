using System;
using System.Linq;
using Colossal.Logging;
using Game;
using Game.Simulation;

namespace CityCouncil
{
    /// <summary>
    /// Tire périodiquement un évènement (fait divers / scandale / évènement) parmi
    /// CouncilEventCatalog.Events, et le garde actif pendant EventDurationDays.
    ///
    /// Volontairement NON sauvegardé (pas de component ECS/ISerializable) : un évènement en
    /// cours redémarre à zéro au chargement d'une partie. C'est un choix assumé, pas un oubli —
    /// l'effet est ponctuel et cosmétique, sans impact sur l'intégrité de la sauvegarde ; lui
    /// donner une persistance ECS ajouterait de la complexité (entité singleton, sérialisation,
    /// versioning) pour un bénéfice quasi nul. Si tu veux le rendre persistant plus tard, le
    /// pattern à suivre est celui de CouncilDistrictData (IComponentData + ISerializable) sur
    /// une entité singleton créée au premier OnUpdate.
    /// </summary>
    public partial class CouncilCityEventSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        // Probabilité qu'un nouvel évènement se déclenche à chaque vérification (~1x/jour in-game
        // au rythme de GetUpdateInterval). Purement un curseur de gameplay, ajuste librement.
        private const float DailyEventChance = 0.08f; // ~1 évènement tous les 12-13 jours en moyenne

        // Poids relatifs des catégories quand un évènement se déclenche (n'ont pas besoin de
        // sommer à 1, c'est juste des poids relatifs).
        private const float FaitDiversWeight = 0.70f;
        private const float ScandaleWeight = 0.20f;
        private const float EvenementWeight = 0.10f;

        private const int EventDurationDays = 7; // reste actif ~1 cycle électoral complet

        private SimulationSystem m_SimulationSystem;
        private readonly Random m_Rng = new Random();

        private string m_ActiveEventId;
        private double m_ExpiryDay = -1;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
        }

        // Même fréquence que CouncilElectionSystem — pas besoin de vérifier à chaque frame.
        // NOTE : valeur non confirmée par décompilation, même remarque que sur CouncilElectionSystem.
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 4096;

        protected override void OnUpdate()
        {
            double currentDay = m_SimulationSystem.frameIndex / 262144.0; // TimeSystem.kTicksPerDay, confirmé

            if (!string.IsNullOrEmpty(m_ActiveEventId) && currentDay >= m_ExpiryDay)
            {
                s_Log.Info($"[CouncilCityEventSystem] Évènement '{m_ActiveEventId}' expiré.");
                m_ActiveEventId = null;
            }

            if (string.IsNullOrEmpty(m_ActiveEventId) && m_Rng.NextDouble() < DailyEventChance)
            {
                TryRollNewEvent(currentDay);
            }
        }

        private void TryRollNewEvent(double currentDay)
        {
            var category = RollCategory();
            var candidates = CouncilEventCatalog.Events.Where(e => e.Category == category).ToList();
            if (candidates.Count == 0) return; // catégorie vide, rien à tirer cette fois

            var chosen = candidates[m_Rng.Next(candidates.Count)];
            m_ActiveEventId = chosen.Id;
            m_ExpiryDay = currentDay + EventDurationDays;

            s_Log.Info($"[CouncilCityEventSystem] Nouvel évènement : '{chosen.Id}' ({chosen.Category}), actif jusqu'au jour {m_ExpiryDay:F1}.");
        }

        private EventCategory RollCategory()
        {
            float total = FaitDiversWeight + ScandaleWeight + EvenementWeight;
            float roll = (float)m_Rng.NextDouble() * total;
            if (roll < FaitDiversWeight) return EventCategory.FaitDivers;
            roll -= FaitDiversWeight;
            if (roll < ScandaleWeight) return EventCategory.Scandale;
            return EventCategory.Evenement;
        }

        /// <summary>Retourne l'évènement actif, ou null si aucun n'est en cours.</summary>
        public CouncilEventDefinition GetActiveEventDefinition()
        {
            if (string.IsNullOrEmpty(m_ActiveEventId)) return null;
            return CouncilEventCatalog.Events.FirstOrDefault(e => e.Id == m_ActiveEventId);
        }
    }
}