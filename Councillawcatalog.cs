using System.Collections.Generic;

namespace CityCouncil
{
    /// <summary>
    /// Échelle de satisfaction d'un parti vis-à-vis d'une loi (grille matricielle fournie par le
    /// joueur). L'ordre numérique (0..4) est important : il sert à mesurer un "écart" entre deux
    /// partis (ex. pour l'affinité de coalition), et à comparer une adhésion à un seuil (ex. le
    /// malus joueur se déclenche à partir de PlutotDefavorable inclus).
    /// </summary>
    public enum LawAdherence : byte
    {
        TresDefavorable = 0,   // --
        PlutotDefavorable = 1, // -
        Pragmatique = 2,       // =
        PlutotFavorable = 3,   // +
        TresFavorable = 4,     // ++
    }

    /// <summary>
    /// Thème politique d'une loi, purement indicatif (regroupement visuel côté UI, cf. tableau
    /// fourni : "Transports", "Infrastructures routières", "Environnement", ...). Libre d'ajouter
    /// de nouvelles valeurs au fur et à mesure du catalogue.
    /// </summary>
    public enum LawTheme : byte
    {
        Transports = 0,
        Environnement = 1,
        Energie = 2,
        Securite = 3,
        Fiscalite = 4,
        Logement = 5,
        ServicesPublics = 6,
        Urbanisme = 7,
        Economie = 8,
        Culture = 9,
        Social = 10,
    }

    /// <summary>
    /// Définition statique d'une loi proposable : son thème, un libellé (clé de localisation,
    /// même pattern que CouncilEventCatalog.Headline), et sa grille d'adhésion pour les 5 partis
    /// (correspond à une ligne du tableau Excel/Word fourni par le joueur).
    ///
    /// NOTE — Id est un identifiant TECHNIQUE stable (ne jamais le renommer une fois utilisé en
    /// jeu : il est référencé par CouncilLawSystem/l'historique et potentiellement sérialisé).
    /// </summary>
    public class CouncilLawDefinition
    {
        public string Id;
        public string TitleLocaleKey; // ex. "CityCouncil.Law.TRANSPORTS_GRATUITS_TITLE"
        public LawTheme Theme;
        public Dictionary<PoliticalParty, LawAdherence> Adherence;

        /// <summary>Adhésion d'un parti donné, Pragmatique par défaut si absent de la grille (garde-fou).</summary>
        public LawAdherence GetAdherence(PoliticalParty party)
        {
            return Adherence.TryGetValue(party, out var value) ? value : LawAdherence.Pragmatique;
        }
    }

    /// <summary>
    /// Catalogue statique des lois proposables, avec leur grille matricielle d'adhésion par parti.
    /// Reprend l'ensemble des 20 thèmes politiques fournis par la grille matricielle.
    /// </summary>
    public static class CouncilLawCatalog
    {
        public static readonly List<CouncilLawDefinition> Laws = new()
        {
            // 1. Transports en commun gratuits
            new CouncilLawDefinition
            {
                Id = "transports_commun_gratuits",
                TitleLocaleKey = LocaleKeys.Law_TransportsGratuitsTitle,
                Theme = LawTheme.Transports,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.TresFavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.TresFavorable },
                    { PoliticalParty.Democrate, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Republicain, LawAdherence.TresDefavorable },
                    { PoliticalParty.Populiste, LawAdherence.Pragmatique },
                },
            },

            // 2. Développement du réseau routier / Autoroutes
            new CouncilLawDefinition
            {
                Id = "developpement_reseau_routier",
                TitleLocaleKey = LocaleKeys.Law_ReseauRoutierTitle,
                Theme = LawTheme.Transports,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.TresDefavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.TresDefavorable },
                    { PoliticalParty.Democrate, LawAdherence.Pragmatique },
                    { PoliticalParty.Republicain, LawAdherence.TresFavorable },
                    { PoliticalParty.Populiste, LawAdherence.PlutotFavorable },
                },
            },

            // 3. Zones à faibles émissions (ZFE)
            new CouncilLawDefinition
            {
                Id = "zones_faibles_emissions",
                TitleLocaleKey = LocaleKeys.Law_ZfeTitle,
                Theme = LawTheme.Environnement,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.TresFavorable },
                    { PoliticalParty.Democrate, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Republicain, LawAdherence.PlutotDefavorable },
                    { PoliticalParty.Populiste, LawAdherence.TresDefavorable },
                },
            },

            // 4. Énergies renouvelables (Éolien / Solaire)
            new CouncilLawDefinition
            {
                Id = "energies_renouvelables",
                TitleLocaleKey = LocaleKeys.Law_EnergiesRenouvelablesTitle,
                Theme = LawTheme.Energie,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.TresFavorable },
                    { PoliticalParty.Democrate, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Republicain, LawAdherence.PlutotDefavorable },
                    { PoliticalParty.Populiste, LawAdherence.PlutotDefavorable },
                },
            },

            // 5. Énergie nucléaire
            new CouncilLawDefinition
            {
                Id = "energie_nucleaire",
                TitleLocaleKey = LocaleKeys.Law_EnergieNucleaireTitle,
                Theme = LawTheme.Energie,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.TresDefavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Democrate, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Republicain, LawAdherence.TresFavorable },
                    { PoliticalParty.Populiste, LawAdherence.PlutotFavorable },
                },
            },

            // 6. Budget de la police & Surveillance
            new CouncilLawDefinition
            {
                Id = "police_et_surveillance",
                TitleLocaleKey = LocaleKeys.Law_PoliceSurveillanceTitle,
                Theme = LawTheme.Securite,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.TresDefavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.PlutotDefavorable },
                    { PoliticalParty.Democrate, LawAdherence.Pragmatique },
                    { PoliticalParty.Republicain, LawAdherence.TresFavorable },
                    { PoliticalParty.Populiste, LawAdherence.TresFavorable },
                },
            },

            // 7. Législations / Taxes environnementales
            new CouncilLawDefinition
            {
                Id = "taxes_environnementales",
                TitleLocaleKey = LocaleKeys.Law_TaxesEnvironnementalesTitle,
                Theme = LawTheme.Environnement,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.TresFavorable },
                    { PoliticalParty.Democrate, LawAdherence.Pragmatique },
                    { PoliticalParty.Republicain, LawAdherence.TresDefavorable },
                    { PoliticalParty.Populiste, LawAdherence.TresDefavorable },
                },
            },

            // 8. Baisse des impôts fonciers / Entreprises
            new CouncilLawDefinition
            {
                Id = "baisse_impots_fonciers_entreprises",
                TitleLocaleKey = LocaleKeys.Law_BaisseImpotsTitle,
                Theme = LawTheme.Fiscalite,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.TresDefavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.PlutotDefavorable },
                    { PoliticalParty.Democrate, LawAdherence.Pragmatique },
                    { PoliticalParty.Republicain, LawAdherence.TresFavorable },
                    { PoliticalParty.Populiste, LawAdherence.PlutotFavorable },
                },
            },

            // 9. Logements sociaux obligatoires
            new CouncilLawDefinition
            {
                Id = "logements_sociaux_obligatoires",
                TitleLocaleKey = LocaleKeys.Law_LogementsSociauxTitle,
                Theme = LawTheme.Logement,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.TresFavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Democrate, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Republicain, LawAdherence.PlutotDefavorable },
                    { PoliticalParty.Populiste, LawAdherence.PlutotDefavorable },
                },
            },

            // 10. Privatisation des services publics
            new CouncilLawDefinition
            {
                Id = "privatisation_services_publics",
                TitleLocaleKey = LocaleKeys.Law_PrivatisationServicesTitle,
                Theme = LawTheme.ServicesPublics,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.TresDefavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.TresDefavorable },
                    { PoliticalParty.Democrate, LawAdherence.PlutotDefavorable },
                    { PoliticalParty.Republicain, LawAdherence.TresFavorable },
                    { PoliticalParty.Populiste, LawAdherence.PlutotDefavorable },
                },
            },

            // 11. Parcs et espaces verts urbains
            new CouncilLawDefinition
            {
                Id = "parcs_espaces_verts",
                TitleLocaleKey = LocaleKeys.Law_ParcsEspacesVertsTitle,
                Theme = LawTheme.Urbanisme,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.TresFavorable },
                    { PoliticalParty.Democrate, LawAdherence.TresFavorable },
                    { PoliticalParty.Republicain, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Populiste, LawAdherence.Pragmatique },
                },
            },

            // 12. Zones commerciales / Districts industriels
            new CouncilLawDefinition
            {
                Id = "zones_commerciales_industrielles",
                TitleLocaleKey = LocaleKeys.Law_ZonesCommercialesTitle,
                Theme = LawTheme.Economie,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.TresDefavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.TresDefavorable },
                    { PoliticalParty.Democrate, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Republicain, LawAdherence.TresFavorable },
                    { PoliticalParty.Populiste, LawAdherence.TresFavorable },
                },
            },

            // 13. Protection des zones naturelles non constructibles
            new CouncilLawDefinition
            {
                Id = "protection_zones_naturelles",
                TitleLocaleKey = LocaleKeys.Law_ProtectionZonesNaturellesTitle,
                Theme = LawTheme.Environnement,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.TresFavorable },
                    { PoliticalParty.Democrate, LawAdherence.Pragmatique },
                    { PoliticalParty.Republicain, LawAdherence.PlutotDefavorable },
                    { PoliticalParty.Populiste, LawAdherence.TresDefavorable },
                },
            },

            // 14. Subventions à la culture et aux arts
            new CouncilLawDefinition
            {
                Id = "subventions_culture_arts",
                TitleLocaleKey = LocaleKeys.Law_SubventionsCultureTitle,
                Theme = LawTheme.Culture,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.TresFavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Democrate, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Republicain, LawAdherence.PlutotDefavorable },
                    { PoliticalParty.Populiste, LawAdherence.TresDefavorable },
                },
            },

            // 15. Traitement / Recyclage avancé des déchets
            new CouncilLawDefinition
            {
                Id = "recyclage_avance_dechets",
                TitleLocaleKey = LocaleKeys.Law_RecyclageDechetsTitle,
                Theme = LawTheme.Environnement,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.Pragmatique },
                    { PoliticalParty.Ecologiste, LawAdherence.TresFavorable },
                    { PoliticalParty.Democrate, LawAdherence.TresFavorable },
                    { PoliticalParty.Republicain, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Populiste, LawAdherence.PlutotDefavorable },
                },
            },

            // 16. Couvre-feu commercial / Régulation du bruit
            new CouncilLawDefinition
            {
                Id = "couvre_feu_commercial_bruit",
                TitleLocaleKey = LocaleKeys.Law_CouvreFeuCommercialTitle,
                Theme = LawTheme.Securite,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.PlutotDefavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Democrate, LawAdherence.Pragmatique },
                    { PoliticalParty.Republicain, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Populiste, LawAdherence.PlutotDefavorable },
                },
            },

            // 17. Politiques d'intégration & Services d'accueil
            new CouncilLawDefinition
            {
                Id = "integration_services_accueil",
                TitleLocaleKey = LocaleKeys.Law_IntegrationServicesAccueilTitle,
                Theme = LawTheme.Social,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.TresFavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.TresFavorable },
                    { PoliticalParty.Democrate, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Republicain, LawAdherence.TresDefavorable },
                    { PoliticalParty.Populiste, LawAdherence.TresDefavorable },
                },
            },

            // 18. Mesures de souveraineté locale / Préférence locale
            new CouncilLawDefinition
            {
                Id = "souverainete_preference_locale",
                TitleLocaleKey = LocaleKeys.Law_PreferenceLocaleTitle,
                Theme = LawTheme.Economie,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.Pragmatique },
                    { PoliticalParty.Ecologiste, LawAdherence.PlutotDefavorable },
                    { PoliticalParty.Democrate, LawAdherence.PlutotDefavorable },
                    { PoliticalParty.Republicain, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Populiste, LawAdherence.TresFavorable },
                },
            },

            // 19. Soutien financier aux petites entreprises locales
            new CouncilLawDefinition
            {
                Id = "soutien_petites_entreprises_locales",
                TitleLocaleKey = LocaleKeys.Law_SoutienPetitesEntreprisesTitle,
                Theme = LawTheme.Economie,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Democrate, LawAdherence.TresFavorable },
                    { PoliticalParty.Republicain, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Populiste, LawAdherence.PlutotFavorable },
                },
            },

            // 20. Projets de grands équipements (Stades, Casino)
            new CouncilLawDefinition
            {
                Id = "grands_equipements_stades_casino",
                TitleLocaleKey = LocaleKeys.Law_GrandsEquipementsTitle,
                Theme = LawTheme.Urbanisme,
                Adherence = new Dictionary<PoliticalParty, LawAdherence>
                {
                    { PoliticalParty.GaucheRadicale, LawAdherence.TresDefavorable },
                    { PoliticalParty.Ecologiste, LawAdherence.TresDefavorable },
                    { PoliticalParty.Democrate, LawAdherence.PlutotFavorable },
                    { PoliticalParty.Republicain, LawAdherence.TresFavorable },
                    { PoliticalParty.Populiste, LawAdherence.TresFavorable },
                },
            },
        };

        public static CouncilLawDefinition GetById(string id)
        {
            foreach (var law in Laws)
                if (law.Id == id) return law;
            return null;
        }
    }
}