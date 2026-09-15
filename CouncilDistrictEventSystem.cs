using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Logging;
using Game;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;
using static CityCouncil.CouncilDistrictData;

namespace CityCouncil
{
    /// <summary>
    /// Tire périodiquement un évènement de DISTRICT (fait divers/scandale/évènement) parmi
    /// CouncilDistrictEventCatalog.Events, ciblant un district aléatoire éligible. Jusqu'à
    /// MaxConcurrentDistrictEvents évènements actifs simultanément (un seul par district — pas
    /// de cumul sur le même district), chacun actif pendant EventDurationDays.
    ///
    /// Volontairement NON sauvegardé — même choix assumé que CouncilCityEventSystem : effet
    /// ponctuel et cosmétique, sans impact sur l'intégrité de la sauvegarde. Toute la liste
    /// d'évènements actifs redémarre à zéro au chargement d'une partie.
    /// </summary>
    public partial class CouncilDistrictEventSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        // Même valeurs que CouncilCityEventSystem par défaut, ajustables indépendamment.
        private const float DailyEventChance = 0.08f;
        private const float FaitDiversWeight = 0.70f;
        private const float ScandaleWeight = 0.20f;
        private const float EvenementWeight = 0.10f;
        private const int EventDurationDays = 7;

        public const int MaxConcurrentDistrictEvents = 6;

        private struct ActiveDistrictEvent
        {
            public Entity m_District;
            public string m_EventId;
            public double m_ExpiryDay;
        }

        private readonly List<ActiveDistrictEvent> m_ActiveEvents = new();
        private SimulationSystem m_SimulationSystem;
        private EntityQuery m_DistrictQuery;
        private readonly Random m_Rng = new Random();

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_DistrictQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilDistrictData>());
        }

        // Même fréquence que CouncilCityEventSystem — pas besoin de vérifier à chaque frame.
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 4096;

        protected override void OnUpdate()
        {
            double currentDay = m_SimulationSystem.frameIndex / 262144.0;

            for (int i = m_ActiveEvents.Count - 1; i >= 0; i--)
            {
                if (currentDay < m_ActiveEvents[i].m_ExpiryDay) continue;
                s_Log.Info($"[CouncilDistrictEventSystem] Évènement '{m_ActiveEvents[i].m_EventId}' expiré (district {m_ActiveEvents[i].m_District.Index}).");
                m_ActiveEvents.RemoveAt(i);
            }

            if (m_ActiveEvents.Count >= MaxConcurrentDistrictEvents) return;
            if (m_Rng.NextDouble() >= DailyEventChance) return;

            TryRollNewEvent(currentDay);
        }

        private void TryRollNewEvent(double currentDay)
        {
            var eligibleDistricts = GetEligibleDistricts();
            if (eligibleDistricts.Count == 0) return; // AJOUT — aucun district éligible sur la carte, rien à faire

            var category = RollCategory();
            var candidates = CouncilDistrictEventCatalog.Events.Where(e => e.Category == category).ToList();
            if (candidates.Count == 0) return;

            var chosen = candidates[m_Rng.Next(candidates.Count)];
            var district = eligibleDistricts[m_Rng.Next(eligibleDistricts.Count)];

            m_ActiveEvents.Add(new ActiveDistrictEvent
            {
                m_District = district,
                m_EventId = chosen.Id,
                m_ExpiryDay = currentDay + EventDurationDays
            });

            s_Log.Info($"[CouncilDistrictEventSystem] Nouvel évènement : '{chosen.Id}' ({chosen.Category}) sur district {district.Index}, actif jusqu'au jour {currentDay + EventDurationDays:F1}.");
        }

        /// <summary>
        /// Districts éligibles : doivent avoir une élection déjà COMPLÈTE (donc un m_LeadingParty
        /// significatif — nécessaire pour l'effet LeadingPartyInDistrict), et ne pas déjà porter
        /// un évènement de district actif (un seul évènement par district à la fois).
        /// </summary>
        private List<Entity> GetEligibleDistricts()
        {
            var result = new List<Entity>();
            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    if (!EntityManager.HasComponent<CouncilDistrictData>(d)) continue;
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    if (data.m_Phase != ElectionPhase.Completed) continue;
                    if (m_ActiveEvents.Any(e => e.m_District == d)) continue;
                    result.Add(d);
                }
            }
            finally { districts.Dispose(); }
            return result;
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

        /// <summary>Retourne l'évènement actif pour CE district précis, ou null si aucun.</summary>
        public CouncilDistrictEventDefinition GetActiveEventForDistrict(Entity districtEntity)
        {
            foreach (var e in m_ActiveEvents)
            {
                if (e.m_District != districtEntity) continue;
                return CouncilDistrictEventCatalog.GetById(e.m_EventId);
            }
            return null;
        }

        /// <summary>OUTIL DE DEBUG TEMPORAIRE — force un roll immédiat, ignore la chance quotidienne.</summary>
        public void DebugForceRollDistrictEvent()
        {
            double currentDay = m_SimulationSystem.frameIndex / 262144.0;
            TryRollNewEvent(currentDay);
            s_Log.Info("[CouncilDistrictEventSystem] DEBUG : roll d'évènement de district forcé.");
        }
    }
}