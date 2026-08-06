using System;
using System.Linq;
using Colossal.UI.Binding;
using Game.Areas;
using Game.Tools;
using Game.UI;
using Unity.Collections;
using Unity.Entities;
using static CityCouncil.CouncilDistrictData;

namespace CityCouncil.Systems
{
    /// <summary>
    /// Expose les données électorales à l'UI React :
    ///   - encart "Administration" du panneau district sélectionné
    ///   - panneau hémicycle (résultats ville entière)
    ///   - onglet "Votre Parti" (création/gestion du parti joueur)
    /// Suit le pattern ValueBinding poussé manuellement (cf. DistrictNotesMod) plutôt que
    /// GetterValueBinding, pour éviter un trafic constant vers le JS à chaque frame alors
    /// que les données électorales ne changent qu'une fois par jour in-game.
    /// </summary>
    public partial class CouncilUISystem : UISystemBase
    {
        private const string kGroup = "cityCouncil";

        private ToolSystem m_ToolSystem;
        private EntityManager m_EntityManager;
        private EntityQuery m_DistrictQuery;
        private CityCouncil.CouncilCityEventSystem m_CityEventSystem;
        private CityCouncil.CouncilCustomPartySystem m_CustomPartySystem;
        private CityCouncil.CouncilFundingSystem m_FundingSystem;
        private CityCouncil.CouncilElectionSystem m_ElectionSystem;
        private CouncilCustomPartyData m_LastPushedCustomPartyData;
       

        // --- Évènement de ville actif (affiché en bas de l'encart Administration) ---
        private ValueBinding<string> m_CityEventHeadlineBinding;
        private string m_LastPushedEventId; // pour ne repousser que sur changement réel

        // --- Encart "Administration" (district sélectionné) ---
        private ValueBinding<bool> m_AdminVisibleBinding;
        private ValueBinding<string> m_AdminPhaseBinding;          // "NoElection" | "Round1Done" | "Completed" | ...
        private ValueBinding<string> m_AdminLeadingPartyBinding;   // nom du parti, ou "" si non applicable
        private ValueBinding<string> m_AdminFinalist1Binding;      // 2e tour en attente : 1er finaliste
        private ValueBinding<string> m_AdminFinalist2Binding;      // 2e tour en attente : 2e finaliste
        private ValueBinding<int> m_AdminSeatsBinding;
        private ValueBinding<int> m_AdminVotersBinding;
        private ValueBinding<int> m_AdminAbstentionBinding;
        private ValueBinding<string> m_AdminResultsBinding; // répartition finale des sièges, sérialisée en JSON
        private ValueBinding<string> m_AdminBastionStreakPartyBinding; // nom du parti en série, ou "" si aucune série
        private ValueBinding<int> m_AdminBastionStreakCountBinding;    // 0..3
        private ValueBinding<bool> m_AdminBastionActiveBinding;        // true si Bastion effectivement acquis

        // --- Panneau hémicycle (ville entière) ---
        private ValueBinding<string> m_HemicycleSeatsBinding; // agrégat tous districts confondus, sérialisé en JSON
        private ValueBinding<string> m_HemicycleLeaderBinding;
        private ValueBinding<int> m_FundingFixedAmountBinding;
        private ValueBinding<bool> m_FundingLockedBinding;

        // --- Onglet "Votre Parti" ---
        private ValueBinding<bool> m_CustomPartyExistsBinding;
        private ValueBinding<string> m_CustomPartyNameBinding;
        private ValueBinding<string> m_CustomPartyColorBinding;   // enum PartyColor, sérialisé en ToString()
        private ValueBinding<string> m_CustomPartySpaceBinding;   // enum PoliticalParty, sérialisé en ToString()
        private ValueBinding<bool> m_CustomPartyPendingDeletionBinding;
        private ValueBinding<bool> m_CustomPartyPendingActivationBinding;

        // État caché pour détecter les changements
        private Entity m_LastSelectedEntity = Entity.Null;
        private CouncilDistrictData m_LastPushedData;
        private bool m_HasLastPushedData;

        private CouncilPartyMembershipSystem m_MembershipSystem;
        private ValueBinding<string> m_PartyMembershipJsonBinding;
        private CouncilPartyMembershipData m_LastPushedMembership;
        private bool m_HasLastPushedMembership;
        private int m_LastPushedFundingAmount = -1;
        private bool m_LastPushedFundingLocked;
        private bool m_HasLastPushedFunding;

        // Bonus offensif et défensif
        private CityCouncil.CouncilBonusSystem m_BonusSystem;
        private ValueBinding<string> m_AdminLeadingPartyBonusBinding; // "None" | "Defensif" | "Offensif"
        private ValueBinding<string> m_PartyBonusesJsonBinding;
        private ValueBinding<bool> m_PlayerBonusChoicePendingBinding;
        private ValueBinding<string> m_PlayerBonusChoiceSpaceBinding;
        private string m_LastPushedPartyBonusesJson;
        private bool m_LastPushedBonusPending;
        private bool m_HasLastPushedBonus;
        private bool m_HasLastPushedCustomParty;

        //Propagande
        private CityCouncil.CouncilPropagandaSystem m_PropagandaSystem;
        private ValueBinding<string> m_PropagandaStateJsonBinding;
        private string m_LastPushedPropagandaJson;
        private bool m_HasLastPushedPropaganda;


        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_EntityManager = World.EntityManager;
            m_DistrictQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilDistrictData>());
            m_CityEventSystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilCityEventSystem>();
            m_CustomPartySystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilCustomPartySystem>();
            m_MembershipSystem = World.GetOrCreateSystemManaged<CouncilPartyMembershipSystem>();
            m_FundingSystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilFundingSystem>();
            m_ElectionSystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilElectionSystem>();
            m_BonusSystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilBonusSystem>();
            m_PropagandaSystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilPropagandaSystem>();



            m_AdminVisibleBinding = new ValueBinding<bool>(kGroup, "adminVisible", false);
            m_AdminPhaseBinding = new ValueBinding<string>(kGroup, "adminPhase", "NoElection");
            m_AdminLeadingPartyBinding = new ValueBinding<string>(kGroup, "adminLeadingParty", "");
            m_AdminFinalist1Binding = new ValueBinding<string>(kGroup, "adminFinalist1", "");
            m_AdminFinalist2Binding = new ValueBinding<string>(kGroup, "adminFinalist2", "");
            m_AdminSeatsBinding = new ValueBinding<int>(kGroup, "adminSeats", 0);
            m_AdminVotersBinding = new ValueBinding<int>(kGroup, "adminVoters", 0);
            m_AdminAbstentionBinding = new ValueBinding<int>(kGroup, "adminAbstention", 0);
            m_AdminResultsBinding = new ValueBinding<string>(kGroup, "adminResultsJson", "[]");
            m_PartyMembershipJsonBinding = new ValueBinding<string>(kGroup, "partyMembershipJson", "[]");

            m_HemicycleSeatsBinding = new ValueBinding<string>(kGroup, "hemicycleSeatsJson", "[]");
            m_HemicycleLeaderBinding = new ValueBinding<string>(kGroup, "hemicycleLeader", "");
            m_CityEventHeadlineBinding = new ValueBinding<string>(kGroup, "cityEventHeadline", "");

            m_CustomPartyExistsBinding = new ValueBinding<bool>(kGroup, "customPartyExists", false);
            m_CustomPartyNameBinding = new ValueBinding<string>(kGroup, "customPartyName", "");
            m_CustomPartyColorBinding = new ValueBinding<string>(kGroup, "customPartyColor", "");
            m_CustomPartySpaceBinding = new ValueBinding<string>(kGroup, "customPartySpace", "");
            m_CustomPartyPendingDeletionBinding = new ValueBinding<bool>(kGroup, "customPartyPendingDeletion", false);
            m_CustomPartyPendingActivationBinding = new ValueBinding<bool>(kGroup, "customPartyPendingActivation", false);
            m_FundingFixedAmountBinding = new ValueBinding<int>(kGroup, "fundingFixedAmount", 0);
            m_FundingLockedBinding = new ValueBinding<bool>(kGroup, "fundingLocked", false);
            m_AdminBastionStreakPartyBinding = new ValueBinding<string>(kGroup, "adminBastionStreakParty", "");
            m_AdminBastionStreakCountBinding = new ValueBinding<int>(kGroup, "adminBastionStreakCount", 0);
            m_AdminBastionActiveBinding = new ValueBinding<bool>(kGroup, "adminBastionActive", false);
            m_AdminLeadingPartyBonusBinding = new ValueBinding<string>(kGroup, "adminLeadingPartyBonus", "None");
            m_PartyBonusesJsonBinding = new ValueBinding<string>(kGroup, "partyBonusesJson", "[]");
            m_PlayerBonusChoicePendingBinding = new ValueBinding<bool>(kGroup, "playerBonusChoicePending", false);
            m_PlayerBonusChoiceSpaceBinding = new ValueBinding<string>(kGroup, "playerBonusChoiceSpace", "");
            m_PropagandaStateJsonBinding = new ValueBinding<string>(kGroup, "propagandaStateJson", "[]");
            



            AddBinding(m_AdminVisibleBinding);
            AddBinding(m_AdminPhaseBinding);
            AddBinding(m_AdminLeadingPartyBinding);
            AddBinding(m_AdminFinalist1Binding);
            AddBinding(m_AdminFinalist2Binding);
            AddBinding(m_AdminSeatsBinding);
            AddBinding(m_AdminVotersBinding);
            AddBinding(m_AdminAbstentionBinding);
            AddBinding(m_AdminResultsBinding);

            AddBinding(m_HemicycleSeatsBinding);
            AddBinding(m_HemicycleLeaderBinding);
            AddBinding(m_CityEventHeadlineBinding);

            AddBinding(m_CustomPartyExistsBinding);
            AddBinding(m_CustomPartyNameBinding);
            AddBinding(m_CustomPartyColorBinding);
            AddBinding(m_CustomPartySpaceBinding);
            AddBinding(m_CustomPartyPendingDeletionBinding);
            AddBinding(m_CustomPartyPendingActivationBinding);
            AddBinding(m_PartyMembershipJsonBinding);
            AddBinding(m_FundingFixedAmountBinding);
            AddBinding(m_FundingLockedBinding);
            AddBinding(m_AdminBastionStreakPartyBinding);
            AddBinding(m_AdminBastionStreakCountBinding);
            AddBinding(m_AdminBastionActiveBinding);
            AddBinding(m_AdminLeadingPartyBonusBinding);
            AddBinding(m_PartyBonusesJsonBinding);
            AddBinding(m_PlayerBonusChoicePendingBinding);
            AddBinding(m_PlayerBonusChoiceSpaceBinding);
            AddBinding(m_PropagandaStateJsonBinding);

            AddBinding(new TriggerBinding<string, string, string, string>(kGroup, "launchCampaign", // AJOUT un paramètre string
    (partyStr, targetStr, intensityStr, autoRenewStr) =>
    {
        if (System.Enum.TryParse<PoliticalParty>(partyStr, out var party)
            && System.Enum.TryParse<CampaignTarget>(targetStr, out var target)
            && System.Enum.TryParse<CampaignIntensity>(intensityStr, out var intensity))
        {
            bool autoRenew = autoRenewStr == "true";
            m_PropagandaSystem.TryLaunchCampaign(party, target, intensity, autoRenew, out _);
        }
        UpdatePropagandaBindingIfChanged(force: true);
    }));

            AddBinding(new TriggerBinding<string>(kGroup, "cancelCampaign",
    (partyStr) =>
    {
        if (System.Enum.TryParse<PoliticalParty>(partyStr, out var party))
            m_PropagandaSystem.TryCancelCampaign(party, out _);
        UpdatePropagandaBindingIfChanged(force: true);
    }));

            AddBinding(new TriggerBinding<string>(kGroup, "choosePlayerPermanentBonus",
    (typeStr) =>
    {
        if (System.Enum.TryParse<PermanentBonusType>(typeStr, out var type))
            m_BonusSystem.ChoosePlayerBonus(type);
        UpdateBonusBindingIfChanged(force: true);
    }));

            AddBinding(new TriggerBinding(kGroup, "debugExpireCampaigns",
    () => m_PropagandaSystem.DebugExpireAllCampaigns()));

            // OUTIL DEBUG TEMPORAIRE POUR COMPTAGE DES ADHERENTS ET COTISATION
            AddBinding(new TriggerBinding(kGroup, "debugForceCycleCheck",
    () => m_MembershipSystem.DebugForceCycleCheck()));

            // OUTIL DE DEBUG TEMPORAIRE POUR BONUS PERMANENT
            AddBinding(new TriggerBinding(kGroup, "debugForceMajorityCheck",
                () => m_BonusSystem.DebugForceMajorityCheck()));

            // OUTIL DE DEBUG TEMPORAIRE — cf. CouncilElectionSystem.DebugForceAllDistrictsToNextStep.
            AddBinding(new TriggerBinding(kGroup, "debugForceNextElection",
                () => m_ElectionSystem.DebugForceAllDistrictsToNextStep()));

            // Déclenché par le clic sur l'icône hémicycle en haut à gauche.
            AddBinding(new TriggerBinding(kGroup, "refreshHemicycle", RefreshHemicycle));

            // --- Triggers "Votre Parti" ---
            // NOTE : signature TriggerBinding<string,byte,byte> non confirmée par décompilation
            // (seul TriggerBinding sans argument l'a été, via refreshHemicycle ci-dessus et
            // DistrictNotesMod). Si le SDK expose une autre forme pour les triggers avec
            // arguments côté C#/Colossal.UI.Binding, adapter cette ligne et l'appel React
            // correspondant (trigger("cityCouncil", "createOrUpdateCustomParty", name, colorIdx,
            // spaceIdx) côté JS) en conséquence.
            AddBinding(new TriggerBinding<string, string, string>(kGroup, "createOrUpdateCustomParty",
     (name, colorName, spaceName) =>
     {
         if (!System.Enum.TryParse<CityCouncil.PartyColor>(colorName, out var color))
             color = CityCouncil.PartyColor.Bleu;
         if (!System.Enum.TryParse<CityCouncil.PoliticalParty>(spaceName, out var space))
             space = CityCouncil.PoliticalParty.Democrate;

         m_CustomPartySystem.TryCreateOrUpdate(name, color, space, out _);
         PushCustomPartyState();
         PushFundingState();
     }));

            AddBinding(new TriggerBinding(kGroup, "requestDeleteCustomParty",
                () => { m_CustomPartySystem.RequestDeletion(); PushCustomPartyState(); }));

            AddBinding(new TriggerBinding(kGroup, "cancelDeleteCustomParty",
                () => { m_CustomPartySystem.CancelPendingDeletion(); PushCustomPartyState(); }));

            m_LastSelectedEntity = m_ToolSystem.selected;

            PushCustomPartyState();

            AddBinding(new TriggerBinding<string>(kGroup, "setFundingFixedAmount",
    (amountStr) =>
    {
        if (int.TryParse(amountStr, out var amount))
            m_FundingSystem.TrySetFixedAmount(amount, out _);
        PushFundingState();
    }));

            AddBinding(new TriggerBinding(kGroup, "validateFundingFixedAmount",
                () => { m_FundingSystem.ValidateFixedAmount(); PushFundingState(); }));
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            UpdateMembershipBindingIfChanged();
            UpdateFundingBindingIfChanged();
            UpdateCityEventBinding();
            UpdateBonusBindingIfChanged();
            UpdateCustomPartyBindingIfChanged();
            UpdatePropagandaBindingIfChanged();


            Entity selected = m_ToolSystem.selected;
            bool isDistrict = selected != Entity.Null
                && m_EntityManager.Exists(selected)
                && m_EntityManager.HasComponent<District>(selected);

            bool selectionChanged = selected != m_LastSelectedEntity;

            if (selectionChanged)
            {
                m_LastSelectedEntity = selected;
                m_HasLastPushedData = false; // force un refresh complet sur nouvelle sélection
            }

            if (!isDistrict)
            {
                if (m_AdminVisibleBinding.value)
                    m_AdminVisibleBinding.Update(false);
                return;
            }

            bool hasCouncilData = m_EntityManager.HasComponent<CouncilDistrictData>(selected);
            if (!hasCouncilData)
            {
                // District pas encore traité par CouncilElectionSystem (première frame après création)
                if (m_AdminVisibleBinding.value)
                    m_AdminVisibleBinding.Update(false);
                return;
            }

            var data = m_EntityManager.GetComponentData<CouncilDistrictData>(selected);

            // Ne repousse que si quelque chose a réellement changé, comme sur DistrictNotesMod.
            if (!selectionChanged && m_HasLastPushedData && DataEquals(data, m_LastPushedData))
                return;

            PushAdminData(data);
            m_LastPushedData = data;
            m_HasLastPushedData = true;
        }

        private void UpdateCustomPartyBindingIfChanged()
        {
            var data = m_CustomPartySystem.GetData();
            if (m_HasLastPushedCustomParty && CustomPartyEquals(data, m_LastPushedCustomPartyData)) return;

            PushCustomPartyState(data);
        }

        private static bool CustomPartyEquals(in CouncilCustomPartyData a, in CouncilCustomPartyData b)
        {
            return a.m_Exists == b.m_Exists
                && a.m_Name.Equals(b.m_Name)
                && a.m_Color == b.m_Color
                && a.m_Space == b.m_Space
                && a.m_PendingDeletion == b.m_PendingDeletion
                && a.m_SubstitutionActive == b.m_SubstitutionActive
                && a.m_ActiveSpace == b.m_ActiveSpace;
        }

        private void UpdateMembershipBindingIfChanged()
        {
            var data = m_MembershipSystem.GetData();
            if (m_HasLastPushedMembership && MembershipEquals(data, m_LastPushedMembership)) return;
            PushMembershipState(data);
        }

        private void PushMembershipState(CouncilPartyMembershipData? preloaded = null)
        {
            var data = preloaded ?? m_MembershipSystem.GetData();
            var dto = data.m_Entries.ToArray().Select(e => new PartyMembershipDto
            {
                party = e.m_Party.ToString(),
                members = (int)MathF.Round(e.m_Members),
                treasury = e.m_Treasury
            }).ToArray();

            m_PartyMembershipJsonBinding.Update(PartyMembershipDto.ToJsonArray(dto));
            m_LastPushedMembership = data;
            m_HasLastPushedMembership = true;
        }

        private static bool MembershipEquals(in CouncilPartyMembershipData a, in CouncilPartyMembershipData b)
        {
            if (a.m_Entries.Length != b.m_Entries.Length) return false;
            for (int i = 0; i < a.m_Entries.Length; i++)
            {
                if (a.m_Entries[i].m_Party != b.m_Entries[i].m_Party) return false;
                if (a.m_Entries[i].m_Members != b.m_Entries[i].m_Members) return false;
                if (a.m_Entries[i].m_Treasury != b.m_Entries[i].m_Treasury) return false;
            }
            return true;
        }

        protected override void OnGameLoaded(Colossal.Serialization.Entities.Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);

            // OnCreate() s'exécute avant que la sauvegarde soit chargée : PushCustomPartyState()
            // y lisait donc l'état par défaut de CouncilCustomPartySystem (avant que son propre
            // OnGameLoaded ait restauré les vraies données). On repousse ici, une fois la
            // sérialisation terminée, pour que l'UI reflète l'état réellement chargé.
            PushCustomPartyState();
            PushMembershipState(); // AJOUT — même raison : lire l'état APRÈS restauration, pas avant
            PushFundingState();
            UpdateBonusBindingIfChanged(force: true);
            UpdatePropagandaBindingIfChanged(force: true);
        }

        private void UpdatePropagandaBindingIfChanged(bool force = false)
        {
            var data = m_PropagandaSystem.GetData();
            string json = PropagandaDto.ToJsonArray(
                data.m_Entries.ToArray().Select(e => new PropagandaDto
                {
                    party = e.m_Party.ToString(),
                    active = e.m_Active,
                    target = e.m_Active ? e.m_Target.ToString() : "",
                    bonusPercent = e.m_Active ? e.m_BonusPercent : 0f,
                    autoRenew = e.m_Active && e.m_AutoRenew // AJOUT
                }));

            if (!force && m_HasLastPushedPropaganda && json == m_LastPushedPropagandaJson) return;

            m_PropagandaStateJsonBinding.Update(json);
            m_LastPushedPropagandaJson = json;
            m_HasLastPushedPropaganda = true;
        }

        private void UpdateFundingBindingIfChanged()
        {
            var data = m_FundingSystem.GetData();
            if (m_HasLastPushedFunding
                && data.m_FixedAmount == m_LastPushedFundingAmount
                && data.m_FixedAmountLocked == m_LastPushedFundingLocked)
                return;

            PushFundingState(data);
        }

        private void PushFundingState(CouncilFundingData? preloaded = null)
        {
            var data = preloaded ?? m_FundingSystem.GetData();
            m_FundingFixedAmountBinding.Update(data.m_FixedAmount);
            m_FundingLockedBinding.Update(data.m_FixedAmountLocked);
            m_LastPushedFundingAmount = data.m_FixedAmount;
            m_LastPushedFundingLocked = data.m_FixedAmountLocked;
            m_HasLastPushedFunding = true;
        }

        /// <summary>
        /// Pousse le texte de l'évènement de ville actif (ou "" si aucun), uniquement quand
        /// il change réellement — même principe que le reste du fichier.
        /// </summary>
        private void UpdateCityEventBinding()
        {
            var activeEvent = m_CityEventSystem.GetActiveEventDefinition();
            string currentId = activeEvent?.Id;

            if (currentId == m_LastPushedEventId) return;

            m_LastPushedEventId = currentId;
            m_CityEventHeadlineBinding.Update(activeEvent?.Headline ?? "");
        }

        /// <summary>Pousse l'état complet du parti joueur (appelé au OnCreate et après chaque action).</summary>
        private void PushCustomPartyState(CouncilCustomPartyData? preloaded = null)
        {
            var data = preloaded ?? m_CustomPartySystem.GetData();
            m_CustomPartyExistsBinding.Update(data.m_Exists);
            m_CustomPartyNameBinding.Update(data.m_Exists ? data.m_Name.ToString() : "");
            m_CustomPartyColorBinding.Update(data.m_Exists ? data.m_Color.ToString() : "");
            m_CustomPartySpaceBinding.Update(data.m_Exists ? data.m_Space.ToString() : "");
            m_CustomPartyPendingDeletionBinding.Update(data.m_Exists && data.m_PendingDeletion);

            bool pendingActivation = data.m_Exists && !data.m_PendingDeletion
                && (!data.m_SubstitutionActive || data.m_ActiveSpace != data.m_Space);
            m_CustomPartyPendingActivationBinding.Update(pendingActivation);

            m_LastPushedCustomPartyData = data;
            m_HasLastPushedCustomParty = true;
        }

        private void PushAdminData(CouncilDistrictData data)
        {
            m_AdminVisibleBinding.Update(true);
            m_AdminPhaseBinding.Update(data.m_Phase.ToString());
            m_AdminSeatsBinding.Update(data.m_TotalSeats);

            bool hasResults = data.m_Phase == ElectionPhase.Completed && data.m_FinalResults.Length > 0;

            m_AdminLeadingPartyBinding.Update(hasResults ? data.m_LeadingParty.ToString() : "");

            if (data.m_Phase == ElectionPhase.Round1Done && !data.m_WonInRound1 && data.m_Round1Results.Length >= 2)
            {
                m_AdminFinalist1Binding.Update(data.m_Round1Results[0].m_Party.ToString());
                m_AdminFinalist2Binding.Update(data.m_Round1Results[1].m_Party.ToString());
            }
            else
            {
                m_AdminFinalist1Binding.Update("");
                m_AdminFinalist2Binding.Update("");
            }

            // Round2 prévaut sur Round1 pour l'affichage "votants/abstention" une fois disponible.
            bool round2Done = data.m_VotersRound2 > 0 || data.m_AbstentionRound2 > 0;
            m_AdminVotersBinding.Update(round2Done ? data.m_VotersRound2 : data.m_VotersRound1);
            m_AdminAbstentionBinding.Update(round2Done ? data.m_AbstentionRound2 : data.m_AbstentionRound1);

            var results = hasResults
     ? data.m_FinalResults.ToArray().Select(PartyResultDto.From).Select(DecorateWithCustomParty).ToArray()
     : System.Array.Empty<PartyResultDto>();
            m_AdminResultsBinding.Update(PartyResultDto.ToJsonArray(results));

            // Barre de progression Bastion.
            m_AdminBastionStreakPartyBinding.Update(data.m_StreakCount > 0 ? data.m_StreakParty.ToString() : "");
            m_AdminBastionStreakCountBinding.Update(data.m_StreakCount);
            m_AdminBastionActiveBinding.Update(data.m_IsBastion);

            // Bonus permanent du parti leader (icône affichée à côté du logo côté React).
            m_AdminLeadingPartyBonusBinding.Update(
                hasResults ? m_BonusSystem.GetBonus(data.m_LeadingParty).ToString() : "None");
        }

        private void UpdateBonusBindingIfChanged(bool force = false)
        {
            var bonusData = m_BonusSystem.GetData();
            string json = PartyBonusDto.ToJsonArray(
                System.Enum.GetValues(typeof(PoliticalParty))
                    .Cast<PoliticalParty>()
                    .Select(p => new PartyBonusDto { party = p.ToString(), bonus = m_BonusSystem.GetBonus(p).ToString() }));

            bool pending = bonusData.m_PlayerChoicePending;

            if (!force && m_HasLastPushedBonus && json == m_LastPushedPartyBonusesJson && pending == m_LastPushedBonusPending)
                return;

            m_PartyBonusesJsonBinding.Update(json);
            m_PlayerBonusChoicePendingBinding.Update(pending);
            m_PlayerBonusChoiceSpaceBinding.Update(pending ? bonusData.m_PlayerChoiceSpace.ToString() : "");

            m_LastPushedPartyBonusesJson = json;
            m_LastPushedBonusPending = pending;
            m_HasLastPushedBonus = true;
        }

        public struct PartyBonusDto
        {
            public string party;
            public string bonus;

            public static string ToJsonArray(System.Collections.Generic.IEnumerable<PartyBonusDto> items)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append('[');
                bool first = true;
                foreach (var e in items)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('{');
                    sb.Append("\"party\":\"").Append(e.party).Append("\",");
                    sb.Append("\"bonus\":\"").Append(e.bonus).Append("\"");
                    sb.Append('}');
                }
                sb.Append(']');
                return sb.ToString();
            }
        }

        public struct PropagandaDto
        {
            public string party;
            public bool active;
            public string target;
            public float bonusPercent;
            public bool autoRenew; // AJOUT

            public static string ToJsonArray(System.Collections.Generic.IEnumerable<PropagandaDto> items)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append('[');
                bool first = true;
                foreach (var e in items)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('{');
                    sb.Append("\"party\":\"").Append(e.party).Append("\",");
                    sb.Append("\"active\":").Append(e.active ? "true" : "false").Append(',');
                    sb.Append("\"target\":\"").Append(e.target).Append("\",");
                    sb.Append("\"bonusPercent\":").Append(e.bonusPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                    sb.Append("\"autoRenew\":").Append(e.autoRenew ? "true" : "false"); // AJOUT
                    sb.Append('}');
                }
                sb.Append(']');
                return sb.ToString();
            }
        }

        /// <summary>
        /// Recalcule l'agrégat ville entière à la demande (clic sur l'icône), plutôt qu'en continu.
        /// </summary>
        private void RefreshHemicycle()
        {
            var totals = new System.Collections.Generic.Dictionary<CityCouncil.PoliticalParty, int>();
            CityCouncil.PoliticalParty leader = default;
            int leaderSeats = -1;

            var districts = m_DistrictQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            try
            {
                foreach (var districtEntity in districts)
                {
                    var data = m_EntityManager.GetComponentData<CouncilDistrictData>(districtEntity);
                    if (data.m_Phase != ElectionPhase.Completed) continue;

                    foreach (var result in data.m_FinalResults)
                    {
                        totals.TryGetValue(result.m_Party, out int current);
                        int updated = current + result.m_Seats;
                        totals[result.m_Party] = updated;

                        if (updated > leaderSeats)
                        {
                            leaderSeats = updated;
                            leader = result.m_Party;
                        }
                    }
                }
            }
            finally
            {
                districts.Dispose();
            }

            var seatsDto = totals
                .Select(kv => new PartyResultDto { party = kv.Key.ToString(), seats = kv.Value, voteShare = 0f })
                .Select(DecorateWithCustomParty)
                .OrderByDescending(r => r.seats)
                .ToArray();

            m_HemicycleSeatsBinding.Update(PartyResultDto.ToJsonArray(seatsDto));
            m_HemicycleLeaderBinding.Update(leaderSeats > 0 ? leader.ToString() : "");
        }

        /// <summary>
        /// Si le parti joueur existe et que son bord politique correspond à ce résultat,
        /// remplace le nom/la couleur affichés par ceux du parti joueur. La clé technique
        /// "party" (utilisée pour PARTY_COLORS/PARTY_ORDER côté React comme fallback et pour
        /// le tri idéologique) reste inchangée : seuls displayName/displayColor sont ajoutés.
        /// </summary>
        /// 
        // --- DecorateWithCustomParty : ne décore que si la substitution est ACTIVE ---
        private PartyResultDto DecorateWithCustomParty(PartyResultDto dto)
        {
            var custom = m_CustomPartySystem.GetData();
            if (custom.m_Exists && custom.m_SubstitutionActive && custom.m_ActiveSpace.ToString() == dto.party)
            {
                dto.displayName = custom.m_Name.ToString();
                dto.displayColor = custom.m_Color.ToString();
            }
            return dto;
        }

        private static bool DataEquals(in CouncilDistrictData a, in CouncilDistrictData b)
        {
            return a.m_Phase == b.m_Phase
               && a.m_LeadingParty == b.m_LeadingParty
               && a.m_TotalSeats == b.m_TotalSeats
               && a.m_VotersRound1 == b.m_VotersRound1
               && a.m_AbstentionRound1 == b.m_AbstentionRound1
               && a.m_VotersRound2 == b.m_VotersRound2
               && a.m_AbstentionRound2 == b.m_AbstentionRound2
               && a.m_StreakParty == b.m_StreakParty       
               && a.m_StreakCount == b.m_StreakCount       
               && a.m_IsBastion == b.m_IsBastion;          
        }
    }

    /// <summary>
    /// DTO utilisé pour sérialiser un résultat de parti en JSON manuellement (utilisé pour
    /// adminResultsJson et hemicycleSeatsJson). Sérialisation manuelle plutôt que
    /// ValueBinding&lt;PartyResultDto[]&gt; : le binding system CS2 ne sait pas résoudre
    /// automatiquement un writer pour un tableau de struct custom (MissingMethodException
    /// sur ArrayWriter&lt;T&gt; faute de constructeur sans paramètre) ; passer par une simple
    /// string JSON, parsée côté React, contourne le problème sans dépendre d'une API de
    /// writer non documentée.
    ///
    /// displayName/displayColor : optionnels (chaîne vide = absent), remplissent uniquement le
    /// parti habillé par le parti joueur (cf. CouncilUISystem.DecorateWithCustomParty). Le React
    /// doit retomber sur PARTY_LABELS[party]/PARTY_COLORS[party] quand ils sont vides.
    /// </summary>
    public struct PartyResultDto
    {
        public string party;
        public int seats;
        public float voteShare;
        public string displayName;
        public string displayColor;

        public static PartyResultDto From(PartyResult r) => new PartyResultDto
        {
            party = r.m_Party.ToString(),
            seats = r.m_Seats,
            voteShare = r.m_VoteShare,
            displayName = "",
            displayColor = ""
        };

        /// <summary>
        /// Sérialisation JSON minimale et manuelle. Les noms de partis (enum ToString()) et les
        /// couleurs prédéfinies sont alphanumériques sans caractère spécial. displayName vient
        /// du joueur (texte libre) : on échappe a minima guillemets et backslash pour éviter de
        /// casser le JSON — la validation de longueur/contenu stricte reste côté C# (TryCreateOrUpdate).
        /// </summary>
        public static string ToJsonArray(System.Collections.Generic.IEnumerable<PartyResultDto> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var r in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{');
                sb.Append("\"party\":\"").Append(r.party).Append("\",");
                sb.Append("\"seats\":").Append(r.seats.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                sb.Append("\"voteShare\":").Append(r.voteShare.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                sb.Append("\"displayName\":\"").Append(EscapeJson(r.displayName ?? "")).Append("\",");
                sb.Append("\"displayColor\":\"").Append(r.displayColor ?? "").Append("\"");
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();


        }

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }



    public struct PartyMembershipDto
    {
        public string party;
        public int members;
        public int treasury;

        public static string ToJsonArray(System.Collections.Generic.IEnumerable<PartyMembershipDto> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var e in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{');
                sb.Append("\"party\":\"").Append(e.party).Append("\",");
                sb.Append("\"members\":").Append(e.members).Append(',');
                sb.Append("\"treasury\":").Append(e.treasury);
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }


    }


}