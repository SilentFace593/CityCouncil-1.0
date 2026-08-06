using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Simulation;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace CityCouncil
{
    /// <summary>
    /// Gère les campagnes de propagande : lancement (débit de trésorerie + activation),
    /// expiration automatique après CampaignCatalog.CampaignDurationDays. Le bonus est appliqué
    /// par CouncilElectionSystem via GetActiveCampaigns(), lu à chaque 1er tour de district
    /// (effet city-wide : la même liste s'applique à tous les districts).
    /// </summary>
    public partial class CouncilPropagandaSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        private EntityQuery m_SingletonQuery;
        private SimulationSystem m_SimulationSystem;
        private CouncilPartyMembershipSystem m_MembershipSystem;
        private Entity m_SingletonEntity = Entity.Null;
        private const double AiCycleIntervalDays = 7.0; // même rythme que CouncilBonusSystem
        private double m_LastAiCycleDay = -1;
        private readonly System.Random m_AiRng = new System.Random();
        private CouncilCustomPartySystem m_CustomPartySystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilPropagandaData>());
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_MembershipSystem = World.GetOrCreateSystemManaged<CouncilPartyMembershipSystem>();
            m_CustomPartySystem = World.GetOrCreateSystemManaged<CouncilCustomPartySystem>();
        }

        protected override void OnGamePreload(Purpose purpose, Game.GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            DestroyExistingSingleton();
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            EnsureSingleton();
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase) => 4096;

        protected override void OnUpdate()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            ExpireCampaignsIfNeeded();
            RunAiCycleIfNeeded();
        }

        /// <summary>
        /// Cycle périodique IA (même durée qu'un cycle électoral) : chaque parti sans campagne
        /// active et non contrôlé par le joueur a une chance de lancer une campagne, avec une
        /// intensité dépendant de sa trésorerie disponible.
        /// </summary>
        private void RunAiCycleIfNeeded()
        {
            double currentDay = CurrentDay();

            if (m_LastAiCycleDay < 0)
            {
                m_LastAiCycleDay = currentDay;
                return;
            }

            if (currentDay - m_LastAiCycleDay < AiCycleIntervalDays) return;
            m_LastAiCycleDay = currentDay;

            RunAiCycle();
        }

        private void RunAiCycle()
        {
            var custom = m_CustomPartySystem.GetData();
            var playerSpace = (custom.m_Exists && custom.m_SubstitutionActive) ? custom.m_ActiveSpace : (PoliticalParty?)null;

            var propagandaData = GetData();
            var membershipData = m_MembershipSystem.GetData();

            foreach (PoliticalParty party in System.Enum.GetValues(typeof(PoliticalParty)))
            {
                if (playerSpace.HasValue && party == playerSpace.Value) continue; // le joueur garde la main sur son parti

                bool alreadyActive = false;
                foreach (var e in propagandaData.m_Entries)
                {
                    if (e.m_Party == party && e.m_Active) { alreadyActive = true; break; }
                }
                if (alreadyActive) continue;

                int treasury = 0;
                foreach (var m in membershipData.m_Entries)
                {
                    if (m.m_Party == party) { treasury = m.m_Treasury; break; }
                }

                TryAiLaunchCampaign(party, treasury);
            }
        }

        /// <summary>
        /// Décide si et comment un parti IA lance une campagne. Probabilité de déclenchement fixe
        /// (indépendante de la richesse — un petit parti peut aussi vouloir se relancer), mais
        /// l'intensité choisie est plafonnée par ce que le parti peut se permettre (max ~50% de sa
        /// trésorerie disponible, pour éviter qu'il se ruine à chaque cycle).
        /// </summary>
        private void TryAiLaunchCampaign(PoliticalParty party, int treasury)
        {
            const float LaunchChance = 0.35f; // ~1 chance sur 3 par cycle de 7 jours, ajustable librement
            if (m_AiRng.NextDouble() >= LaunchChance) return;

            int budget = treasury / 2;

            CampaignIntensity? affordable = null;
            foreach (var tier in new[] { CampaignIntensity.Forte, CampaignIntensity.Moyenne, CampaignIntensity.Petite })
            {
                if (CampaignCatalog.Tiers[tier].cost <= budget)
                {
                    affordable = tier;
                    break;
                }
            }
            if (!affordable.HasValue) return; // même la petite campagne est hors de portée, on ne lance rien

            var target = m_AiRng.NextDouble() < 0.5 ? CampaignTarget.Adultes : CampaignTarget.Seniors;

            if (TryLaunchCampaign(party, target, affordable.Value, autoRenew: false, out _)) // AJOUT paramètre
                s_Log.Info($"[CouncilPropagandaSystem] IA : {party} lance une campagne {affordable.Value} ciblant {target}.");
        }


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
                        s_Log.Warn($"[CouncilPropagandaSystem] Entité singleton en doublon détruite : {e}");
                        if (e != keep) EntityManager.DestroyEntity(e);
                    }
                    m_SingletonEntity = keep;
                    return;
                }
            }
            finally
            {
                existing.Dispose();
            }

            var initial = new CouncilPropagandaData { m_Entries = new FixedList512Bytes<CampaignEntry>() };
            foreach (PoliticalParty p in System.Enum.GetValues(typeof(PoliticalParty)))
                initial.m_Entries.Add(new CampaignEntry { m_Party = p, m_Active = false });

            m_SingletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(m_SingletonEntity, initial);
            s_Log.Info("[CouncilPropagandaSystem] Entité singleton créée (5 partis, aucune campagne active).");
        }

        public CouncilPropagandaData GetData()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            return EntityManager.GetComponentData<CouncilPropagandaData>(m_SingletonEntity);
        }

        private void SetData(CouncilPropagandaData data)
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            EntityManager.SetComponentData(m_SingletonEntity, data);
        }

        private double CurrentDay() => (double)m_SimulationSystem.frameIndex / 262144.0;

        /// <summary>
        /// Lance (ou remplace, sans remboursement) une campagne pour un parti. Débite la
        /// trésorerie du parti CIBLÉ (celui qui finance sa propre campagne), pas le trésor
        /// municipal — contrairement au financement public (CouncilFundingSystem).
        /// </summary>
        public bool TryLaunchCampaign(PoliticalParty party, CampaignTarget target, CampaignIntensity intensity, bool autoRenew, out string error)
        {
            error = null;

            // AJOUT — empêche d'écraser une campagne déjà active pour ce parti (le joueur payait
            // sans bénéfice réel : le bonus ne se cumule pas, seul le timer d'expiration redémarrait).
            var data = GetData();
            foreach (var e in data.m_Entries)
            {
                if (e.m_Party == party && e.m_Active)
                {
                    error = "Une campagne est déjà en cours pour ce parti.";
                    return false;
                }
            }

            var (cost, bonus) = CampaignCatalog.Tiers[intensity];

            if (!m_MembershipSystem.TrySpendTreasury(party, cost))
            {
                error = "Réserves insuffisantes.";
                return false;
            }

            data = GetData(); // relu après TrySpendTreasury (qui a modifié la trésorerie)
            var entries = data.m_Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].m_Party != party) continue;
                entries[i] = new CampaignEntry
                {
                    m_Party = party,
                    m_Active = true,
                    m_Target = target,
                    m_Intensity = intensity,
                    m_BonusPercent = bonus,
                    m_ExpiryDay = CurrentDay() + CampaignCatalog.CampaignDurationDays,
                    m_AutoRenew = autoRenew
                };
                break;
            }
            data.m_Entries = entries;
            SetData(data);

            s_Log.Info($"[CouncilPropagandaSystem] Campagne lancée : {party}, cible {target}, intensité {intensity} ({cost} crédits, +{bonus:P0}, autoRenew={autoRenew}).");
            return true;
        }

        public bool TryCancelCampaign(PoliticalParty party, out string error)
        {
            error = null;
            var data = GetData();
            var entries = data.m_Entries;
            bool found = false;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].m_Party != party || !entries[i].m_Active) continue;
                var e = entries[i];
                e.m_Active = false;
                e.m_AutoRenew = false;
                entries[i] = e;
                found = true;
                break;
            }

            if (!found)
            {
                error = "Aucune campagne active pour ce parti.";
                return false;
            }

            data.m_Entries = entries;
            SetData(data);
            s_Log.Info($"[CouncilPropagandaSystem] Campagne de {party} annulée manuellement (sans remboursement).");
            return true;
        }

        private void ExpireCampaignsIfNeeded()
        {
            double currentDay = CurrentDay();
            var data = GetData();
            var entries = data.m_Entries;
            bool changed = false;

            for (int i = 0; i < entries.Length; i++)
            {
                if (!entries[i].m_Active) continue;
                if (currentDay < entries[i].m_ExpiryDay) continue;

                var entry = entries[i];
                var party = entry.m_Party;
                var target = entry.m_Target;
                var intensity = entry.m_Intensity;
                bool wantsRenew = entry.m_AutoRenew;

                entry.m_Active = false;
                entries[i] = entry;
                changed = true;
                s_Log.Info($"[CouncilPropagandaSystem] Campagne de {party} expirée.");

                // AJOUT — reconduction automatique : tentée APRÈS avoir désactivé l'ancienne entrée,
                // pour permettre à TryLaunchCampaign de réutiliser normalement ce slot. Si les réserves
                // sont insuffisantes, on n'insiste pas silencieusement — le joueur devra relancer
                // manuellement (le flag autoRenew reste perdu, cohérent avec "sauf si réserve insuffisante").
                if (wantsRenew)
                {
                    data.m_Entries = entries; // commit l'état "inactive" avant de retenter un lancement
                    SetData(data);

                    if (TryLaunchCampaign(party, target, intensity, autoRenew: true, out var renewError))
                    {
                        s_Log.Info($"[CouncilPropagandaSystem] Campagne de {party} reconduite automatiquement.");
                    }
                    else
                    {
                        s_Log.Info($"[CouncilPropagandaSystem] Reconduction automatique de {party} annulée : {renewError}");
                    }

                    data = GetData(); // relu, TryLaunchCampaign peut avoir modifié l'entrée
                    entries = data.m_Entries;
                }
            }

            if (changed)
            {
                data.m_Entries = entries;
                SetData(data);
            }
        }

        /// <summary>Campagnes actives (non expirées), utilisées par CouncilElectionSystem pour chaque 1er tour.</summary>
        public List<(PoliticalParty party, CampaignTarget target, float percent)> GetActiveCampaigns()
        {
            var result = new List<(PoliticalParty, CampaignTarget, float)>();
            var data = GetData();
            foreach (var e in data.m_Entries)
            {
                if (e.m_Active)
                    result.Add((e.m_Party, e.m_Target, e.m_BonusPercent));
            }
            return result;
        }

        /// <summary>
        /// OUTIL DE DEBUG TEMPORAIRE — force l'expiration immédiate de toutes les campagnes actives,
        /// sans attendre les 7 jours in-game réels. Même remarque que sur les autres systèmes
        /// périodiques du mod (Bonus, Membership) : le temps réel n'est jamais avancé par le bouton
        /// d'élection accélérée.
        /// </summary>
        public void DebugExpireAllCampaigns()
        {
            var data = GetData();
            var entries = data.m_Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!entries[i].m_Active) continue;
                var e = entries[i];
                e.m_Active = false;
                entries[i] = e;
            }
            data.m_Entries = entries;
            SetData(data);
            s_Log.Info("[CouncilPropagandaSystem] DEBUG : toutes les campagnes forcées à expiration.");
        }

    }
}