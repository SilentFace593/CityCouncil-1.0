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
                { LocaleKeys.YourPartyTab_PendingActivation, "Votre parti ne pourra concourir qu'à partir de la prochaine élection." },

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
                { LocaleKeys.Forces_PendingReplacement, "Parti remplacé à la prochaine élection !" },
                { LocaleKeys.Treasury_Header, "Détail de la trésorerie" },
                { LocaleKeys.Treasury_CityFunding, "Financement municipal" },
                { LocaleKeys.Treasury_Dues, "Cotisations des adhérents" },
                { LocaleKeys.Treasury_PropagandaSpent, "Dépenses de propagande" },
                { LocaleKeys.Treasury_CurrentTotal, "Solde actuel" },


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

                // Bastion
                { LocaleKeys.Admin_BastionLabel, "Bastion : " },            
                { LocaleKeys.Admin_BastionActiveSuffix, " — Bonus actif (+4%)" },

                { LocaleKeys.Forces_BonusDefensifLabel, "Bonus permanent : Défensif" },
                { LocaleKeys.Forces_BonusOffensifLabel, "Bonus permanent : Offensif" },
                { LocaleKeys.Admin_BonusDefensifTooltip, "Bonus permanent Défensif" },
                { LocaleKeys.Admin_BonusOffensifTooltip, "Bonus permanent Offensif" },
                { LocaleKeys.Hemicycle_BonusChoiceTitle, "Bonus permanent obtenu !" },
                { LocaleKeys.Hemicycle_BonusChoiceDesc, "Votre parti a remporté la majorité au conseil municipal plusieurs fois de suite. Choisissez votre bonus permanent :" },
                { LocaleKeys.Hemicycle_BonusChoiceDefensif, "Défensif" },
                { LocaleKeys.Hemicycle_BonusChoiceOffensif, "Offensif" },
                { LocaleKeys.Hemicycle_BonusChoiceConfirm, "Valider" },
                { LocaleKeys.Hemicycle_BonusChoiceHint, "Pour changer de bonus, vous devrez remporter la majorité au moins une fois de plus." },
                { LocaleKeys.Hemicycle_BonusPendingTooltip, "Bonus Permanent à choisir !" },

                // Propagande
                { LocaleKeys.Propaganda_TabLabel, "Propagande" },
                { LocaleKeys.Propaganda_PartyLabel, "Parti" },
                { LocaleKeys.Propaganda_TargetLabel, "Cible" },
                { LocaleKeys.Propaganda_TargetAdults, "Adultes" },
                { LocaleKeys.Propaganda_TargetSeniors, "Séniors" },
                { LocaleKeys.Propaganda_IntensityLabel, "Intensité" },
                { LocaleKeys.Propaganda_IntensitySmall, "Petite campagne" },
                { LocaleKeys.Propaganda_IntensityMedium, "Campagne moyenne" },
                { LocaleKeys.Propaganda_IntensityStrong, "Forte campagne" },
                { LocaleKeys.Propaganda_CostLabel, "Coût : " },
                { LocaleKeys.Propaganda_BonusLabel, "Bonus : " },
                { LocaleKeys.Propaganda_TreasuryLabel, "Réserves : " },
                { LocaleKeys.Propaganda_LaunchButton, "Lancer la campagne" },
                { LocaleKeys.Propaganda_InsufficientFunds, "Réserves insuffisantes." },
                { LocaleKeys.Propaganda_ActiveCampaignsHeader, "Campagnes en cours" },
                { LocaleKeys.Propaganda_NoActiveCampaigns, "Aucune campagne active." },
                { LocaleKeys.Propaganda_NoPlayerParty, "Créez votre propre parti (onglet \"Votre Parti\") pour lancer vos propres campagnes de propagande." },
                { LocaleKeys.Propaganda_AutoRenewLabel, "Reconduire automatiquement" },
                { LocaleKeys.Propaganda_AlreadyActive, "Une campagne est déjà en cours pour votre parti. Annulez-la ou attendez son terme pour en lancer une nouvelle." },
                { LocaleKeys.Propaganda_CancelButton, "Annuler la campagne" },

                // Campagnes de district
                { LocaleKeys.DistrictCampaign_Header, "Campagnes de district" },
                { LocaleKeys.DistrictCampaign_SlotsUsed, "campagnes de district actives" },
                { LocaleKeys.DistrictCampaign_SelectDistrict, "District ciblé" },
                { LocaleKeys.DistrictCampaign_TypeLabel, "Type de campagne" },
                { LocaleKeys.DistrictCampaign_TypeBoost, "Campagne classique" },
                { LocaleKeys.DistrictCampaign_TypeAttack, "Campagne ciblée" },
                { LocaleKeys.DistrictCampaign_TargetLabel, "Parti visé" },
                { LocaleKeys.DistrictCampaign_AttackCleanLabel, "Campagne propre" },
                { LocaleKeys.DistrictCampaign_AttackCleanDesc, "-2% pour le parti visé dans ce district. Aucun risque pour vous." },
                { LocaleKeys.DistrictCampaign_AttackDirtyLabel, "Campagne sale" },
                { LocaleKeys.DistrictCampaign_AttackDirtyDesc, "-4% pour le parti visé, mais un malus aléatoire (0 à -5%) frappe aussi votre propre parti dans ce district." },
                { LocaleKeys.DistrictCampaign_MaxReached, "Nombre maximum de campagnes de district atteint (3)." },
                { LocaleKeys.DistrictCampaign_LaunchButton, "Lancer la campagne" },
                { LocaleKeys.DistrictCampaign_CancelButton, "Annuler" },
                { LocaleKeys.DistrictCampaign_ActiveListHeader, "Campagnes de district en cours" },
                { LocaleKeys.DistrictCampaign_NoActiveCampaigns, "Aucune campagne de district active." },
                { LocaleKeys.DistrictCampaign_BoostSummary, "Boost" },
                { LocaleKeys.DistrictCampaign_AttackDirtyRiskSuffix, " (risque : -{0}% pour vous)" },

                // Caisse noire
                { LocaleKeys.BlackFund_Header, "Caisse noire" },
                { LocaleKeys.BlackFund_ActivateButton, "Ouvrir une caisse noire" },
                { LocaleKeys.BlackFund_CloseButton, "Fermer la caisse noire" },
                { LocaleKeys.BlackFund_CloseWarning, "Fermer la caisse noire fera perdre tout l'argent qu'elle contient." },
                { LocaleKeys.BlackFund_CloseConfirm, "Confirmer la fermeture" },
                { LocaleKeys.BlackFund_BalanceLabel, "Solde de la caisse noire" },
                { LocaleKeys.BlackFund_TransferToLabel, "Transférer vers la caisse noire" },
                { LocaleKeys.BlackFund_TransferFromLabel, "Transférer vers le compte principal" },
                { LocaleKeys.BlackFund_AmountPlaceholder, "Montant" },
                { LocaleKeys.BlackFund_TransferButton, "Transférer" },
                { LocaleKeys.BlackFund_InsufficientMainFunds, "Fonds insuffisants sur le compte principal." },
                { LocaleKeys.BlackFund_InsufficientBlackFunds, "Fonds insuffisants dans la caisse noire." },
                { LocaleKeys.BlackFund_MovementsHeader, "Mouvements récents" },
                { LocaleKeys.BlackFund_NoMovements, "Aucun mouvement pour le moment." },

                { LocaleKeys.BlackFund_Invoice1, "Achat de 25 kg de maquillage" },
                { LocaleKeys.BlackFund_Invoice2, "3 oignons, 2 carottes, fromage, papier toilette..." },
                { LocaleKeys.BlackFund_Invoice3, "Prestation d'éclairage de scène pour discours électoral" },
                { LocaleKeys.BlackFund_Invoice4, "Nettoyage de la façade du siège du parti" },

                // Commission Électorale
                { LocaleKeys.Commission_TabLabel, "Commission Électorale" },
                { LocaleKeys.Commission_ReportTitle, "Rapport d'Activités" },
                { LocaleKeys.Commission_LevelLow, "Peu vigilant" },
                { LocaleKeys.Commission_LevelMedium, "Sous surveillance" },
                { LocaleKeys.Commission_LevelHigh, "En Alerte !" },
                { LocaleKeys.Commission_ActiveIllegalCount, "Campagnes illégales actives" },
                { LocaleKeys.Commission_ActiveSanction, "Sanction active" },
                { LocaleKeys.Commission_NoSanction, "Aucune sanction active." },

                // Campagne illégale de district
                { LocaleKeys.Illegal_SectionHeader, "Campagne illégale" },
                { LocaleKeys.Illegal_Description, "Cible un parti dans ce district avec un malus aléatoire de 0 à 6%, financé par la caisse noire. En cas de détection par la Commission Électorale, votre parti encourt une sanction à l'échelle de la ville, une amende, et la perte de la caisse noire." },
                { LocaleKeys.Illegal_MalusLabel, "Malus infligé : aléatoire, 0 à 6%" },
                { LocaleKeys.Illegal_CostLabel, "Coût : " },
                { LocaleKeys.Illegal_LaunchButton, "Lancer la campagne illégale" },
                { LocaleKeys.Illegal_RequiresBlackFund, "Nécessite une caisse noire active." },
                { LocaleKeys.Illegal_InsufficientBlackFund, "Solde de la caisse noire insuffisant." },
                { LocaleKeys.Illegal_MaxReached, "Nombre maximum de campagnes illégales atteint (3)." },

            };
        }
        public void Unload() { }
    }
}