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
    /// Gère la formation de coalitions ville entière quand aucun parti n'a la majorité absolue
    /// (+1 siège) des sièges du conseil municipal. Deux voies de formation :
    ///   - IA : tirage pondéré par affinité (CouncilCoalitionAffinity), une fois par cycle électoral
    ///     complet, uniquement si le joueur n'a pas déjà une proposition en cours ou acceptée.
    ///   - Joueur : propose librement un ou plusieurs partis via l'UI (HemicyclePanel), chaque parti
    ///     sollicité répond individuellement selon sa probabilité d'affinité avec le groupe déjà formé.
    /// Une coalition existante est entièrement réévaluée (dissoute puis reformable) à chaque nouveau
    /// cycle électoral, cohérent avec le fait que les rapports de sièges changent à chaque cycle.
    /// </summary>
    public partial class CouncilCoalitionSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        private const double CycleIntervalDays = 7.0; // même rythme que CouncilBonusSystem

        // Seuil minimal d'affinité moyenne pour qu'une coalition IA tente de se former du tout
        // (en dessous, on considère qu'aucun attelage n'est envisageable spontanément).
        private const float MinAffinityToAttempt = 0.20f;

        private EntityQuery m_SingletonQuery;
        private EntityQuery m_DistrictQuery;
        private SimulationSystem m_SimulationSystem;
        private CouncilCustomPartySystem m_CustomPartySystem;
        private readonly Random m_Rng = new Random();

        private Entity m_SingletonEntity = Entity.Null;
        private double m_LastCycleDay = -1;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilCoalitionData>());
            m_DistrictQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilDistrictData>());
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_CustomPartySystem = World.GetOrCreateSystemManaged<CouncilCustomPartySystem>();
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
                RunCoalitionCycle();
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
                        if (e != keep) { s_Log.Warn($"[CouncilCoalitionSystem] Doublon détruit : {e}"); EntityManager.DestroyEntity(e); }
                    m_SingletonEntity = keep;
                    return;
                }
            }
            finally { existing.Dispose(); }

            m_SingletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(m_SingletonEntity, new CouncilCoalitionData
            {
                m_Coalitions = new FixedList512Bytes<CoalitionEntry>(), // MODIFIÉ — remplace m_HasActiveCoalition/m_ActiveCoalition
                m_PlayerProposalTargets = new FixedList128Bytes<byte>(),
                m_PlayerProposalAccepted = new FixedList128Bytes<byte>()
            });
            s_Log.Info("[CouncilCoalitionSystem] Entité singleton créée (aucune coalition).");
        }

        public CouncilCoalitionData GetData()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            return EntityManager.GetComponentData<CouncilCoalitionData>(m_SingletonEntity);
        }

        private void SetData(CouncilCoalitionData data)
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            EntityManager.SetComponentData(m_SingletonEntity, data);
        }

        // --- Lecture d'état pour l'UI ---


        public bool IsPlayerProposalPending() => GetData().m_PlayerProposalPending;

        public List<PoliticalParty> GetPlayerProposalTargets()
        {
            var data = GetData();
            var result = new List<PoliticalParty>();
            foreach (var b in data.m_PlayerProposalTargets) result.Add((PoliticalParty)b);
            return result;
        }

        public List<PoliticalParty> GetPlayerProposalAccepted()
        {
            var data = GetData();
            var result = new List<PoliticalParty>();
            foreach (var b in data.m_PlayerProposalAccepted) result.Add((PoliticalParty)b);
            return result;
        }

        // --- Calcul de majorité, dupliqué volontairement (même choix que le reste du mod) ---

        private Dictionary<PoliticalParty, int> GetSeatsByParty()
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
            return totals;
        }

        /// <summary>true si un parti détient strictement plus de la moitié des sièges attribués.</summary>
        public bool HasAbsoluteMajority(out PoliticalParty majorityParty)
        {
            var seats = GetSeatsByParty();
            majorityParty = default;
            if (seats.Count == 0) return false;

            int total = seats.Values.Sum();
            var best = seats.OrderByDescending(kv => kv.Value).First();
            if (best.Value > total / 2)
            {
                majorityParty = best.Key;
                return true;
            }
            return false;
        }

        private void RunCoalitionCycle()
        {
            var data = GetData();
            data.m_Coalitions = new FixedList512Bytes<CoalitionEntry>(); // MODIFIÉ
            data.m_PlayerProposalPending = false;
            data.m_PlayerProposalTargets = new FixedList128Bytes<byte>();
            data.m_PlayerProposalAccepted = new FixedList128Bytes<byte>();
            data.m_AwaitingPlayerDecision = false;
            SetData(data);

            if (HasAbsoluteMajority(out var majority))
            {
                s_Log.Info($"[CouncilCoalitionSystem] {majority} détient la majorité absolue, aucune coalition possible ce cycle.");
                return;
            }

            var custom = m_CustomPartySystem.GetData();
            bool playerHasActiveParty = custom.m_Exists && custom.m_SubstitutionActive;

            if (!playerHasActiveParty)
            {
                s_Log.Info("[CouncilCoalitionSystem] Aucun parti joueur actif, formation IA immédiate.");
                TryFormAiCoalition();
                return;
            }

            data = GetData();
            data.m_AwaitingPlayerDecision = true;
            SetData(data);
            s_Log.Info("[CouncilCoalitionSystem] En attente de la décision du joueur (proposer ou renoncer) avant formation IA.");
        }


        /// <summary>
        /// Tirage IA, restreint aux partis NON déjà engagés dans une coalition existante (excludedParties).
        /// Appelé soit directement (aucun parti joueur actif), soit en cascade après une proposition
        /// joueur qui n'atteint pas la majorité, pour vérifier si les partis restants peuvent former un
        /// bloc rival capable, lui, d'atteindre la majorité (ou simplement la pluralité).
        /// </summary>
        private void TryFormAiCoalition(IEnumerable<PoliticalParty> excludedParties = null)
        {
            var excluded = new HashSet<PoliticalParty>(excludedParties ?? Enumerable.Empty<PoliticalParty>());
            var seats = GetSeatsByParty()
                .Where(kv => !excluded.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            if (seats.Count < 2) return; // pas assez de partis disponibles pour une coalition

            int totalSeats = GetSeatsByParty().Values.Sum(); // MODIFIÉ — total GLOBAL (tous partis), pas seulement les restants
            var ordered = seats.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();

            var coalition = new List<PoliticalParty> { ordered[0] };
            int coalitionSeats = seats[ordered[0]];
            var candidates = ordered.Skip(1).ToList();

            while (coalitionSeats <= totalSeats / 2 && candidates.Count > 0)
            {
                var best = candidates
                    .OrderByDescending(p => CouncilCoalitionAffinity.GetAverageAffinity(p, coalition))
                    .First();

                float affinity = CouncilCoalitionAffinity.GetAverageAffinity(best, coalition);
                candidates.Remove(best);

                if (affinity < MinAffinityToAttempt) continue;
                if (m_Rng.NextDouble() > affinity) continue;

                coalition.Add(best);
                coalitionSeats += seats[best];
            }

            // MODIFIÉ — accepte désormais aussi une coalition non majoritaire (bloc d'opposition),
            // du moment qu'au moins 2 partis se sont réellement associés : cohérent avec le fait
            // qu'une coalition du joueur non majoritaire est elle aussi acceptée (cf. TryProposeCoalition).
            if (coalition.Count < 2)
            {
                s_Log.Info("[CouncilCoalitionSystem] Aucune coalition IA viable formée avec les partis restants.");
                return;
            }

            var finalOrder = coalition.OrderByDescending(p => seats[p]).ToList();
            CommitCoalition(finalOrder, playerInitiated: false);
        }

        private void CommitCoalition(List<PoliticalParty> membersOrderedBySeats, bool playerInitiated)
        {
            var entry = new CoalitionEntry
            {
                m_Members = new FixedList128Bytes<byte>(),
                m_PlayerInitiated = playerInitiated
            };
            foreach (var p in membersOrderedBySeats) entry.m_Members.Add((byte)p);

            var data = GetData();
            data.m_Coalitions.Add(entry); // MODIFIÉ — ajout, ne remplace plus
            data.m_PlayerProposalPending = false;
            SetData(data);

            s_Log.Info($"[CouncilCoalitionSystem] Coalition formée ({(playerInitiated ? "joueur" : "IA")}) : {string.Join(", ", membersOrderedBySeats)}.");
        }

        // --- Proposition du joueur ---

        /// <summary>
        /// Le joueur sollicite un ensemble de partis. Chaque parti sollicité répond individuellement
        /// (immédiatement, tirage par affinité avec le parti joueur ET avec les autres membres déjà
        /// acceptés — ordre d'évaluation = ordre de la liste fournie). Refusé si un parti a déjà la
        /// majorité absolue, ou si le joueur n'a pas de parti actif.
        /// </summary>
        public bool TryProposeCoalition(IEnumerable<PoliticalParty> targets, out string error)
        {
            error = null;

            var custom = m_CustomPartySystem.GetData();
            if (!custom.m_Exists || !custom.m_SubstitutionActive)
            {
                error = "Aucun parti joueur actif.";
                return false;
            }

            var data = GetData();
            if (!data.m_AwaitingPlayerDecision)
            {
                error = "Aucune fenêtre de proposition de coalition n'est ouverte actuellement.";
                return false;
            }

            var playerParty = custom.m_ActiveSpace;
            var seats = GetSeatsByParty();
            var accepted = new List<PoliticalParty> { playerParty };
            var proposalTargets = new List<PoliticalParty>();

            foreach (var target in targets)
            {
                if (target == playerParty) continue;
                proposalTargets.Add(target);

                float affinity = CouncilCoalitionAffinity.GetAverageAffinity(target, accepted);
                bool accepts = m_Rng.NextDouble() <= affinity;
                s_Log.Info($"[CouncilCoalitionSystem] Proposition joueur -> {target} : affinité {affinity:P0}, réponse {(accepts ? "ACCEPTÉ" : "refusé")}.");
                if (accepts) accepted.Add(target);
            }

            data = GetData();
            data.m_AwaitingPlayerDecision = false;
            data.m_PlayerProposalTargets = new FixedList128Bytes<byte>();
            foreach (var p in proposalTargets) data.m_PlayerProposalTargets.Add((byte)p);
            data.m_PlayerProposalAccepted = new FixedList128Bytes<byte>();
            foreach (var p in accepted) data.m_PlayerProposalAccepted.Add((byte)p);
            SetData(data);

            if (accepted.Count < 2)
            {
                error = "Aucun parti n'a accepté de rejoindre la coalition.";
                TryFormAiCoalition(); // aucun parti engagé encore, l'IA tente librement
                return false;
            }

            var finalOrder = accepted.OrderByDescending(p => seats.TryGetValue(p, out int s) ? s : 0).ToList();
            CommitCoalition(finalOrder, playerInitiated: true);

            // AJOUT — la coalition du joueur est actée, mais si elle ne dépasse pas les autres partis
            // restants en sièges cumulés, ceux-ci peuvent à leur tour tenter de se coaliser entre eux
            // (cf. ton scénario : coalition joueur à 27, Rép+Pop pourraient atteindre 33).
            TryFormAiCoalition(excludedParties: accepted);

            return true;
        }

        /// <summary>
        /// Le joueur renonce explicitement à proposer une coalition ce cycle. Lève le gel et laisse
        /// l'IA tenter sa propre formation entre les partis restants (cf. discussion design : la
        /// formation IA reste gelée tant que le joueur n'a pas décidé, dans un sens ou dans l'autre).
        /// </summary>
        /// 
        public bool IsAwaitingPlayerDecision() => GetData().m_AwaitingPlayerDecision;

        public bool TryDeclineCoalitionProposal(out string error)
        {
            error = null;
            var data = GetData();

            if (!data.m_AwaitingPlayerDecision)
            {
                error = "Aucune décision en attente.";
                return false;
            }

            data.m_AwaitingPlayerDecision = false;
            SetData(data);

            s_Log.Info("[CouncilCoalitionSystem] Le joueur a renoncé à proposer une coalition ce cycle. Formation IA autorisée.");
            TryFormAiCoalition();
            return true;
        }

        /// <summary>Un "bloc" politique : soit un parti seul, soit une coalition, avec son total de sièges.</summary>
        public struct PoliticalBloc
        {
            public List<PoliticalParty> Members; // 1 élément = parti seul, 2+ = coalition
            public int Seats;
            public bool IsCoalition => Members.Count >= 2;
        }

        /// <summary>
        /// Calcule tous les blocs actuellement en présence (partis seuls + coalitions formées) et
        /// retourne celui qui cumule le plus de sièges — c'est LUI qui doit être affiché comme
        /// dirigeant l'hémicycle, coalition ou non, majoritaire ou non (même logique que l'actuel
        /// "parti en tête" qui n'exige pas non plus la majorité absolue).
        /// </summary>
        public PoliticalBloc GetLeadingBloc()
        {
            var seats = GetSeatsByParty();
            var data = GetData();

            var partyToCoalitionIndex = new Dictionary<PoliticalParty, int>();
            for (int i = 0; i < data.m_Coalitions.Length; i++)
                foreach (var b in data.m_Coalitions[i].m_Members)
                    partyToCoalitionIndex[(PoliticalParty)b] = i;

            var blocs = new List<PoliticalBloc>();

            // Coalitions formées
            for (int i = 0; i < data.m_Coalitions.Length; i++)
            {
                var members = new List<PoliticalParty>();
                int total = 0;
                foreach (var b in data.m_Coalitions[i].m_Members)
                {
                    var p = (PoliticalParty)b;
                    members.Add(p);
                    total += seats.TryGetValue(p, out int s) ? s : 0;
                }
                blocs.Add(new PoliticalBloc { Members = members, Seats = total });
            }

            // Partis non engagés dans une coalition = blocs solo
            foreach (var kv in seats)
            {
                if (partyToCoalitionIndex.ContainsKey(kv.Key)) continue;
                blocs.Add(new PoliticalBloc { Members = new List<PoliticalParty> { kv.Key }, Seats = kv.Value });
            }

            if (blocs.Count == 0) return new PoliticalBloc { Members = new List<PoliticalParty>(), Seats = 0 };
            return blocs.OrderByDescending(b => b.Seats).First();
        }

        public bool LeadingBlocHasAbsoluteMajority()
        {
            var leading = GetLeadingBloc();
            int total = GetSeatsByParty().Values.Sum();
            return total > 0 && leading.Seats > total / 2;
        }



        /// <summary>
        /// OUTIL DE DEBUG TEMPORAIRE — force l'exécution immédiate du cycle de coalition, sans
        /// attendre les 7 jours in-game réels. Même remarque que CouncilBonusSystem.DebugForceMajorityCheck :
        /// ce système dépend du temps de simulation réel, jamais avancé par le bouton d'élection accélérée.
        /// </summary>
        public void DebugForceCoalitionCheck()
        {
            double currentDay = (double)m_SimulationSystem.frameIndex / 262144.0;
            m_LastCycleDay = currentDay - CycleIntervalDays - 0.001;
            RunCoalitionCycle();
            s_Log.Info("[CouncilCoalitionSystem] DEBUG : cycle de coalition forcé immédiatement.");
        }

    }
}