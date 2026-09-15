using System.Collections.Generic;

namespace CityCouncil
{
    /// <summary>
    /// Cible d'un effet d'évènement de DISTRICT — volontairement plus restreint que
    /// EventEffectTarget (CouncilEventCatalog, ville entière) : pas d'Abstention ni de
    /// TransferOverride pour l'instant, le catalogue actuel se limite à des bonus/malus de
    /// parti localisés à un seul district.
    /// </summary>
    public enum DistrictEventEffectTarget
    {
        SpecificParty,          // parti nommé, bonus/malus dans CE district uniquement
        LeadingPartyInDistrict, // parti actuellement en tête dans CE district (avant ce tour)
    }

    public struct DistrictEventEffect
    {
        public DistrictEventEffectTarget Target;
        public PoliticalParty Party;   // valide seulement si Target == SpecificParty
        public float Percent;
        public EventAgeScope AgeScope; // réutilise l'enum de CouncilEventCatalog.cs

        public static DistrictEventEffect PartyBonus(PoliticalParty party, float percent, EventAgeScope scope = EventAgeScope.All)
            => new DistrictEventEffect { Target = DistrictEventEffectTarget.SpecificParty, Party = party, Percent = percent, AgeScope = scope };

        public static DistrictEventEffect LeadingPartyBonus(float percent, EventAgeScope scope = EventAgeScope.All)
            => new DistrictEventEffect { Target = DistrictEventEffectTarget.LeadingPartyInDistrict, Percent = percent, AgeScope = scope };
    }

    public class CouncilDistrictEventDefinition
    {
        public string Id;
        public string Headline; // clé de localisation — texte SANS le nom du district (pas
                                // d'interpolation de chaîne ailleurs dans le mod ; le contexte
                                // du district est déjà visible dans le panneau où ce texte s'affiche)
        public EventCategory Category; // réutilise l'enum de CouncilEventCatalog.cs
        public DistrictEventEffect[] Effects;
    }

    /// <summary>
    /// Catalogue statique des évènements de DISTRICT, séparé de CouncilEventCatalog (ville) pour
    /// ne pas mélanger deux sémantiques d'effets différentes dans la même liste (cf. discussion
    /// design). Réutilise EventCategory/EventAgeScope existants pour rester cohérent avec le
    /// système ville (mêmes catégories Fait Divers/Scandale/Évènement, même scope Adultes/Séniors).
    /// </summary>
    public static class CouncilDistrictEventCatalog
    {
        public static readonly List<CouncilDistrictEventDefinition> Events = new()
        {
            new CouncilDistrictEventDefinition
            {
                Id = "district_travaux_voirie_retard",
                Headline = LocaleKeys.DistrictEvent_TravauxVoirieRetard,
                Category = EventCategory.FaitDivers,
                Effects = new[] { DistrictEventEffect.LeadingPartyBonus(-0.03f, EventAgeScope.All) },
            },

            new CouncilDistrictEventDefinition
            {
                Id = "district_meeting_populiste",
                Headline = LocaleKeys.DistrictEvent_MeetingPopuliste,
                Category = EventCategory.FaitDivers,
                Effects = new[] { DistrictEventEffect.PartyBonus(PoliticalParty.Populiste, 0.03f, EventAgeScope.All) },
            },

            new CouncilDistrictEventDefinition
            {
                Id = "district_scandale_favoritisme",
                Headline = LocaleKeys.DistrictEvent_ScandaleFavoritisme,
                Category = EventCategory.Scandale,
                Effects = new[] { DistrictEventEffect.LeadingPartyBonus(-0.05f, EventAgeScope.All) },
            },

            new CouncilDistrictEventDefinition
            {
                Id = "district_petition_vegetalisation",
                Headline = LocaleKeys.DistrictEvent_PetitionVegetalisation,
                Category = EventCategory.Evenement,
                Effects = new[] { DistrictEventEffect.PartyBonus(PoliticalParty.Ecologiste, 0.04f, EventAgeScope.All) },
            },

            new CouncilDistrictEventDefinition
            {
                Id = "district_securite_quartier_seniors",
                Headline = LocaleKeys.DistrictEvent_SecuriteQuartierSeniors,
                Category = EventCategory.FaitDivers,
                Effects = new[] { DistrictEventEffect.PartyBonus(PoliticalParty.Republicain, 0.03f, EventAgeScope.SeniorsOnly) },
            },

            new CouncilDistrictEventDefinition
            {
                Id = "district_tensions_gauche_radicale",
                Headline = LocaleKeys.DistrictEvent_TensionsGaucheRadicale,
                Category = EventCategory.Scandale,
                Effects = new[] { DistrictEventEffect.PartyBonus(PoliticalParty.GaucheRadicale, -0.04f, EventAgeScope.All) },
            },

            // --- FAITS DIVERS (7 nouveaux) ---
            new CouncilDistrictEventDefinition
            {
                Id = "district_nuisances_chantier_fermeture",
                Headline = LocaleKeys.DistrictEvent_NuisancesChantierFermeture,
                Category = EventCategory.FaitDivers,
                Effects = new[] { DistrictEventEffect.LeadingPartyBonus(-0.03f, EventAgeScope.All) },
            },
            new CouncilDistrictEventDefinition
            {
                Id = "district_tensions_commerçants_seniors",
                Headline = LocaleKeys.DistrictEvent_TensionsCommerçantsSeniors,
                Category = EventCategory.FaitDivers,
                Effects = new[] { DistrictEventEffect.PartyBonus(PoliticalParty.Republicain, 0.03f, EventAgeScope.SeniorsOnly) },
            },
            new CouncilDistrictEventDefinition
            {
                Id = "district_squat_immeuble_ancien",
                Headline = LocaleKeys.DistrictEvent_SquatImmeubleAncien,
                Category = EventCategory.FaitDivers,
                Effects = new[] {
                    DistrictEventEffect.PartyBonus(PoliticalParty.GaucheRadicale, 0.03f, EventAgeScope.AdultsOnly),
                    DistrictEventEffect.PartyBonus(PoliticalParty.Populiste, 0.03f, EventAgeScope.All)
                },
            },
            new CouncilDistrictEventDefinition
            {
                Id = "district_ronde_citoyenne_securite",
                Headline = LocaleKeys.DistrictEvent_RondeCitoyenneSécurité,
                Category = EventCategory.FaitDivers,
                Effects = new[] { DistrictEventEffect.PartyBonus(PoliticalParty.Populiste, 0.04f, EventAgeScope.All) },
            },
            new CouncilDistrictEventDefinition
            {
                Id = "district_succes_vide_grenier_quartier",
                Headline = LocaleKeys.DistrictEvent_SuccesVideGrenierQuartier,
                Category = EventCategory.FaitDivers,
                Effects = new[] { DistrictEventEffect.LeadingPartyBonus(0.02f, EventAgeScope.All) },
            },
            new CouncilDistrictEventDefinition
            {
                Id = "district_inauguration_zone_artisanale",
                Headline = LocaleKeys.DistrictEvent_InaugurationZoneArtisanale,
                Category = EventCategory.FaitDivers,
                Effects = new[] { DistrictEventEffect.PartyBonus(PoliticalParty.Democrate, 0.03f, EventAgeScope.All) },
            },
            new CouncilDistrictEventDefinition
            {
                Id = "district_collecte_proprete_quartier",
                Headline = LocaleKeys.DistrictEvent_CollectePropreteQuartier,
                Category = EventCategory.FaitDivers,
                Effects = new[] { DistrictEventEffect.PartyBonus(PoliticalParty.Ecologiste, 0.03f, EventAgeScope.All) },
            },

            // --- SCANDALES (4 nouveaux) ---
            new CouncilDistrictEventDefinition
            {
                Id = "district_scandale_permis_construire",
                Headline = LocaleKeys.DistrictEvent_ScandalePermisConstruire,
                Category = EventCategory.Scandale,
                Effects = new[] { DistrictEventEffect.LeadingPartyBonus(-0.05f, EventAgeScope.All) },
            },
            new CouncilDistrictEventDefinition
            {
                Id = "district_soupçon_prise_interet_elu_local",
                Headline = LocaleKeys.DistrictEvent_SoupçonPriseInteretEluLocal,
                Category = EventCategory.Scandale,
                Effects = new[] { DistrictEventEffect.LeadingPartyBonus(-0.04f, EventAgeScope.All) },
            },
            new CouncilDistrictEventDefinition
            {
                Id = "district_rivalite_interne_gauche",
                Headline = LocaleKeys.DistrictEvent_RivaliteInterneGauche,
                Category = EventCategory.Scandale,
                Effects = new[] { DistrictEventEffect.PartyBonus(PoliticalParty.GaucheRadicale, -0.05f, EventAgeScope.All) },
            },
            new CouncilDistrictEventDefinition
            {
                Id = "district_detournement_caisse_fete",
                Headline = LocaleKeys.DistrictEvent_DétournementCaisseFete,
                Category = EventCategory.Scandale,
                Effects = new[] { DistrictEventEffect.LeadingPartyBonus(-0.04f, EventAgeScope.All) },
            },

            // --- ÉVÈNEMENTS MAJEURS DE DISTRICT (4 nouveaux) ---
            new CouncilDistrictEventDefinition
            {
                Id = "district_inauguration_centre_social",
                Headline = LocaleKeys.DistrictEvent_InaugurationCentreSocial,
                Category = EventCategory.Evenement,
                Effects = new[] { DistrictEventEffect.PartyBonus(PoliticalParty.GaucheRadicale, 0.04f, EventAgeScope.All) },
            },
            new CouncilDistrictEventDefinition
            {
                Id = "district_renovation_piste_cycable",
                Headline = LocaleKeys.DistrictEvent_RénovationPisteCycable,
                Category = EventCategory.Evenement,
                Effects = new[] { DistrictEventEffect.PartyBonus(PoliticalParty.Ecologiste, 0.05f, EventAgeScope.All) },
            },
            new CouncilDistrictEventDefinition
            {
                Id = "district_fermeture_classe_maternelle",
                Headline = LocaleKeys.DistrictEvent_FermetureClasseMaternelle,
                Category = EventCategory.Evenement,
                Effects = new[] { DistrictEventEffect.LeadingPartyBonus(-0.04f, EventAgeScope.AdultsOnly) },
            },
            new CouncilDistrictEventDefinition
            {
                Id = "district_alerte_degat_eaux_commerces",
                Headline = LocaleKeys.DistrictEvent_AlerteDegatEauxCommerces,
                Category = EventCategory.Evenement,
                Effects = new[] { DistrictEventEffect.LeadingPartyBonus(-0.03f, EventAgeScope.All) },
            },
        };

        public static CouncilDistrictEventDefinition GetById(string id)
        {
            foreach (var e in Events)
                if (e.Id == id) return e;
            return null;
        }
    }
}