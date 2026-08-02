using System.Collections.Generic;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace CityCouncil
{
    /// <summary>
    /// Système "service" (même pattern que EVSubsidyPolicyRegistry d'ElectricCarSubsidy) :
    /// résout une seule fois au chargement les entités prefab des 9 politiques de district
    /// concernées par les bonus/malus électoraux, pour éviter de refaire une recherche par
    /// nom à chaque élection (7 jours in-game, donc pas critique en perf, mais autant faire
    /// propre et suivre le pattern déjà validé sur un autre mod).
    /// </summary>
    public partial class CouncilPolicyRegistry : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil");

        public static readonly string[] TrackedPolicyNames =
        {
            "Energy Consumption Awareness",
            "Recycling",
            "Roadside Parking Fee",
            "Speed Bumps",
            "Heavy Traffic Ban",
            "Gated Community",
            "Combustion Engine Ban",
            "Urban Cycling Initiative",
            "Bicycle Traffic Restriction",
        };

        private PrefabSystem m_PrefabSystem;
        private readonly Dictionary<string, Entity> m_PolicyPrefabs = new();

        public IReadOnlyDictionary<string, Entity> PolicyPrefabs => m_PolicyPrefabs;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            ResolvePolicyPrefabs();
        }

        private void ResolvePolicyPrefabs()
        {
            m_PolicyPrefabs.Clear();

            var query = GetEntityQuery(ComponentType.ReadOnly<PolicyData>());
            var entities = query.ToEntityArray(Allocator.Temp);

            try
            {
                var remaining = new HashSet<string>(TrackedPolicyNames);
                foreach (var e in entities)
                {
                    if (remaining.Count == 0) break;

                    var prefab = m_PrefabSystem.GetPrefab<PolicyPrefab>(e);
                    if (remaining.Contains(prefab.name))
                    {
                        m_PolicyPrefabs[prefab.name] = e;
                        remaining.Remove(prefab.name);
                        s_Log.Info($"[CouncilPolicyRegistry] Prefab résolu pour '{prefab.name}' : {e}");
                    }
                }

                foreach (var missing in remaining)
                    s_Log.Warn($"[CouncilPolicyRegistry] Prefab introuvable pour la politique '{missing}'.");
            }
            finally
            {
                entities.Dispose();
            }
        }

        // Pas de logique per-frame nécessaire, ce système est purement passif
        // (même remarque que EVSubsidyPolicyRegistry).
        protected override void OnUpdate() { }
    }
}
