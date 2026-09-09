using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;

namespace CityCouncil
{
    public enum LawRecordOutcome : byte
    {
        Adopted = 0,
        Rejected = 1,
        CancelledElection = 2,
    }

    /// <summary>
    /// Un vote de loi (proposition ou abrogation) actuellement en cours. Durée fixe de 12h
    /// in-game (0.5 jour). Un seul vote actif à la fois par BLOC politique (parti seul non
    /// coalisé, ou coalition entière) — cf. CouncilCoalitionSystem.GetAllBlocs.
    /// </summary>
    public struct LawVoteEntry
    {
        public FixedString64Bytes m_LawId;
        public FixedString64Bytes m_CustomName;
        public byte m_ProposerBlocKey;     // PoliticalParty leader du bloc proposeur (cf. GetBlocKey)
        public bool m_IsRepeal;
        public int m_TargetRecordIndex;    // valide seulement si m_IsRepeal (index dans m_Records)
        public double m_ExpiryDay;

        // Le bloc du joueur doit répondre explicitement s'il n'est pas le proposeur. Les autres
        // blocs (IA) sont tirés au sort au moment de la RÉSOLUTION (pas ici) : la composition des
        // blocs ne peut pas changer pendant les 12h du vote (une élection annule le vote avant que
        // ça puisse arriver), donc rien à figer à l'avance.
        public bool m_HasPlayerBloc;
        public bool m_PlayerHasAnswered;
        public bool m_PlayerAnsweredFor;
    }

    /// <summary>
    /// Trace une loi proposée (adoptée ou rejetée), et sert AUSSI de source de vérité pour les lois
    /// actuellement en vigueur (Outcome == Adopted && !m_Repealed) — pas de liste séparée, pour
    /// éviter de dupliquer un même fait à deux endroits (cf. remarque design du projet ailleurs).
    /// </summary>
    public struct LawRecordEntry
    {
        public FixedString64Bytes m_LawId;
        public FixedString64Bytes m_CustomName;
        public byte m_ProposerBlocKey;
        public bool m_ProposerWasCoalition;
        public LawRecordOutcome m_Outcome;
        public double m_ResolvedDay;

        public bool m_Repealed;
        public byte m_RepealerBlocKey;
        public bool m_RepealerWasCoalition;
        public double m_RepealedDay;
    }

    /// <summary>Malus d'intention de vote (-5%, ville entière) infligé au parti joueur pour avoir voté
    /// "pour" une loi mal vue par son propre bord politique. Expire à la fin du cycle électoral
    /// suivant son déclenchement (pas indéfiniment) — plusieurs entrées peuvent coexister et se
    /// cumulent (cf. discussion design).</summary>
    public struct PlayerLawMalusEntry
    {
        public FixedString64Bytes m_LawId;
        public double m_ExpiryDay;
    }

    public struct CouncilLawData : IComponentData, ISerializable
    {
        public FixedList4096Bytes<LawVoteEntry> m_ActiveVotes;
        public FixedList4096Bytes<LawRecordEntry> m_Records;
        public FixedList512Bytes<PlayerLawMalusEntry> m_PlayerMalus;

        private const int kVersion = 1;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(kVersion);

            writer.Write(m_ActiveVotes.Length);
            for (int i = 0; i < m_ActiveVotes.Length; i++)
            {
                var v = m_ActiveVotes[i];
                writer.Write(v.m_LawId.ToString());
                writer.Write(v.m_CustomName.ToString());
                writer.Write(v.m_ProposerBlocKey);
                writer.Write(v.m_IsRepeal);
                writer.Write(v.m_TargetRecordIndex);
                writer.Write(v.m_ExpiryDay);
                writer.Write(v.m_HasPlayerBloc);
                writer.Write(v.m_PlayerHasAnswered);
                writer.Write(v.m_PlayerAnsweredFor);
            }

            writer.Write(m_Records.Length);
            for (int i = 0; i < m_Records.Length; i++)
            {
                var r = m_Records[i];
                writer.Write(r.m_LawId.ToString());
                writer.Write(r.m_CustomName.ToString());
                writer.Write(r.m_ProposerBlocKey);
                writer.Write(r.m_ProposerWasCoalition);
                writer.Write((byte)r.m_Outcome);
                writer.Write(r.m_ResolvedDay);
                writer.Write(r.m_Repealed);
                writer.Write(r.m_RepealerBlocKey);
                writer.Write(r.m_RepealerWasCoalition);
                writer.Write(r.m_RepealedDay);
            }

            writer.Write(m_PlayerMalus.Length);
            for (int i = 0; i < m_PlayerMalus.Length; i++)
            {
                writer.Write(m_PlayerMalus[i].m_LawId.ToString());
                writer.Write(m_PlayerMalus[i].m_ExpiryDay);
            }
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int _);

            reader.Read(out int voteCount);
            m_ActiveVotes = new FixedList4096Bytes<LawVoteEntry>();
            for (int i = 0; i < voteCount; i++)
            {
                reader.Read(out string lawId);
                reader.Read(out string customName);
                reader.Read(out byte proposerKey);
                reader.Read(out bool isRepeal);
                reader.Read(out int targetIndex);
                reader.Read(out double expiry);
                reader.Read(out bool hasPlayerBloc);
                reader.Read(out bool playerAnswered);
                reader.Read(out bool playerAnsweredFor);
                m_ActiveVotes.Add(new LawVoteEntry
                {
                    m_LawId = lawId,
                    m_CustomName = customName,
                    m_ProposerBlocKey = proposerKey,
                    m_IsRepeal = isRepeal,
                    m_TargetRecordIndex = targetIndex,
                    m_ExpiryDay = expiry,
                    m_HasPlayerBloc = hasPlayerBloc,
                    m_PlayerHasAnswered = playerAnswered,
                    m_PlayerAnsweredFor = playerAnsweredFor,
                });
            }

            reader.Read(out int recordCount);
            m_Records = new FixedList4096Bytes<LawRecordEntry>();
            for (int i = 0; i < recordCount; i++)
            {
                reader.Read(out string lawId);
                reader.Read(out string customName);
                reader.Read(out byte proposerKey);
                reader.Read(out bool proposerWasCoalition);
                reader.Read(out byte outcome);
                reader.Read(out double resolvedDay);
                reader.Read(out bool repealed);
                reader.Read(out byte repealerKey);
                reader.Read(out bool repealerWasCoalition);
                reader.Read(out double repealedDay);
                m_Records.Add(new LawRecordEntry
                {
                    m_LawId = lawId,
                    m_CustomName = customName,
                    m_ProposerBlocKey = proposerKey,
                    m_ProposerWasCoalition = proposerWasCoalition,
                    m_Outcome = (LawRecordOutcome)outcome,
                    m_ResolvedDay = resolvedDay,
                    m_Repealed = repealed,
                    m_RepealerBlocKey = repealerKey,
                    m_RepealerWasCoalition = repealerWasCoalition,
                    m_RepealedDay = repealedDay,
                });
            }

            reader.Read(out int malusCount);
            m_PlayerMalus = new FixedList512Bytes<PlayerLawMalusEntry>();
            for (int i = 0; i < malusCount; i++)
            {
                reader.Read(out string lawId);
                reader.Read(out double expiry);
                m_PlayerMalus.Add(new PlayerLawMalusEntry { m_LawId = lawId, m_ExpiryDay = expiry });
            }
        }
    }

    /// <summary>
    /// Gère la proposition, le vote (12h in-game) et l'abrogation des lois. Une loi propose par un
    /// BLOC (parti seul hors coalition, ou coalition entière) — cf. CouncilCoalitionSystem.GetAllBlocs.
    /// Un seul vote actif à la fois par bloc. Le check IA (nouvelle proposition ou tentative
    /// d'abrogation) tourne au même rythme que le cycle électoral (7 jours, même limitation
    /// assumée que Bonus/Score/Coalition : pas de signal "fin de 2e tour" city-wide unique dans le
    /// mod), PLUS une relance immédiate pour un bloc donné dès que son vote se résout (cf. discussion
    /// design : "12h après, l'IA peut repartir sur un nouveau cycle").
    /// </summary>
    public partial class CouncilLawSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        private const double VoteDurationDays = 0.5; // 12h in-game
        private const double CycleIntervalDays = 7.0; // même rythme que les autres cycles du mod
        private const long LawTrophyPoints = 500;
        private const float PlayerMalusPercent = 0.05f;
        public const int MaxCustomNameLength = 50;
        private const int MaxRecordsKept = 25; // cap arbitraire, évite une croissance illimitée (cf. FixedList4096Bytes)

        // Probabilités IA, ajustables librement.
        private const float AiProposeLawChance = 0.20f;
        private const float AiProposeRepealChance = 0.30f;

        private EntityQuery m_SingletonQuery;
        private SimulationSystem m_SimulationSystem;
        private CouncilCoalitionSystem m_CoalitionSystem;
        private CouncilCustomPartySystem m_CustomPartySystem;
        private CouncilScoreSystem m_ScoreSystem;
        private readonly Random m_Rng = new Random();

        private Entity m_SingletonEntity = Entity.Null;
        private double m_LastCycleDay = -1;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilLawData>());
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_CoalitionSystem = World.GetOrCreateSystemManaged<CouncilCoalitionSystem>();
            m_CustomPartySystem = World.GetOrCreateSystemManaged<CouncilCustomPartySystem>();
            m_ScoreSystem = World.GetOrCreateSystemManaged<CouncilScoreSystem>();
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

            double currentDay = CurrentDay();
            ResolveExpiredVotesIfNeeded(currentDay);
            ExpirePlayerMalusIfNeeded(currentDay);

            if (m_LastCycleDay < 0)
            {
                m_LastCycleDay = currentDay;
                return;
            }

            if (currentDay - m_LastCycleDay >= CycleIntervalDays)
            {
                m_LastCycleDay = currentDay;
                RunPeriodicCycle(currentDay);
            }
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
                        if (e != keep) { s_Log.Warn($"[CouncilLawSystem] Doublon détruit : {e}"); EntityManager.DestroyEntity(e); }
                    m_SingletonEntity = keep;
                    return;
                }
            }
            finally { existing.Dispose(); }

            m_SingletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(m_SingletonEntity, new CouncilLawData
            {
                m_ActiveVotes = new FixedList4096Bytes<LawVoteEntry>(),
                m_Records = new FixedList4096Bytes<LawRecordEntry>(),
                m_PlayerMalus = new FixedList512Bytes<PlayerLawMalusEntry>(),
            });
            s_Log.Info("[CouncilLawSystem] Entité singleton créée.");
        }

        public CouncilLawData GetData()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            return EntityManager.GetComponentData<CouncilLawData>(m_SingletonEntity);
        }

        private void SetData(CouncilLawData data)
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            EntityManager.SetComponentData(m_SingletonEntity, data);
        }

        private double CurrentDay() => (double)m_SimulationSystem.frameIndex / 262144.0;

        // ------------------------------------------------------------------
        // --- Lecture d'état (parti joueur actif, bloc courant) ---
        // ------------------------------------------------------------------

        /// <summary>
        /// Le parti joueur actif, ou null si aucun parti joueur n'est actif OU s'il n'a
        /// actuellement AUCUN siège au conseil — un parti sans siège n'a pas de légitimité
        /// pour proposer, répondre à, ou abroger une loi (cf. discussion design). Ce point
        /// unique de lecture fait cascader la règle vers StartVote (m_HasPlayerBloc),
        /// ResolveVote (malus/décompte) et RunPeriodicCycle (le joueur ne "bloque" plus son
        /// bloc pour l'IA s'il n'a pas de siège — voir aussi le garde-fou dédié plus bas pour
        /// les blocs IA à 0 siège).
        /// </summary>
        private PoliticalParty? GetPlayerActiveParty()
        {
            var custom = m_CustomPartySystem.GetData();
            if (!custom.m_Exists || !custom.m_SubstitutionActive) return null;

            var space = custom.m_ActiveSpace;
            return m_CoalitionSystem.GetSeatsForParty(space) > 0 ? space : (PoliticalParty?)null;
        }

        /// <summary>true si le bloc identifié par blocKey contient le parti joueur actif.</summary>
        private bool BlocContainsPlayer(PoliticalParty blocKey, PoliticalParty? playerParty)
        {
            if (!playerParty.HasValue) return false;
            return m_CoalitionSystem.GetBlocKey(playerParty.Value) == blocKey;
        }

        private bool HasActiveVoteForBloc(PoliticalParty blocKey)
        {
            foreach (var v in GetData().m_ActiveVotes)
                if ((PoliticalParty)v.m_ProposerBlocKey == blocKey) return true;
            // Un bloc peut aussi être "occupé" en tant que CIBLE d'une abrogation en cours qu'il
            // n'a pas proposée lui-même ? Non : ce garde-fou ne concerne que SES PROPRES votes
            // (proposition ou abrogation qu'IL a initiée) — une loi qu'il a fait voter peut très
            // bien être la cible du vote d'abrogation d'un AUTRE bloc en parallèle.
            return false;
        }

        // ------------------------------------------------------------------
        // --- Proposition (joueur ou IA) ---
        // ------------------------------------------------------------------

        /// <summary>
        /// Propose une nouvelle loi au nom du bloc auquel appartient `actingParty` (son bloc entier
        /// si coalisé). Refuse si ce bloc a déjà un vote en cours, si le nom est invalide, ou si la
        /// loi n'existe pas dans le catalogue.
        /// </summary>
        public bool TryProposeLaw(PoliticalParty actingParty, string lawId, string customName, out string error)
        {
            error = null;

            if (m_CoalitionSystem.GetSeatsForParty(actingParty) <= 0)
            {
                error = "Votre parti ne détient aucun siège au conseil : aucune légitimité pour proposer une loi.";
                return false;
            }

            var lawDef = CouncilLawCatalog.GetById(lawId);
            if (lawDef == null) { error = "Loi introuvable dans le catalogue."; return false; }

            customName = (customName ?? "").Trim();
            if (customName.Length == 0 || customName.Length > MaxCustomNameLength)
            {
                error = $"Nom invalide (1 à {MaxCustomNameLength} caractères).";
                return false;
            }

            var bloc = m_CoalitionSystem.GetBlocOf(actingParty);
            var blocKey = m_CoalitionSystem.GetBlocKey(actingParty);

            if (HasActiveVoteForBloc(blocKey))
            {
                error = "Ce bloc a déjà un vote de loi en cours.";
                return false;
            }

            StartVote(blocKey, bloc, lawId, customName, isRepeal: false, targetRecordIndex: -1);
            return true;
        }

        /// <summary>Propose l'abrogation d'une loi déjà en vigueur (record à l'index donné).</summary>
        public bool TryProposeRepeal(PoliticalParty actingParty, int targetRecordIndex, out string error)
        {
            error = null;

            if (m_CoalitionSystem.GetSeatsForParty(actingParty) <= 0)
            {
                error = "Votre parti ne détient aucun siège au conseil : aucune légitimité pour proposer une abrogation.";
                return false;
            }

            var data = GetData();
            if (targetRecordIndex < 0 || targetRecordIndex >= data.m_Records.Length)
            {
                error = "Loi introuvable dans l'historique.";
                return false;
            }
            var record = data.m_Records[targetRecordIndex];
            if (record.m_Outcome != LawRecordOutcome.Adopted || record.m_Repealed)
            {
                error = "Cette loi n'est pas actuellement en vigueur.";
                return false;
            }

            var bloc = m_CoalitionSystem.GetBlocOf(actingParty);
            var blocKey = m_CoalitionSystem.GetBlocKey(actingParty);

            if (blocKey == (PoliticalParty)record.m_ProposerBlocKey)
            {
                error = "Un bloc ne peut pas abroger sa propre loi.";
                return false;
            }
            if (HasActiveVoteForBloc(blocKey))
            {
                error = "Ce bloc a déjà un vote de loi en cours.";
                return false;
            }

            var lawDef = CouncilLawCatalog.GetById(record.m_LawId.ToString());
            string label = lawDef != null ? record.m_LawId.ToString() : record.m_LawId.ToString();
            StartVote(blocKey, bloc, record.m_LawId.ToString(), record.m_CustomName.ToString(), isRepeal: true, targetRecordIndex);
            return true;
        }

        private void StartVote(PoliticalParty blocKey, CouncilCoalitionSystem.PoliticalBloc bloc, string lawId, string customName, bool isRepeal, int targetRecordIndex)
        {
            var playerParty = GetPlayerActiveParty();

            // Le bloc du joueur doit-il répondre explicitement à CE vote ? Uniquement s'il existe,
            // n'est PAS le proposeur (auto-FOR dans ce cas), et n'est pas déjà tranché.
            bool hasPlayerBloc = playerParty.HasValue && m_CoalitionSystem.GetBlocKey(playerParty.Value) != blocKey;

            var entry = new LawVoteEntry
            {
                m_LawId = lawId,
                m_CustomName = customName,
                m_ProposerBlocKey = (byte)blocKey,
                m_IsRepeal = isRepeal,
                m_TargetRecordIndex = targetRecordIndex,
                m_ExpiryDay = CurrentDay() + VoteDurationDays,
                m_HasPlayerBloc = hasPlayerBloc,
                m_PlayerHasAnswered = false,
                m_PlayerAnsweredFor = false,
            };

            var data = GetData();
            data.m_ActiveVotes.Add(entry);
            SetData(data);

            s_Log.Info($"[CouncilLawSystem] Vote démarré : {(isRepeal ? "ABROGATION" : "PROPOSITION")} '{lawId}' (\"{customName}\") par le bloc de {blocKey}" +
                       (bloc.IsCoalition ? $" (coalition : {string.Join("+", bloc.Members)})" : "") + $", expire jour {entry.m_ExpiryDay:F2}.");
        }

        /// <summary>
        /// Réponse EXPLICITE du joueur à un vote en cours où son bloc n'est pas le proposeur.
        /// Ne s'applique qu'au premier vote en attente d'une décision joueur (il ne peut logiquement
        /// y en avoir qu'un seul à la fois : le bloc du joueur ne peut pas proposer ET répondre à un
        /// autre vote simultanément puisqu'il serait alors lui-même "occupé" — sauf s'il répond à un
        /// vote lancé par un AUTRE bloc pendant qu'aucun vote de son propre bloc n'est actif).
        /// </summary>
        public bool TryRespondToPendingVote(bool accept, out string error)
        {
            error = null;
            var data = GetData();
            for (int i = 0; i < data.m_ActiveVotes.Length; i++)
            {
                var v = data.m_ActiveVotes[i];
                if (!v.m_HasPlayerBloc || v.m_PlayerHasAnswered) continue;

                v.m_PlayerHasAnswered = true;
                v.m_PlayerAnsweredFor = accept;
                data.m_ActiveVotes[i] = v;
                SetData(data);

                s_Log.Info($"[CouncilLawSystem] Réponse du joueur au vote sur '{v.m_LawId}' : {(accept ? "POUR" : "CONTRE")}.");
                return true;
            }
            error = "Aucun vote en attente de votre décision.";
            return false;
        }

        // ------------------------------------------------------------------
        // --- Résolution ---
        // ------------------------------------------------------------------

        private void ResolveExpiredVotesIfNeeded(double currentDay)
        {
            var data = GetData();
            if (data.m_ActiveVotes.Length == 0) return;

            var stillActive = new FixedList4096Bytes<LawVoteEntry>();
            var toResolve = new List<LawVoteEntry>();
            foreach (var v in data.m_ActiveVotes)
            {
                if (currentDay >= v.m_ExpiryDay) toResolve.Add(v);
                else stillActive.Add(v);
            }
            if (toResolve.Count == 0) return;

            data.m_ActiveVotes = stillActive;
            SetData(data);

            foreach (var v in toResolve)
                ResolveVote(v, currentDay);
        }

        private void ResolveVote(LawVoteEntry vote, double currentDay)
        {
            var proposerKey = (PoliticalParty)vote.m_ProposerBlocKey;
            var proposerBloc = m_CoalitionSystem.GetBlocOf(proposerKey);
            var allBlocs = m_CoalitionSystem.GetAllBlocs();
            int totalSeats = allBlocs.Sum(b => b.Seats);
            var playerParty = GetPlayerActiveParty();
            var lawDef = CouncilLawCatalog.GetById(vote.m_LawId.ToString());

            bool adopted;
            var forParties = new List<PoliticalParty>(proposerBloc.Members);

            if (totalSeats > 0 && proposerBloc.Seats > totalSeats / 2)
            {
                // Majorité absolue à elle seule : adoption automatique (les autres blocs sont
                // consultés pour la narration/l'historique mais ne peuvent pas bloquer le vote).
                adopted = true;
            }
            else
            {
                int forSeats = proposerBloc.Seats;
                foreach (var bloc in allBlocs)
                {
                    if (bloc.Members.SequenceEqual(proposerBloc.Members)) continue;

                    bool isPlayerBloc = vote.m_HasPlayerBloc && BlocContainsPlayer(m_CoalitionSystem.GetBlocKey(bloc.Members[0]), playerParty);
                    bool blocVotesFor;

                    if (isPlayerBloc && vote.m_PlayerHasAnswered)
                    {
                        blocVotesFor = vote.m_PlayerAnsweredFor;
                    }
                    else
                    {
                        blocVotesFor = RollBlocDecision(bloc, lawDef);
                    }

                    if (blocVotesFor)
                    {
                        forSeats += bloc.Seats;
                        forParties.AddRange(bloc.Members);
                    }
                }
                adopted = totalSeats > 0 && forSeats > totalSeats / 2;
            }

            if (vote.m_IsRepeal)
            {
                ResolveRepealOutcome(vote, adopted, proposerKey, proposerBloc, currentDay);
            }
            else
            {
                ResolveNewLawOutcome(vote, adopted, proposerKey, proposerBloc, forParties, currentDay, lawDef, playerParty);
            }

            // Relance immédiate du check IA pour CE bloc (cf. discussion design : "12h après,
            // l'IA peut repartir sur un nouveau cycle"), sauf si c'est le bloc du joueur.
            if (!BlocContainsPlayer(proposerKey, playerParty))
                TryRunAiDecisionForBloc(proposerKey, currentDay);
        }

        /// <summary>Probabilité qu'un bloc vote POUR une loi donnée, basée sur la moyenne d'adhésion de ses membres.</summary>
        private bool RollBlocDecision(CouncilCoalitionSystem.PoliticalBloc bloc, CouncilLawDefinition lawDef)
        {
            if (lawDef == null) return false;
            float avgProbability = (float)bloc.Members.Average(p => AdherenceToProbability(lawDef.GetAdherence(p)));
            return m_Rng.NextDouble() < avgProbability;
        }

        private static float AdherenceToProbability(LawAdherence adherence)
        {
            return adherence switch
            {
                LawAdherence.TresFavorable => 0.95f,
                LawAdherence.PlutotFavorable => 0.75f,
                LawAdherence.Pragmatique => 0.50f,
                LawAdherence.PlutotDefavorable => 0.25f,
                LawAdherence.TresDefavorable => 0.05f,
                _ => 0.50f,
            };
        }

        private void ResolveNewLawOutcome(
            LawVoteEntry vote, bool adopted, PoliticalParty proposerKey, CouncilCoalitionSystem.PoliticalBloc proposerBloc,
            List<PoliticalParty> forParties, double currentDay, CouncilLawDefinition lawDef, PoliticalParty? playerParty)
        {
            AddRecord(new LawRecordEntry
            {
                m_LawId = vote.m_LawId,
                m_CustomName = vote.m_CustomName,
                m_ProposerBlocKey = (byte)proposerKey,
                m_ProposerWasCoalition = proposerBloc.IsCoalition,
                m_Outcome = adopted ? LawRecordOutcome.Adopted : LawRecordOutcome.Rejected,
                m_ResolvedDay = currentDay,
                m_Repealed = false,
            });

            if (!adopted)
            {
                s_Log.Info($"[CouncilLawSystem] Loi '{vote.m_LawId}' (\"{vote.m_CustomName}\") REJETÉE (proposeur : {proposerKey}).");
                return;
            }

            GrantLawTrophy(proposerBloc.Members, LawTrophyPoints);
            s_Log.Info($"[CouncilLawSystem] Loi '{vote.m_LawId}' (\"{vote.m_CustomName}\") ADOPTÉE, +{LawTrophyPoints} points partagés entre {string.Join("+", proposerBloc.Members)}.");

            ApplyPlayerMalusIfNeeded(vote.m_LawId.ToString(), forParties, playerParty, currentDay, lawDef);
        }

        private void ResolveRepealOutcome(LawVoteEntry vote, bool adopted, PoliticalParty proposerKey, CouncilCoalitionSystem.PoliticalBloc proposerBloc, double currentDay)
        {
            if (!adopted)
            {
                s_Log.Info($"[CouncilLawSystem] Tentative d'abrogation de '{vote.m_LawId}' par {proposerKey} : ÉCHOUÉE.");
                return;
            }

            var data = GetData();
            if (vote.m_TargetRecordIndex >= 0 && vote.m_TargetRecordIndex < data.m_Records.Length)
            {
                var target = data.m_Records[vote.m_TargetRecordIndex];
                target.m_Repealed = true;
                target.m_RepealerBlocKey = (byte)proposerKey;
                target.m_RepealerWasCoalition = proposerBloc.IsCoalition;
                target.m_RepealedDay = currentDay;
                data.m_Records[vote.m_TargetRecordIndex] = target;
                SetData(data);
            }

            GrantLawTrophy(proposerBloc.Members, LawTrophyPoints);
            s_Log.Info($"[CouncilLawSystem] Loi '{vote.m_LawId}' ABROGÉE par {proposerKey}, +{LawTrophyPoints} points partagés (trophée, n'enlève rien au parti l'ayant fait voter).");
        }

        /// <summary>
        /// Malus joueur : si le parti joueur a voté POUR (proposeur ou réponse explicite acceptée)
        /// une loi notée PlutotDefavorable/TresDefavorable dans SA propre grille, applique -5%
        /// d'intention de vote ville entière, actif jusqu'à la fin du cycle électoral suivant.
        /// Un tirage automatique (le joueur n'a pas répondu à temps) ne déclenche JAMAIS le malus.
        /// </summary>
        private void ApplyPlayerMalusIfNeeded(string lawId, List<PoliticalParty> forParties, PoliticalParty? playerParty, double currentDay, CouncilLawDefinition lawDef)
        {
            if (!playerParty.HasValue || lawDef == null) return;
            if (!forParties.Contains(playerParty.Value)) return;

            var adherence = lawDef.GetAdherence(playerParty.Value);
            if (adherence != LawAdherence.PlutotDefavorable && adherence != LawAdherence.TresDefavorable) return;

            var data = GetData();
            data.m_PlayerMalus.Add(new PlayerLawMalusEntry { m_LawId = lawId, m_ExpiryDay = currentDay + CycleIntervalDays });
            SetData(data);

            s_Log.Info($"[CouncilLawSystem] Malus joueur appliqué (-{PlayerMalusPercent:P0}) pour avoir voté '{lawId}' à contre-courant de son bord — expire jour {currentDay + CycleIntervalDays:F2}.");
        }

        private void ExpirePlayerMalusIfNeeded(double currentDay)
        {
            var data = GetData();
            if (data.m_PlayerMalus.Length == 0) return;

            var kept = new FixedList512Bytes<PlayerLawMalusEntry>();
            bool changed = false;
            foreach (var m in data.m_PlayerMalus)
            {
                if (currentDay < m.m_ExpiryDay) kept.Add(m);
                else changed = true;
            }
            if (changed)
            {
                data.m_PlayerMalus = kept;
                SetData(data);
            }
        }

        /// <summary>Somme des malus actifs (chaque loi mal votée cumule -5%). Utilisé par VoteCalculator (étape ultérieure).</summary>
        public float GetPlayerLawMalusPercent()
        {
            return GetData().m_PlayerMalus.Length * PlayerMalusPercent;
        }

        private void AddRecord(LawRecordEntry entry)
        {
            var data = GetData();
            var records = data.m_Records;
            if (records.Length >= MaxRecordsKept)
            {
                // Éviction FIFO : la loi la plus ancienne quitte l'historique. Limitation assumée
                // (cf. discussion design) — si elle était encore en vigueur, elle "sort du radar"
                // (plus proposable à l'abrogation), mais un malus joueur déjà en cours continue de
                // vivre indépendamment (suivi par m_PlayerMalus, pas par l'historique).
                var trimmed = new FixedList4096Bytes<LawRecordEntry>();
                for (int i = 1; i < records.Length; i++) trimmed.Add(records[i]);
                records = trimmed;
            }
            records.Add(entry);
            data.m_Records = records;
            SetData(data);
        }

        /// <summary>Partage et arrondit les points entre les membres d'un bloc (coalition ou parti seul).</summary>
        private void GrantLawTrophy(List<PoliticalParty> members, long totalPoints)
        {
            if (members.Count == 0) return;
            long share = (long)Math.Round((double)totalPoints / members.Count, MidpointRounding.AwayFromZero);
            foreach (var p in members)
                m_ScoreSystem.AddExternalTrophyScore(p, share);
        }

        // ------------------------------------------------------------------
        // --- Cycle IA périodique ---
        // ------------------------------------------------------------------

        private void RunPeriodicCycle(double currentDay)
        {
            CancelAllActiveVotes(currentDay); // cf. règle : une nouvelle échéance électorale annule tout vote en cours

            var playerParty = GetPlayerActiveParty();
            foreach (var bloc in m_CoalitionSystem.GetAllBlocs())
            {
                if (bloc.Seats <= 0) continue; // aucune légitimité pour un bloc sans siège
                var blocKey = bloc.Members[0];
                if (BlocContainsPlayer(blocKey, playerParty)) continue; // le joueur décide lui-même pour son bloc
                TryRunAiDecisionForBloc(blocKey, currentDay);
            }
        }

        /// <summary>Annule tout vote encore actif (ne devrait normalement jamais arriver : un vote dure 12h,
        /// bien moins qu'un cycle de 7 jours — garde-fou pour les cas limites, ex. avance de temps debug).</summary>
        private void CancelAllActiveVotes(double currentDay)
        {
            var data = GetData();
            if (data.m_ActiveVotes.Length == 0) return;

            foreach (var v in data.m_ActiveVotes)
            {
                if (v.m_IsRepeal) continue; // une tentative d'abrogation annulée ne laisse pas de trace en historique
                AddRecord(new LawRecordEntry
                {
                    m_LawId = v.m_LawId,
                    m_CustomName = v.m_CustomName,
                    m_ProposerBlocKey = v.m_ProposerBlocKey,
                    m_Outcome = LawRecordOutcome.CancelledElection,
                    m_ResolvedDay = currentDay,
                });
            }

            data = GetData();
            data.m_ActiveVotes = new FixedList4096Bytes<LawVoteEntry>();
            SetData(data);
            s_Log.Info("[CouncilLawSystem] Échéance électorale : tous les votes de loi en cours ont été annulés.");
        }

        private void TryRunAiDecisionForBloc(PoliticalParty blocKey, double currentDay)
        {
            if (HasActiveVoteForBloc(blocKey)) return;

            var bloc = m_CoalitionSystem.GetBlocOf(blocKey);

            // Priorité à l'abrogation si une loi en vigueur est détestée par ce bloc.
            var data = GetData();
            for (int i = 0; i < data.m_Records.Length; i++)
            {
                var record = data.m_Records[i];
                if (record.m_Outcome != LawRecordOutcome.Adopted || record.m_Repealed) continue;
                if ((PoliticalParty)record.m_ProposerBlocKey == blocKey) continue; // pas sa propre loi

                var lawDef = CouncilLawCatalog.GetById(record.m_LawId.ToString());
                if (lawDef == null) continue;

                bool hated = bloc.Members.Any(p => lawDef.GetAdherence(p) == LawAdherence.TresDefavorable);
                if (!hated) continue;

                if (m_Rng.NextDouble() < AiProposeRepealChance)
                {
                    StartVote(blocKey, bloc, record.m_LawId.ToString(), record.m_CustomName.ToString(), isRepeal: true, i);
                    return; // une seule action IA par check
                }
            }

            // Sinon, chance de proposer une nouvelle loi (favorable en priorité pour ce bloc).
            if (m_Rng.NextDouble() >= AiProposeLawChance) return;

            var weighted = CouncilLawCatalog.Laws
                .Select(l => (law: l, weight: (float)bloc.Members.Average(p => AdherenceToProbability(l.GetAdherence(p)))))
                .OrderByDescending(x => x.weight * (float)m_Rng.NextDouble()) // tirage pondéré simple
                .ToList();
            if (weighted.Count == 0) return;

            var chosen = weighted[0].law;
            string defaultName = chosen.Id; // nom générique pour l'IA (le joueur nomme les siennes lui-même)
            StartVote(blocKey, bloc, chosen.Id, defaultName, isRepeal: false, -1);
        }

        // ------------------------------------------------------------------
        // --- Lecture pour l'UI (étape ultérieure) ---
        // ------------------------------------------------------------------

        public List<LawVoteEntry> GetActiveVotes() => GetData().m_ActiveVotes.ToArray().ToList();
        public List<LawRecordEntry> GetHistory() => GetData().m_Records.ToArray().ToList();

        /// <summary>OUTIL DE DEBUG TEMPORAIRE — force la résolution immédiate de tous les votes en cours.</summary>
        public void DebugForceResolveAllVotes()
        {
            double currentDay = CurrentDay();
            var data = GetData();
            var votes = data.m_ActiveVotes.ToArray().ToList();
            data.m_ActiveVotes = new FixedList4096Bytes<LawVoteEntry>();
            SetData(data);
            foreach (var v in votes) ResolveVote(v, currentDay);
            s_Log.Info("[CouncilLawSystem] DEBUG : tous les votes en cours résolus immédiatement.");
        }

        /// <summary>OUTIL DE DEBUG TEMPORAIRE — force le cycle périodique (annulation + check IA).</summary>
        public void DebugForcePeriodicCycle()
        {
            double currentDay = CurrentDay();
            m_LastCycleDay = currentDay - CycleIntervalDays - 0.001;
            RunPeriodicCycle(currentDay);
            s_Log.Info("[CouncilLawSystem] DEBUG : cycle périodique forcé immédiatement.");
        }
    }
}
