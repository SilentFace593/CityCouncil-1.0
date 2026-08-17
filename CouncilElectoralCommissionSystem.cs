using System;
using System.Collections.Generic;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;

namespace CityCouncil
{
    /// <summary>
    /// Gère la vigilance de la Commission Électorale (par parti, point 1) et déclenche les
    /// contrôles de détection des campagnes illégales de district, au même rythme que le cycle
    /// électoral (7 jours, point 3). En cas de détection : sanction city-wide temporaire (-10%,
    /// 7 jours), amende de 50000 crédits, et fermeture forcée de la caisse noire avec perte du
    /// solde. Applique la même mécanique aux IA (point 9), sans caisse noire à fermer pour elles.
    /// </summary>
    public partial class CouncilElectoralCommissionSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        private const double CycleIntervalDays = 7.0;
        private const int FineAmount = 50000;
        private const float SanctionMalusPercent = 0.10f;
        private const double SanctionDurationDays = 7.0;

        private EntityQuery m_SingletonQuery;
        private SimulationSystem m_SimulationSystem;
        private CouncilPropagandaSystem m_PropagandaSystem;
        private CouncilBlackFundSystem m_BlackFundSystem;
        private CouncilPartyMembershipSystem m_MembershipSystem;
        private readonly Random m_Rng = new Random();

        private Entity m_SingletonEntity = Entity.Null;
        private double m_LastCycleDay = -1;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilElectoralCommissionData>());
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_PropagandaSystem = World.GetOrCreateSystemManaged<CouncilPropagandaSystem>();
            m_BlackFundSystem = World.GetOrCreateSystemManaged<CouncilBlackFundSystem>();
            m_MembershipSystem = World.GetOrCreateSystemManaged<CouncilPartyMembershipSystem>();
        }

        protected override void OnGamePreload(Purpose purpose, Game.GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            DestroyExistingSingleton();
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            EnsureSingleton();
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase) => 4096;

        protected override void OnUpdate()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            ExpireSanctionsIfNeeded();

            double currentDay = (double)m_SimulationSystem.frameIndex / 262144.0;

            if (m_LastCycleDay < 0)
            {
                m_LastCycleDay = currentDay;
                return;
            }

            if (currentDay - m_LastCycleDay >= CycleIntervalDays)
            {
                m_LastCycleDay = currentDay;
                RunDetectionCheck(currentDay);
            }
        }

        private void DestroyExistingSingleton()
        {
            var existing = m_SingletonQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var e in existing)
                    EntityManager.DestroyEntity(e);
            }
            finally { existing.Dispose(); }
            m_SingletonEntity = Entity.Null;
        }

        private void EnsureSingleton()
        {
            var existing = m_SingletonQuery.ToEntityArray(Allocator.Temp);
            try
            {
                if (existing.Length == 1) { m_SingletonEntity = existing[0]; return; }
                if (existing.Length > 1)
                {
                    Entity keep = existing[0];
                    foreach (var e in existing)
                        if (e != keep) { s_Log.Warn($"[CouncilElectoralCommissionSystem] Doublon détruit : {e}"); EntityManager.DestroyEntity(e); }
                    m_SingletonEntity = keep;
                    return;
                }
            }
            finally { existing.Dispose(); }

            var initial = new CouncilElectoralCommissionData
            {
                m_VigilanceEntries = new FixedList512Bytes<VigilanceEntry>(),
                m_Sanctions = new FixedList512Bytes<SanctionEntry>()
            };
            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty)))
                initial.m_VigilanceEntries.Add(new VigilanceEntry { m_Party = p, m_Level = VigilanceLevel.Low });

            m_SingletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(m_SingletonEntity, initial);
            s_Log.Info("[CouncilElectoralCommissionSystem] Entité singleton créée (5 partis, vigilance Low).");
        }

        public CouncilElectoralCommissionData GetData()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            return EntityManager.GetComponentData<CouncilElectoralCommissionData>(m_SingletonEntity);
        }

        private void SetData(CouncilElectoralCommissionData data)
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            EntityManager.SetComponentData(m_SingletonEntity, data);
        }

        public VigilanceLevel GetVigilance(PoliticalParty party)
        {
            foreach (var e in GetData().m_VigilanceEntries)
                if (e.m_Party == party) return e.m_Level;
            return VigilanceLevel.Low;
        }

        private void SetVigilance(PoliticalParty party, VigilanceLevel level)
        {
            var data = GetData();
            var entries = data.m_VigilanceEntries;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].m_Party != party) continue;
                var e = entries[i];
                e.m_Level = level;
                entries[i] = e;
                break;
            }
            data.m_VigilanceEntries = entries;
            SetData(data);
        }

        /// <summary>Sanctions city-wide actives pour un parti (utilisé par CouncilElectionSystem).</summary>
        public List<SanctionEntry> GetActiveSanctionsForParty(PoliticalParty party, double currentDay)
        {
            var result = new List<SanctionEntry>();
            foreach (var s in GetData().m_Sanctions)
                if (s.m_Party == party && currentDay < s.m_ExpiryDay) result.Add(s);
            return result;
        }

        private void ExpireSanctionsIfNeeded()
        {
            double currentDay = (double)m_SimulationSystem.frameIndex / 262144.0;
            var data = GetData();
            var kept = new FixedList512Bytes<SanctionEntry>();
            bool changed = false;
            foreach (var s in data.m_Sanctions)
            {
                if (currentDay < s.m_ExpiryDay) kept.Add(s);
                else changed = true;
            }
            if (changed)
            {
                data.m_Sanctions = kept;
                SetData(data);
            }
        }

        /// <summary>
        /// Contrôle périodique (7 jours) : pour chaque parti, évalue le nombre de campagnes
        /// illégales actives au moment du check et applique détection/escalade/érosion (point 3).
        /// </summary>
        private void RunDetectionCheck(double currentDay)
        {
            foreach (PoliticalParty party in Enum.GetValues(typeof(PoliticalParty)))
            {
                int activeIllegalCount = m_PropagandaSystem.CountActiveIllegalCampaignsForParty(party);
                var currentLevel = GetVigilance(party);

                if (activeIllegalCount == 0)
                {
                    // Érosion : -1 niveau si calme depuis le dernier contrôle.
                    if (currentLevel > VigilanceLevel.Low)
                    {
                        var eroded = (VigilanceLevel)((byte)currentLevel - 1);
                        SetVigilance(party, eroded);
                        s_Log.Info($"[CouncilElectoralCommissionSystem] Vigilance de {party} en baisse : {eroded}.");
                    }
                    continue;
                }

                var (detectionChance, escalateChance, maxLevelThisCheck) = IllegalCampaignCatalog.GetRisk(activeIllegalCount);

                bool detected = m_Rng.NextDouble() < detectionChance;
                bool escalates = m_Rng.NextDouble() < escalateChance;

                if (escalates && currentLevel < maxLevelThisCheck)
                {
                    var newLevel = (VigilanceLevel)Math.Min((byte)maxLevelThisCheck, (byte)currentLevel + 1);
                    SetVigilance(party, newLevel);
                    s_Log.Info($"[CouncilElectoralCommissionSystem] Vigilance de {party} en hausse : {newLevel} ({activeIllegalCount} campagne(s) illégale(s) actives).");
                }

                if (detected)
                    ApplyDetectionPenalty(party, currentDay);

                s_Log.Info($"[CouncilElectoralCommissionSystem] Contrôle {party} : {activeIllegalCount} campagne(s) illégale(s), " +
                           $"détection={detected} (risque {detectionChance:P0}).");
            }
        }

        /// <summary>
        /// Applique la sanction complète (point : sanction -10% ville 7j, amende 50000, fermeture
        /// de la caisse noire avec perte du solde). Idempotent : une amende s'applique même si le
        /// solde du parti est insuffisant (dette assumée, cohérent avec une amende réelle).
        /// </summary>
        private void ApplyDetectionPenalty(PoliticalParty party, double currentDay)
        {
            var data = GetData();
            var sanctions = data.m_Sanctions;
            sanctions.Add(new SanctionEntry
            {
                m_Party = party,
                m_MalusPercent = SanctionMalusPercent,
                m_ExpiryDay = currentDay + SanctionDurationDays
            });
            data.m_Sanctions = sanctions;
            SetData(data);

            // Amende : débit direct (peut rendre la trésorerie négative, assumé comme une dette).
            m_MembershipSystem.AddTreasury(party, -FineAmount, CouncilPartyMembershipSystem.TreasurySource.CityFunding);

            // Fermeture forcée de la caisse noire (perte du solde gérée dans Close()).
            m_BlackFundSystem.Close(party);

            s_Log.Warn($"[CouncilElectoralCommissionSystem] {party} DÉTECTÉ par la Commission Électorale : " +
                       $"sanction -{SanctionMalusPercent:P0} ({SanctionDurationDays}j), amende {FineAmount}, caisse noire fermée.");
        }

        /// <summary>OUTIL DE DEBUG TEMPORAIRE — force le contrôle immédiat, même pattern que les autres systèmes du mod.</summary>
        public void DebugForceDetectionCheck()
        {
            double currentDay = (double)m_SimulationSystem.frameIndex / 262144.0;
            m_LastCycleDay = currentDay - CycleIntervalDays - 0.001;
            s_Log.Info("[CouncilElectoralCommissionSystem] DEBUG : contrôle de détection forcé au prochain update.");
        }
    }
}