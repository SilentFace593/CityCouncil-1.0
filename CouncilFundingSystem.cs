using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;
using static CityCouncil.CouncilDistrictData;

namespace CityCouncil
{
    /// <summary>
    /// Gère le financement public de la vie politique : une part fixe paramétrable par le
    /// joueur (verrouillée une fois validée) et une part variable (1000 crédits/siège détenu),
    /// versées aux 5 partis depuis les caisses de la ville (Game.City.PlayerMoney), sur le
    /// même rythme périodique ville entière que CouncilPartyMembershipSystem (7 jours in-game).
    /// Compteur de cycle DUPLIQUÉ (pas partagé avec CouncilPartyMembershipSystem) — même choix
    /// de découplage volontaire que documenté ailleurs dans le mod (cf. GetCityLeadingParty
    /// dans CouncilElectionSystem).
    /// </summary>
    public partial class CouncilFundingSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        public const int MaxFixedAmount = 100000;
        private const int CreditsPerSeat = 1000;

        private EntityQuery m_SingletonQuery;
        private EntityQuery m_DistrictQuery;
        private EntityQuery m_PlayerMoneyQuery;
        private SimulationSystem m_SimulationSystem;
        private CouncilPartyMembershipSystem m_MembershipSystem;

        private Entity m_SingletonEntity = Entity.Null;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilFundingData>());
            m_DistrictQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilDistrictData>());

            // NOTE : nom de composant/champ NON confirmé par décompilation. Game.City.PlayerMoney
            // est supposé être un IComponentData singleton portant un champ de type numérique pour
            // le trésor de la ville (ici supposé "m_Money"). Si le SDK expose un nom différent
            // (ex. m_Balance, ou une méthode dédiée type CityMoneySystem.TrySpend), adapter
            // TrySpendCityMoney ci-dessous en conséquence — le reste du système n'en dépend pas.
            m_PlayerMoneyQuery = GetEntityQuery(ComponentType.ReadWrite<Game.City.PlayerMoney>());

            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_MembershipSystem = World.GetOrCreateSystemManaged<CouncilPartyMembershipSystem>();
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

        public override int GetUpdateInterval(SystemUpdatePhase phase) => 4096;

        protected override void OnUpdate() { }

        private void EnsureSingleton()
        {
            var existing = m_SingletonQuery.ToEntityArray(Allocator.Temp);
            try
            {
                if (existing.Length == 1)
                {
                    m_SingletonEntity = existing[0];
                    return;
                }

                if (existing.Length > 1)
                {
                    // Même garde-fou que les autres singletons du mod : on garde la première
                    // entité verrouillée (signe de données réellement restaurées), sinon la 1ère.
                    Entity keep = existing[0];
                    foreach (var e in existing)
                    {
                        var data = EntityManager.GetComponentData<CouncilFundingData>(e);
                        if (data.m_FixedAmountLocked) { keep = e; break; }
                    }
                    foreach (var e in existing)
                    {
                        if (e != keep)
                        {
                            s_Log.Warn($"[CouncilFundingSystem] Entité singleton en doublon détruite : {e}");
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
            EntityManager.AddComponentData(m_SingletonEntity, new CouncilFundingData
            {
                m_FixedAmount = 0,
                m_FixedAmountLocked = false
            });
            s_Log.Info("[CouncilFundingSystem] Entité singleton créée (aucune trouvée).");
        }

        public CouncilFundingData GetData()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            return EntityManager.GetComponentData<CouncilFundingData>(m_SingletonEntity);
        }

        private void SetData(CouncilFundingData data)
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            EntityManager.SetComponentData(m_SingletonEntity, data);
        }

        /// <summary>Modifie le montant de la part fixe. Refusé si déjà verrouillé ou hors bornes.</summary>
        public bool TrySetFixedAmount(int amount, out string error)
        {
            error = null;
            var data = GetData();

            if (data.m_FixedAmountLocked)
            {
                error = "Montant verrouillé jusqu'à la prochaine distribution.";
                return false;
            }

            if (amount < 0 || amount > MaxFixedAmount)
            {
                error = $"Montant hors bornes (0 à {MaxFixedAmount}).";
                return false;
            }

            data.m_FixedAmount = amount;
            SetData(data);
            return true;
        }

        /// <summary>Verrouille le montant actuel jusqu'à la prochaine distribution.</summary>
        public void ValidateFixedAmount()
        {
            var data = GetData();
            if (data.m_FixedAmountLocked) return;

            data.m_FixedAmountLocked = true;
            data.m_FixedAmountPendingDistribution = true; // AJOUT
            SetData(data);
            s_Log.Info($"[CouncilFundingSystem] Part fixe verrouillée à {data.m_FixedAmount} crédits, distribution en attente.");
        }

        /// <summary>
        /// Appelé par CouncilElectionSystem.FinalizeResults pour CHAQUE district qui termine son
        /// scrutin (1er tour gagné ou 2e tour) : verse la part variable (1000/siège obtenu dans CE
        /// district), et si une part fixe est en attente de distribution, la verse une seule fois
        /// (répartie entre les 5 partis) puis lève le flag pending (le montant reste verrouillé —
        /// NON, il se déverrouille : cf. ci-dessous — jusqu'à ce que le joueur en resaisisse un
        /// pour le cycle suivant).
        /// </summary>
        public void DistributeForFinalizedDistrict(IEnumerable<PartyResult> finalResults)
        {
            var funding = GetData();
            bool fundingChanged = false;

            // --- Part fixe : versée une seule fois, au premier district qui termine après validation ---
            if (funding.m_FixedAmountPendingDistribution)
            {
                int fixedPerParty = funding.m_FixedAmount / 5;
                if (fixedPerParty > 0 && TrySpendCityMoney(fixedPerParty * 5))
                {
                    foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty)))
                        m_MembershipSystem.AddTreasury(p, fixedPerParty, CouncilPartyMembershipSystem.TreasurySource.CityFunding);

                    s_Log.Info($"[CouncilFundingSystem] Part fixe distribuée : {fixedPerParty * 5} crédits ({fixedPerParty}/parti).");
                }
                else if (fixedPerParty > 0)
                {
                    s_Log.Warn("[CouncilFundingSystem] Trésor municipal insuffisant pour la part fixe, distribution annulée.");
                }

                funding.m_FixedAmountPendingDistribution = false;
                funding.m_FixedAmountLocked = false; // redevient modifiable pour le prochain cycle
                fundingChanged = true;
            }

            if (fundingChanged) SetData(funding);

            // --- Part variable : versée à chaque district finalisé, sur les sièges de CE district ---
            int variableCost = 0;
            var seatsByParty = new Dictionary<PoliticalParty, int>();
            foreach (var r in finalResults)
            {
                if (r.m_Seats <= 0) continue;
                seatsByParty[r.m_Party] = r.m_Seats;
                variableCost += r.m_Seats * CreditsPerSeat;
            }

            if (variableCost > 0 && TrySpendCityMoney(variableCost))
            {
                foreach (var kv in seatsByParty)
                    m_MembershipSystem.AddTreasury(kv.Key, kv.Value * CreditsPerSeat, CouncilPartyMembershipSystem.TreasurySource.CityFunding);
            }
            else if (variableCost > 0)
            {
                s_Log.Warn($"[CouncilFundingSystem] Trésor municipal insuffisant pour la part variable ({variableCost}), distribution annulée.");
            }
        }

        /// <summary>
        /// Débite le trésor municipal. Retourne false (sans rien débiter) si les fonds sont
        /// insuffisants ou si le composant PlayerMoney est introuvable.
        /// PlayerMoney.m_Money est privé (seul le getter "money" et le constructeur PlayerMoney(int)
        /// sont publics) : impossible de faire +=/-= directement. On reconstruit donc une nouvelle
        /// struct via le constructeur, en reportant m_Unlimited (public) pour ne pas désactiver
        /// l'argent illimité si le joueur l'a activé.
        /// </summary>
        private bool TrySpendCityMoney(int amount)
        {
            if (amount <= 0) return true;
            if (m_PlayerMoneyQuery.IsEmptyIgnoreFilter) return false;

            var entity = m_PlayerMoneyQuery.GetSingletonEntity();
            var money = EntityManager.GetComponentData<Game.City.PlayerMoney>(entity);

            if (!money.m_Unlimited && money.money < amount) return false;

            int updated = money.m_Unlimited ? money.money : money.money - amount;
            var newMoney = new Game.City.PlayerMoney(updated) { m_Unlimited = money.m_Unlimited };
            EntityManager.SetComponentData(entity, newMoney);
            return true;
        }
    }
}