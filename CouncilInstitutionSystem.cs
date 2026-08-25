using System.Linq;
using Colossal.Logging;
using Game;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace CityCouncil
{
    /// <summary>
    /// Système "service" détectant la présence de bâtiments spécifiques en ville, par nom de
    /// prefab. Utilisé pour les conditions de bonus spéciaux (ex. bonus Républicain lié au
    /// Central Intelligence Bureau). Contrairement à CouncilPolicyRegistry (résolution une fois
    /// au chargement), la présence est recalculée À LA DEMANDE : un bâtiment peut être construit
    /// ou détruit en cours de partie, donc pas de cache à invalider manuellement.
    ///
    /// NOTE — NOM DE PREFAB NON CONFIRMÉ PAR DÉCOMPILATION : "Central Intelligence Bureau" est le
    /// nom affiché en jeu, mais le nom technique du prefab (BuildingPrefab.name) peut différer
    /// (ex. underscore, nom anglais interne différent). Utiliser DebugLogAllBuildingPrefabNames()
    /// pour identifier le nom exact à mettre dans TrackedBuildingNames, puis ajuster.
    /// </summary>
    public partial class CouncilInstitutionSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        /// Nom de prefab confirmé par CouncilInstitutionSystem.DebugLogAllBuildingPrefabNames.
        public const string CentralIntelligenceBureauName = "CentralInvestigationBureau01";
        public const string PrisonName = "Prison01";
        public const string NuclearPowerPlantName = "NuclearPowerPlant01";
        public const string UniversityName = "University01";

        public bool IsCentralIntelligenceBureauPresent() => IsBuildingPresent(CentralIntelligenceBureauName);
        public bool IsPrisonPresent() => IsBuildingPresent(PrisonName);
        public bool IsNuclearPowerPlantPresent() => IsBuildingPresent(NuclearPowerPlantName);
        public bool IsUniversityPresent() => IsBuildingPresent(UniversityName);


        private EntityQuery m_BuildingQuery;
        private PrefabSystem m_PrefabSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            m_BuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Game.Buildings.Building>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Game.Tools.Temp>(),
                    ComponentType.ReadOnly<Game.Common.Deleted>(),
                }
            });
        }

        protected override void OnUpdate() { } // purement passif, interrogé à la demande

        /// <summary>
        /// True si au moins un bâtiment COMPLET (pas en construction) portant exactement ce nom
        /// de prefab est présent en ville. Coût : scan de tous les bâtiments, acceptable ici car
        /// appelé seulement au rythme d'un cycle électoral (7 jours), pas à chaque frame.
        /// </summary>
        public bool IsBuildingPresent(string exactPrefabName)
        {
            var entities = m_BuildingQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var e in entities)
                {
                    // Bâtiment en cours de construction : ne compte pas encore comme "présent".
                    // if (EntityManager.HasComponent<Game.Buildings.UnderConstruction>(e)) continue;

                    if (!EntityManager.HasComponent<PrefabRef>(e)) continue;
                    var prefabRef = EntityManager.GetComponentData<PrefabRef>(e);
                    if (prefabRef.m_Prefab == Entity.Null) continue;

                    // AJOUT — GetPrefab<T> plante (ArgumentOutOfRangeException) si l'entité référencée
                    // n'a pas encore d'index de prefab valide (bâtiment pas totalement initialisé au
                    // moment du chargement de la sauvegarde). TryGetPrefab est la version sûre.
                    if (!m_PrefabSystem.TryGetPrefab<BuildingPrefab>(prefabRef.m_Prefab, out var prefab)) continue;
                    if (prefab == null) continue;

                    if (prefab.name == exactPrefabName)
                        return true;
                }
            }
            finally { entities.Dispose(); }
            return false;
        }

        

        /// <summary>
        /// OUTIL DE DEBUG TEMPORAIRE — logge le nom de prefab de TOUS les bâtiments actuellement
        /// placés en ville (dédupliqué), pour identifier le nom exact du Central Intelligence
        /// Bureau (ou de tout autre bâtiment). Filtrer les logs sur "intelligence", "police",
        /// "bureau" pour retrouver l'entrée recherchée.
        /// </summary>
        public void DebugLogAllBuildingPrefabNames()
        {
            var names = new System.Collections.Generic.HashSet<string>();
            var entities = m_BuildingQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var e in entities)
                {
                    if (!EntityManager.HasComponent<PrefabRef>(e)) continue;
                    var prefabRef = EntityManager.GetComponentData<PrefabRef>(e);
                    if (prefabRef.m_Prefab == Entity.Null) continue;

                    if (!m_PrefabSystem.TryGetPrefab<BuildingPrefab>(prefabRef.m_Prefab, out var prefab)) continue;
                    if (prefab != null) names.Add(prefab.name);
                }
            }
            finally { entities.Dispose(); }

            s_Log.Info($"[CouncilInstitutionSystem] DEBUG : {names.Count} nom(s) de prefab de bâtiment distincts en ville :");
            foreach (var n in names.OrderBy(n => n))
                s_Log.Info($"[CouncilInstitutionSystem]   - '{n}'");
        }
    }
}