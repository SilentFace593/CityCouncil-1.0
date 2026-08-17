using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Unity.Collections;
using Unity.Entities;

namespace CityCouncil
{
    /// <summary>
    /// Gère la caisse noire des 5 partis : activation/fermeture, transferts vers/depuis la
    /// trésorerie officielle (CouncilPartyMembershipSystem). Système "service" pur, pas de
    /// logique périodique — les mouvements sont dynamiques (déclenchés par le joueur ou l'IA),
    /// contrairement au cycle de cotisation/commission qui reste calé sur 7 jours (cf. Livraison 2).
    /// </summary>
    public partial class CouncilBlackFundSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        private EntityQuery m_SingletonQuery;
        private Entity m_SingletonEntity = Entity.Null;
        private CouncilPartyMembershipSystem m_MembershipSystem;
        private readonly System.Random m_Rng = new System.Random();

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilBlackFundData>());
            m_MembershipSystem = World.GetOrCreateSystemManaged<CouncilPartyMembershipSystem>();
        }

        protected override void OnGamePreload(Purpose purpose, Game.GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            DestroyExistingSingleton();
        }

        /// <summary>
        /// Détruit l'entité singleton AVANT désérialisation d'une nouvelle sauvegarde — même
        /// garde-fou que tous les autres singletons du mod (cf. CouncilPartyMembershipSystem).
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

        protected override void OnUpdate() { } // purement passif, pas de logique per-frame

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
                    Entity keep = existing[0];
                    foreach (var e in existing)
                    {
                        if (e != keep)
                        {
                            s_Log.Warn($"[CouncilBlackFundSystem] Entité singleton en doublon détruite : {e}");
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

            var initial = new CouncilBlackFundData { m_Entries = new FixedList512Bytes<BlackFundEntry>() };
            foreach (PoliticalParty p in System.Enum.GetValues(typeof(PoliticalParty)))
                initial.m_Entries.Add(new BlackFundEntry { m_Party = p, m_Active = false, m_Balance = 0 });

            m_SingletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(m_SingletonEntity, initial);
            s_Log.Info("[CouncilBlackFundSystem] Entité singleton créée (5 partis, caisses noires inactives).");
        }

        public CouncilBlackFundData GetData()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            return EntityManager.GetComponentData<CouncilBlackFundData>(m_SingletonEntity);
        }

        private void SetData(CouncilBlackFundData data)
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            EntityManager.SetComponentData(m_SingletonEntity, data);
        }

        public bool IsActive(PoliticalParty party)
        {
            foreach (var e in GetData().m_Entries)
                if (e.m_Party == party) return e.m_Active;
            return false;
        }

        public int GetBalance(PoliticalParty party)
        {
            foreach (var e in GetData().m_Entries)
                if (e.m_Party == party) return e.m_Balance;
            return 0;
        }

        /// <summary>Active la caisse noire d'un parti. Idempotent si déjà active.</summary>
        public void Activate(PoliticalParty party)
        {
            var data = GetData();
            var entries = data.m_Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].m_Party != party) continue;
                if (entries[i].m_Active) return; // déjà active, rien à faire
                var e = entries[i];
                e.m_Active = true;
                entries[i] = e;
                break;
            }
            data.m_Entries = entries;
            SetData(data);
            s_Log.Info($"[CouncilBlackFundSystem] Caisse noire activée pour {party}.");
        }

        /// <summary>
        /// Ferme la caisse noire d'un parti : l'argent qu'elle contient est PERDU (ne repart pas
        /// vers la trésorerie officielle — cf. énoncé). Idempotent si déjà inactive.
        /// </summary>
        public void Close(PoliticalParty party)
        {
            var data = GetData();
            var entries = data.m_Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].m_Party != party) continue;
                if (!entries[i].m_Active) return;
                var e = entries[i];
                int lostAmount = e.m_Balance;
                e.m_Active = false;
                e.m_Balance = 0;
                entries[i] = e;

                if (lostAmount > 0)
                    s_Log.Info($"[CouncilBlackFundSystem] Caisse noire de {party} fermée, {lostAmount} crédits perdus.");
                else
                    s_Log.Info($"[CouncilBlackFundSystem] Caisse noire de {party} fermée (déjà vide).");
                break;
            }
            data.m_Entries = entries;
            SetData(data);
        }

        /// <summary>
        /// Transfère un montant entre la trésorerie officielle et la caisse noire d'un parti,
        /// dans le sens indiqué. Refuse si la caisse noire n'est pas active, si le montant est
        /// invalide, ou si le solde source est insuffisant. Retourne le libellé de fausse
        /// facture tiré aléatoirement pour ce mouvement (cf. point 10), à null si échec.
        /// </summary>
        public bool TryTransfer(PoliticalParty party, int amount, bool toBlackFund, out string invoiceLocaleKey, out string error)
        {
            invoiceLocaleKey = null;
            error = null;

            if (amount <= 0)
            {
                error = "Montant invalide.";
                return false;
            }

            if (!IsActive(party))
            {
                error = "Caisse noire inactive.";
                return false;
            }

            var data = GetData();
            var entries = data.m_Entries;
            int index = -1;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].m_Party == party) { index = i; break; }

            if (index < 0)
            {
                error = "Parti introuvable.";
                return false;
            }

            if (toBlackFund)
            {
                // Compte principal -> caisse noire : débite via TrySpendTreasury (source de vérité
                // unique déjà en place côté CouncilPartyMembershipSystem).
                if (!m_MembershipSystem.TrySpendTreasury(party, amount))
                {
                    error = "Réserves du parti insuffisantes.";
                    return false;
                }

                var e = entries[index];
                e.m_Balance += amount;
                entries[index] = e;
            }
            else
            {
                // Caisse noire -> compte principal.
                var e = entries[index];
                if (e.m_Balance < amount)
                {
                    error = "Solde de la caisse noire insuffisant.";
                    return false;
                }
                e.m_Balance -= amount;
                entries[index] = e;

                m_MembershipSystem.AddTreasury(party, amount, CouncilPartyMembershipSystem.TreasurySource.CityFunding);
                // NOTE — TreasurySource.CityFunding utilisé ici par défaut car un retrait de caisse
                // noire n'a pas vocation à polluer les compteurs "Dues"/"CityFunding" du breakdown
                // officiel (cf. TreasuryBreakdown.tsx) ; à réévaluer en Livraison 3 si vous voulez
                // une source dédiée "BlackFund" visible dans ce détail.
            }

            data.m_Entries = entries;
            SetData(data);

            invoiceLocaleKey = BlackFundInvoiceCatalog.InvoiceLocaleKeys[m_Rng.Next(BlackFundInvoiceCatalog.InvoiceLocaleKeys.Length)];

            s_Log.Info($"[CouncilBlackFundSystem] Transfert {(toBlackFund ? "vers" : "depuis")} caisse noire : {party}, {amount} crédits.");
            return true;
        }

        /// <summary>
        /// Débite la caisse noire pour financer une campagne illégale (cf. Livraison 2). Retourne
        /// false sans rien débiter si la caisse n'est pas active ou si le solde est insuffisant.
        /// </summary>
        public bool TrySpendFromBlackFund(PoliticalParty party, int amount)
        {
            if (amount <= 0) return true;
            if (!IsActive(party)) return false;

            var data = GetData();
            var entries = data.m_Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].m_Party != party) continue;
                if (entries[i].m_Balance < amount) return false;

                var e = entries[i];
                e.m_Balance -= amount;
                entries[i] = e;

                data.m_Entries = entries;
                SetData(data);
                return true;
            }
            return false;
        }
    }
}