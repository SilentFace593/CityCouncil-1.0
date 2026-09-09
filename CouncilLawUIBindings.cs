using System.Collections.Generic;
using System.Linq;
using Colossal.UI.Binding;
using Unity.Entities;

namespace CityCouncil.Systems
{
    /// <summary>
    /// Extension PARTIELLE de CouncilUISystem : bindings/triggers du système de lois
    /// (CouncilLawSystem). Séparé dans son propre fichier pour ne pas alourdir davantage
    /// l'énorme CouncilUISystem.cs — même classe grâce à `partial`, donc accès direct aux
    /// champs privés déjà déclarés là-bas (m_CustomPartySystem, EntityManager, etc.).
    ///
    /// IMPORTANT — deux lignes à ajouter à la main dans CouncilUISystem.cs :
    ///   - à la fin de OnCreate()  : SetupLawBindings(); RegisterLawTriggers();
    ///   - à la fin de OnUpdate()  : UpdateLawBindingIfChanged();
    /// </summary>
    public partial class CouncilUISystem
    {
        private CityCouncil.CouncilLawSystem m_LawSystem;

        private ValueBinding<string> m_LawCatalogJsonBinding;      // catalogue complet, poussé une seule fois (statique)
        private ValueBinding<string> m_LawActiveVotesJsonBinding;  // votes en cours (tous blocs, pour affichage + décision joueur)
        private ValueBinding<string> m_LawHistoryJsonBinding;      // historique complet (adoptées/rejetées/abrogées/annulées)
        private ValueBinding<float> m_LawPlayerMalusPercentBinding;
        private ValueBinding<bool> m_LawPlayerCanProposeBinding;   // true si le bloc du joueur n'a pas déjà un vote en cours

        private string m_LastPushedLawActiveVotesJson;
        private string m_LastPushedLawHistoryJson;
        private float m_LastPushedLawPlayerMalusPercent = -1f;
        private bool m_LastPushedLawPlayerCanPropose;
        private bool m_HasLastPushedLawState;

        private void SetupLawBindings()
        {
            m_LawSystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilLawSystem>();

            m_LawCatalogJsonBinding = new ValueBinding<string>(kGroup, "lawCatalogJson", "[]");
            m_LawActiveVotesJsonBinding = new ValueBinding<string>(kGroup, "lawActiveVotesJson", "[]");
            m_LawHistoryJsonBinding = new ValueBinding<string>(kGroup, "lawHistoryJson", "[]");
            m_LawPlayerMalusPercentBinding = new ValueBinding<float>(kGroup, "lawPlayerMalusPercent", 0f);
            m_LawPlayerCanProposeBinding = new ValueBinding<bool>(kGroup, "lawPlayerCanPropose", false);

            AddBinding(m_LawCatalogJsonBinding);
            AddBinding(m_LawActiveVotesJsonBinding);
            AddBinding(m_LawHistoryJsonBinding);
            AddBinding(m_LawPlayerMalusPercentBinding);
            AddBinding(m_LawPlayerCanProposeBinding);

            // Le catalogue ne change jamais en cours de partie (défini en dur côté C#) :
            // poussé une seule fois, pas besoin de le suivre dans UpdateLawBindingIfChanged.
            m_LawCatalogJsonBinding.Update(LawCatalogDto.ToJsonArray(
                CityCouncil.CouncilLawCatalog.Laws.Select(LawCatalogDto.From)));
        }

        private void RegisterLawTriggers()
        {
            AddBinding(new TriggerBinding<string, string>(kGroup, "proposeLaw",
                (lawId, customName) =>
                {
                    var custom = m_CustomPartySystem.GetData();
                    if (!custom.m_Exists || !custom.m_SubstitutionActive) return;
                    m_LawSystem.TryProposeLaw(custom.m_ActiveSpace, lawId, customName, out _);
                    UpdateLawBindingIfChanged(force: true);
                }));

            AddBinding(new TriggerBinding<string>(kGroup, "proposeLawRepeal",
                (recordIndexStr) =>
                {
                    var custom = m_CustomPartySystem.GetData();
                    if (!custom.m_Exists || !custom.m_SubstitutionActive) return;
                    if (!int.TryParse(recordIndexStr, out int recordIndex)) return;
                    m_LawSystem.TryProposeRepeal(custom.m_ActiveSpace, recordIndex, out _);
                    UpdateLawBindingIfChanged(force: true);
                }));

            AddBinding(new TriggerBinding<string>(kGroup, "respondToLawVote",
                (acceptStr) =>
                {
                    m_LawSystem.TryRespondToPendingVote(acceptStr == "true", out _);
                    UpdateLawBindingIfChanged(force: true);
                }));

            AddBinding(new TriggerBinding(kGroup, "debugForceResolveLawVotes",
                () => { m_LawSystem.DebugForceResolveAllVotes(); UpdateLawBindingIfChanged(force: true); }));

            AddBinding(new TriggerBinding(kGroup, "debugForceLawPeriodicCycle",
                () => { m_LawSystem.DebugForcePeriodicCycle(); UpdateLawBindingIfChanged(force: true); }));
        }

        private void UpdateLawBindingIfChanged(bool force = false)
        {
            var playerParty = (m_CustomPartySystem.GetData().m_Exists && m_CustomPartySystem.GetData().m_SubstitutionActive)
                ? m_CustomPartySystem.GetData().m_ActiveSpace
                : (CityCouncil.PoliticalParty?)null;

            var activeVotes = m_LawSystem.GetActiveVotes();
            var history = m_LawSystem.GetHistory();

            bool playerCanPropose = false;
            if (playerParty.HasValue)
            {
                var coalitionSystem = World.GetExistingSystemManaged<CityCouncil.CouncilCoalitionSystem>();
                if (coalitionSystem != null)
                {
                    bool hasSeats = coalitionSystem.GetSeatsForParty(playerParty.Value) > 0;
                    var blocKey = coalitionSystem.GetBlocKey(playerParty.Value);
                    playerCanPropose = hasSeats && !activeVotes.Any(v => (CityCouncil.PoliticalParty)v.m_ProposerBlocKey == blocKey);
                }
            }

            string activeVotesJson = LawActiveVoteDto.ToJsonArray(activeVotes.Select(v => LawActiveVoteDto.From(v, playerParty, World)));
            string historyJson = LawHistoryDto.ToJsonArray(
                history.Select((r, i) => LawHistoryDto.From(r, i, playerParty, activeVotes, World)));
            float malusPercent = m_LawSystem.GetPlayerLawMalusPercent();

            if (!force && m_HasLastPushedLawState
                && activeVotesJson == m_LastPushedLawActiveVotesJson
                && historyJson == m_LastPushedLawHistoryJson
                && malusPercent == m_LastPushedLawPlayerMalusPercent
                && playerCanPropose == m_LastPushedLawPlayerCanPropose)
                return;

            m_LawActiveVotesJsonBinding.Update(activeVotesJson);
            m_LawHistoryJsonBinding.Update(historyJson);
            m_LawPlayerMalusPercentBinding.Update(malusPercent);
            m_LawPlayerCanProposeBinding.Update(playerCanPropose);

            m_LastPushedLawActiveVotesJson = activeVotesJson;
            m_LastPushedLawHistoryJson = historyJson;
            m_LastPushedLawPlayerMalusPercent = malusPercent;
            m_LastPushedLawPlayerCanPropose = playerCanPropose;
            m_HasLastPushedLawState = true;
        }
    }

    // ------------------------------------------------------------------
    // --- DTOs (sérialisation JSON manuelle, même pattern que le reste du fichier) ---
    // ------------------------------------------------------------------

    public struct LawCatalogAdherenceDto
    {
        public string party;
        public string adherence;
    }

    public struct LawCatalogDto
    {
        public string id;
        public string titleLocaleKey;
        public string theme;
        public List<LawCatalogAdherenceDto> adherence;

        public static LawCatalogDto From(CityCouncil.CouncilLawDefinition def)
        {
            return new LawCatalogDto
            {
                id = def.Id,
                titleLocaleKey = def.TitleLocaleKey,
                theme = def.Theme.ToString(),
                adherence = def.Adherence.Select(kv => new LawCatalogAdherenceDto
                {
                    party = kv.Key.ToString(),
                    adherence = kv.Value.ToString()
                }).ToList()
            };
        }

        public static string ToJsonArray(System.Collections.Generic.IEnumerable<LawCatalogDto> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var d in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{');
                sb.Append("\"id\":\"").Append(d.id).Append("\",");
                sb.Append("\"titleLocaleKey\":\"").Append(d.titleLocaleKey).Append("\",");
                sb.Append("\"theme\":\"").Append(d.theme).Append("\",");
                sb.Append("\"adherence\":[");
                for (int i = 0; i < d.adherence.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('{');
                    sb.Append("\"party\":\"").Append(d.adherence[i].party).Append("\",");
                    sb.Append("\"adherence\":\"").Append(d.adherence[i].adherence).Append("\"");
                    sb.Append('}');
                }
                sb.Append(']');
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }
    }

    public struct LawActiveVoteDto
    {
        public string lawId;
        public string customName;
        public string titleLocaleKey;
        public bool isRepeal;
        public string proposerParty;
        public bool proposerIsCoalition;
        public List<string> proposerMembers;
        public double expiryDay;
        public bool hasPlayerBloc;
        public bool playerHasAnswered;
        public bool isPlayerProposer;

        public static LawActiveVoteDto From(CityCouncil.LawVoteEntry v, CityCouncil.PoliticalParty? playerParty, World world)
        {
            var coalitionSystem = world.GetExistingSystemManaged<CityCouncil.CouncilCoalitionSystem>();
            var proposerKey = (CityCouncil.PoliticalParty)v.m_ProposerBlocKey;
            var bloc = coalitionSystem.GetBlocOf(proposerKey);
            var lawDef = CityCouncil.CouncilLawCatalog.GetById(v.m_LawId.ToString());

            bool isPlayerProposer = playerParty.HasValue && bloc.Members.Contains(playerParty.Value);

            return new LawActiveVoteDto
            {
                lawId = v.m_LawId.ToString(),
                customName = v.m_CustomName.ToString(),
                titleLocaleKey = lawDef?.TitleLocaleKey ?? "",
                isRepeal = v.m_IsRepeal,
                proposerParty = proposerKey.ToString(),
                proposerIsCoalition = bloc.IsCoalition,
                proposerMembers = bloc.Members.Select(p => p.ToString()).ToList(),
                expiryDay = v.m_ExpiryDay,
                hasPlayerBloc = v.m_HasPlayerBloc,
                playerHasAnswered = v.m_PlayerHasAnswered,
                isPlayerProposer = isPlayerProposer,
            };
        }

        public static string ToJsonArray(System.Collections.Generic.IEnumerable<LawActiveVoteDto> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var v in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{');
                sb.Append("\"lawId\":\"").Append(v.lawId).Append("\",");
                sb.Append("\"customName\":\"").Append(Escape(v.customName)).Append("\",");
                sb.Append("\"titleLocaleKey\":\"").Append(v.titleLocaleKey).Append("\",");
                sb.Append("\"isRepeal\":").Append(v.isRepeal ? "true" : "false").Append(',');
                sb.Append("\"proposerParty\":\"").Append(v.proposerParty).Append("\",");
                sb.Append("\"proposerIsCoalition\":").Append(v.proposerIsCoalition ? "true" : "false").Append(',');
                sb.Append("\"proposerMembers\":[").Append(string.Join(",", v.proposerMembers.Select(m => $"\"{m}\""))).Append("],");
                sb.Append("\"expiryDay\":").Append(v.expiryDay.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                sb.Append("\"hasPlayerBloc\":").Append(v.hasPlayerBloc ? "true" : "false").Append(',');
                sb.Append("\"playerHasAnswered\":").Append(v.playerHasAnswered ? "true" : "false").Append(',');
                sb.Append("\"isPlayerProposer\":").Append(v.isPlayerProposer ? "true" : "false");
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public struct LawHistoryDto
    {
        public int recordIndex;
        public string lawId;
        public string customName;
        public string titleLocaleKey;
        public string proposerParty;
        public bool proposerIsCoalition;
        public string outcome;
        public double resolvedDay;
        public bool repealed;
        public string repealerParty;
        public bool repealerIsCoalition;
        public double repealedDay;
        public bool canPlayerRepeal;

        public static LawHistoryDto From(
            CityCouncil.LawRecordEntry r, int index, CityCouncil.PoliticalParty? playerParty,
            List<CityCouncil.LawVoteEntry> activeVotes, World world)
        {
            var coalitionSystem = world.GetExistingSystemManaged<CityCouncil.CouncilCoalitionSystem>();
            var lawDef = CityCouncil.CouncilLawCatalog.GetById(r.m_LawId.ToString());
            var proposerKey = (CityCouncil.PoliticalParty)r.m_ProposerBlocKey;

            bool canPlayerRepeal = false;
            if (playerParty.HasValue && r.m_Outcome == CityCouncil.LawRecordOutcome.Adopted && !r.m_Repealed)
            {
                var playerBlocKey = coalitionSystem.GetBlocKey(playerParty.Value);
                bool notOwnLaw = playerBlocKey != proposerKey;
                bool noActiveVoteForPlayerBloc = !activeVotes.Any(v => (CityCouncil.PoliticalParty)v.m_ProposerBlocKey == playerBlocKey);
                canPlayerRepeal = notOwnLaw && noActiveVoteForPlayerBloc;
            }

            return new LawHistoryDto
            {
                recordIndex = index,
                lawId = r.m_LawId.ToString(),
                customName = r.m_CustomName.ToString(),
                titleLocaleKey = lawDef?.TitleLocaleKey ?? "",
                proposerParty = proposerKey.ToString(),
                proposerIsCoalition = r.m_ProposerWasCoalition,
                outcome = r.m_Outcome.ToString(),
                resolvedDay = r.m_ResolvedDay,
                repealed = r.m_Repealed,
                repealerParty = r.m_Repealed ? ((CityCouncil.PoliticalParty)r.m_RepealerBlocKey).ToString() : "",
                repealerIsCoalition = r.m_RepealerWasCoalition,
                repealedDay = r.m_RepealedDay,
                canPlayerRepeal = canPlayerRepeal,
            };
        }

        public static string ToJsonArray(System.Collections.Generic.IEnumerable<LawHistoryDto> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var r in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{');
                sb.Append("\"recordIndex\":").Append(r.recordIndex).Append(',');
                sb.Append("\"lawId\":\"").Append(r.lawId).Append("\",");
                sb.Append("\"customName\":\"").Append(Escape(r.customName)).Append("\",");
                sb.Append("\"titleLocaleKey\":\"").Append(r.titleLocaleKey).Append("\",");
                sb.Append("\"proposerParty\":\"").Append(r.proposerParty).Append("\",");
                sb.Append("\"proposerIsCoalition\":").Append(r.proposerIsCoalition ? "true" : "false").Append(',');
                sb.Append("\"outcome\":\"").Append(r.outcome).Append("\",");
                sb.Append("\"resolvedDay\":").Append(r.resolvedDay.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                sb.Append("\"repealed\":").Append(r.repealed ? "true" : "false").Append(',');
                sb.Append("\"repealerParty\":\"").Append(r.repealerParty).Append("\",");
                sb.Append("\"repealerIsCoalition\":").Append(r.repealerIsCoalition ? "true" : "false").Append(',');
                sb.Append("\"repealedDay\":").Append(r.repealedDay.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                sb.Append("\"canPlayerRepeal\":").Append(r.canPlayerRepeal ? "true" : "false");
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
