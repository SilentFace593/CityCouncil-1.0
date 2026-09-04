using System.Collections.Generic;
using System.Linq;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

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
        private EntityQuery m_DistrictQueryForCampaigns;
        private CouncilBlackFundSystem m_BlackFundSystem;
        private CouncilBonusSystem m_BonusSystem;
        private CouncilScoreSystem m_ScoreSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilPropagandaData>());
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_MembershipSystem = World.GetOrCreateSystemManaged<CouncilPartyMembershipSystem>();
            m_CustomPartySystem = World.GetOrCreateSystemManaged<CouncilCustomPartySystem>();
            m_DistrictQueryForCampaigns = GetEntityQuery(ComponentType.ReadWrite<CouncilDistrictData>());
            m_BlackFundSystem = World.GetOrCreateSystemManaged<CouncilBlackFundSystem>();
            m_BonusSystem = World.GetOrCreateSystemManaged<CouncilBonusSystem>();
            m_BonusSystem = World.GetOrCreateSystemManaged<CouncilBonusSystem>();
            m_ScoreSystem = World.GetOrCreateSystemManaged<CouncilScoreSystem>();
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
            ExpireDistrictCampaignsIfNeeded();
            ExpireIllegalCampaignsIfNeeded(); // AJOUT
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

            var aggressionTiers = GetAggressionTiers();

            foreach (var kv in aggressionTiers)
                s_Log.Info($"[CouncilPropagandaSystem][IA-Agressivité] {kv.Key} -> {kv.Value}");

            foreach (PoliticalParty party in System.Enum.GetValues(typeof(PoliticalParty)))
            {
                if (playerSpace.HasValue && party == playerSpace.Value) continue;

                bool alreadyActive = false;
                foreach (var e in propagandaData.m_Entries)
                    if (e.m_Party == party && e.m_Active) { alreadyActive = true; break; }

                int treasury = 0;
                foreach (var m in membershipData.m_Entries)
                    if (m.m_Party == party) { treasury = m.m_Treasury; break; }

                aggressionTiers.TryGetValue(party, out var tier);

                if (!alreadyActive)
                    TryAiLaunchCampaign(party, treasury, tier);

                TryAiLaunchDistrictCampaign(party, treasury, tier);
                TryAiLaunchIllegalCampaign(party, treasury);
            }
        }

        // Palier d'agressivité IA dérivé du classement général (CouncilScoreSystem).
        // Recalculé une fois par cycle IA (pas par district), pour éviter les incohérences si
        // le score change entre deux appels de TryAiLaunch* au sein du même cycle.
        private enum AiAggressionTier { Last, Normal, First }

        private System.Collections.Generic.Dictionary<PoliticalParty, AiAggressionTier> GetAggressionTiers()
        {
            var totals = m_ScoreSystem.GetTotalScores();
            var tiers = new System.Collections.Generic.Dictionary<PoliticalParty, AiAggressionTier>();
            if (totals.Count == 0) return tiers;

            var ordered = totals.OrderByDescending(kv => kv.Value).ToList();
            var firstParty = ordered.First().Key;
            var lastParty = ordered.Last().Key;

            foreach (var kv in totals)
            {
                tiers[kv.Key] = (kv.Key == lastParty && lastParty != firstParty) ? AiAggressionTier.Last
                               : kv.Key == firstParty ? AiAggressionTier.First
                               : AiAggressionTier.Normal;
            }
            return tiers;
        }

        /// <summary>
        /// Heuristique IA pour les campagnes de district : chance fixe par cycle, plafonnée par le
        /// budget et par le plafond de 3 campagnes actives. 50% renforce un district déjà détenu
        /// (Boost), 50% attaque le leader d'un district où l'IA n'est pas en tête (70% propre / 30% sale).
        /// </summary>
        private void TryAiLaunchDistrictCampaign(PoliticalParty party, int treasury, AiAggressionTier tier) 
        {

            float launchChance = tier switch
            {
                AiAggressionTier.Last => 0.50f,
                AiAggressionTier.First => 0.10f,
                _ => 0.25f
            };
            if (m_AiRng.NextDouble() >= launchChance) return;
            if (CountActiveDistrictCampaignsForParty(party) >= GetMaxDistrictCampaignsForParty(party)) return;

            var districts = m_DistrictQueryForCampaigns.ToEntityArray(Allocator.Temp);
            try
            {
                var ownedDistricts = new List<Entity>();
                var contestedDistricts = new List<(Entity district, PoliticalParty leader)>();

                foreach (var d in districts)
                {
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    if (data.m_Phase != ElectionPhase.Completed) continue;

                    if (data.m_LeadingParty == party) ownedDistricts.Add(d);
                    else contestedDistricts.Add((d, data.m_LeadingParty));
                }

                // Un dernier attaque en priorité (20% boost / 80% attaque), un premier
                // reste sur du renforcement défensif (85% boost / 15% attaque).
                float boostChance = tier switch
                {
                    AiAggressionTier.Last => 0.20f,
                    AiAggressionTier.First => 0.85f,
                    _ => 0.50f
                };
                bool wantsBoost = m_AiRng.NextDouble() < boostChance;

                if (wantsBoost && ownedDistricts.Count > 0)
                {
                    var target = ownedDistricts[m_AiRng.Next(ownedDistricts.Count)];

                    // Budget élargi pour un dernier, plafonné à Petite pour un premier.
                    float budgetRatio = tier == AiAggressionTier.Last ? 0.80f : 0.50f;
                    int budget = (int)(treasury * budgetRatio);

                    CampaignIntensity? affordableTier = tier == AiAggressionTier.First
                        ? (DistrictCampaignCatalog.BoostTiers[CampaignIntensity.Petite].cost <= budget ? CampaignIntensity.Petite : (CampaignIntensity?)null)
                        : CheapestAffordableBoostTierFromBudget(budget);

                    if (affordableTier.HasValue)
                        TryLaunchDistrictCampaign(target, party, DistrictCampaignType.Boost, default, affordableTier.Value, out _);
                }
                else if (contestedDistricts.Count > 0)
                {
                    var (target, leader) = contestedDistricts[m_AiRng.Next(contestedDistricts.Count)];

                    // Un dernier privilégie largement le sale (60% au lieu de 30%),
                    // un premier ne se salit jamais les mains (0%).
                    float dirtyChance = tier switch
                    {
                        AiAggressionTier.Last => 0.60f,
                        AiAggressionTier.First => 0f,
                        _ => 0.30f
                    };
                    bool dirty = m_AiRng.NextDouble() < dirtyChance;
                    int cost = dirty ? DistrictCampaignCatalog.AttackDirtyCost : DistrictCampaignCatalog.AttackCleanCost;

                    float budgetRatio = tier == AiAggressionTier.Last ? 0.80f : 0.50f;
                    if ((int)(treasury * budgetRatio) >= cost)
                        TryLaunchDistrictCampaign(target, party,
                            dirty ? DistrictCampaignType.AttackDirty : DistrictCampaignType.AttackClean,
                            leader, CampaignIntensity.Petite, out _);
                }
            }
            finally { districts.Dispose(); }
        }

        // Variante de CheapestAffordableBoostTier acceptant un budget déjà calculé
        // (au lieu de recalculer treasury/2 en interne), nécessaire pour le budget élargi du palier Last.
        private CampaignIntensity? CheapestAffordableBoostTierFromBudget(int budget)
        {
            foreach (var tier in new[] { CampaignIntensity.Forte, CampaignIntensity.Moyenne, CampaignIntensity.Petite })
                if (DistrictCampaignCatalog.BoostTiers[tier].cost <= budget) return tier;
            return null;
        }

        /// <summary>
        /// Heuristique IA simplifiée pour la campagne illégale (point 9) : chance fixe, plafonnée par
        /// budget et par le plafond de 3, cible le leader d'un district contesté. Pas de caisse noire
        /// pour l'IA : financement direct depuis la trésorerie officielle (fromBlackFund=false).
        /// </summary>
        private void TryAiLaunchIllegalCampaign(PoliticalParty party, int treasury)
        {
            const float LaunchChance = 0.15f;
            if (m_AiRng.NextDouble() >= LaunchChance) return;

            int cost = GetIllegalCampaignCost(party);
            if (treasury < cost) return;
            if (CountActiveIllegalCampaignsForParty(party) >= IllegalCampaignCatalog.MaxActiveCampaignsPerParty) return;


            var districts = m_DistrictQueryForCampaigns.ToEntityArray(Allocator.Temp);
            try
            {
                var contestedDistricts = new List<(Entity district, PoliticalParty leader)>();
                foreach (var d in districts)
                {
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    if (data.m_Phase != ElectionPhase.Completed) continue;
                    if (data.m_LeadingParty != party) contestedDistricts.Add((d, data.m_LeadingParty));
                }
                if (contestedDistricts.Count == 0) return;

                var (target, leader) = contestedDistricts[m_AiRng.Next(contestedDistricts.Count)];
                TryLaunchIllegalDistrictCampaign(target, party, leader, fromBlackFund: false, out _);
            }
            finally { districts.Dispose(); }
        }


        private int GetMaxDistrictCampaignsForParty(PoliticalParty party)
        {
            if (party == PoliticalParty.GaucheRadicale && m_BonusSystem.IsRadicalLeftUniversityBonusActive())
                return DistrictCampaignCatalog.MaxActiveCampaignsPerPartyWithUniversityBonus;
            return DistrictCampaignCatalog.MaxActiveCampaignsPerParty;
        }


        private CampaignIntensity? CheapestAffordableBoostTier(int treasury)
        {
            int budget = treasury / 2;
            foreach (var tier in new[] { CampaignIntensity.Forte, CampaignIntensity.Moyenne, CampaignIntensity.Petite })
                if (DistrictCampaignCatalog.BoostTiers[tier].cost <= budget) return tier;
            return null;
        }


        /// <summary>
        /// Décide si et comment un parti IA lance une campagne. Probabilité de déclenchement fixe
        /// (indépendante de la richesse — un petit parti peut aussi vouloir se relancer), mais
        /// l'intensité choisie est plafonnée par ce que le parti peut se permettre (max ~50% de sa
        /// trésorerie disponible, pour éviter qu'il se ruine à chaque cycle).
        /// </summary>
        private void TryAiLaunchCampaign(PoliticalParty party, int treasury, AiAggressionTier tier)
        {
            // Chance de lancement modulée par le classement : un parti dernier tente sa
            // chance bien plus souvent (quasi systématique), un parti premier se montre prudent.
            float launchChance = tier switch
            {
                AiAggressionTier.Last => 0.75f,
                AiAggressionTier.First => 0.15f,
                _ => 0.35f
            };
            if (m_AiRng.NextDouble() >= launchChance) return;

            // Un parti dernier engage jusqu'à 80% de sa trésorerie (au lieu de 50%),
            // un parti premier reste prudent (50% comme avant).
            float budgetRatio = tier == AiAggressionTier.Last ? 0.80f : 0.50f;
            int budget = (int)(treasury * budgetRatio);

            // Un parti premier ne dépasse jamais l'intensité Petite (campagne symbolique).
            var candidateTiers = tier == AiAggressionTier.First
                ? new[] { CampaignIntensity.Petite }
                : new[] { CampaignIntensity.Forte, CampaignIntensity.Moyenne, CampaignIntensity.Petite };

            CampaignIntensity? affordable = null;
            foreach (var t in candidateTiers)
            {
                if (CampaignCatalog.Tiers[t].cost <= budget) { affordable = t; break; }
            }
            if (!affordable.HasValue) return;

            // Un parti dernier cible systématiquement les Adultes, ville entière, en
            // cohérence avec la demande ("forte campagne chez les adultes dans toute la ville").
            // Les autres cas gardent le tirage 50/50 existant.
            var target = tier == AiAggressionTier.Last
                ? CampaignTarget.Adultes
                : (m_AiRng.NextDouble() < 0.5 ? CampaignTarget.Adultes : CampaignTarget.Seniors);

            if (TryLaunchCampaign(party, target, affordable.Value, autoRenew: false, out _))
                s_Log.Info($"[CouncilPropagandaSystem] IA ({tier}) : {party} lance une campagne {affordable.Value} ciblant {target}.");
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

        /// <summary>Coût effectif d'une campagne illégale pour un parti, après bonus éventuel.</summary>
        public int GetIllegalCampaignCost(PoliticalParty party)
        {
            int baseCost = IllegalCampaignCatalog.Cost;
            if (party == PoliticalParty.Populiste && m_BonusSystem.IsPopulistPrisonBonusActive())
                return Mathf.RoundToInt(baseCost * IllegalCampaignCatalog.PopulistBonusCostMultiplier);
            return baseCost;
        }

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

        /// <summary>
        /// Lance la campagne digitale exclusive au Démocrate (bonus débloqué par le Liaison Satellite +
        /// 3 victoires générales consécutives, cf. CouncilBonusSystem.IsDemocratDigitalBonusActive).
        /// Coût fixe de 90000, bonus tiré ALÉATOIREMENT entre 3% et 7% au lancement (fixe pour la durée
        /// de la campagne), appliqué symétriquement aux deux tranches d'âge (CampaignTarget.Toute).
        /// Partage le même slot qu'une campagne classique : un parti ne peut pas cumuler une campagne
        /// normale ET la campagne digitale en même temps (même garde-fou "déjà active" que TryLaunchCampaign).
        /// </summary>
        public bool TryLaunchDigitalCampaign(PoliticalParty party, bool autoRenew, out string error)
        {
            error = null;

            if (party != PoliticalParty.Democrate)
            {
                error = "Campagne digitale réservée au Démocrate.";
                return false;
            }

            if (!m_BonusSystem.IsDemocratDigitalBonusActive())
            {
                error = "Bonus digital non débloqué (Liaison Satellite + 3 victoires générales requises).";
                return false;
            }

            var data = GetData();
            foreach (var e in data.m_Entries)
            {
                if (e.m_Party == party && e.m_Active)
                {
                    error = "Une campagne est déjà en cours pour ce parti.";
                    return false;
                }
            }

            if (!m_MembershipSystem.TrySpendTreasury(party, CampaignCatalog.DigitalCampaignCost))
            {
                error = "Réserves insuffisantes.";
                return false;
            }

            float roll = CampaignCatalog.DigitalCampaignBonusMin
                + (float)m_AiRng.NextDouble() * (CampaignCatalog.DigitalCampaignBonusMax - CampaignCatalog.DigitalCampaignBonusMin);

            data = GetData(); // relu après TrySpendTreasury
            var entries = data.m_Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].m_Party != party) continue;
                entries[i] = new CampaignEntry
                {
                    m_Party = party,
                    m_Active = true,
                    m_Target = CampaignTarget.Toute,
                    m_Intensity = CampaignIntensity.Forte, // valeur arbitraire, non utilisée pour le coût (m_IsDigital prime)
                    m_BonusPercent = roll,
                    m_ExpiryDay = CurrentDay() + CampaignCatalog.CampaignDurationDays,
                    m_AutoRenew = autoRenew,
                    m_IsDigital = true
                };
                break;
            }
            data.m_Entries = entries;
            SetData(data);

            s_Log.Info($"[CouncilPropagandaSystem] Campagne DIGITALE lancée : {party}, bonus tiré +{roll:P1} (coût {CampaignCatalog.DigitalCampaignCost}, autoRenew={autoRenew}).");
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
                bool wasDigital = entry.m_IsDigital;

                entry.m_Active = false;
                entries[i] = entry;
                changed = true;
                s_Log.Info($"[CouncilPropagandaSystem] Campagne de {party} expirée.");

                if (wantsRenew)
                {
                    data.m_Entries = entries;
                    SetData(data);

                    bool renewed;
                    string renewError;
                    if (wasDigital) 
                        renewed = TryLaunchDigitalCampaign(party, autoRenew: true, out renewError);
                    else
                        renewed = TryLaunchCampaign(party, target, intensity, autoRenew: true, out renewError);

                    if (renewed)
                        s_Log.Info($"[CouncilPropagandaSystem] Campagne de {party} reconduite automatiquement.");
                    else
                        s_Log.Info($"[CouncilPropagandaSystem] Reconduction automatique de {party} annulée : {renewError}");

                    data = GetData();
                    entries = data.m_Entries;
                }
            }

            if (changed)
            {
                data.m_Entries = entries;
                SetData(data);
            }
        }

        /// <summary>
        /// Lance une campagne de district. Refuse si le parti a déjà atteint le plafond de 3
        /// campagnes de district actives simultanément (tous districts confondus), ou si les
        /// réserves sont insuffisantes. Pour une attaque sale, le malus auto-infligé est tiré
        /// UNE SEULE FOIS ici et reste fixe pour toute la durée de la campagne (cf. remarque design).
        /// </summary>
        public bool TryLaunchDistrictCampaign(
    Entity districtEntity, PoliticalParty party, DistrictCampaignType type,
    PoliticalParty targetParty, CampaignIntensity boostTier, out string error)
        {
            error = null;

            if (!EntityManager.HasComponent<CouncilDistrictData>(districtEntity))
            {
                error = "District invalide.";
                return false;
            }

            int activeCount = CountActiveDistrictCampaignsForParty(party);
            int maxAllowed = GetMaxDistrictCampaignsForParty(party);
            if (activeCount >= maxAllowed) 
            {
                error = $"Nombre maximum de campagnes de district atteint ({maxAllowed}).";
                return false;
            }

            var data = EntityManager.GetComponentData<CouncilDistrictData>(districtEntity);
            double currentDay = CurrentDay();

            // Un seul type de campagne de district actif par parti PAR district (pas de cumul de
            // deux campagnes du même parti sur le même district).
            foreach (var c in data.m_DistrictCampaigns)
            {
                if (c.m_Party == party && currentDay < c.m_ExpiryDay)
                {
                    error = "Une campagne de ce parti est déjà active dans ce district.";
                    return false;
                }
            }

            int cost;
            float bonusOrMalus;
            float selfMalus = 0f;

            switch (type)
            {
                case DistrictCampaignType.Boost:
                    (cost, bonusOrMalus) = DistrictCampaignCatalog.BoostTiers[boostTier];
                    break;
                case DistrictCampaignType.AttackClean:
                    cost = DistrictCampaignCatalog.AttackCleanCost;
                    bonusOrMalus = DistrictCampaignCatalog.AttackCleanMalus;
                    break;
                case DistrictCampaignType.AttackDirty:
                    cost = DistrictCampaignCatalog.AttackDirtyCost;
                    bonusOrMalus = DistrictCampaignCatalog.AttackDirtyMalus;
                    selfMalus = (float)m_AiRng.NextDouble() * DistrictCampaignCatalog.AttackDirtySelfMalusMax; // tiré une fois
                    break;
                default:
                    error = "Type de campagne invalide.";
                    return false;
            }

            if (!m_MembershipSystem.TrySpendTreasury(party, cost))
            {
                error = "Réserves insuffisantes.";
                return false;
            }

            data = EntityManager.GetComponentData<CouncilDistrictData>(districtEntity); // relu après débit
            data.m_DistrictCampaigns.Add(new DistrictCampaignEntry
            {
                m_Party = party,
                m_Type = type,
                m_TargetParty = (type == DistrictCampaignType.Boost) ? default : targetParty,
                m_BonusPercent = bonusOrMalus,
                m_SelfMalusPercent = selfMalus,
                m_ExpiryDay = currentDay + DistrictCampaignCatalog.DistrictCampaignDurationDays
            });
            EntityManager.SetComponentData(districtEntity, data);

            s_Log.Info($"[CouncilPropagandaSystem] Campagne de district lancée : {party} ({type}) sur district {districtEntity.Index}" +
                       (type != DistrictCampaignType.Boost ? $", cible {targetParty}" : "") + $", coût {cost}.");
            return true;
        }

        private int CountActiveDistrictCampaignsForParty(PoliticalParty party)
        {
            int count = 0;
            double currentDay = CurrentDay();
            var districts = m_DistrictQueryForCampaigns.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    foreach (var c in data.m_DistrictCampaigns)
                        if (c.m_Party == party && currentDay < c.m_ExpiryDay) count++;
                }
            }
            finally { districts.Dispose(); }
            return count;
        }

        /// <summary>Purge les campagnes de district expirées, tous districts confondus.</summary>
        private void ExpireDistrictCampaignsIfNeeded()
        {
            double currentDay = CurrentDay();
            var districts = m_DistrictQueryForCampaigns.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    bool changed = false;
                    var kept = new FixedList64Bytes<DistrictCampaignEntry>();
                    foreach (var c in data.m_DistrictCampaigns)
                    {
                        if (currentDay < c.m_ExpiryDay) kept.Add(c);
                        else changed = true;
                    }
                    if (changed)
                    {
                        data.m_DistrictCampaigns = kept;
                        EntityManager.SetComponentData(d, data);
                    }
                }
            }
            finally { districts.Dispose(); }
        }

        /// <summary>Campagnes de district actives pour UN district donné (utilisé par VoteCalculator via CouncilElectionSystem).</summary>
        public List<DistrictCampaignEntry> GetActiveDistrictCampaigns(Entity districtEntity)
        {
            var result = new List<DistrictCampaignEntry>();
            if (!EntityManager.HasComponent<CouncilDistrictData>(districtEntity)) return result;

            double currentDay = CurrentDay();
            var data = EntityManager.GetComponentData<CouncilDistrictData>(districtEntity);
            foreach (var c in data.m_DistrictCampaigns)
                if (currentDay < c.m_ExpiryDay) result.Add(c);
            return result;
        }

        /// <summary>Annule une campagne de district (sans remboursement), même pattern que TryCancelCampaign.</summary>
        public bool TryCancelDistrictCampaign(Entity districtEntity, PoliticalParty party, out string error)
        {
            error = null;
            if (!EntityManager.HasComponent<CouncilDistrictData>(districtEntity))
            {
                error = "District invalide.";
                return false;
            }

            var data = EntityManager.GetComponentData<CouncilDistrictData>(districtEntity);
            var kept = new FixedList64Bytes<DistrictCampaignEntry>();
            bool found = false;
            foreach (var c in data.m_DistrictCampaigns)
            {
                if (!found && c.m_Party == party) { found = true; continue; }
                kept.Add(c);
            }

            if (!found)
            {
                error = "Aucune campagne active de ce parti dans ce district.";
                return false;
            }

            data.m_DistrictCampaigns = kept;
            EntityManager.SetComponentData(districtEntity, data);
            return true;
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
        /// Lance une campagne illégale de district contre un parti visé. Financée par la caisse noire
        /// (fromBlackFund=true, cas joueur) ou directement par la trésorerie officielle (fromBlackFund=false,
        /// cas IA simplifié, cf. point 9). Le malus est tiré UNE SEULE FOIS ici (0 à 6%) et reste fixe.
        /// </summary>
        public bool TryLaunchIllegalDistrictCampaign(
    Entity districtEntity, PoliticalParty party, PoliticalParty targetParty, bool fromBlackFund, out string error)
        {
            error = null;

            if (!EntityManager.HasComponent<CouncilDistrictData>(districtEntity))
            {
                error = "District invalide.";
                return false;
            }

            if (fromBlackFund && !m_BlackFundSystem.IsActive(party))
            {
                error = "Caisse noire inactive.";
                return false;
            }

            if (CountActiveIllegalCampaignsForParty(party) >= IllegalCampaignCatalog.MaxActiveCampaignsPerParty)
            {
                error = "Nombre maximum de campagnes illégales atteint (3).";
                return false;
            }

            var data = EntityManager.GetComponentData<CouncilDistrictData>(districtEntity);
            double currentDay = CurrentDay();

            foreach (var c in data.m_IllegalCampaigns)
            {
                if (c.m_Party == party && currentDay < c.m_ExpiryDay)
                {
                    error = "Une campagne illégale de ce parti est déjà active dans ce district.";
                    return false;
                }
            }

            int cost = GetIllegalCampaignCost(party); // AJOUT — remplace IllegalCampaignCatalog.Cost

            bool spent = fromBlackFund
                ? m_BlackFundSystem.TrySpendFromBlackFund(party, cost)
                : m_MembershipSystem.TrySpendTreasury(party, cost);

            if (!spent)
            {
                error = fromBlackFund ? "Solde de la caisse noire insuffisant." : "Réserves insuffisantes.";
                return false;
            }

            data = EntityManager.GetComponentData<CouncilDistrictData>(districtEntity);
            float malus = (float)m_AiRng.NextDouble() * IllegalCampaignCatalog.MaxMalus;

            data.m_IllegalCampaigns.Add(new IllegalCampaignEntry
            {
                m_Party = party,
                m_TargetParty = targetParty,
                m_MalusPercent = malus,
                m_ExpiryDay = currentDay + IllegalCampaignCatalog.CampaignDurationDays
            });
            EntityManager.SetComponentData(districtEntity, data);

            s_Log.Info($"[CouncilPropagandaSystem] Campagne ILLÉGALE lancée : {party} contre {targetParty} " +
                       $"sur district {districtEntity.Index}, malus {malus:P1}, coût {cost}, financement {(fromBlackFund ? "caisse noire" : "trésorerie")}.");
            return true;
        }

        public int CountActiveIllegalCampaignsForParty(PoliticalParty party)
        {
            int count = 0;
            double currentDay = CurrentDay();
            var districts = m_DistrictQueryForCampaigns.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    foreach (var c in data.m_IllegalCampaigns)
                        if (c.m_Party == party && currentDay < c.m_ExpiryDay) count++;
                }
            }
            finally { districts.Dispose(); }
            return count;
        }

        private void ExpireIllegalCampaignsIfNeeded()
        {
            double currentDay = CurrentDay();
            var districts = m_DistrictQueryForCampaigns.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    bool changed = false;
                    var kept = new FixedList64Bytes<IllegalCampaignEntry>();
                    foreach (var c in data.m_IllegalCampaigns)
                    {
                        if (currentDay < c.m_ExpiryDay) kept.Add(c);
                        else changed = true;
                    }
                    if (changed)
                    {
                        data.m_IllegalCampaigns = kept;
                        EntityManager.SetComponentData(d, data);
                    }
                }
            }
            finally { districts.Dispose(); }
        }

        /// <summary>Campagnes illégales actives pour UN district (utilisé par VoteCalculator).</summary>
        public List<IllegalCampaignEntry> GetActiveIllegalCampaigns(Entity districtEntity)
        {
            var result = new List<IllegalCampaignEntry>();
            if (!EntityManager.HasComponent<CouncilDistrictData>(districtEntity)) return result;

            double currentDay = CurrentDay();
            var data = EntityManager.GetComponentData<CouncilDistrictData>(districtEntity);
            foreach (var c in data.m_IllegalCampaigns)
                if (currentDay < c.m_ExpiryDay) result.Add(c);
            return result;
        }

        /// <summary>Force l'expiration immédiate de TOUTES les campagnes illégales d'un parti (utilisé par la Commission lors d'une fermeture forcée, si besoin).</summary>
        public void ForceExpireIllegalCampaignsForParty(PoliticalParty party)
        {
            var districts = m_DistrictQueryForCampaigns.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    bool changed = false;
                    var kept = new FixedList64Bytes<IllegalCampaignEntry>();
                    foreach (var c in data.m_IllegalCampaigns)
                    {
                        if (c.m_Party == party) changed = true;
                        else kept.Add(c);
                    }
                    if (changed)
                    {
                        data.m_IllegalCampaigns = kept;
                        EntityManager.SetComponentData(d, data);
                    }
                }
            }
            finally { districts.Dispose(); }
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

        /// <summary>
        /// OUTIL DE DEBUG TEMPORAIRE — force l'exécution immédiate du cycle IA de propagande
        /// (campagnes classiques, de district, illégales), sans attendre les 7 jours in-game réels.
        /// Même remarque que CouncilBonusSystem.DebugForceMajorityCheck et
        /// CouncilPartyMembershipSystem.DebugForceCycleCheck : ce système dépend du temps de
        /// simulation réel, jamais avancé par le bouton d'élection accélérée.
        /// </summary>
        public void DebugForceAiCycle()
        {
            m_LastAiCycleDay = CurrentDay() - AiCycleIntervalDays - 0.001;
            RunAiCycleIfNeeded();
            s_Log.Info("[CouncilPropagandaSystem] DEBUG : cycle IA de propagande forcé immédiatement.");
        }

    }
}