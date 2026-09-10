using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Unity.Collections;
using Unity.Entities;
using static CityCouncil.CouncilDistrictData;

namespace CityCouncil
{
    /// <summary>
    /// Suit les records historiques (plus haute valeur jamais atteinte par un parti) pour 6
    /// catégories, et détermine le détenteur actuel de chacune. Recalculé au même rythme que les
    /// autres systèmes périodiques du mod (~16x/jour in-game), ce qui capture chaque pic peu de
    /// temps après un scrutin sans avoir besoin de s'accrocher explicitement à
    /// CouncilElectionSystem.FinalizeResults.
    /// </summary>
    public partial class CouncilRecordSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        private EntityQuery m_SingletonQuery;
        private EntityQuery m_DistrictQuery;
        private CouncilPartyMembershipSystem m_MembershipSystem;
        private CouncilLawSystem m_LawSystem;
        private Entity m_SingletonEntity = Entity.Null;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilRecordData>());
            m_DistrictQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilDistrictData>());
            m_MembershipSystem = World.GetOrCreateSystemManaged<CouncilPartyMembershipSystem>();
            m_LawSystem = World.GetOrCreateSystemManaged<CouncilLawSystem>();
        }

        protected override void OnGamePreload(Purpose purpose, Game.GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            var existing = m_SingletonQuery.ToEntityArray(Allocator.Temp);
            try { foreach (var e in existing) EntityManager.DestroyEntity(e); }
            finally { existing.Dispose(); }
            m_SingletonEntity = Entity.Null;
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
            RunRecordCheck();
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
                        if (e != keep) { s_Log.Warn($"[CouncilRecordSystem] Doublon détruit : {e}"); EntityManager.DestroyEntity(e); }
                    m_SingletonEntity = keep;
                    return;
                }
            }
            finally { existing.Dispose(); }

            m_SingletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(m_SingletonEntity, new CouncilRecordData { m_Peaks = new FixedList512Bytes<RecordPeakEntry>() });
            s_Log.Info("[CouncilRecordSystem] Entité singleton créée (aucun record).");
        }

        public CouncilRecordData GetData()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            return EntityManager.GetComponentData<CouncilRecordData>(m_SingletonEntity);
        }

        private void SetData(CouncilRecordData data)
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            EntityManager.SetComponentData(m_SingletonEntity, data);
        }

        /// <summary>Met à jour le pic d'un parti pour une catégorie SI la valeur actuelle le dépasse (jamais de baisse).</summary>
        private void SubmitValue(ref CouncilRecordData data, RecordCategory category, PoliticalParty party, float currentValue)
        {
            var peaks = data.m_Peaks;
            for (int i = 0; i < peaks.Length; i++)
            {
                if (peaks[i].m_Category != category || peaks[i].m_Party != party) continue;
                if (currentValue > peaks[i].m_PeakValue)
                {
                    var e = peaks[i];
                    e.m_PeakValue = currentValue;
                    peaks[i] = e;
                    data.m_Peaks = peaks;
                }
                return;
            }
            if (currentValue > 0f)
            {
                peaks.Add(new RecordPeakEntry { m_Category = category, m_Party = party, m_PeakValue = currentValue });
                data.m_Peaks = peaks;
            }
        }

        private void RunRecordCheck()
        {
            var data = GetData();

            var seatsByParty = new Dictionary<PoliticalParty, int>();
            var bastionsByParty = new Dictionary<PoliticalParty, int>();
            int totalSeats = 0;

            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    if (!EntityManager.HasComponent<CouncilDistrictData>(d)) continue;
                    var dd = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    if (dd.m_Phase != ElectionPhase.Completed) continue;

                    totalSeats += dd.m_TotalSeats;
                    foreach (var r in dd.m_FinalResults)
                    {
                        seatsByParty.TryGetValue(r.m_Party, out int cur);
                        seatsByParty[r.m_Party] = cur + r.m_Seats;
                    }
                    if (dd.m_IsBastion)
                    {
                        bastionsByParty.TryGetValue(dd.m_BastionParty, out int cb);
                        bastionsByParty[dd.m_BastionParty] = cb + 1;
                    }
                }
            }
            finally { districts.Dispose(); }

            var membershipData = m_MembershipSystem.GetData();

            // Lois votées + abrogées : cumulatif, attribué au bloc proposeur/abrogateur (leader du bloc).
            var lawCounts = new Dictionary<PoliticalParty, int>();
            foreach (var r in m_LawSystem.GetHistory())
            {
                if (r.m_Outcome == LawRecordOutcome.Adopted)
                {
                    var p = (PoliticalParty)r.m_ProposerBlocKey;
                    lawCounts.TryGetValue(p, out int c);
                    lawCounts[p] = c + 1;
                }
                if (r.m_Repealed)
                {
                    var p = (PoliticalParty)r.m_RepealerBlocKey;
                    lawCounts.TryGetValue(p, out int c);
                    lawCounts[p] = c + 1;
                }
            }

            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty)))
            {
                float sharePercent = totalSeats > 0 && seatsByParty.TryGetValue(p, out int s) ? (s * 100f / totalSeats) : 0f;
                SubmitValue(ref data, RecordCategory.CouncilSharePercent, p, sharePercent);

                int bastionCount = bastionsByParty.TryGetValue(p, out int b) ? b : 0;
                SubmitValue(ref data, RecordCategory.BastionsHeld, p, bastionCount);

                int lawCount = lawCounts.TryGetValue(p, out int lc) ? lc : 0;
                SubmitValue(ref data, RecordCategory.LawsVotedAbrogated, p, lawCount);

                foreach (var m in membershipData.m_Entries)
                {
                    if (m.m_Party != p) continue;
                    SubmitValue(ref data, RecordCategory.MembersCount, p, m.m_Members);
                    SubmitValue(ref data, RecordCategory.Treasury, p, m.m_Treasury);
                    SubmitValue(ref data, RecordCategory.PropagandaSpent, p, (float)m.m_TotalSpentPropaganda);
                    break;
                }
            }

            SetData(data);
        }

        /// <summary>Détenteur actuel du record (plus haut pic parmi tous les partis), ou false si aucune valeur n'a jamais dépassé 0.</summary>
        public bool TryGetHolder(RecordCategory category, out PoliticalParty holder, out float value)
        {
            var data = GetData();
            holder = default;
            value = 0f;
            bool found = false;

            foreach (var e in data.m_Peaks)
            {
                if (e.m_Category != category) continue;
                if (!found || e.m_PeakValue > value)
                {
                    value = e.m_PeakValue;
                    holder = e.m_Party;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Nombre de records actuellement détenus par un parti — utilisé par CouncilScoreSystem (200 pts/record).</summary>
        public int GetRecordsHeldCount(PoliticalParty party)
        {
            int count = 0;
            foreach (RecordCategory cat in Enum.GetValues(typeof(RecordCategory)))
                if (TryGetHolder(cat, out var holder, out _) && holder == party) count++;
            return count;
        }
    }
}