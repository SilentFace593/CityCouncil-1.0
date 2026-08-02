using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;
using static CityCouncil.CouncilDistrictData;

namespace CityCouncil
{
    /// <summary>
    /// Gère les adhérents et la trésorerie des 5 partis. Deux mécanismes distincts :
    ///   - ApplyDistrictSeatDelta : appelé par CouncilElectionSystem à CHAQUE district qui
    ///     obtient de nouveaux résultats finaux -> ±10 adhérents par siège gagné/perdu dans
    ///     CE district précisément (signal local, immédiat).
    ///   - RunCycleCheck (interne, périodique) : une fois par cycle électoral complet (~7 jours
    ///     in-game), calcule la cotisation des adhérents (trésorerie) et applique le bonus de
    ///     +3% (parti vainqueur d'au moins un district, ou majoritaire ville) / malus de -2%
    ///     (sinon) — notion VILLE ENTIÈRE, donc volontairement découplée du traitement
    ///     par-district ci-dessus pour ne pas s'appliquer plusieurs fois par cycle.
    /// </summary>
    public partial class CouncilPartyMembershipSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        // --- Curseurs de gameplay, ajustables librement sans toucher au reste de la logique ---
        private const float CreditsPerMemberPerCycle = 1f;
        private const float WinBonusPct = 0.03f;
        private const float LoseMalusPct = 0.02f;
        private const float MembersPerSeatGained = 10f;
        private const float MembersPerSeatLost = 10f;

        // Même durée que le cycle électoral (CouncilElectionSystem.ElectionCycleDays) : le
        // contrôle "gagne au moins un district / est majoritaire" est réévalué au rythme d'un
        // mandat complet, pas plus souvent.
        private const double CycleCheckIntervalDays = 7.0;

        private EntityQuery m_SingletonQuery;
        private EntityQuery m_DistrictQuery;
        private SimulationSystem m_SimulationSystem;
        private Entity m_SingletonEntity = Entity.Null;

        private double m_LastCycleCheckDay = -1;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilPartyMembershipData>());
            m_DistrictQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilDistrictData>());
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            EnsureSingleton();
        }

        // Même remarque que sur CouncilElectionSystem : valeur non confirmée par décompilation.
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 4096;

        protected override void OnUpdate()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();

            double currentDay = (double)m_SimulationSystem.frameIndex / 262144.0;

            if (m_LastCycleCheckDay < 0)
            {
                // Amorce le compteur sans rien appliquer à la toute première frame utile
                // (évite un déclenchement immédiat et arbitraire au chargement).
                m_LastCycleCheckDay = currentDay;
                return;
            }

            if (currentDay - m_LastCycleCheckDay >= CycleCheckIntervalDays)
            {
                m_LastCycleCheckDay = currentDay;
                RunCycleCheck();
            }
        }

        private void EnsureSingleton()
        {
            var existing = m_SingletonQuery.ToEntityArray(Allocator.Temp);
            try
            {
                if (existing.Length == 1)
                {
                    m_SingletonEntity = existing[0];
                    return;
                }

                if (existing.Length > 1)
                {
                    // Même garde-fou que CouncilCustomPartySystem.EnsureSingleton : on garde
                    // l'entité dont le total d'adhérents cumulés est le plus élevé (signe de
                    // données réellement restaurées), on détruit le reste.
                    Entity keep = existing[0];
                    float bestTotal = -1f;
                    foreach (var e in existing)
                    {
                        var data = EntityManager.GetComponentData<CouncilPartyMembershipData>(e);
                        float total = 0f;
                        foreach (var entry in data.m_Entries) total += entry.m_Members;
                        if (total > bestTotal) { bestTotal = total; keep = e; }
                    }
                    foreach (var e in existing)
                    {
                        if (e != keep)
                        {
                            s_Log.Warn($"[CouncilPartyMembershipSystem] Entité singleton en doublon détruite : {e}");
                            EntityManager.DestroyEntity(e);
                        }
                    }
                    m_SingletonEntity = keep;
                    return;
                }
            }
            finally
            {
                existing.Dispose();
            }

            var initial = new CouncilPartyMembershipData { m_Entries = new FixedList512Bytes<PartyMembershipEntry>() };
            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty)))
                initial.m_Entries.Add(new PartyMembershipEntry { m_Party = p, m_Members = 0f, m_Treasury = 0 });

            m_SingletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(m_SingletonEntity, initial);
            s_Log.Info("[CouncilPartyMembershipSystem] Entité singleton créée (5 partis à 0 adhérent).");
        }

        public CouncilPartyMembershipData GetData()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            return EntityManager.GetComponentData<CouncilPartyMembershipData>(m_SingletonEntity);
        }

        private void SetData(CouncilPartyMembershipData data)
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            EntityManager.SetComponentData(m_SingletonEntity, data);
        }

        /// <summary>
        /// Appliqué à chaque district qui obtient de nouveaux résultats finaux (appelé par
        /// CouncilElectionSystem.FinalizeResults, AVANT écrasement de l'ancien résultat) :
        /// ±10 adhérents par siège gagné/perdu dans ce district précisément.
        /// </summary>
        public void ApplyDistrictSeatDelta(IEnumerable<PartyResult> oldResults, IEnumerable<PartyResult> newResults)
        {
            var oldSeats = oldResults.ToDictionary(r => r.m_Party, r => r.m_Seats);
            var newSeats = newResults.ToDictionary(r => r.m_Party, r => r.m_Seats);

            var data = GetData();
            var entries = data.m_Entries;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                oldSeats.TryGetValue(entry.m_Party, out int oldCount);
                newSeats.TryGetValue(entry.m_Party, out int newCount);
                int delta = newCount - oldCount;

                if (delta > 0)
                    entry.m_Members += delta * MembersPerSeatGained;
                else if (delta < 0)
                    entry.m_Members = MathF.Max(0f, entry.m_Members + delta * MembersPerSeatLost);

                entries[i] = entry;
            }

            data.m_Entries = entries;
            SetData(data);
        }

        /// <summary>
        /// Contrôle ville entière périodique : cotisation des adhérents (trésorerie) puis
        /// bonus +3% (parti vainqueur d'au moins un district, ou majoritaire en sièges ville
        /// entière) / malus -2% (sinon). Ne fait rien si aucune élection n'est encore terminée
        /// nulle part (état de tout début de partie).
        /// </summary>
        private void RunCycleCheck()
        {
            var partiesWithAtLeastOneDistrict = new HashSet<PoliticalParty>();
            var totalSeatsByParty = new Dictionary<PoliticalParty, int>();

            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    var districtData = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    if (districtData.m_Phase != ElectionPhase.Completed) continue;

                    partiesWithAtLeastOneDistrict.Add(districtData.m_LeadingParty);

                    foreach (var r in districtData.m_FinalResults)
                    {
                        totalSeatsByParty.TryGetValue(r.m_Party, out int current);
                        totalSeatsByParty[r.m_Party] = current + r.m_Seats;
                    }
                }
            }
            finally
            {
                districts.Dispose();
            }

            if (totalSeatsByParty.Count == 0) return;

            PoliticalParty cityMajority = totalSeatsByParty.OrderByDescending(kv => kv.Value).First().Key;

            var data = GetData();
            var entries = data.m_Entries;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];

                entry.m_Treasury += (int)MathF.Floor(entry.m_Members * CreditsPerMemberPerCycle);

                bool wonAtLeastOneDistrict = partiesWithAtLeastOneDistrict.Contains(entry.m_Party);
                bool isCityMajority = entry.m_Party == cityMajority;

                entry.m_Members *= (wonAtLeastOneDistrict || isCityMajority) ? (1f + WinBonusPct) : (1f - LoseMalusPct);
                entry.m_Members = MathF.Max(0f, entry.m_Members);

                entries[i] = entry;
            }

            data.m_Entries = entries;
            SetData(data);

            s_Log.Info("[CouncilPartyMembershipSystem] Cycle d'adhérents traité (cotisations + bonus/malus).");
        }
    }
}