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
        private EntityQuery m_DistrictQuery;
        private Entity m_SingletonEntity = Entity.Null;
        private CouncilPartyMembershipSystem m_MembershipSystem;
        private CouncilScoreSystem m_ScoreSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilCustomPartyData>());
            m_DistrictQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilDistrictData>());
            m_MembershipSystem = World.GetOrCreateSystemManaged<CouncilPartyMembershipSystem>();
            m_ScoreSystem = World.GetOrCreateSystemManaged<CouncilScoreSystem>();
        }

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, Game.GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            DestroyExistingSingleton();
        }

        /// <summary>
        /// Détruit l'entité singleton AVANT la désérialisation d'une nouvelle sauvegarde. Sans ça,
        /// une entité créée manuellement pendant une session précédente (save A) peut survivre au
        /// chargement d'une autre sauvegarde (save B) qui ne la contient pas réellement, si le World
        /// n'est pas entièrement recréé entre deux chargements. EnsureSingleton() (appelé après, dans
        /// OnGameLoaded) repart alors sur un état garanti frais, ou sur les données réellement
        /// désérialisées pour CETTE sauvegarde si elles existent.
        /// </summary>
        private void DestroyExistingSingleton()
        {
            var existing = m_SingletonQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var e in existing)
                    EntityManager.DestroyEntity(e);
            }
            finally
            {
                existing.Dispose();
            }
            m_SingletonEntity = Entity.Null;
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
        /// <summary>
        /// Création ou mise à jour. Nom/couleur/bord ciblé sont mis à jour immédiatement (utilisés
        /// tels quels dès la prochaine activation), mais la SUBSTITUTION VISUELLE elle-même
        /// (remplacement effectif de l'hôte) reste gouvernée par m_SubstitutionActive/m_ActiveSpace,
        /// jamais touchés ici — seule ApplyPendingChangesForNewElection peut les faire évoluer.
        /// Un changement de m_Space vers un bord différent de m_ActiveSpace remet donc naturellement
        /// la substitution "en attente" jusqu'à la prochaine élection, même pour un parti déjà actif.
        /// </summary>
        public bool TryCreateOrUpdate(string name, PartyColor color, PoliticalParty space, PartyStructureType structureType, out string error)
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
            data.m_StructureType = structureType; // AJOUT
            data.m_PendingDeletion = false;
            SetData(data);

            s_Log.Info($"[CouncilCustomPartySystem] Parti joueur défini : '{name}' ({color}, bord ciblé {space}, structure {structureType}).");
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
        /// <summary>
        /// Appelé par CouncilElectionSystem juste avant un nouveau 1er tour. Deux responsabilités,
        /// mutuellement exclusives pour un même appel :
        ///   - suppression en attente -> désactivation complète du parti joueur ;
        ///   - sinon, activation d'une substitution en attente (première création OU changement de
        ///     bord ciblé) -> la décoration prend effet à partir de CETTE élection, et les adhérents/
        ///     trésorerie du bord nouvellement substitué repartent à zéro (le nouveau parti ne
        ///     n'hérite PAS du passif de l'ancien).
        /// Idempotent (peut être rappelé plusieurs fois par cycle, une fois par district).
        /// </summary>
        public void ApplyPendingChangesForNewElection()
        {
            var data = GetData();
            if (!data.m_Exists) return;

            if (data.m_PendingDeletion)
            {
                // AJOUT — le slot qu'occupait le parti joueur retrouve son type de structure par défaut
                // (celui du parti vanilla hôte), puisque le parti custom qui l'habillait disparaît.
                if (data.m_SubstitutionActive)
                {
                    var defaultType = PartyStructureCatalog.GetDefaultForSpace(data.m_ActiveSpace);
                    m_MembershipSystem.SetStructureType(data.m_ActiveSpace, defaultType);
                }

                data.m_Exists = false;
                data.m_PendingDeletion = false;
                data.m_SubstitutionActive = false;
                data.m_Name = default;
                SetData(data);
                s_Log.Info("[CouncilCustomPartySystem] Parti joueur supprimé (effectif dès cette élection).");
                return;
            }

            bool needsActivation = !data.m_SubstitutionActive || data.m_ActiveSpace != data.m_Space;
            if (!needsActivation)
            {
                // AJOUT — même sans changement d'espace, le joueur a pu changer son type de structure
                // via "Mettre à jour le parti" sans attendre une nouvelle substitution : on l'applique
                // quand même à chaque appel (idempotent, coût négligeable).
                m_MembershipSystem.SetStructureType(data.m_ActiveSpace, data.m_StructureType);
                return;
            }

            var newSpace = data.m_Space;
            data.m_ActiveSpace = newSpace;
            data.m_SubstitutionActive = true;
            SetData(data);

            m_MembershipSystem.ResetPartyTreasuryAndMembers(newSpace);
            m_MembershipSystem.SetStructureType(newSpace, data.m_StructureType); // AJOUT
            ResetBastionProgressForParty(newSpace);
            m_ScoreSystem.ResetScore(newSpace);

            s_Log.Info($"[CouncilCustomPartySystem] Substitution activée pour le bord {newSpace} (adhérents/trésorerie/Bastion remis à zéro, structure {data.m_StructureType}).");
        }

        /// <summary>
        /// Remet à zéro la progression Bastion (série + statut) de TOUS les districts où ce slot
        /// politique était en série ou détenait le titre — cohérent avec le fait qu'un parti
        /// nouvellement créé n'a aucun historique électoral, contrairement au parti hôte qu'il
        /// remplace visuellement.
        /// </summary>
        private void ResetBastionProgressForParty(PoliticalParty party)
        {
            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    var districtData = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    bool touched = false;

                    if (districtData.m_StreakCount > 0 && districtData.m_StreakParty == party)
                    {
                        districtData.m_StreakCount = 0;
                        districtData.m_StreakParty = default;
                        touched = true;
                    }

                    if (districtData.m_IsBastion && districtData.m_BastionParty == party)
                    {
                        districtData.m_IsBastion = false;
                        districtData.m_BastionParty = default;
                        touched = true;
                    }

                    if (touched)
                        EntityManager.SetComponentData(d, districtData);
                }
            }
            finally
            {
                districts.Dispose();
            }
        }

        // Pas de logique per-frame nécessaire, ce système est purement passif
        // (même remarque que CouncilPolicyRegistry).
        protected override void OnUpdate() { }
    }
}