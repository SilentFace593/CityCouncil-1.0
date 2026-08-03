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

                // HemicyclePanel
                { LocaleKeys.Hemicycle_PanelTitle, "City Council" },
                { LocaleKeys.Hemicycle_TabResults, "Results" },
                { LocaleKeys.Hemicycle_TabYourParty, "Your Party" },
                { LocaleKeys.Hemicycle_TabForces, "Political Forces" },
                { LocaleKeys.Hemicycle_NoElection, "No completed election yet." },
                { LocaleKeys.Hemicycle_LeaderPrefix, "Leading party: " },
                { LocaleKeys.Hemicycle_SeatsSuffix, " total seats" },

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

                // City Events
                { LocaleKeys.Event_ScandalePolitique, "A political scandal breaks out in the city, citizens are disillusioned!" },
                { LocaleKeys.Event_IncendiesForets, "Major forest fires ravage thousands of hectares." },
                { LocaleKeys.Event_ReformePensions, "Rumors swirl in city hall about a pension reform proposal. Seniors are up in arms!" },

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
            };
        }
        public void Unload() { }
    }
}