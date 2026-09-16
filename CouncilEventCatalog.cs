using System.Collections.Generic;

namespace CityCouncil
{
    public enum EventCategory
    {
        FaitDivers,
        Scandale,
        Evenement,
    }

    public enum EventAgeScope
    {
        All,
        AdultsOnly,
        SeniorsOnly,
    }

    public enum EventEffectTarget
    {
        SpecificParty,
        LeadingPartyCityWide,
        Abstention,
        TransferOverride,
    }

    public struct EventEffect
    {
        public EventEffectTarget Target;
        public PoliticalParty Party;       
        public PoliticalParty TargetParty; 
        public float Percent;
        public EventAgeScope AgeScope;

        public static EventEffect PartyBonus(PoliticalParty party, float percent, EventAgeScope scope = EventAgeScope.All)
            => new EventEffect { Target = EventEffectTarget.SpecificParty, Party = party, Percent = percent, AgeScope = scope };

        public static EventEffect LeadingPartyBonus(float percent, EventAgeScope scope = EventAgeScope.All)
            => new EventEffect { Target = EventEffectTarget.LeadingPartyCityWide, Percent = percent, AgeScope = scope };

        public static EventEffect AbstentionDelta(float percent, EventAgeScope scope = EventAgeScope.All)
            => new EventEffect { Target = EventEffectTarget.Abstention, Percent = percent, AgeScope = scope };

        public static EventEffect TransferOverride(PoliticalParty eliminated, PoliticalParty finalist, float newValue)
            => new EventEffect { Target = EventEffectTarget.TransferOverride, Party = eliminated, TargetParty = finalist, Percent = newValue, AgeScope = EventAgeScope.All };
    }

    public class CouncilEventDefinition
    {
        public string Id;
        public string Headline; // Contient la clé de localisation (LocaleKeys)
        public EventCategory Category;
        public EventEffect[] Effects;
    }

    public static class CouncilEventCatalog
    {
        public static readonly List<CouncilEventDefinition> Events = new()
        {
            new CouncilEventDefinition
            {
                Id = "scandale_politique_desabuse",
                Headline = LocaleKeys.Event_ScandalePolitique,
                Category = EventCategory.Scandale,
                Effects = new[]
                {
                    EventEffect.AbstentionDelta(0.10f, EventAgeScope.All),
                },
            },

            new CouncilEventDefinition
            {
                Id = "incendies_forets",
                Headline = LocaleKeys.Event_IncendiesForets,
                Category = EventCategory.Evenement,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Ecologiste, 0.10f, EventAgeScope.All),
                },
            },

            new CouncilEventDefinition
            {
                Id = "reforme_pensions_retraite",
                Headline = LocaleKeys.Event_ReformePensions,
                Category = EventCategory.Evenement,
                Effects = new[]
                {
                    EventEffect.LeadingPartyBonus(-0.50f, EventAgeScope.SeniorsOnly),
                },
            },

            new CouncilEventDefinition
            {
                Id = "region_economie_sante",
                Headline = LocaleKeys.Event_RegionEconomieSante,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.GaucheRadicale, 0.10f, EventAgeScope.All),
                },

            },

            new CouncilEventDefinition
            {
                Id = "cacophonie_gauche_radicale",
                Headline = LocaleKeys.Event_CacophonieGaucheRadicale,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.GaucheRadicale, -0.05f, EventAgeScope.All),
                },

            },

            new CouncilEventDefinition
            {
                Id = "evolution_climat_impact",
                Headline = LocaleKeys.Event_EvolutionClimatImpact,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Ecologiste, 0.05f, EventAgeScope.All),
                },

            },

            new CouncilEventDefinition
            {
                Id = "deploiement_wifi",
                Headline = LocaleKeys.Event_DeploiementWifi,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Democrate, 0.02f, EventAgeScope.All),
                },

            },

            new CouncilEventDefinition
            {
                Id = "relance_nucleaire",
                Headline = LocaleKeys.Event_RelanceNucleaire,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Ecologiste, 0.02f, EventAgeScope.All),
                },

            },

            new CouncilEventDefinition
            {
                Id = "scandale_democrate",
                Headline = LocaleKeys.Event_ScandaleDemocrate,
                Category = EventCategory.Scandale,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Democrate, -0.06f, EventAgeScope.All),
                },

            },

            new CouncilEventDefinition
            {
                Id = "zi_developpement",
                Headline = LocaleKeys.Event_ZIDeveloppement,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Republicain, 0.05f, EventAgeScope.All),
                    EventEffect.PartyBonus(PoliticalParty.Ecologiste, 0.05f, EventAgeScope.All),
                },

            },

            new CouncilEventDefinition
            {
                Id = "populiste_animal_magazine",
                Headline = LocaleKeys.Event_PopulisteAnimalMagazine,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Populiste, 0.02f, EventAgeScope.All),
                },

            },

             new CouncilEventDefinition
            {
                Id = "police_bavure",
                Headline = LocaleKeys.Event_PoliceBavure,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.GaucheRadicale, 0.05f, EventAgeScope.All),
                },

            },

            new CouncilEventDefinition
            {
                Id = "investissement_securite",
                Headline = LocaleKeys.Event_InvestissementSecurite,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Republicain, 0.05f, EventAgeScope.All),
                },

            },

            new CouncilEventDefinition
            {
                Id = "durcissement_norme",
                Headline = LocaleKeys.Event_DurcissementNorme,
                Category = EventCategory.Evenement,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Populiste, 0.10f, EventAgeScope.All),
                },

            },

            new CouncilEventDefinition
            {
                Id = "desaccord_ecolo_democrate_inegalites",
                Headline = LocaleKeys.Event_DesaccordEcoloDemocrateInegalites,
                Category = EventCategory.Evenement,
                Effects = new[]
                {
                    // Report normal Ecologiste -> Democrate = 0.60f (cf. TransferMatrix) ; réduit à 0.30f
                    // tant que l'évènement est actif.
                    EventEffect.TransferOverride(PoliticalParty.Ecologiste, PoliticalParty.Democrate, 0.30f),
                },
            },

            // --- SCANDALES (4 nouveaux) ---
            new CouncilEventDefinition
            {
                Id = "fraude_subventions_culture",
                Headline = LocaleKeys.Event_FraudeSubventionsCulture,
                Category = EventCategory.Scandale,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Democrate, -0.07f, EventAgeScope.All),
                },
            },
            new CouncilEventDefinition
            {
                Id = "detournement_fonds_publics",
                Headline = LocaleKeys.Event_DétournementFondsPublics,
                Category = EventCategory.Scandale,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Republicain, -0.08f, EventAgeScope.All),
                },
            },
            new CouncilEventDefinition
            {
                Id = "conflit_interet_urbanisme",
                Headline = LocaleKeys.Event_ConflitInteretUrbanisme,
                Category = EventCategory.Scandale,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.GaucheRadicale, -0.06f, EventAgeScope.All),
                },
            },
            new CouncilEventDefinition
            {
                Id = "fuite_donnees_citoyens",
                Headline = LocaleKeys.Event_FuiteDonneesCitoyens,
                Category = EventCategory.Scandale,
                Effects = new[]
                {
                    EventEffect.LeadingPartyBonus(-0.08f, EventAgeScope.All),
                    EventEffect.AbstentionDelta(0.05f, EventAgeScope.All),
                },
            },

            // --- ÉVÈNEMENTS MAJEURS (5 nouveaux) ---
            new CouncilEventDefinition
            {
                Id = "greve_transports_collectifs",
                Headline = LocaleKeys.Event_GreveTransportsCollectifs,
                Category = EventCategory.Evenement,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.GaucheRadicale, 0.08f, EventAgeScope.AdultsOnly),
                    EventEffect.PartyBonus(PoliticalParty.Republicain, 0.04f, EventAgeScope.All),
                },
            },
            new CouncilEventDefinition
            {
                Id = "inauguration_parc_eolien",
                Headline = LocaleKeys.Event_InaugurationParcEolien,
                Category = EventCategory.Evenement,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Ecologiste, 0.08f, EventAgeScope.All),
                    EventEffect.PartyBonus(PoliticalParty.Populiste, 0.03f, EventAgeScope.SeniorsOnly),
                },
            },
            new CouncilEventDefinition
            {
                Id = "pic_pollution_atmospherique",
                Headline = LocaleKeys.Event_PicPollutionAtmospherique,
                Category = EventCategory.Evenement,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Ecologiste, 0.10f, EventAgeScope.All),
                },
            },
            new CouncilEventDefinition
            {
                Id = "sommet_economique_local",
                Headline = LocaleKeys.Event_SommetEconomiqueLocal,
                Category = EventCategory.Evenement,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Republicain, 0.07f, EventAgeScope.All),
                    EventEffect.PartyBonus(PoliticalParty.Democrate, 0.04f, EventAgeScope.All),
                },
            },
            new CouncilEventDefinition
            {
                Id = "canicule_extreme",
                Headline = LocaleKeys.Event_CaniculeExtreme,
                Category = EventCategory.Evenement,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Ecologiste, 0.06f, EventAgeScope.All),
                },
            },

            // --- FAITS DIVERS (6 nouveaux) ---
            new CouncilEventDefinition
            {
                Id = "penurie_logements_etudiants",
                Headline = LocaleKeys.Event_PenurieLogementsEtudiants,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.GaucheRadicale, 0.05f, EventAgeScope.AdultsOnly),
                },
            },
            new CouncilEventDefinition
            {
                Id = "nuit_securite_routiere",
                Headline = LocaleKeys.Event_NuitSecuriteRoutiere,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Republicain, 0.03f, EventAgeScope.SeniorsOnly),
                    EventEffect.PartyBonus(PoliticalParty.Populiste, 0.03f, EventAgeScope.All),
                },
            },
            new CouncilEventDefinition
            {
                Id = "echec_festival_eco_responsable",
                Headline = LocaleKeys.Event_EchecFestivalEcoResponsable,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Ecologiste, -0.04f, EventAgeScope.All),
                },
            },
            new CouncilEventDefinition
            {
                Id = "saisie_historique_drogue",
                Headline = LocaleKeys.Event_SaisieHistoriqueDrogue,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Populiste, 0.05f, EventAgeScope.All),
                },
            },
            new CouncilEventDefinition
            {
                Id = "manifestation_taxis_thermiques",
                Headline = LocaleKeys.Event_ManifestationTaxisThermiques,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Populiste, 0.04f, EventAgeScope.All),
                    EventEffect.PartyBonus(PoliticalParty.Ecologiste, -0.03f, EventAgeScope.All),
                },
            },
            new CouncilEventDefinition
            {
                Id = "succes_projet_teletravail",
                Headline = LocaleKeys.Event_SuccesProjetTélétravail,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Democrate, 0.04f, EventAgeScope.AdultsOnly),
                },
            },

        };
    }
}