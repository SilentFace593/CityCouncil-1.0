using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Unity.Collections;
using Unity.Entities;

namespace CityCouncil
{
    /// <summary>
    /// Gère l'entité singleton portant CouncilCustomPartyData (création, mise à jour,
    /// suppression différée). Système "service" appelé par CouncilUISystem (côté écriture,
    /// déclenché par les actions du joueur en React) et par CouncilElectionSystem (côté
    /// application de la suppression en attente, une fois par cycle électoral).
    /// </summary>
    public partial class CouncilCustomPartySystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        private EntityQuery m_SingletonQuery;
        private Entity m_SingletonEntity = Entity.Null;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilCustomPartyData>());
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            EnsureSingleton();
        }

        private void EnsureSingleton()
        {
            var existing = m_SingletonQuery.ToEntityArray(Allocator.Temp);
            try
            {
                s_Log.Info($"[CouncilCustomPartySystem] EnsureSingleton : {existing.Length} entité(s) trouvée(s) au chargement.");

                if (existing.Length == 1)
                {
                    m_SingletonEntity = existing[0];
                    return;
                }

                if (existing.Length > 1)
                {
                    // Cas anormal (doublon créé par une désérialisation qui n'était pas encore
                    // terminée au moment du premier EnsureSingleton) : on garde l'entité qui a
                    // réellement des données (m_Exists=true), on détruit les autres. Si plusieurs
                    // ont m_Exists=true (ne devrait pas arriver), on garde la première et on logge
                    // un avertissement plutôt que de deviner.
                    Entity keep = existing[0];
                    bool foundReal = false;

                    foreach (var e in existing)
                    {
                        var data = EntityManager.GetComponentData<CouncilCustomPartyData>(e);
                        if (data.m_Exists && !foundReal)
                        {
                            keep = e;
                            foundReal = true;
                        }
                    }

                    foreach (var e in existing)
                    {
                        if (e != keep)
                        {
                            s_Log.Warn($"[CouncilCustomPartySystem] Entité singleton en doublon détruite : {e}");
                            EntityManager.DestroyEntity(e);
                        }
                    }

                    m_SingletonEntity = keep;
                    return;
                }
            }
            finally
            {
                existing.Dispose();
            }

            m_SingletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(m_SingletonEntity, new CouncilCustomPartyData
            {
                m_Exists = false
            });
            s_Log.Info("[CouncilCustomPartySystem] Entité singleton créée (aucune trouvée).");
        }

        public CouncilCustomPartyData GetData()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            return EntityManager.GetComponentData<CouncilCustomPartyData>(m_SingletonEntity);
        }

        private void SetData(CouncilCustomPartyData data)
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            EntityManager.SetComponentData(m_SingletonEntity, data);
        }

        /// <summary>
        /// Création ou mise à jour complète du parti joueur (un seul parti actif à la fois).
        /// Effective IMMÉDIATEMENT (contrairement à la suppression) : rien dans l'énoncé
        /// n'impose de délai pour la création/le renommage, seulement pour la suppression.
        /// </summary>
        public bool TryCreateOrUpdate(string name, PartyColor color, PoliticalParty space, out string error)
        {
            error = null;
            name = (name ?? string.Empty).Trim();

            if (name.Length == 0 || name.Length > 40)
            {
                error = "Nom invalide (1 à 40 caractères).";
                s_Log.Warn($"[CouncilCustomPartySystem] Création refusée : {error}");
                return false;
            }

            var data = GetData();
            data.m_Exists = true;
            data.m_Name = name;
            data.m_Color = color;
            data.m_Space = space;
            data.m_PendingDeletion = false; // une création/màj annule une suppression en attente
            SetData(data);

            s_Log.Info($"[CouncilCustomPartySystem] Parti joueur défini : '{name}' ({color}, bord {space}).");
            return true;
        }

        /// <summary>Marque le parti pour suppression : reste actif jusqu'à la prochaine élection.</summary>
        public void RequestDeletion()
        {
            var data = GetData();
            if (!data.m_Exists) return;

            data.m_PendingDeletion = true;
            SetData(data);
            s_Log.Info("[CouncilCustomPartySystem] Suppression du parti joueur planifiée (prochaine élection).");
        }

        /// <summary>
        /// Annule une suppression en attente (le joueur change d'avis avant la prochaine élection).
        /// </summary>
        public void CancelPendingDeletion()
        {
            var data = GetData();
            if (!data.m_Exists || !data.m_PendingDeletion) return;

            data.m_PendingDeletion = false;
            SetData(data);
            s_Log.Info("[CouncilCustomPartySystem] Suppression du parti joueur annulée.");
        }

        /// <summary>
        /// Appelé par CouncilElectionSystem juste avant de lancer un nouveau 1er tour, pour
        /// appliquer une suppression en attente. Idempotent si rien n'est en attente : peut
        /// être appelé plusieurs fois par cycle électoral (une fois par district) sans risque.
        /// </summary>
        public void ApplyPendingChangesForNewElection()
        {
            var data = GetData();
            if (!data.m_Exists || !data.m_PendingDeletion) return;

            data.m_Exists = false;
            data.m_PendingDeletion = false;
            data.m_Name = default;
            SetData(data);
            s_Log.Info("[CouncilCustomPartySystem] Parti joueur supprimé (effectif dès cette élection).");
        }

        // Pas de logique per-frame nécessaire, ce système est purement passif
        // (même remarque que CouncilPolicyRegistry).
        protected override void OnUpdate() { }
    }
}