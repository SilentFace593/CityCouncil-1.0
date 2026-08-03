using System.Collections.Generic;
using Colossal;

namespace CityCouncil
{
    public class CityCouncilLocaleFR : IDictionarySource
    {
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                // YourPartyTab
                { LocaleKeys.YourPartyTab_CreateButton, "Créer un parti politique" },
                { LocaleKeys.YourPartyTab_UpdateButton, "Mettre à jour le parti" },
                { LocaleKeys.YourPartyTab_DeleteButton, "Supprimer le parti" },
                { LocaleKeys.YourPartyTab_CancelButton, "Annuler" },
                { LocaleKeys.YourPartyTab_PendingDeletion, "Suppression prévue à la prochaine élection." },
                { LocaleKeys.YourPartyTab_SpacePrefix, "Bord politique : " },
                { LocaleKeys.YourPartyTab_SectionHeaderCreate, "Créer votre parti politique" },
                { LocaleKeys.YourPartyTab_SectionHeaderEdit, "Modifier votre parti" },
                { LocaleKeys.YourPartyTab_NameLabel, "Nom du parti" },
                { LocaleKeys.YourPartyTab_NamePlaceholder, "Ex : Renouveau Citoyen" },
                { LocaleKeys.YourPartyTab_ColorLabel, "Couleur" },
                { LocaleKeys.YourPartyTab_SpaceLabel, "Espace politique" },

                // HemicyclePanel
                { LocaleKeys.Hemicycle_PanelTitle, "Conseil municipal" },
                { LocaleKeys.Hemicycle_TabResults, "Résultats" },
                { LocaleKeys.Hemicycle_TabYourParty, "Votre Parti" },
                { LocaleKeys.Hemicycle_TabForces, "Forces Politiques" },
                { LocaleKeys.Hemicycle_NoElection, "Aucune élection terminée pour le moment." },
                { LocaleKeys.Hemicycle_LeaderPrefix, "Parti en tête : " },
                { LocaleKeys.Hemicycle_SeatsSuffix, " sièges au total" },

                // AdministrationSection
                { LocaleKeys.Admin_Header, "Administration" },
                { LocaleKeys.Admin_NoElectionDesc, "Pas d'élections dans ce District car aucun habitant. Il est géré par une Commission Spéciale." },
                { LocaleKeys.Admin_Round1WonPrefix, "District remporté par le Parti \"" },
                { LocaleKeys.Admin_Round1WonSuffix, "\" dès le 1er Tour. Election terminée et en attente de la fin du 2ème tour général." },
                { LocaleKeys.Admin_Round1PendingPrefix, "Aucune majorité au 1er tour. Second tour en attente entre " },
                { LocaleKeys.Admin_Round1PendingMiddle, " et " },
                { LocaleKeys.Admin_Round1PendingSuffix, "." },
                { LocaleKeys.Admin_Round1PendingDefault, "Aucune majorité au 1er tour. Le 2e tour est en attente." },
                { LocaleKeys.Admin_VotersLabel, "Votants" },
                { LocaleKeys.Admin_AbstentionLabel, "Abstention" },
                { LocaleKeys.Admin_SeatsPlural, "sièges" },
                { LocaleKeys.Admin_SeatsSingular, "siège" },

                // PoliticalForcesTab
                { LocaleKeys.Forces_CustomPartyDesc, "Votre parti politique. La personnalisation de la description est prévue dans une prochaine étape." },
                { LocaleKeys.Forces_MembersPlural, "adhérents" },
                { LocaleKeys.Forces_MembersSingular, "adhérent" },
                { LocaleKeys.Forces_SeatsPlural, "sièges au total" },
                { LocaleKeys.Forces_SeatsSingular, "siège au total" },
                { LocaleKeys.Forces_Treasury, "crédits en caisse" },
                { LocaleKeys.Forces_DescEcologiste, "Défend une transition écologique ambitieuse et la préservation des espaces naturels." },
                { LocaleKeys.Forces_DescDemocrate, "Parti de centre, favorable au dialogue social et à une gestion pragmatique de la ville." },
                { LocaleKeys.Forces_DescPopuliste, "Porte-voix des mécontentements populaires, critique des taxes et des élites locales." },
                { LocaleKeys.Forces_DescRepublicain, "Défend l'ordre, la sécurité et une gestion rigoureuse des finances municipales." },
                { LocaleKeys.Forces_DescGaucheRadicale, "Milite pour une redistribution radicale des richesses et des services publics renforcés." },

                // City Events
                { LocaleKeys.Event_ScandalePolitique, "Un scandale politique éclate dans la ville, les citoyens sont désabusés !" },
                { LocaleKeys.Event_IncendiesForets, "D'importants incendies ravagent des milliers d'hectares de forêts." },
                { LocaleKeys.Event_ReformePensions, "Il se murmure dans les couloirs de la mairie un projet de réforme des pensions de retraite. Les séniors sont vent debout !" },

                // Party Labels
                { LocaleKeys.Party_Ecologiste, "Écologiste" },
                { LocaleKeys.Party_Democrate, "Démocrate" },
                { LocaleKeys.Party_Populiste, "Populiste" },
                { LocaleKeys.Party_Republicain, "Républicain" },
                { LocaleKeys.Party_GaucheRadicale, "Gauche radicale" },

                // FundingTab
                { LocaleKeys.Funding_TabLabel, "Financement Politique" },
                { LocaleKeys.Funding_FixedAmountLabel, "Part fixe (par cycle)" },
                { LocaleKeys.Funding_FixedAmountHint, "Répartie à parts égales entre les 5 partis à la fin du cycle électoral." },
                { LocaleKeys.Funding_LockedMessage, "Montant verrouillé jusqu'à la prochaine distribution." },
                { LocaleKeys.Funding_VariableInfo, "Part variable : 1 000 crédits par siège obtenu, versée automatiquement." },
            };
        }
        public void Unload() { }
    }
}