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
    /// Gère le score des 5 partis (slots politiques) en deux couches distinctes :
    ///   - Trophées (persistés, jamais décrémentés) : conquête de district (+150, sur changement
    ///     de leader), conquête de majorité générale (+500, sur changement de majorité, contrôlé
    ///     périodiquement au même rythme que CouncilBonusSystem.RunMajorityCheck).
    ///   - Possession (recalculée à la volée, jamais stockée) : sièges/bastions/districts
    ///     actuellement détenus, obtenus par un simple scan des districts — toujours exacte,
    ///     insensible à un ajout tardif du système en cours de partie.
    /// </summary>
    public partial class CouncilScoreSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        private const double CycleIntervalDays = 7.0;

        private EntityQuery m_SingletonQuery;
        private EntityQuery m_DistrictQuery;
        private SimulationSystem m_SimulationSystem;

        private Entity m_SingletonEntity = Entity.Null;
        private double m_LastCycleDay = -1;
        private CouncilPartyMembershipSystem m_MembershipSystem;
        private CouncilReinforcedBastionSystem m_ReinforcedBastionSystem;
        private CouncilRecordSystem m_RecordSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilScoreData>());
            m_DistrictQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilDistrictData>());
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_MembershipSystem = World.GetOrCreateSystemManaged<CouncilPartyMembershipSystem>();
            m_ReinforcedBastionSystem = World.GetOrCreateSystemManaged<CouncilReinforcedBastionSystem>();
            m_RecordSystem = World.GetOrCreateSystemManaged<CouncilRecordSystem>();
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

            double currentDay = (double)m_SimulationSystem.frameIndex / 262144.0;

            if (m_LastCycleDay < 0)
            {
                m_LastCycleDay = currentDay;
                return;
            }

            if (currentDay - m_LastCycleDay >= CycleIntervalDays)
            {
                m_LastCycleDay = currentDay;
                RunGeneralElectionCheck();
            }
        }

        private void DestroyExistingSingleton()
        {
            var existing = m_SingletonQuery.ToEntityArray(Allocator.Temp);
            try { foreach (var e in existing) EntityManager.DestroyEntity(e); }
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
                        if (e != keep) { s_Log.Warn($"[CouncilScoreSystem] Doublon détruit : {e}"); EntityManager.DestroyEntity(e); }
                    m_SingletonEntity = keep;
                    return;
                }
            }
            finally { existing.Dispose(); }

            var initial = new CouncilScoreData
            {
                m_TrophyEntries = new FixedList512Bytes<ScoreEntry>(),
                m_CoalitionEntries = new FixedList512Bytes<ScoreEntry>(),
                m_LawEntries = new FixedList512Bytes<ScoreEntry>(),
                m_HasLastGeneralMajority = false
            };
            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty)))
            {
                initial.m_TrophyEntries.Add(new ScoreEntry { m_Party = p, m_TrophyScore = 0 });
                initial.m_CoalitionEntries.Add(new ScoreEntry { m_Party = p, m_TrophyScore = 0 });
                initial.m_LawEntries.Add(new ScoreEntry { m_Party = p, m_TrophyScore = 0 });
            }

            m_SingletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(m_SingletonEntity, initial);
            s_Log.Info("[CouncilScoreSystem] Entité singleton créée (5 partis, 0 trophée).");
        }

        public CouncilScoreData GetData()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            return EntityManager.GetComponentData<CouncilScoreData>(m_SingletonEntity);
        }

        private void SetData(CouncilScoreData data)
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            EntityManager.SetComponentData(m_SingletonEntity, data);
        }

        private void AddTrophyScore(PoliticalParty party, long delta)
        {
            if (delta == 0) return;
            var data = GetData();
            var entries = data.m_TrophyEntries;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].m_Party != party) continue;
                var e = entries[i];
                e.m_TrophyScore += delta;
                entries[i] = e;
                break;
            }
            data.m_TrophyEntries = entries;
            SetData(data);
        }

        public long GetTrophyScore(PoliticalParty party)
        {
            foreach (var e in GetData().m_TrophyEntries)
                if (e.m_Party == party) return e.m_TrophyScore;
            return 0;
        }

        /// <summary>
        /// Point d'entrée PUBLIC pour créditer un trophée depuis un autre système (ex.
        /// CouncilLawSystem : adoption/abrogation de loi, +500 points partagés/arrondis entre
        /// les membres d'un bloc). Même mécanique que les trophées internes (district/majorité
        /// générale) : jamais retiré, cumulatif. Nommée différemment d'AddTrophyScore (privée)
        /// pour marquer explicitement qu'il s'agit d'un point d'entrée externe au système.
        /// </summary>
        public void AddExternalTrophyScore(PoliticalParty party, long amount)
        {
            AddTrophyScore(party, amount);
        }

        /// <summary>
        /// Crédite le score "Coalitions conclues" d'un parti — catégorie séparée des trophées
        /// (district/majorité générale), appelée par CouncilCoalitionSystem.CommitCoalition.
        /// </summary>
        public void AddCoalitionScore(PoliticalParty party, long amount)
        {
            var data = GetData();
            AddToScoreList(ref data.m_CoalitionEntries, party, amount);
            SetData(data);
        }

        public long GetCoalitionScore(PoliticalParty party)
        {
            foreach (var e in GetData().m_CoalitionEntries)
                if (e.m_Party == party) return e.m_TrophyScore;
            return 0;
        }

        /// <summary>
        /// Crédite le score "Lois votées" d'un parti — catégorie séparée des trophées, appelée
        /// par CouncilLawSystem.GrantLawScore (adoption ou abrogation réussie).
        /// </summary>
        public void AddLawScore(PoliticalParty party, long amount)
        {
            var data = GetData();
            AddToScoreList(ref data.m_LawEntries, party, amount);
            SetData(data);
        }

        public long GetLawScore(PoliticalParty party)
        {
            foreach (var e in GetData().m_LawEntries)
                if (e.m_Party == party) return e.m_TrophyScore;
            return 0;
        }

        /// <summary>
        /// Helper générique partagé par AddCoalitionScore/AddLawScore : incrémente l'entrée d'un
        /// parti dans une liste "parti -> points", ou l'ajoute si absente (compat v1, listes vides
        /// au premier chargement d'une sauvegarde antérieure à ce système).
        /// </summary>
        private static void AddToScoreList(ref FixedList512Bytes<ScoreEntry> list, PoliticalParty party, long delta)
        {
            if (delta == 0) return;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].m_Party != party) continue;
                var e = list[i];
                e.m_TrophyScore += delta;
                list[i] = e;
                return;
            }
            list.Add(new ScoreEntry { m_Party = party, m_TrophyScore = delta });
        }

        /// <summary>
        /// Conquête de district (+150 points, trophée) — appelé par CouncilElectionSystem
        /// UNIQUEMENT quand le leader du district change réellement (première élection du
        /// district, ou changement de vainqueur par rapport au cycle précédent). Une reconduction
        /// du même parti ne redéclenche PAS le trophée (cf. "district possédé" ci-dessous pour
        /// la valeur de possession continue).
        /// </summary>
        public void ApplyDistrictConquest(PoliticalParty newLeader)
        {
            AddTrophyScore(newLeader, ScoreCatalog.PointsDistrictWon);
        }

        /// <summary>
        /// Contrôle périodique ville entière (7 jours, même duplication volontaire de
        /// GetCityMajorityParty que CouncilBonusSystem.RunMajorityCheck) : accorde le trophée
        /// "élection générale remportée" (+500) UNIQUEMENT si le parti majoritaire change par
        /// rapport au dernier contrôle — même sémantique "conquête" que les districts, pas un
        /// bonus reconduit à chaque cycle pour le même parti.
        /// </summary>
        private void RunGeneralElectionCheck()
        {
            var majority = GetCityMajorityParty();
            if (majority == null) return;

            var data = GetData();
            bool isNewConquest = !data.m_HasLastGeneralMajority || data.m_LastGeneralMajorityParty != majority.Value;

            data.m_HasLastGeneralMajority = true;
            data.m_LastGeneralMajorityParty = majority.Value;
            SetData(data);

            if (isNewConquest)
            {
                AddTrophyScore(majority.Value, ScoreCatalog.PointsGeneralElectionWon);
                s_Log.Info($"[CouncilScoreSystem] Élection générale conquise par {majority.Value} (+{ScoreCatalog.PointsGeneralElectionWon} points).");
            }
        }


        private PoliticalParty? GetCityMajorityParty()
        {
            var totals = new Dictionary<PoliticalParty, int>();
            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    if (!EntityManager.HasComponent<CouncilDistrictData>(d)) continue;
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    if (data.m_Phase != ElectionPhase.Completed) continue;

                    foreach (var r in data.m_FinalResults)
                    {
                        totals.TryGetValue(r.m_Party, out int current);
                        totals[r.m_Party] = current + r.m_Seats;
                    }
                }
            }
            finally { districts.Dispose(); }

            if (totals.Count == 0) return null;
            return totals.OrderByDescending(kv => kv.Value).First().Key;
        }

        /// <summary>
        /// Possession actuelle (sièges×10 + bastions×1000 + districts dirigés×300), recalculée
        /// EN DIRECT par scan des districts — jamais stockée, donc toujours exacte pour tous
        /// les partis, y compris rétroactivement si le système est ajouté en cours de partie.
        /// </summary>
        public Dictionary<PoliticalParty, long> GetPossessionScores()
        {
            var possession = new Dictionary<PoliticalParty, long>();
            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty))) possession[p] = 0;

            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    if (!EntityManager.HasComponent<CouncilDistrictData>(d)) continue;
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    if (data.m_Phase != ElectionPhase.Completed) continue;

                    foreach (var r in data.m_FinalResults)
                        possession[r.m_Party] += r.m_Seats * ScoreCatalog.PointsPerSeatHeld;

                    possession[data.m_LeadingParty] += ScoreCatalog.PointsPerDistrictHeld;

                    if (data.m_IsBastion)
                        possession[data.m_BastionParty] += ScoreCatalog.PointsPerBastionHeld;

                    if (m_ReinforcedBastionSystem.IsReinforcedBastion(data.m_BastionParty, d.Index))
                        possession[data.m_BastionParty] += ScoreCatalog.PointsPerReinforcedBastionHeld;
                }
            
            }
            finally { districts.Dispose(); }

            var membershipData = m_MembershipSystem.GetData();
            foreach (var entry in membershipData.m_Entries)
            {
                possession[entry.m_Party] += (long)MathF.Round(entry.m_Members) * ScoreCatalog.PointsPerMember;
            }

            return possession;
        }

        /// <summary>Score total (trophées + possession live) pour tous les partis.</summary>
        public Dictionary<PoliticalParty, long> GetTotalScores()
        {
            var possession = GetPossessionScores();
            var data = GetData();
            var totals = new Dictionary<PoliticalParty, long>();
            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty)))
            {
                long trophy = 0;
                foreach (var e in data.m_TrophyEntries) if (e.m_Party == p) { trophy = e.m_TrophyScore; break; }
                long coalition = 0;
                foreach (var e in data.m_CoalitionEntries) if (e.m_Party == p) { coalition = e.m_TrophyScore; break; }
                long law = 0;
                foreach (var e in data.m_LawEntries) if (e.m_Party == p) { law = e.m_TrophyScore; break; }
                long records = GetRecordsScore(p); // AJOUT
                totals[p] = trophy + coalition + law + records + possession[p];
            }
            return totals;
        }

        /// <summary>
        /// Détail décomposé du score de chaque parti (trophées + décompte brut de possession), pour
        /// affichage détaillé côté UI (onglet Score, ligne dépliable). Scan dédié des districts,
        /// volontairement indépendant de GetPossessionScores/GetTotalScores — même choix de
        /// découplage que documenté ailleurs dans le mod (cf. GetCityMajorityParty dupliqué).
        /// </summary>
        public Dictionary<PoliticalParty, PartyScoreBreakdown> GetScoreBreakdowns()
        {
            var counts = new Dictionary<PoliticalParty, (int seats, int districtsHeld, int bastionsHeld, int reinforcedBastionsHeld)>(); // MODIFIÉ
            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty))) counts[p] = (0, 0, 0, 0); // MODIFIÉ

            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    if (!EntityManager.HasComponent<CouncilDistrictData>(d)) continue;
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    if (data.m_Phase != ElectionPhase.Completed) continue;

                    foreach (var r in data.m_FinalResults)
                    {
                        var c = counts[r.m_Party];
                        c.seats += r.m_Seats;
                        counts[r.m_Party] = c;
                    }

                    var leaderCount = counts[data.m_LeadingParty];
                    leaderCount.districtsHeld += 1;
                    counts[data.m_LeadingParty] = leaderCount;

                    if (data.m_IsBastion)
                    {
                        var bastionCount = counts[data.m_BastionParty];
                        bastionCount.bastionsHeld += 1;

                        if (m_ReinforcedBastionSystem.IsReinforcedBastion(data.m_BastionParty, d.Index))
                            bastionCount.reinforcedBastionsHeld += 1;

                        counts[data.m_BastionParty] = bastionCount;
                    }
                }
            }
            finally { districts.Dispose(); }

          
            var membersCount = new Dictionary<PoliticalParty, int>();
            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty))) membersCount[p] = 0;
            var membershipData = m_MembershipSystem.GetData();
            foreach (var entry in membershipData.m_Entries)
                membersCount[entry.m_Party] = (int)MathF.Round(entry.m_Members);

            var trophyData = GetData();
            var result = new Dictionary<PoliticalParty, PartyScoreBreakdown>();
            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty)))
            {
                long trophy = 0;
                foreach (var e in trophyData.m_TrophyEntries)
                    if (e.m_Party == p) { trophy = e.m_TrophyScore; break; }

                long coalition = 0; // AJOUT
                foreach (var e in trophyData.m_CoalitionEntries)
                    if (e.m_Party == p) { coalition = e.m_TrophyScore; break; }

                long law = 0; // AJOUT
                foreach (var e in trophyData.m_LawEntries)
                    if (e.m_Party == p) { law = e.m_TrophyScore; break; }

                var c = counts[p];
                int members = membersCount[p];
                long possession = c.seats * ScoreCatalog.PointsPerSeatHeld
                                 + c.districtsHeld * ScoreCatalog.PointsPerDistrictHeld
                                 + c.bastionsHeld * ScoreCatalog.PointsPerBastionHeld
                                 + c.reinforcedBastionsHeld * ScoreCatalog.PointsPerReinforcedBastionHeld
                                 + members * ScoreCatalog.PointsPerMember;

                long records = GetRecordsScore(p);

                result[p] = new PartyScoreBreakdown
                {
                    TrophyScore = trophy,
                    CoalitionScore = coalition,
                    LawScore = law,
                    RecordsScore = records,
                    SeatsHeld = c.seats,
                    DistrictsHeld = c.districtsHeld,
                    BastionsHeld = c.bastionsHeld,
                    ReinforcedBastionsHeld = c.reinforcedBastionsHeld,
                    MembersCount = members,
                    PossessionScore = possession,
                    TotalScore = trophy + coalition + law + possession
                };
            }
            return result;
        }

        /// <summary>Remet les trophées d'un slot à 0 — appelé par CouncilCustomPartySystem lors d'une activation de substitution.</summary>
        public void ResetScore(PoliticalParty party)
        {
            var data = GetData();
            ResetInList(ref data.m_TrophyEntries, party);
            ResetInList(ref data.m_CoalitionEntries, party); // AJOUT
            ResetInList(ref data.m_LawEntries, party);        // AJOUT
            SetData(data);
        }

        private static void ResetInList(ref FixedList512Bytes<ScoreEntry> list, PoliticalParty party)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].m_Party != party) continue;
                var e = list[i];
                e.m_TrophyScore = 0;
                list[i] = e;
                return;
            }
        }

        /// <summary>OUTIL DE DEBUG TEMPORAIRE — force le contrôle d'élection générale immédiatement.</summary>
        public void DebugForceGeneralElectionCheck()
        {
            double currentDay = (double)m_SimulationSystem.frameIndex / 262144.0;
            m_LastCycleDay = currentDay - CycleIntervalDays - 0.001;
            RunGeneralElectionCheck();
            s_Log.Info("[CouncilScoreSystem] DEBUG : contrôle d'élection générale forcé immédiatement.");
        }

        /// <summary>Détail du score d'un parti, décomposé pour affichage dans l'onglet Score.</summary>
        public struct PartyScoreBreakdown
        {
            public long TrophyScore;
            public long CoalitionScore;
            public long LawScore;
            public long RecordsScore;
            public int SeatsHeld;
            public int DistrictsHeld;
            public int BastionsHeld;
            public int ReinforcedBastionsHeld;
            public int MembersCount;
            public long PossessionScore;
            public long TotalScore;
        }

        /// <summary>Score "Records" : 200 points par record actuellement détenu (live, jamais persisté — recalculé à chaque lecture comme la possession).</summary>
        public long GetRecordsScore(PoliticalParty party) => m_RecordSystem.GetRecordsHeldCount(party) * RecordCatalog.PointsPerRecordHeld;


    }
}
