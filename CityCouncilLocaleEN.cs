using System.Collections.Generic;
using Colossal;

namespace CityCouncil
{
    public class CityCouncilLocaleEN : IDictionarySource
    {
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                // YourPartyTab
                { LocaleKeys.YourPartyTab_CreateButton, "Create a political party" },
                { LocaleKeys.YourPartyTab_UpdateButton, "Update party" },
                { LocaleKeys.YourPartyTab_DeleteButton, "Delete party" },
                { LocaleKeys.YourPartyTab_CancelButton, "Cancel" },
                { LocaleKeys.YourPartyTab_PendingDeletion, "Deletion scheduled for the next election." },
                { LocaleKeys.YourPartyTab_SpacePrefix, "Political space: " },
                { LocaleKeys.YourPartyTab_SectionHeaderCreate, "Create your political party" },
                { LocaleKeys.YourPartyTab_SectionHeaderEdit, "Edit your party" },
                { LocaleKeys.YourPartyTab_NameLabel, "Party name" },
                { LocaleKeys.YourPartyTab_NamePlaceholder, "E.g.: Citizens' Renewal" },
                { LocaleKeys.YourPartyTab_ColorLabel, "Color" },
                { LocaleKeys.YourPartyTab_SpaceLabel, "Political space" },
                { LocaleKeys.YourPartyTab_PendingActivation, "Your party will only be able to compete starting from the next election." },
                { LocaleKeys.YourPartyTab_StructureLabel, "Party type" },
                { LocaleKeys.YourPartyTab_StructureCadres, "Cadre party" },
                { LocaleKeys.YourPartyTab_StructureMasse, "Mass party" },
                { LocaleKeys.YourPartyTab_StructureCadresDesc, "+10 member per seat won, 45 credits due per member." },
                { LocaleKeys.YourPartyTab_StructureMasseDesc, "+30 members per seat won, 10 credits due per member." },

                // HemicyclePanel
                { LocaleKeys.Hemicycle_PanelTitle, "City Council" },
                { LocaleKeys.Hemicycle_TabResults, "Results" },
                { LocaleKeys.Hemicycle_TabYourParty, "Your Party" },
                { LocaleKeys.Hemicycle_TabForces, "Political Forces" },
                { LocaleKeys.Hemicycle_NoElection, "No completed election yet." },
                { LocaleKeys.Hemicycle_LeaderPrefix, "Leading party: " },
                { LocaleKeys.Hemicycle_SeatsSuffix, " total seats" },
                { LocaleKeys.Hemicycle_LegendSeatsPlural, "seats" },
                { LocaleKeys.Hemicycle_LegendSeatsSingular, "seat" },
                { LocaleKeys.Hemicycle_LegendBastionsPlural, "strongholds" },
                { LocaleKeys.Hemicycle_LegendBastionsSingular, "stronghold" },
                { LocaleKeys.VotingInstruction_Header, "Voting instructions" },
                { LocaleKeys.VotingInstruction_Intro, "You can give voting instructions in the following districts, though your supporters may not follow them..." },
                { LocaleKeys.VotingInstruction_ValidateButton, "Confirm" },
                { LocaleKeys.Hemicycle_LegendReinforcedBastionsPlural, "reinforced strongholds" },
                { LocaleKeys.Hemicycle_LegendReinforcedBastionsSingular, "reinforced stronghold" },
                { LocaleKeys.Hemicycle_PowerfulDistrictPrefix, "Most powerful district: " },
                { LocaleKeys.Hemicycle_PowerfulDistrictVoters, "eligibles voters" },
                { LocaleKeys.Hemicycle_PowerfulDistrictCouncilSuffix, "of the City Council" },
                { LocaleKeys.Hemicycle_PowerfulDistrictLedBy, "Led by " },
                { LocaleKeys.Hemicycle_PowerfulDistrictBonusHint, "+1% voter intent on the entire city for this party." },


                // Coalition
                { LocaleKeys.Coalition_Header, "Coalition" },
                { LocaleKeys.Coalition_MajoritySuffix, " (absolute majority)" },
                { LocaleKeys.Coalition_PluralitySuffix, " (relative majority)" },
                { LocaleKeys.Coalition_ProposalHeader, "Propose a coalition" },
                { LocaleKeys.Coalition_ProposalIntro, "No party holds an absolute majority. Select one or more parties to approach — each will respond based on their political proximity to your party and the other members already on board." },
                { LocaleKeys.Coalition_ProposeButton, "Propose" },
                { LocaleKeys.Coalition_DeclineButton, "Decline" },


                // AdministrationSection
                { LocaleKeys.Admin_Header, "Administration" },
                { LocaleKeys.Admin_NoElectionDesc, "No elections in this district because there are no residents. It is managed by a Special Commission." },
                { LocaleKeys.Admin_Round1WonPrefix, "District won by the \"" },
                { LocaleKeys.Admin_Round1WonSuffix, "\" Party in the 1st round. Election finished, awaiting the end of the 2nd general round." },
                { LocaleKeys.Admin_Round1PendingPrefix, "No majority in round 1. Second round pending between " },
                { LocaleKeys.Admin_Round1PendingMiddle, " and " },
                { LocaleKeys.Admin_Round1PendingSuffix, "." },
                { LocaleKeys.Admin_Round1PendingDefault, "No majority in round 1. 2nd round pending." },
                { LocaleKeys.Admin_VotersLabel, "Voters" },
                { LocaleKeys.Admin_AbstentionLabel, "Abstention" },
                { LocaleKeys.Admin_SeatsPlural, "seats" },
                { LocaleKeys.Admin_SeatsSingular, "seat" },

                // PoliticalForcesTab
                { LocaleKeys.Forces_CustomPartyDesc, "Your political party. Description customization will be available in a future update." },
                { LocaleKeys.Forces_MembersPlural, "members" },
                { LocaleKeys.Forces_MembersSingular, "member" },
                { LocaleKeys.Forces_SeatsPlural, "seats in total" },
                { LocaleKeys.Forces_SeatsSingular, "seat in total" },
                { LocaleKeys.Forces_Treasury, "credits in treasury" },
                { LocaleKeys.Forces_DescEcologiste, "Advocates for an ambitious ecological transition and natural space preservation." },
                { LocaleKeys.Forces_DescDemocrate, "Centrist party favoring social dialogue and pragmatic municipal management." },
                { LocaleKeys.Forces_DescPopuliste, "Voice of popular discontent, critical of local taxes and elites." },
                { LocaleKeys.Forces_DescRepublicain, "Defends order, security, and rigorous management of municipal finances." },
                { LocaleKeys.Forces_DescGaucheRadicale, "Campaigns for radical wealth redistribution and strengthened public services." },
                { LocaleKeys.Forces_PendingReplacement, "Party replaced at the next election!" },
                { LocaleKeys.Treasury_Header, "Treasury breakdown" },
                { LocaleKeys.Treasury_CityFunding, "City funding" },
                { LocaleKeys.Treasury_Dues, "Membership dues" },
                { LocaleKeys.Treasury_PropagandaSpent, "Propaganda spending" },
                { LocaleKeys.Treasury_CurrentTotal, "Current balance" },
                { LocaleKeys.PartyDesc_Ecologiste, "YOUR ENGLISH TEXT HERE, with **bold**\n\nand paragraphs separated by a blank line." },


                // City Events
                { LocaleKeys.Event_ScandalePolitique, "A political scandal breaks out in the city, citizens are disillusioned!" },
                { LocaleKeys.Event_IncendiesForets, "Major forest fires ravage thousands of hectares." },
                { LocaleKeys.Event_ReformePensions, "Rumors swirl in city hall about a pension reform proposal. Seniors are up in arms!" },
                { LocaleKeys.Event_PoliceBavure, "Il se murmure dans les couloirs de la mairie un projet de réforme des pensions de retraite. Les séniors sont vent debout !" },
                { LocaleKeys.Event_RegionEconomieSante, ""},
                { LocaleKeys.Event_CacophonieGaucheRadicale, ""},
                { LocaleKeys.Event_EvolutionClimatImpact, ""},
                { LocaleKeys.Event_DeploiementWifi, ""},
                { LocaleKeys.Event_RelanceNucleaire, ""},
                { LocaleKeys.Event_ScandaleDemocrate, ""},
                { LocaleKeys.Event_ZIDeveloppement, ""},
                { LocaleKeys.Event_PopulisteAnimalMagazine, ""},
                { LocaleKeys.Event_DesaccordEcoloDemocrateInegalites, "The Greens have a deep disagreement with the Democrats over the inequality reduction plan. Green voters are no longer as reliably following Democrats if eliminated in the first round." },

                // Ajouts dans CityCouncilLocaleEN
                { LocaleKeys.Party_Ecologiste, "Greens" },
                { LocaleKeys.Party_Democrate, "Democrats" },
                { LocaleKeys.Party_Populiste, "Populists" },
                { LocaleKeys.Party_Republicain, "Republicans" },
                { LocaleKeys.Party_GaucheRadicale, "Radical Left" },

                // FundingTab
                { LocaleKeys.Funding_TabLabel, "Political Funding" },
                { LocaleKeys.Funding_FixedAmountLabel, "Fixed share (per cycle)" },
                { LocaleKeys.Funding_FixedAmountHint, "Split equally among the 5 parties at the end of the election cycle." },
                { LocaleKeys.Funding_ValidateButton, "Confirm amount" },
                { LocaleKeys.Funding_LockedMessage, "Amount locked until the next distribution." },
                { LocaleKeys.Funding_VariableInfo, "Variable share: 1,000 credits per seat won, paid automatically." },
                { LocaleKeys.Funding_AutoRenewLabel, "Automatically renew" },
                { LocaleKeys.Funding_TabTitle, "Funding" },
{ LocaleKeys.Funding_TabIntro, "Public funding comes from the city budget.\n\nIt consists of a fixed portion—which is configurable and automatically renewed if you wish—and a variable portion paid based on the number of seats won in each election cycle." },

                // Bastion
                { LocaleKeys.Admin_BastionLabel, "Stronghold: " },
                { LocaleKeys.Admin_BastionActiveSuffix, " — Bonus active (+4%)" },
                { LocaleKeys.ReinforcedBastion_Header, "Reinforced Stronghold" },
                { LocaleKeys.ReinforcedBastion_Intro, "You may optionally choose an eligible Reinforced Stronghold - +5% vote intention bonus in that District. You can only hold one at a time, but you may switch it at the next election if another District becomes eligible." },
                { LocaleKeys.ReinforcedBastion_ValidateButton, "Confirm" },
                { LocaleKeys.Admin_BastionReinforcedLabel, "Reinforced Stronghold: " },
                { LocaleKeys.Admin_BastionReinforcedActiveSuffix, " — Bonus active (+5%)" },

                // Bonus Permanent
                { LocaleKeys.Forces_BonusDefensifLabel, "Permanent bonus: Defensive" },
                { LocaleKeys.Forces_BonusOffensifLabel, "Permanent bonus: Offensive" },
                { LocaleKeys.Admin_BonusDefensifTooltip, "Permanent Defensive bonus (protects one stronghold streak slot)" }, // MODIFIÉ
                { LocaleKeys.Admin_BonusOffensifTooltip, "Permanent Offensive bonus (+3% vote intention in opposing strongholds)" }, // MODIFIÉ
                { LocaleKeys.Hemicycle_BonusChoiceTitle, "Permanent bonus earned!" },
                { LocaleKeys.Hemicycle_BonusChoiceDesc, "Your party has won the city council majority several times in a row. Choose your permanent bonus:" },
                { LocaleKeys.Hemicycle_BonusChoiceDefensif, "Defensive" },
                { LocaleKeys.Hemicycle_BonusChoiceOffensif, "Offensive" },
                { LocaleKeys.Hemicycle_BonusChoiceConfirm, "Confirm" },
                { LocaleKeys.Hemicycle_BonusChoiceHint, "To change bonus, you'll need to win the majority at least once more." },
                { LocaleKeys.Hemicycle_BonusPendingTooltip, "Permanent Bonus to choose!" },

                // Bonus Exclusif
                { LocaleKeys.Forces_BureauBonusActive, "Central Intelligence Bureau: +50% dues" },
                { LocaleKeys.Forces_BureauPresentHint, "The Central Intelligence Bureau is built — 3 consecutive general-majority wins will activate the dues bonus." },
                { LocaleKeys.Forces_PrisonBonusActive, "Prison: illegal campaigns -30% cost / -30% detection" },
                { LocaleKeys.Forces_NuclearBonusActive, "Nuclear power plant: +4% low-income neighborhoods, +4% seniors city-wide" },
                { LocaleKeys.Forces_UniversityBonusActive, "University: 5 district campaigns instead of 3" },
                { LocaleKeys.Forces_SatelliteBonusActive, "Satellite Uplink: Innovative Digital Campaign unlocked" },

                // Propaganda
                { LocaleKeys.Propaganda_TabLabel, "Propaganda" },
                { LocaleKeys.Propaganda_PartyLabel, "Party" },
                { LocaleKeys.Propaganda_TargetLabel, "Target" },
                { LocaleKeys.Propaganda_TargetAdults, "Adults" },
                { LocaleKeys.Propaganda_TargetSeniors, "Seniors" },
                { LocaleKeys.Propaganda_IntensityLabel, "Intensity" },
                { LocaleKeys.Propaganda_IntensitySmall, "Small campaign" },
                { LocaleKeys.Propaganda_IntensityMedium, "Medium campaign" },
                { LocaleKeys.Propaganda_IntensityStrong, "Strong campaign" },
                { LocaleKeys.Propaganda_CostLabel, "Cost: " },
                { LocaleKeys.Propaganda_BonusLabel, "Bonus: " },
                { LocaleKeys.Propaganda_TreasuryLabel, "Reserves: " },
                { LocaleKeys.Propaganda_LaunchButton, "Launch campaign" },
                { LocaleKeys.Propaganda_InsufficientFunds, "Insufficient reserves." },
                { LocaleKeys.Propaganda_ActiveCampaignsHeader, "Ongoing campaigns" },
                { LocaleKeys.Propaganda_NoActiveCampaigns, "No active campaign." },
                { LocaleKeys.Propaganda_NoPlayerParty, "Create your own party (\"Your Party\" tab) to launch your own propaganda campaigns." },
                { LocaleKeys.Propaganda_AutoRenewLabel, "Automatically renew" },
                { LocaleKeys.Propaganda_AlreadyActive, "A campaign is already running for your party. Cancel it or wait for it to end before launching a new one." },
                { LocaleKeys.Propaganda_CancelButton, "Cancel campaign" },
                { LocaleKeys.Propaganda_DigitalLabel, "Innovative digital campaign" },
                { LocaleKeys.Propaganda_DigitalDesc, "Random vote intention bonus between 3% and 7%, city-wide, all age groups." },
                { LocaleKeys.Propaganda_DigitalLaunchButton, "Launch digital campaign" },
                { LocaleKeys.Propaganda_TargetToute, "City-wide" },

                // District Campaigns
                { LocaleKeys.DistrictCampaign_Header, "District campaigns" },
                { LocaleKeys.DistrictCampaign_SlotsUsed, "District campaigns active" },
                { LocaleKeys.DistrictCampaign_SelectDistrict, "Target district" },
                { LocaleKeys.DistrictCampaign_TypeLabel, "Campaign type" },
                { LocaleKeys.DistrictCampaign_TypeBoost, "Classic campaign" },
                { LocaleKeys.DistrictCampaign_TypeAttack, "Targeted campaign" },
                { LocaleKeys.DistrictCampaign_TargetLabel, "Target party" },
                { LocaleKeys.DistrictCampaign_AttackCleanLabel, "Clean campaign" },
                { LocaleKeys.DistrictCampaign_AttackCleanDesc, "-2% for the targeted party in this district. No risk to you." },
                { LocaleKeys.DistrictCampaign_AttackDirtyLabel, "Dirty campaign" },
                { LocaleKeys.DistrictCampaign_AttackDirtyDesc, "-4% for the targeted party, but a random penalty (0 to -5%) also hits your own party in this district." },
                { LocaleKeys.DistrictCampaign_MaxReached, "Maximum number of district campaigns reached (3)." },
                { LocaleKeys.DistrictCampaign_LaunchButton, "Launch campaign" },
                { LocaleKeys.DistrictCampaign_CancelButton, "Cancel" },
                { LocaleKeys.DistrictCampaign_ActiveListHeader, "Active district campaigns" },
                { LocaleKeys.DistrictCampaign_NoActiveCampaigns, "No active district campaign." },
                { LocaleKeys.DistrictCampaign_BoostSummary, "Boost" },
                { LocaleKeys.DistrictCampaign_AttackDirtyRiskSuffix, " (risk: -{0}% for you)" },

                // BlackFund
                { LocaleKeys.BlackFund_Header, "Black fund" },
                { LocaleKeys.BlackFund_ActivateButton, "Open a black fund" },
                { LocaleKeys.BlackFund_CloseButton, "Close black fund" },
                { LocaleKeys.BlackFund_CloseWarning, "Closing the black fund will forfeit all the money it holds." },
                { LocaleKeys.BlackFund_CloseConfirm, "Confirm closure" },
                { LocaleKeys.BlackFund_BalanceLabel, "Black fund balance" },
                { LocaleKeys.BlackFund_TransferToLabel, "Move to black fund" },
                { LocaleKeys.BlackFund_TransferFromLabel, "Move to main account" },
                { LocaleKeys.BlackFund_AmountPlaceholder, "Amount" },
                { LocaleKeys.BlackFund_TransferButton, "Transfer" },
                { LocaleKeys.BlackFund_InsufficientMainFunds, "Insufficient funds in the main account." },
                { LocaleKeys.BlackFund_InsufficientBlackFunds, "Insufficient funds in the black fund." },
                { LocaleKeys.BlackFund_MovementsHeader, "Recent movements" },
                { LocaleKeys.BlackFund_NoMovements, "No movements yet." },

                { LocaleKeys.BlackFund_Invoice1, "Purchase of 25 kg of stage makeup" },
                { LocaleKeys.BlackFund_Invoice2, "3 onions, 2 carrots, cheese, toilet paper..." },
                { LocaleKeys.BlackFund_Invoice3, "Stage lighting service for campaign speech" },
                { LocaleKeys.BlackFund_Invoice4, "Cleaning of the party headquarters facade" },

                // Electoral Commission
                { LocaleKeys.Commission_TabLabel, "Electoral Commission" },
                { LocaleKeys.Commission_ReportTitle, "Activity report" },
                { LocaleKeys.Commission_LevelLow, "Low vigilance" },
                { LocaleKeys.Commission_LevelMedium, "Under surveillance" },
                { LocaleKeys.Commission_LevelHigh, "On alert!" },
                { LocaleKeys.Commission_ActiveIllegalCount, "Active illegal campaigns" },
                { LocaleKeys.Commission_ActiveSanction, "Active sanction" },
                { LocaleKeys.Commission_NoSanction, "No active sanction." },
                { LocaleKeys.Commission_TabTitle, "Electoral Commission" },
                { LocaleKeys.Commission_TabIntro, "The Electoral Commission monitors each party's illegal campaigns and runs a check every 7 days.\n\n The more active illegal campaigns a party runs, the higher the risk of detection and escalating vigilance." },

                // Illegal district campaign
                { LocaleKeys.Illegal_SectionHeader, "Illegal campaign" },
                { LocaleKeys.Illegal_Description, "Targets a party in this district with a random penalty of 0 to 6%, funded by the black fund. If detected by the Electoral Commission, your party faces a city-wide penalty, a fine, and the loss of the black fund." },
                { LocaleKeys.Illegal_MalusLabel, "Penalty inflicted: random, 0 to 6%" },
                { LocaleKeys.Illegal_CostLabel, "Cost: " },
                { LocaleKeys.Illegal_LaunchButton, "Launch illegal campaign" },
                { LocaleKeys.Illegal_RequiresBlackFund, "Requires an active black fund." },
                { LocaleKeys.Illegal_InsufficientBlackFund, "Insufficient black fund balance." },
                { LocaleKeys.Illegal_MaxReached, "Maximum number of illegal campaigns reached (3)." },

                // Poll
                { LocaleKeys.Poll_TabLabel, "Polls" },
                { LocaleKeys.Poll_CostLabel, "Cost: " },
                { LocaleKeys.Poll_OrderButton, "Order a poll" },
                { LocaleKeys.Poll_InsufficientFunds, "Insufficient reserves." },
                { LocaleKeys.Poll_BlackoutMessage, "Polls are forbidden from the eve of the 1st round until the end of the 2nd round." },
                { LocaleKeys.Poll_ResultsHeader, "Latest results" },
                { LocaleKeys.Poll_NoResults, "No poll has been ordered yet." },
                { LocaleKeys.Poll_NoPlayerParty, "Create your own party (\"Your Party\" tab) to order polls." },
                { LocaleKeys.Treasury_PollsSpent, "Poll spending" },
                { LocaleKeys.Poll_ContextHeader, "Electoral context" },
                { LocaleKeys.Poll_ContextEmpty, "No notable city-wide factor at the moment." },
                { LocaleKeys.Poll_ContextUnemployment, "High unemployment" },
                { LocaleKeys.Poll_ContextTaxPoor, "Taxes too high for low-income households" },
                { LocaleKeys.Poll_ContextTaxRich, "Tax advantages for the wealthiest" },
                { LocaleKeys.Poll_ContextPropagandaPrefix, "Propaganda campaign: " },
                { LocaleKeys.Poll_ContextSanctionPrefix, "City-wide sanction: " },

                //Score
                { LocaleKeys.Score_TabLabel, "Score" },
                { LocaleKeys.Score_Header, "Ranking" },
                { LocaleKeys.Score_DetailTrophy, "Trophies (permanent conquests)" },
                { LocaleKeys.Score_DetailSeats, "Seats held" },
                { LocaleKeys.Score_DetailDistricts, "Districts led" },
                { LocaleKeys.Score_DetailBastions, "Strongholds held" },
                { LocaleKeys.Score_DetailReinforcedBastions, "Reinforced strongholds" },
                { LocaleKeys.Score_DetailMembers, "Members" },
                { LocaleKeys.Score_DetailPossessionTotal, "Possession total" },
                { LocaleKeys.Score_DetailGrandTotal, "Total score" },

                //Rules
                { LocaleKeys.Rules_TabLabel, "Rules" },
            };
        }
        public void Unload() { }
    }
}