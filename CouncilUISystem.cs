using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using Colossal.UI.Binding;
using Game.Areas;
using Game.Tools;
using Game.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
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
        private CityCouncil.CouncilBlackFundSystem m_BlackFundSystem;
        private CityCouncil.CouncilElectoralCommissionSystem m_CommissionSystem;
        private CouncilCustomPartyData m_LastPushedCustomPartyData;
        private CityCouncil.CouncilEconomySystem m_EconomySystem;
        private CityCouncil.CouncilTaxSystem m_TaxSystem;


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
        private ValueBinding<string> m_AdminRound1ResultsBinding;
        private ValueBinding<string> m_AdminBastionStreakPartyBinding; // nom du parti en série, ou "" si aucune série
        private ValueBinding<int> m_AdminBastionStreakCountBinding;    // 0..3
        private ValueBinding<bool> m_AdminBastionActiveBinding;        // true si Bastion effectivement acquis

        // --- Panneau hémicycle (ville entière) ---
        private ValueBinding<string> m_HemicycleSeatsBinding; // agrégat tous districts confondus, sérialisé en JSON
        private ValueBinding<string> m_HemicycleLeaderBinding;
        private ValueBinding<int> m_FundingFixedAmountBinding;
        private ValueBinding<bool> m_FundingLockedBinding;
        private ValueBinding<bool> m_FundingAutoRenewBinding;

        // --- Onglet "Votre Parti" ---
        private ValueBinding<bool> m_CustomPartyExistsBinding;
        private ValueBinding<string> m_CustomPartyNameBinding;
        private ValueBinding<string> m_CustomPartyColorBinding;   // enum PartyColor, sérialisé en ToString()
        private ValueBinding<string> m_CustomPartySpaceBinding;   // enum PoliticalParty, sérialisé en ToString()
        private ValueBinding<bool> m_CustomPartyPendingDeletionBinding;
        private ValueBinding<bool> m_CustomPartyPendingActivationBinding;
        private ValueBinding<string> m_CustomPartyStructureTypeBinding;

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

        //Bonus Exclusif
        private CityCouncil.CouncilInstitutionSystem m_InstitutionSystem;
        private ValueBinding<bool> m_RepublicanBureauBonusActiveBinding;
        private ValueBinding<bool> m_CentralIntelligenceBureauPresentBinding;
        private bool m_LastPushedBureauBonusActive;
        private bool m_LastPushedBureauPresent;
        private bool m_HasLastPushedBureauState;
        private ValueBinding<bool> m_PopulistPrisonBonusActiveBinding;
        private ValueBinding<bool> m_PrisonPresentBinding;
        private ValueBinding<int> m_IllegalCampaignCostBinding;
        private bool m_LastPushedPrisonBonusActive;
        private bool m_LastPushedPrisonPresent;
        private int m_LastPushedIllegalCampaignCost = -1;
        private bool m_HasLastPushedPrisonState;
        private ValueBinding<bool> m_EcologistNuclearBonusActiveBinding;
        private ValueBinding<bool> m_NuclearPowerPlantPresentBinding;
        private bool m_LastPushedNuclearBonusActive;
        private bool m_LastPushedNuclearPresent;
        private bool m_HasLastPushedNuclearState;
        private ValueBinding<bool> m_RadicalLeftUniversityBonusActiveBinding;
        private ValueBinding<bool> m_UniversityPresentBinding;
        private ValueBinding<int> m_DistrictCampaignMaxBinding;
        private bool m_LastPushedUniversityBonusActive;
        private bool m_LastPushedUniversityPresent;
        private int m_LastPushedDistrictCampaignMax = -1;
        private bool m_HasLastPushedUniversityState;
        private ValueBinding<bool> m_DemocratDigitalBonusActiveBinding;
        private ValueBinding<bool> m_SatelliteUplinkPresentBinding;
        private bool m_LastPushedDigitalBonusActive;
        private bool m_LastPushedSatellitePresent;
        private bool m_HasLastPushedDigitalState;

        //Propagande
        private CityCouncil.CouncilPropagandaSystem m_PropagandaSystem;
        private ValueBinding<string> m_PropagandaStateJsonBinding;
        private ValueBinding<string> m_DistrictListJsonBinding;      // [{id, name}] pour le menu déroulant
        private ValueBinding<string> m_DistrictCampaignsJsonBinding; // campagnes actives du parti joueur, tous districts
        private int m_LastPushedDistrictCount = -1;
        private string m_LastPushedPropagandaJson;
        private bool m_HasLastPushedPropaganda;
        private string m_LastPushedDistrictCampaignsJson;
        private bool m_HasLastPushedDistrictCampaigns;
        private Game.UI.NameSystem m_NameSystem;


        // caisse noire et campagnes illégales
        private ValueBinding<string> m_BlackFundJsonBinding;         // état caisse noire du parti joueur
        private ValueBinding<string> m_IllegalCampaignsJsonBinding;  // campagnes illégales du parti joueur
        private ValueBinding<string> m_CommissionReportJsonBinding;  // vigilance + sanctions, tous partis
        private ValueBinding<string> m_LastInvoiceLocaleKeyBinding;  // dernier libellé de facture tiré
        private string m_LastPushedBlackFundJson;
        private bool m_HasLastPushedBlackFund;
        private string m_LastPushedIllegalCampaignsJson;
        private bool m_HasLastPushedIllegalCampaigns;
        private string m_LastPushedCommissionReportJson;
        private bool m_HasLastPushedCommissionReport;

        // Sondages
        private CityCouncil.CouncilPollSystem m_PollSystem;
        private ValueBinding<string> m_PollResultsJsonBinding;
        private ValueBinding<bool> m_PollAllowedBinding;
        private double m_LastPushedPollDay = double.MinValue;
        private bool m_HasLastPushedPoll;
        private ValueBinding<string> m_ElectoralContextJsonBinding;
        private string m_LastPushedElectoralContextJson;
        private bool m_HasLastPushedElectoralContext;

        //Score
        private CityCouncil.CouncilScoreSystem m_ScoreSystem;
        private ValueBinding<string> m_ScoreJsonBinding;
        private string m_LastPushedScoreJson;
        private bool m_HasLastPushedScore;

        // Consigne de vote
        private CityCouncil.CouncilVotingInstructionSystem m_VotingInstructionSystem;
        private ValueBinding<string> m_VotingInstructionDistrictsJsonBinding;
        private string m_LastPushedVotingInstructionDistrictsJson;
        private bool m_HasLastPushedVotingInstructionDistricts;

        //Onglet DEBUG
        private ValueBinding<bool> m_ShowDebugTabBinding;
        private bool m_LastPushedShowDebugTab;
        private bool m_HasLastPushedShowDebugTab;



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
            m_NameSystem = World.GetOrCreateSystemManaged<Game.UI.NameSystem>();
            m_BlackFundSystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilBlackFundSystem>();
            m_CommissionSystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilElectoralCommissionSystem>();
            m_PollSystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilPollSystem>();
            m_ScoreSystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilScoreSystem>();
            m_EconomySystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilEconomySystem>();
            m_TaxSystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilTaxSystem>();
            m_InstitutionSystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilInstitutionSystem>();
            m_VotingInstructionSystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilVotingInstructionSystem>();




            m_AdminVisibleBinding = new ValueBinding<bool>(kGroup, "adminVisible", false);
            m_AdminPhaseBinding = new ValueBinding<string>(kGroup, "adminPhase", "NoElection");
            m_AdminLeadingPartyBinding = new ValueBinding<string>(kGroup, "adminLeadingParty", "");
            m_AdminFinalist1Binding = new ValueBinding<string>(kGroup, "adminFinalist1", "");
            m_AdminFinalist2Binding = new ValueBinding<string>(kGroup, "adminFinalist2", "");
            m_AdminSeatsBinding = new ValueBinding<int>(kGroup, "adminSeats", 0);
            m_AdminVotersBinding = new ValueBinding<int>(kGroup, "adminVoters", 0);
            m_AdminAbstentionBinding = new ValueBinding<int>(kGroup, "adminAbstention", 0);
            m_AdminResultsBinding = new ValueBinding<string>(kGroup, "adminResultsJson", "[]");
            m_AdminRound1ResultsBinding = new ValueBinding<string>(kGroup, "adminRound1ResultsJson", "[]");
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
            m_DistrictListJsonBinding = new ValueBinding<string>(kGroup, "districtListJson", "[]");
            m_DistrictCampaignsJsonBinding = new ValueBinding<string>(kGroup, "districtCampaignsJson", "[]");
            m_BlackFundJsonBinding = new ValueBinding<string>(kGroup, "blackFundJson", "{}");
            m_IllegalCampaignsJsonBinding = new ValueBinding<string>(kGroup, "illegalCampaignsJson", "[]");
            m_CommissionReportJsonBinding = new ValueBinding<string>(kGroup, "commissionReportJson", "[]");
            m_LastInvoiceLocaleKeyBinding = new ValueBinding<string>(kGroup, "lastInvoiceLocaleKey", "");
            m_PollResultsJsonBinding = new ValueBinding<string>(kGroup, "pollResultsJson", "[]");
            m_PollAllowedBinding = new ValueBinding<bool>(kGroup, "pollAllowed", true);
            m_ScoreJsonBinding = new ValueBinding<string>(kGroup, "scoreJson", "[]");
            m_ShowDebugTabBinding = new ValueBinding<bool>(kGroup, "showDebugTab", false);
            m_FundingAutoRenewBinding = new ValueBinding<bool>(kGroup, "fundingAutoRenew", false);
            m_CustomPartyStructureTypeBinding = new ValueBinding<string>(kGroup, "customPartyStructureType", "Cadres");           
            m_ElectoralContextJsonBinding = new ValueBinding<string>(kGroup, "electoralContextJson", "{}");
            m_RepublicanBureauBonusActiveBinding = new ValueBinding<bool>(kGroup, "republicanBureauBonusActive", false);
            m_CentralIntelligenceBureauPresentBinding = new ValueBinding<bool>(kGroup, "centralIntelligenceBureauPresent", false);
            m_PopulistPrisonBonusActiveBinding = new ValueBinding<bool>(kGroup, "populistPrisonBonusActive", false);
            m_PrisonPresentBinding = new ValueBinding<bool>(kGroup, "prisonPresent", false);
            m_IllegalCampaignCostBinding = new ValueBinding<int>(kGroup, "illegalCampaignCost", CityCouncil.IllegalCampaignCatalog.Cost);
            m_EcologistNuclearBonusActiveBinding = new ValueBinding<bool>(kGroup, "ecologistNuclearBonusActive", false);
            m_NuclearPowerPlantPresentBinding = new ValueBinding<bool>(kGroup, "nuclearPowerPlantPresent", false);
            m_RadicalLeftUniversityBonusActiveBinding = new ValueBinding<bool>(kGroup, "radicalLeftUniversityBonusActive", false);
            m_UniversityPresentBinding = new ValueBinding<bool>(kGroup, "universityPresent", false);
            m_DistrictCampaignMaxBinding = new ValueBinding<int>(kGroup, "districtCampaignMax", CityCouncil.DistrictCampaignCatalog.MaxActiveCampaignsPerParty);
            m_DemocratDigitalBonusActiveBinding = new ValueBinding<bool>(kGroup, "democratDigitalBonusActive", false);
            m_SatelliteUplinkPresentBinding = new ValueBinding<bool>(kGroup, "satelliteUplinkPresent", false);
            m_VotingInstructionDistrictsJsonBinding = new ValueBinding<string>(kGroup, "votingInstructionDistrictsJson", "[]");


            AddBinding(m_AdminVisibleBinding);
            AddBinding(m_AdminPhaseBinding);
            AddBinding(m_AdminLeadingPartyBinding);
            AddBinding(m_AdminFinalist1Binding);
            AddBinding(m_AdminFinalist2Binding);
            AddBinding(m_AdminSeatsBinding);
            AddBinding(m_AdminVotersBinding);
            AddBinding(m_AdminAbstentionBinding);
            AddBinding(m_AdminResultsBinding);
            AddBinding(m_AdminRound1ResultsBinding);

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
            AddBinding(m_DistrictListJsonBinding);
            AddBinding(m_DistrictCampaignsJsonBinding);
            AddBinding(m_BlackFundJsonBinding);
            AddBinding(m_IllegalCampaignsJsonBinding);
            AddBinding(m_CommissionReportJsonBinding);
            AddBinding(m_LastInvoiceLocaleKeyBinding);
            AddBinding(m_PollResultsJsonBinding);
            AddBinding(m_PollAllowedBinding);
            AddBinding(m_ScoreJsonBinding);
            AddBinding(m_ShowDebugTabBinding);
            AddBinding(m_FundingAutoRenewBinding);
            AddBinding(m_CustomPartyStructureTypeBinding);
            AddBinding(m_ElectoralContextJsonBinding);
            AddBinding(m_RepublicanBureauBonusActiveBinding);
            AddBinding(m_CentralIntelligenceBureauPresentBinding);
            AddBinding(m_PopulistPrisonBonusActiveBinding);
            AddBinding(m_PrisonPresentBinding);
            AddBinding(m_IllegalCampaignCostBinding);
            AddBinding(m_EcologistNuclearBonusActiveBinding);
            AddBinding(m_NuclearPowerPlantPresentBinding);
            AddBinding(m_RadicalLeftUniversityBonusActiveBinding);
            AddBinding(m_UniversityPresentBinding);
            AddBinding(m_DistrictCampaignMaxBinding);
            AddBinding(m_DemocratDigitalBonusActiveBinding);
            AddBinding(m_SatelliteUplinkPresentBinding);
            AddBinding(m_VotingInstructionDistrictsJsonBinding);

            AddBinding(new TriggerBinding<string>(kGroup, "submitVotingInstructions",
    (choicesStr) =>
    {
        // Format : "districtId:PartyName,districtId:PartyName,..."
        var choices = new List<System.Collections.Generic.KeyValuePair<int, PoliticalParty>>();
        if (!string.IsNullOrEmpty(choicesStr))
        {
            foreach (var pair in choicesStr.Split(','))
            {
                var parts = pair.Split(':');
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[0], out int districtId)) continue;
                if (!System.Enum.TryParse<PoliticalParty>(parts[1], out var party)) continue;
                choices.Add(new System.Collections.Generic.KeyValuePair<int, PoliticalParty>(districtId, party));
            }
        }
        m_VotingInstructionSystem.SubmitInstructions(choices);
        UpdateVotingInstructionDistrictsBinding(force: true);
    }));

            AddBinding(new TriggerBinding<string>(kGroup, "launchDigitalCampaign",
                (autoRenewStr) =>
                {
                    var custom = m_CustomPartySystem.GetData();
                    if (!custom.m_Exists || !custom.m_SubstitutionActive) return;
                    if (custom.m_ActiveSpace != PoliticalParty.Democrate) return;

                    bool autoRenew = autoRenewStr == "true";
                    m_PropagandaSystem.TryLaunchDigitalCampaign(custom.m_ActiveSpace, autoRenew, out _);
                    UpdatePropagandaBindingIfChanged(force: true);
                }));

            // Outil de debug : logge tous les noms de prefabs de bâtiments présents en ville.
            AddBinding(new TriggerBinding(kGroup, "debugLogAllBuildings",
                () => m_InstitutionSystem.DebugLogAllBuildingPrefabNames()));

            AddBinding(new TriggerBinding(kGroup, "debugForceGeneralElectionCheck",
    () => { m_ScoreSystem.DebugForceGeneralElectionCheck(); UpdateScoreBindingIfChanged(force: true); }));


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

            AddBinding(new TriggerBinding<string, string, string, string>(kGroup, "launchDistrictCampaign",
    (districtIdStr, typeStr, targetStr, tierStr) =>
    {
        if (!int.TryParse(districtIdStr, out int districtId)) return;
        var districtEntity = FindDistrictByIndex(districtId);
        if (districtEntity == Entity.Null) return;

        var custom = m_CustomPartySystem.GetData();
        if (!custom.m_Exists || !custom.m_SubstitutionActive) return;
        var party = custom.m_ActiveSpace;

        System.Enum.TryParse<DistrictCampaignType>(typeStr, out var type);
        System.Enum.TryParse<PoliticalParty>(targetStr, out var target);
        System.Enum.TryParse<CampaignIntensity>(tierStr, out var tier);

        m_PropagandaSystem.TryLaunchDistrictCampaign(districtEntity, party, type, target, tier, out _);
        UpdateDistrictCampaignsBinding();
    }));

            AddBinding(new TriggerBinding<string>(kGroup, "cancelDistrictCampaign",
                (districtIdStr) =>
                {
                    if (!int.TryParse(districtIdStr, out int districtId)) return;
                    var districtEntity = FindDistrictByIndex(districtId);
                    if (districtEntity == Entity.Null) return;

                    var custom = m_CustomPartySystem.GetData();
                    if (!custom.m_Exists || !custom.m_SubstitutionActive) return;

                    m_PropagandaSystem.TryCancelDistrictCampaign(districtEntity, custom.m_ActiveSpace, out _);
                    UpdateDistrictCampaignsBinding();
                }));

            // OUTIL DEBUG TEMPORAIRE POUR COMPTAGE DES ADHERENTS ET COTISATION
            AddBinding(new TriggerBinding(kGroup, "debugForceCycleCheck",
    () => m_MembershipSystem.DebugForceCycleCheck()));

            // OUTIL DE DEBUG TEMPORAIRE POUR BONUS PERMANENT
            AddBinding(new TriggerBinding(kGroup, "debugForceMajorityCheck",
                () => m_BonusSystem.DebugForceMajorityCheck()));

            // OUTIL DE DEBUG TEMPORAIRE — cf. CouncilElectionSystem.DebugForceAllDistrictsToNextStep.
            AddBinding(new TriggerBinding(kGroup, "debugForceNextElection",
                () => m_ElectionSystem.DebugForceAllDistrictsToNextStep()));

            // OUTIL DE DEBUG TEMPORAIRE - TEST DE CYCLE IA PROPAGANDE
            AddBinding(new TriggerBinding(kGroup, "debugForceAiCycle",
    () => { m_PropagandaSystem.DebugForceAiCycle(); UpdatePropagandaBindingIfChanged(force: true); UpdateDistrictCampaignsBinding(); }));

            // Déclenché par le clic sur l'icône hémicycle en haut à gauche.
            AddBinding(new TriggerBinding(kGroup, "refreshHemicycle", RefreshHemicycle));

            // --- Triggers "Votre Parti" ---
            // NOTE : signature TriggerBinding<string,byte,byte> non confirmée par décompilation
            // (seul TriggerBinding sans argument l'a été, via refreshHemicycle ci-dessus et
            // DistrictNotesMod). Si le SDK expose une autre forme pour les triggers avec
            // arguments côté C#/Colossal.UI.Binding, adapter cette ligne et l'appel React
            // correspondant (trigger("cityCouncil", "createOrUpdateCustomParty", name, colorIdx,
            // spaceIdx) côté JS) en conséquence.
            AddBinding(new TriggerBinding<string, string, string, string>(kGroup, "createOrUpdateCustomParty",
     (name, colorName, spaceName, structureTypeName) =>
     {
         if (!System.Enum.TryParse<CityCouncil.PartyColor>(colorName, out var color))
             color = CityCouncil.PartyColor.Bleu;
         if (!System.Enum.TryParse<CityCouncil.PoliticalParty>(spaceName, out var space))
             space = CityCouncil.PoliticalParty.Democrate;
         if (!System.Enum.TryParse<CityCouncil.PartyStructureType>(structureTypeName, out var structureType))
             structureType = CityCouncil.PartyStructureType.Cadres;

         m_CustomPartySystem.TryCreateOrUpdate(name, color, space, structureType, out _);
         PushCustomPartyState();
         PushFundingState();
     }));

            AddBinding(new TriggerBinding(kGroup, "requestDeleteCustomParty",
                () => { m_CustomPartySystem.RequestDeletion(); PushCustomPartyState(); }));

            AddBinding(new TriggerBinding(kGroup, "activateBlackFund",
    () =>
    {
        var custom = m_CustomPartySystem.GetData();
        if (custom.m_Exists && custom.m_SubstitutionActive)
            m_BlackFundSystem.Activate(custom.m_ActiveSpace);
        UpdateBlackFundBindingIfChanged();
    }));

            AddBinding(new TriggerBinding(kGroup, "closeBlackFund",
                () =>
                {
                    var custom = m_CustomPartySystem.GetData();
                    if (custom.m_Exists && custom.m_SubstitutionActive)
                        m_BlackFundSystem.Close(custom.m_ActiveSpace);
                    UpdateBlackFundBindingIfChanged();
                }));

            AddBinding(new TriggerBinding<string, string>(kGroup, "transferBlackFund",
                (amountStr, directionStr) =>
                {
                    var custom = m_CustomPartySystem.GetData();
                    if (!custom.m_Exists || !custom.m_SubstitutionActive) return;
                    if (!int.TryParse(amountStr, out int amount)) return;
                    bool toBlackFund = directionStr == "toBlackFund";

                    if (m_BlackFundSystem.TryTransfer(custom.m_ActiveSpace, amount, toBlackFund, out var invoiceKey, out _))
                        m_LastInvoiceLocaleKeyBinding.Update(invoiceKey ?? "");

                    UpdateBlackFundBindingIfChanged();
                    UpdateMembershipBindingIfChanged();
                }));

            AddBinding(new TriggerBinding<string, string>(kGroup, "launchIllegalCampaign",
                (districtIdStr, targetStr) =>
                {
                    if (!int.TryParse(districtIdStr, out int districtId)) return;
                    var districtEntity = FindDistrictByIndex(districtId);
                    if (districtEntity == Entity.Null) return;

                    var custom = m_CustomPartySystem.GetData();
                    if (!custom.m_Exists || !custom.m_SubstitutionActive) return;

                    System.Enum.TryParse<PoliticalParty>(targetStr, out var target);
                    m_PropagandaSystem.TryLaunchIllegalDistrictCampaign(districtEntity, custom.m_ActiveSpace, target, fromBlackFund: true, out _);

                    UpdateIllegalCampaignsBindingIfChanged();
                    UpdateBlackFundBindingIfChanged();
                }));

            AddBinding(new TriggerBinding(kGroup, "debugForceCommissionCheck",
                () => m_CommissionSystem.DebugForceDetectionCheck()));

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

            AddBinding(new TriggerBinding<string>(kGroup, "validateFundingFixedAmount",
            (autoRenewStr) =>
                {
                    m_FundingSystem.ValidateFixedAmount(autoRenewStr == "true");
                    PushFundingState();
                }));
            
            AddBinding(new TriggerBinding<string>(kGroup, "setFundingAutoRenew",
            (valueStr) =>
                {
                                m_FundingSystem.SetAutoRenew(valueStr == "true");
                    PushFundingState();
                }));

            AddBinding(new TriggerBinding(kGroup, "orderPoll",
    () =>
    {
        m_PollSystem.TryOrderPoll(out _);
        UpdatePollBindingIfChanged(force: true);
        UpdateMembershipBindingIfChanged(); // reflète le débit du coût + spentPolls
    }));

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
            UpdateDistrictListBinding();       // peu coûteux : uniquement au changement de nombre de districts, ou throttle
            UpdateDistrictCampaignsBinding();  // basé sur le parti joueur actif
            UpdateBlackFundBindingIfChanged();
            UpdateIllegalCampaignsBindingIfChanged();
            UpdateCommissionReportBindingIfChanged();
            UpdatePollBindingIfChanged();
            UpdateScoreBindingIfChanged();
            UpdateShowDebugTabBinding();
            UpdateElectoralContextBindingIfChanged();
            UpdateRepublicanBureauBindingIfChanged();
            UpdatePrisonBonusBindingIfChanged();
            UpdateNuclearBonusBindingIfChanged();
            UpdateUniversityBonusBindingIfChanged();
            UpdateDigitalBonusBindingIfChanged();
            UpdateVotingInstructionDistrictsBinding();


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

        /// <summary>
        /// Reflète Mod.Instance.Setting.ShowDebugTab vers React. Lecture directe d'une propriété
        /// bool à chaque frame UI (négligeable en coût), pas besoin d'un système d'évènement dédié
        /// pour un réglage aussi simple.
        /// </summary>
        private void UpdateShowDebugTabBinding()
        {
            bool value = CityCouncil.Mod.Instance != null && CityCouncil.Mod.Instance.ShowDebugTab;
            if (m_HasLastPushedShowDebugTab && value == m_LastPushedShowDebugTab) return;

            m_ShowDebugTabBinding.Update(value);
            m_LastPushedShowDebugTab = value;
            m_HasLastPushedShowDebugTab = true;
        }


        private void UpdateVotingInstructionDistrictsBinding(bool force = false)
        {
            var custom = m_CustomPartySystem.GetData();
            var dtos = new List<VotingInstructionDistrictDto>();

            if (custom.m_Exists && custom.m_SubstitutionActive)
            {
                var playerParty = custom.m_ActiveSpace;
                var districts = m_DistrictQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                try
                {
                    foreach (var d in districts)
                    {
                        if (!m_EntityManager.HasComponent<CouncilDistrictData>(d)) continue;
                        var data = m_EntityManager.GetComponentData<CouncilDistrictData>(d);
                        if (data.m_Phase != ElectionPhase.Round1Done || data.m_WonInRound1) continue;
                        if (data.m_Round1Results.Length < 2) continue;

                        var f1 = data.m_Round1Results[0].m_Party;
                        var f2 = data.m_Round1Results[1].m_Party;
                        if (playerParty == f1 || playerParty == f2) continue; // joueur qualifié, pas de consigne

                        bool playerWasInRace = false;
                        foreach (var r in data.m_Round1Results)
                            if (r.m_Party == playerParty) { playerWasInRace = true; break; }
                        if (!playerWasInRace) continue;

                        dtos.Add(new VotingInstructionDistrictDto
                        {
                            districtId = d.Index,
                            districtName = GetDistrictDisplayName(d),
                            finalist1 = f1.ToString(),
                            finalist2 = f2.ToString(),
                            submitted = m_VotingInstructionSystem.IsSubmitted(d.Index),
                            selectedParty = m_VotingInstructionSystem.TryGetInstruction(d.Index, out var sel) ? sel.ToString() : ""
                        });
                    }
                }
                finally { districts.Dispose(); }
            }

            string json = VotingInstructionDistrictDto.ToJsonArray(dtos);
            if (!force && m_HasLastPushedVotingInstructionDistricts && json == m_LastPushedVotingInstructionDistrictsJson) return;

            m_VotingInstructionDistrictsJsonBinding.Update(json);
            m_LastPushedVotingInstructionDistrictsJson = json;
            m_HasLastPushedVotingInstructionDistricts = true;
        }


        private void UpdateBlackFundBindingIfChanged()
        {
            var custom = m_CustomPartySystem.GetData();
            if (!custom.m_Exists || !custom.m_SubstitutionActive)
            {
                if (!m_HasLastPushedBlackFund || m_LastPushedBlackFundJson != "{}")
                {
                    m_BlackFundJsonBinding.Update("{}");
                    m_LastPushedBlackFundJson = "{}";
                    m_HasLastPushedBlackFund = true;
                }
                return;
            }

            var party = custom.m_ActiveSpace;
            var dto = new BlackFundDto
            {
                active = m_BlackFundSystem.IsActive(party),
                balance = m_BlackFundSystem.GetBalance(party)
            };
            string json = dto.ToJson();

            if (!m_HasLastPushedBlackFund || json != m_LastPushedBlackFundJson)
            {
                m_BlackFundJsonBinding.Update(json);
                m_LastPushedBlackFundJson = json;
                m_HasLastPushedBlackFund = true;
            }
        }

        private void UpdateDigitalBonusBindingIfChanged()
        {
            bool bonusActive = m_BonusSystem.IsDemocratDigitalBonusActive();
            bool present = m_InstitutionSystem.IsSatelliteUplinkPresent();

            if (m_HasLastPushedDigitalState
                && bonusActive == m_LastPushedDigitalBonusActive
                && present == m_LastPushedSatellitePresent)
                return;

            m_DemocratDigitalBonusActiveBinding.Update(bonusActive);
            m_SatelliteUplinkPresentBinding.Update(present);
            m_LastPushedDigitalBonusActive = bonusActive;
            m_LastPushedSatellitePresent = present;
            m_HasLastPushedDigitalState = true;
        }

        private void UpdateNuclearBonusBindingIfChanged()
        {
            bool bonusActive = m_BonusSystem.IsEcologistNuclearBonusActive();
            bool present = m_InstitutionSystem.IsNuclearPowerPlantPresent();

            if (m_HasLastPushedNuclearState
                && bonusActive == m_LastPushedNuclearBonusActive
                && present == m_LastPushedNuclearPresent)
                return;

            m_EcologistNuclearBonusActiveBinding.Update(bonusActive);
            m_NuclearPowerPlantPresentBinding.Update(present);
            m_LastPushedNuclearBonusActive = bonusActive;
            m_LastPushedNuclearPresent = present;
            m_HasLastPushedNuclearState = true;
        }

        private void UpdateIllegalCampaignsBindingIfChanged()
        {
            var custom = m_CustomPartySystem.GetData();
            var dtos = new System.Collections.Generic.List<IllegalCampaignDto>();

            if (custom.m_Exists && custom.m_SubstitutionActive)
            {
                var party = custom.m_ActiveSpace;
                var districts = m_DistrictQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                try
                {
                    foreach (var d in districts)
                    {
                        if (!m_EntityManager.HasComponent<CouncilDistrictData>(d)) continue;
                        var data = m_EntityManager.GetComponentData<CouncilDistrictData>(d);
                        foreach (var c in data.m_IllegalCampaigns)
                        {
                            if (c.m_Party != party) continue;
                            dtos.Add(new IllegalCampaignDto
                            {
                                districtId = d.Index,
                                districtName = GetDistrictDisplayName(d),
                                targetParty = c.m_TargetParty.ToString(),
                                malusPercent = c.m_MalusPercent
                            });
                        }
                    }
                }
                finally { districts.Dispose(); }
            }

            string json = IllegalCampaignDto.ToJsonArray(dtos);
            if (!m_HasLastPushedIllegalCampaigns || json != m_LastPushedIllegalCampaignsJson)
            {
                m_IllegalCampaignsJsonBinding.Update(json);
                m_LastPushedIllegalCampaignsJson = json;
                m_HasLastPushedIllegalCampaigns = true;
            }
        }

        /// <summary>
        /// Contexte électoral city-wide affiché sous le sondage : agrège tous les modificateurs
        /// city-wide utilisés par VoteCalculator.ComputeRound1 (donc pertinents pour interpréter un
        /// sondage), à l'exclusion des effets purement locaux (Bastion, campagnes de district,
        /// campagnes illégales) qui n'ont pas de sens à l'échelle de la ville.
        /// </summary>
        private void UpdateElectoralContextBindingIfChanged()
        {
            double currentDay = GetApproxCurrentDay();

            var activeEvent = m_CityEventSystem.GetActiveEventDefinition();
            bool hasEvent = activeEvent != null;
            string eventHeadlineKey = activeEvent?.Headline ?? "";

            bool unemploymentActive = m_EconomySystem.IsUnemploymentCrisisActive();
            var (taxPoorBonus, taxRichBonus) = m_TaxSystem.GetTaxDiscontentBonus();
            bool taxPoorActive = taxPoorBonus > 0f;
            bool taxRichActive = taxRichBonus > 0f;

            var propaganda = new List<ElectoralContextPropagandaDto>();
            foreach (var (party, target, percent) in m_PropagandaSystem.GetActiveCampaigns())
            {
                propaganda.Add(new ElectoralContextPropagandaDto
                {
                    party = party.ToString(),
                    target = target.ToString(),
                    bonusPercent = percent
                });
            }

            var sanctions = new List<ElectoralContextSanctionDto>();
            foreach (PoliticalParty p in System.Enum.GetValues(typeof(PoliticalParty)))
            {
                foreach (var s in m_CommissionSystem.GetActiveSanctionsForParty(p, currentDay))
                {
                    sanctions.Add(new ElectoralContextSanctionDto
                    {
                        party = p.ToString(),
                        malusPercent = s.m_MalusPercent
                    });
                }
            }

            var dto = new ElectoralContextDto
            {
                hasEvent = hasEvent,
                eventHeadlineKey = eventHeadlineKey,
                unemploymentActive = unemploymentActive,
                taxPoorActive = taxPoorActive,
                taxRichActive = taxRichActive,
                propaganda = propaganda,
                sanctions = sanctions
            };

            string json = dto.ToJson();
            if (m_HasLastPushedElectoralContext && json == m_LastPushedElectoralContextJson) return;

            m_ElectoralContextJsonBinding.Update(json);
            m_LastPushedElectoralContextJson = json;
            m_HasLastPushedElectoralContext = true;
        }

        private void UpdateCommissionReportBindingIfChanged()
        {
            var custom = m_CustomPartySystem.GetData();
            var playerParty = (custom.m_Exists && custom.m_SubstitutionActive) ? custom.m_ActiveSpace : (PoliticalParty?)null;
            double currentDay = GetApproxCurrentDay();

            var dtos = new System.Collections.Generic.List<CommissionReportDto>();
            foreach (PoliticalParty p in System.Enum.GetValues(typeof(PoliticalParty)))
            {
                bool isPlayer = playerParty.HasValue && playerParty.Value == p;
                var sanctions = m_CommissionSystem.GetActiveSanctionsForParty(p, currentDay);
                bool hasSanction = sanctions.Count > 0;
                double expiry = hasSanction ? sanctions[0].m_ExpiryDay : 0;

                dtos.Add(new CommissionReportDto
                {
                    party = p.ToString(),
                    vigilanceLevel = m_CommissionSystem.GetVigilance(p).ToString(),
                    isPlayer = isPlayer,
                    activeIllegalCount = isPlayer ? m_PropagandaSystem.CountActiveIllegalCampaignsForParty(p) : 0,
                    hasSanction = hasSanction,
                    sanctionExpiryDay = expiry
                });
            }

            string json = CommissionReportDto.ToJsonArray(dtos);
            if (!m_HasLastPushedCommissionReport || json != m_LastPushedCommissionReportJson)
            {
                m_CommissionReportJsonBinding.Update(json);
                m_LastPushedCommissionReportJson = json;
                m_HasLastPushedCommissionReport = true;
            }
        }

        /// <summary>Même calcul que GetCurrentSimulationDay() de CouncilElectionSystem, dupliqué ici pour ne pas coupler les deux systèmes UI/simulation (cf. remarque déjà présente ailleurs dans le mod).</summary>
        private double GetApproxCurrentDay()
        {
            // NOTE : UISystemBase n'a pas de SimulationSystem par défaut ; si vous en avez déjà un
            // accessible ailleurs dans CouncilUISystem, réutilisez-le. Sinon, ce calcul basé sur
            // Time.frameCount ou un World.GetExistingSystemManaged<SimulationSystem>() est nécessaire.
            var sim = World.GetExistingSystemManaged<Game.Simulation.SimulationSystem>();
            return sim != null ? (double)sim.frameIndex / 262144.0 : 0.0;
        }

        private Entity FindDistrictByIndex(int index)
        {
            var districts = m_DistrictQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            try
            {
                foreach (var d in districts)
                    if (d.Index == index) return d;
            }
            finally { districts.Dispose(); }
            return Entity.Null;
        }

        private void UpdateDistrictListBinding()
        {
            // Ne recalcule que si le nombre de districts a changé (création/destruction de district,
            // rare en cours de partie) — évite de re-sérialiser une liste identique à chaque frame UI.
            int currentCount = m_DistrictQuery.CalculateEntityCount();
            if (currentCount == m_LastPushedDistrictCount) return;

            var dtos = new List<DistrictDto>();
            var districts = m_DistrictQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            try
            {
                foreach (var d in districts)
                    dtos.Add(new DistrictDto { id = d.Index, name = GetDistrictDisplayName(d) });
            }
            finally { districts.Dispose(); }

            m_DistrictListJsonBinding.Update(DistrictDto.ToJsonArray(dtos));
            m_LastPushedDistrictCount = currentCount;
        }

        private void UpdateDistrictCampaignsBinding()
        {
            var custom = m_CustomPartySystem.GetData();
            if (!custom.m_Exists || !custom.m_SubstitutionActive) { return; }
            var playerParty = custom.m_ActiveSpace;

            var dtos = new List<DistrictCampaignDto>();
            var districts = m_DistrictQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    if (!m_EntityManager.HasComponent<CouncilDistrictData>(d)) continue;
                    var data = m_EntityManager.GetComponentData<CouncilDistrictData>(d);
                    foreach (var c in data.m_DistrictCampaigns)
                    {
                        if (c.m_Party != playerParty) continue;
                        dtos.Add(new DistrictCampaignDto
                        {
                            districtId = d.Index,
                            districtName = GetDistrictDisplayName(d),
                            party = c.m_Party.ToString(),
                            type = c.m_Type.ToString(),
                            targetParty = c.m_Type == DistrictCampaignType.Boost ? "" : c.m_TargetParty.ToString(),
                            bonusPercent = c.m_BonusPercent,
                            selfMalusPercent = c.m_SelfMalusPercent
                        });
                    }
                }
            }
            finally { districts.Dispose(); }

            string json = DistrictCampaignDto.ToJsonArray(dtos);
            if (!m_HasLastPushedDistrictCampaigns || json != m_LastPushedDistrictCampaignsJson)
            {
                m_DistrictCampaignsJsonBinding.Update(json);
                m_LastPushedDistrictCampaignsJson = json;
                m_HasLastPushedDistrictCampaigns = true;
            }
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
                && a.m_ActiveSpace == b.m_ActiveSpace
                && a.m_StructureType == b.m_StructureType;
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
                treasury = e.m_Treasury,
                fromCityFunding = e.m_TotalFromCityFunding,
                fromDues = e.m_TotalFromDues,
                spentPropaganda = e.m_TotalSpentPropaganda,
                spentPolls = e.m_TotalSpentPolls, // AJOUT
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
                if (a.m_Entries[i].m_TotalFromCityFunding != b.m_Entries[i].m_TotalFromCityFunding) return false; 
                if (a.m_Entries[i].m_TotalFromDues != b.m_Entries[i].m_TotalFromDues) return false;                
                if (a.m_Entries[i].m_TotalSpentPropaganda != b.m_Entries[i].m_TotalSpentPropaganda) return false;
                if (a.m_Entries[i].m_TotalSpentPolls != b.m_Entries[i].m_TotalSpentPolls) return false;
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
            UpdatePollBindingIfChanged(force: true);
            UpdateScoreBindingIfChanged(force: true);
        }

        // Score
        private void UpdateScoreBindingIfChanged(bool force = false)
        {
            var totals = m_ScoreSystem.GetTotalScores();
            var dtos = totals
                .Select(kv => new ScoreDto { party = kv.Key.ToString(), score = kv.Value, displayName = "", displayColor = "" })
                .Select(DecorateScoreWithCustomParty)
                .OrderByDescending(d => d.score)
                .ToArray();

            string json = ScoreDto.ToJsonArray(dtos);
            if (!force && m_HasLastPushedScore && json == m_LastPushedScoreJson) return;

            m_ScoreJsonBinding.Update(json);
            m_LastPushedScoreJson = json;
            m_HasLastPushedScore = true;
        }

        private void UpdateRepublicanBureauBindingIfChanged()
        {
            bool bonusActive = m_BonusSystem.IsRepublicanBureauBonusActive();
            bool bureauPresent = m_InstitutionSystem.IsCentralIntelligenceBureauPresent();

            if (m_HasLastPushedBureauState
                && bonusActive == m_LastPushedBureauBonusActive
                && bureauPresent == m_LastPushedBureauPresent)
                return;

            m_RepublicanBureauBonusActiveBinding.Update(bonusActive);
            m_CentralIntelligenceBureauPresentBinding.Update(bureauPresent);
            m_LastPushedBureauBonusActive = bonusActive;
            m_LastPushedBureauPresent = bureauPresent;
            m_HasLastPushedBureauState = true;
        }


        private ScoreDto DecorateScoreWithCustomParty(ScoreDto dto)
        {
            var custom = m_CustomPartySystem.GetData();
            if (custom.m_Exists && custom.m_SubstitutionActive && custom.m_ActiveSpace.ToString() == dto.party)
            {
                dto.displayName = custom.m_Name.ToString();
                dto.displayColor = custom.m_Color.ToString();
            }
            return dto;
        }


        private void UpdatePollBindingIfChanged(bool force = false)
        {
            bool allowed = m_PollSystem.IsPollAllowedNow();
            if (force || !m_HasLastPushedPoll || allowed != m_PollAllowedBinding.value)
                m_PollAllowedBinding.Update(allowed);

            var data = m_PollSystem.GetData();
            if (!force && m_HasLastPushedPoll && data.m_LastPollDay == m_LastPushedPollDay)
                return;

            var dtos = data.m_LastResults.ToArray()
                .Select(e => new PartyResultDto
                {
                    party = e.m_Party.ToString(),
                    seats = 0,
                    voteShare = e.m_SharePercent,
                    displayName = "",
                    displayColor = ""
                })
                .Select(DecorateWithCustomParty)
                .ToArray();

            m_PollResultsJsonBinding.Update(PartyResultDto.ToJsonArray(dtos));
            m_LastPushedPollDay = data.m_LastPollDay;
            m_HasLastPushedPoll = true;
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
                && data.m_FixedAmountLocked == m_LastPushedFundingLocked
                && data.m_AutoRenew == m_FundingAutoRenewBinding.value)
                return;

            PushFundingState(data);
        }

        private void PushFundingState(CouncilFundingData? preloaded = null)
        {
            var data = preloaded ?? m_FundingSystem.GetData();
            m_FundingFixedAmountBinding.Update(data.m_FixedAmount);
            m_FundingLockedBinding.Update(data.m_FixedAmountLocked);
            m_FundingAutoRenewBinding.Update(data.m_AutoRenew);
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
            m_CustomPartyStructureTypeBinding.Update(data.m_Exists ? data.m_StructureType.ToString() : "Cadres"); // AJOUT

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

            // Détail complet du 1er tour (voix + %), utilisé pour l'histogramme affiché en cas de
            // second tour en attente. Disponible dès que le 1er tour a été calculé, quelle que
            // soit l'issue (majorité directe ou non) — le React ne l'affiche que sur Round1Done.
            var round1Results = data.m_Round1Results.Length > 0
                              ? data.m_Round1Results.ToArray()
                                .Select(r => new Round1ResultDto
                                {
                party = r.m_Party.ToString(),
                voteShare = r.m_VoteShare,
                votes = (int)MathF.Round(r.m_VoteShare * data.m_VotersRound1),
                displayName = "",
                displayColor = ""
                    })
                    .Select(DecorateRound1WithCustomParty)
                    .ToArray()
                : System.Array.Empty<Round1ResultDto>();
            m_AdminRound1ResultsBinding.Update(Round1ResultDto.ToJsonArray(round1Results));

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

        private void UpdateUniversityBonusBindingIfChanged()
        {
            bool bonusActive = m_BonusSystem.IsRadicalLeftUniversityBonusActive();
            bool present = m_InstitutionSystem.IsUniversityPresent();

            // Plafond affiché : celui du parti joueur actif s'il existe, sinon la valeur de base.
            var custom = m_CustomPartySystem.GetData();
            int max = CityCouncil.DistrictCampaignCatalog.MaxActiveCampaignsPerParty;
            if (custom.m_Exists && custom.m_SubstitutionActive
                && custom.m_ActiveSpace == PoliticalParty.GaucheRadicale
                && bonusActive)
            {
                max = CityCouncil.DistrictCampaignCatalog.MaxActiveCampaignsPerPartyWithUniversityBonus;
            }

            if (m_HasLastPushedUniversityState
                && bonusActive == m_LastPushedUniversityBonusActive
                && present == m_LastPushedUniversityPresent
                && max == m_LastPushedDistrictCampaignMax)
                return;

            m_RadicalLeftUniversityBonusActiveBinding.Update(bonusActive);
            m_UniversityPresentBinding.Update(present);
            m_DistrictCampaignMaxBinding.Update(max);
            m_LastPushedUniversityBonusActive = bonusActive;
            m_LastPushedUniversityPresent = present;
            m_LastPushedDistrictCampaignMax = max;
            m_HasLastPushedUniversityState = true;
        }

        private void UpdatePrisonBonusBindingIfChanged()
        {
            bool bonusActive = m_BonusSystem.IsPopulistPrisonBonusActive();
            bool present = m_InstitutionSystem.IsPrisonPresent();

            // Coût affiché : celui du parti joueur actif s'il existe, sinon le coût de base.
            var custom = m_CustomPartySystem.GetData();
            int cost = (custom.m_Exists && custom.m_SubstitutionActive)
                ? m_PropagandaSystem.GetIllegalCampaignCost(custom.m_ActiveSpace)
                : CityCouncil.IllegalCampaignCatalog.Cost;

            if (m_HasLastPushedPrisonState
                && bonusActive == m_LastPushedPrisonBonusActive
                && present == m_LastPushedPrisonPresent
                && cost == m_LastPushedIllegalCampaignCost)
                return;

            m_PopulistPrisonBonusActiveBinding.Update(bonusActive);
            m_PrisonPresentBinding.Update(present);
            m_IllegalCampaignCostBinding.Update(cost);
            m_LastPushedPrisonBonusActive = bonusActive;
            m_LastPushedPrisonPresent = present;
            m_LastPushedIllegalCampaignCost = cost;
            m_HasLastPushedPrisonState = true;
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
            var bastionCounts = new System.Collections.Generic.Dictionary<CityCouncil.PoliticalParty, int>();
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

                    // AJOUT — comptage des Bastions, indépendant des sièges : un seul Bastion par
                    // district (data.m_BastionParty), valide uniquement si data.m_IsBastion.
                    if (data.m_IsBastion)
                    {
                       bastionCounts.TryGetValue(data.m_BastionParty, out int currentBastions);
                       bastionCounts[data.m_BastionParty] = currentBastions + 1;
                    }
                }
            }
            finally
            {
                districts.Dispose();
            }

            var seatsDto = totals
               .Select(kv => new PartyResultDto
                {
                party = kv.Key.ToString(),
                seats = kv.Value,
                voteShare = 0f,
                bastions = bastionCounts.TryGetValue(kv.Key, out int b) ? b : 0
                })
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

        private Round1ResultDto DecorateRound1WithCustomParty(Round1ResultDto dto)
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

        /// <summary>
        /// Nom affiché d'un district, résolu via NameSystem.GetRenderedLabelName (même mécanisme
        /// que les panneaux vanilla) : renvoie le nom personnalisé du joueur si renommé, sinon le
        /// nom généré/localisé par défaut du jeu. Repli sur "District #index" uniquement si
        /// NameSystem ne renvoie rien d'exploitable (cas limite non attendu en usage normal).
        /// </summary>
        private string GetDistrictDisplayName(Entity districtEntity)
        {
            string name = m_NameSystem.GetRenderedLabelName(districtEntity);
            return string.IsNullOrWhiteSpace(name) ? $"District #{districtEntity.Index}" : name;
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
        public int bastions;

        public static PartyResultDto From(PartyResult r) => new PartyResultDto
        {
            party = r.m_Party.ToString(),
            seats = r.m_Seats,
            voteShare = r.m_VoteShare,
            displayName = "",
            displayColor = "",
            bastions = 0
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
                sb.Append("\"displayColor\":\"").Append(r.displayColor ?? "").Append("\",");
                sb.Append("\"bastions\":").Append(r.bastions.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
        public long fromCityFunding;
        public long fromDues;
        public long spentPropaganda;
        public long spentPolls; // AJOUT

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
                sb.Append("\"treasury\":").Append(e.treasury).Append(',');
                sb.Append("\"fromCityFunding\":").Append(e.fromCityFunding).Append(',');
                sb.Append("\"fromDues\":").Append(e.fromDues).Append(',');
                sb.Append("\"spentPropaganda\":").Append(e.spentPropaganda).Append(','); // AJOUT virgule
                sb.Append("\"spentPolls\":").Append(e.spentPolls); // AJOUT
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }
    }

    public struct DistrictDto
    {
        public int id;
        public string name;

        public static string ToJsonArray(System.Collections.Generic.IEnumerable<DistrictDto> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var d in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{').Append("\"id\":").Append(d.id).Append(',')
                  .Append("\"name\":\"").Append(EscapeJson(d.name)).Append("\"}");
            }
            sb.Append(']');
            return sb.ToString();
        }
        private static string EscapeJson(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public struct DistrictCampaignDto
    {
        public int districtId;
        public string districtName;
        public string party;
        public string type;
        public string targetParty;
        public float bonusPercent;
        public float selfMalusPercent;

        public static string ToJsonArray(System.Collections.Generic.IEnumerable<DistrictCampaignDto> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var c in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{');
                sb.Append("\"districtId\":").Append(c.districtId).Append(',');
                sb.Append("\"districtName\":\"").Append(EscapeJson(c.districtName)).Append("\",");
                sb.Append("\"party\":\"").Append(c.party).Append("\",");
                sb.Append("\"type\":\"").Append(c.type).Append("\",");
                sb.Append("\"targetParty\":\"").Append(c.targetParty ?? "").Append("\",");
                sb.Append("\"bonusPercent\":").Append(c.bonusPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                sb.Append("\"selfMalusPercent\":").Append(c.selfMalusPercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string EscapeJson(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public struct BlackFundDto
    {
        public bool active;
        public int balance;

        public string ToJson()
        {
            return "{\"active\":" + (active ? "true" : "false") + ",\"balance\":" + balance + "}";
        }
    }

    public struct IllegalCampaignDto
    {
        public int districtId;
        public string districtName;
        public string targetParty;
        public float malusPercent;

        public static string ToJsonArray(System.Collections.Generic.IEnumerable<IllegalCampaignDto> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var c in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{');
                sb.Append("\"districtId\":").Append(c.districtId).Append(',');
                sb.Append("\"districtName\":\"").Append(EscapeJson(c.districtName)).Append("\",");
                sb.Append("\"targetParty\":\"").Append(c.targetParty).Append("\",");
                sb.Append("\"malusPercent\":").Append(c.malusPercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }
        private static string EscapeJson(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public struct CommissionReportDto
    {
        public string party;
        public string vigilanceLevel;      // "Low" | "Medium" | "High"
        public bool isPlayer;
        public int activeIllegalCount;     // 0 pour les IA côté affichage simplifié (point 9) sauf si vous voulez l'exposer aussi
        public bool hasSanction;
        public double sanctionExpiryDay;

        public static string ToJsonArray(System.Collections.Generic.IEnumerable<CommissionReportDto> items)
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
                sb.Append("\"vigilanceLevel\":\"").Append(r.vigilanceLevel).Append("\",");
                sb.Append("\"isPlayer\":").Append(r.isPlayer ? "true" : "false").Append(',');
                sb.Append("\"activeIllegalCount\":").Append(r.activeIllegalCount).Append(',');
                sb.Append("\"hasSanction\":").Append(r.hasSanction ? "true" : "false").Append(',');
                sb.Append("\"sanctionExpiryDay\":").Append(r.sanctionExpiryDay.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }
    }

    public struct ScoreDto
    {
        public string party;
        public long score;
        public string displayName;
        public string displayColor;

        public static string ToJsonArray(System.Collections.Generic.IEnumerable<ScoreDto> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var d in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{');
                sb.Append("\"party\":\"").Append(d.party).Append("\",");
                sb.Append("\"score\":").Append(d.score.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                sb.Append("\"displayName\":\"").Append(EscapeJson(d.displayName ?? "")).Append("\",");
                sb.Append("\"displayColor\":\"").Append(d.displayColor ?? "").Append("\"");
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    /// <summary>
    /// DTO du détail complet du 1er tour (toutes les voix, contrairement à adminResultsJson qui
    /// ne porte que les sièges finaux). "votes" est arrondi côté C# (share * m_VotersRound1) pour
    /// éviter toute divergence d'arrondi si le calcul était refait côté React.
    /// </summary>
    public struct Round1ResultDto
    {
        public string party;
        public float voteShare;
        public int votes;
        public string displayName;
        public string displayColor;

        public static string ToJsonArray(System.Collections.Generic.IEnumerable<Round1ResultDto> items)
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
                sb.Append("\"voteShare\":").Append(r.voteShare.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                sb.Append("\"votes\":").Append(r.votes.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                sb.Append("\"displayName\":\"").Append(EscapeJson(r.displayName ?? "")).Append("\",");
                sb.Append("\"displayColor\":\"").Append(r.displayColor ?? "").Append("\"");
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string EscapeJson(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public struct ElectoralContextPropagandaDto
    {
        public string party;
        public string target;
        public float bonusPercent;
    }

    public struct ElectoralContextSanctionDto
    {
        public string party;
        public float malusPercent;
    }

    /// <summary>
    /// DTO du contexte électoral city-wide affiché sous le sondage (PollTab.tsx). Sérialisation
    /// manuelle en JSON, même pattern que les autres DTOs de ce fichier.
    /// </summary>
    public struct ElectoralContextDto
    {
        public bool hasEvent;
        public string eventHeadlineKey;
        public bool unemploymentActive;
        public bool taxPoorActive;
        public bool taxRichActive;
        public List<ElectoralContextPropagandaDto> propaganda;
        public List<ElectoralContextSanctionDto> sanctions;

        public string ToJson()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('{');
            sb.Append("\"hasEvent\":").Append(hasEvent ? "true" : "false").Append(',');
            sb.Append("\"eventHeadlineKey\":\"").Append(EscapeJson(eventHeadlineKey ?? "")).Append("\",");
            sb.Append("\"unemploymentActive\":").Append(unemploymentActive ? "true" : "false").Append(',');
            sb.Append("\"taxPoorActive\":").Append(taxPoorActive ? "true" : "false").Append(',');
            sb.Append("\"taxRichActive\":").Append(taxRichActive ? "true" : "false").Append(',');

            sb.Append("\"propaganda\":[");
            for (int i = 0; i < propaganda.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var p = propaganda[i];
                sb.Append('{');
                sb.Append("\"party\":\"").Append(p.party).Append("\",");
                sb.Append("\"target\":\"").Append(p.target).Append("\",");
                sb.Append("\"bonusPercent\":").Append(p.bonusPercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append('}');
            }
            sb.Append("],");

            sb.Append("\"sanctions\":[");
            for (int i = 0; i < sanctions.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var s = sanctions[i];
                sb.Append('{');
                sb.Append("\"party\":\"").Append(s.party).Append("\",");
                sb.Append("\"malusPercent\":").Append(s.malusPercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append('}');
            }
            sb.Append(']');

            sb.Append('}');
            return sb.ToString();
        }

        private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public struct VotingInstructionDistrictDto
    {
        public int districtId;
        public string districtName;
        public string finalist1;
        public string finalist2;
        public bool submitted;
        public string selectedParty; // "" si aucun choix, sinon nom du finaliste sélectionné

        public static string ToJsonArray(System.Collections.Generic.IEnumerable<VotingInstructionDistrictDto> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var d in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{');
                sb.Append("\"districtId\":").Append(d.districtId).Append(',');
                sb.Append("\"districtName\":\"").Append(EscapeJson(d.districtName)).Append("\",");
                sb.Append("\"finalist1\":\"").Append(d.finalist1).Append("\",");
                sb.Append("\"finalist2\":\"").Append(d.finalist2).Append("\",");
                sb.Append("\"submitted\":").Append(d.submitted ? "true" : "false").Append(',');
                sb.Append("\"selectedParty\":\"").Append(d.selectedParty ?? "").Append("\"");
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }
        private static string EscapeJson(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }


}