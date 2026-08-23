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
    /// Gère la commande de sondages d'opinion : coût fixe débité de la trésorerie du parti
    /// joueur, interdiction de la veille du 1er tour jusqu'à l'issue du 2e tour (n'importe quel
    /// district en bloque la commande, cohérent avec un sondage "national"), et calcul du
    /// résultat en agrégeant, pondéré par le nombre de votants, un appel PUR (sans effet de
    /// bord) à VoteCalculator.ComputeRound1 pour chaque district — même pipeline de calcul que
    /// les vraies élections, mais sans persister aucun état électoral. Les effets strictement
    /// locaux (Bastion, bonus offensif, campagnes de district, campagnes illégales) sont
    /// volontairement ignorés ici : ils n'ont pas de sens dans une mesure d'opinion ville
    /// entière ; seuls les effets city-wide (évènement, campagnes de propagande, sanctions de
    /// la Commission Électorale) sont pris en compte.
    /// </summary>
    public partial class CouncilPollSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        public const int PollCost = 4000;

        // Veille du 1er tour jusqu'à l'issue du 2e tour — inspiré du code électoral français.
        private const double PollBlackoutBeforeRound1Days = 1.0;

        private EntityQuery m_SingletonQuery;
        private EntityQuery m_DistrictQuery;
        private SimulationSystem m_SimulationSystem;

        private CouncilElectionSystem m_ElectionSystem;
        private CouncilCustomPartySystem m_CustomPartySystem;
        private CouncilPartyMembershipSystem m_MembershipSystem;
        private CouncilCityEventSystem m_CityEventSystem;
        private CouncilPropagandaSystem m_PropagandaSystem;
        private CouncilElectoralCommissionSystem m_CommissionSystem;
        private CouncilEconomySystem m_EconomySystem;
        private CouncilTaxSystem m_TaxSystem;

        private readonly Random m_Rng = new Random();
        private Entity m_SingletonEntity = Entity.Null;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilPollData>());
            m_DistrictQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilDistrictData>());
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();

            m_ElectionSystem = World.GetOrCreateSystemManaged<CouncilElectionSystem>();
            m_CustomPartySystem = World.GetOrCreateSystemManaged<CouncilCustomPartySystem>();
            m_MembershipSystem = World.GetOrCreateSystemManaged<CouncilPartyMembershipSystem>();
            m_CityEventSystem = World.GetOrCreateSystemManaged<CouncilCityEventSystem>();
            m_PropagandaSystem = World.GetOrCreateSystemManaged<CouncilPropagandaSystem>();
            m_CommissionSystem = World.GetOrCreateSystemManaged<CouncilElectoralCommissionSystem>();
            m_EconomySystem = World.GetOrCreateSystemManaged<CouncilEconomySystem>();
            m_TaxSystem = World.GetOrCreateSystemManaged<CouncilTaxSystem>();
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

        protected override void OnUpdate() { } // purement passif, déclenché par le joueur (trigger UI)

        private void DestroyExistingSingleton()
        {
            var existing = m_SingletonQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var e in existing)
                    EntityManager.DestroyEntity(e);
            }
            finally { existing.Dispose(); }
            m_SingletonEntity = Entity.Null;
        }

        private void EnsureSingleton()
        {
            var existing = m_SingletonQuery.ToEntityArray(Allocator.Temp);
            try
            {
                if (existing.Length == 1) { m_SingletonEntity = existing[0]; return; }
                if (existing.Length > 1)
                {
                    Entity keep = existing[0];
                    foreach (var e in existing)
                        if (e != keep) { s_Log.Warn($"[CouncilPollSystem] Doublon détruit : {e}"); EntityManager.DestroyEntity(e); }
                    m_SingletonEntity = keep;
                    return;
                }
            }
            finally { existing.Dispose(); }

            m_SingletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(m_SingletonEntity, new CouncilPollData
            {
                m_HasResults = false,
                m_LastPollDay = -1,
                m_LastResults = new FixedList512Bytes<PollResultEntry>()
            });
            s_Log.Info("[CouncilPollSystem] Entité singleton créée (aucun sondage).");
        }

        public CouncilPollData GetData()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            return EntityManager.GetComponentData<CouncilPollData>(m_SingletonEntity);
        }

        private void SetData(CouncilPollData data)
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            EntityManager.SetComponentData(m_SingletonEntity, data);
        }

        private double CurrentDay() => (double)m_SimulationSystem.frameIndex / 262144.0;

        /// <summary>
        /// true si aucun district n'est à J-1 (ou après) de son 1er tour, et qu'aucun district
        /// n'est en attente de son 2e tour (Round1Done). Vérification city-wide : un seul
        /// district dans cette fenêtre bloque la commande de sondage pour toute la ville.
        /// </summary>
        public bool IsPollAllowedNow()
        {
            double currentDay = CurrentDay();
            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    if (!EntityManager.HasComponent<CouncilDistrictData>(d)) continue;
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(d);

                    if (data.m_Phase == ElectionPhase.Round1Done)
                        return false; // 2e tour en attente

                    if (data.m_Phase == ElectionPhase.Round1Scheduled
                        && currentDay >= data.m_NextRound1Day - PollBlackoutBeforeRound1Days)
                        return false; // veille du 1er tour ou plus tard
                }
            }
            finally { districts.Dispose(); }
            return true;
        }

        /// <summary>
        /// Débite le coût du sondage sur la trésorerie du parti joueur actif, calcule et
        /// persiste le résultat. Refuse si aucun parti joueur actif, si la fenêtre de blackout
        /// est active, ou si les réserves sont insuffisantes.
        /// </summary>
        public bool TryOrderPoll(out string error)
        {
            error = null;

            if (!IsPollAllowedNow())
            {
                error = "Sondages interdits de la veille du 1er tour à l'issue du 2e tour.";
                return false;
            }

            var custom = m_CustomPartySystem.GetData();
            if (!custom.m_Exists || !custom.m_SubstitutionActive)
            {
                error = "Aucun parti joueur actif.";
                return false;
            }
            var playerParty = custom.m_ActiveSpace;

            if (!m_MembershipSystem.TrySpendTreasury(playerParty, PollCost, CouncilPartyMembershipSystem.TreasurySource.Poll))
            {
                error = "Réserves insuffisantes.";
                return false;
            }

            var results = ComputePollResults();

            var data = GetData();
            data.m_HasResults = true;
            data.m_LastPollDay = CurrentDay();
            data.m_LastResults.Clear();
            foreach (var kv in results)
                data.m_LastResults.Add(new PollResultEntry { m_Party = kv.Key, m_SharePercent = kv.Value });
            SetData(data);

            s_Log.Info("[CouncilPollSystem] Sondage commandé et calculé.");
            return true;
        }

        /// <summary>
        /// Agrège, pondéré par le nombre de votants, un calcul PUR (VoteCalculator.ComputeRound1,
        /// aucune écriture ECS) par district. Les effets purement locaux (Bastion, offensif,
        /// campagnes de district/illégales) sont omis — cf. remarque en tête de fichier.
        /// </summary>
        private Dictionary<PoliticalParty, float> ComputePollResults()
        {
            double currentDay = CurrentDay();
            var activeEvent = m_CityEventSystem.GetActiveEventDefinition();
            PoliticalParty? cityLeadingParty = activeEvent != null ? GetCityLeadingPartyForEvent() : null;
            var activeCampaigns = m_PropagandaSystem.GetActiveCampaigns();

            var citySanctions = new List<SanctionEntry>();
            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty)))
                citySanctions.AddRange(m_CommissionSystem.GetActiveSanctionsForParty(p, currentDay));

            var totals = new Dictionary<PoliticalParty, float>();
            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty))) totals[p] = 0f;
            int totalVoters = 0;

            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                // Graine distincte de celle des vraies élections ("POLL"), pour ne pas reproduire
                // exactement la marge d'erreur d'un scrutin qui se déroulerait le même jour.
                int seedBase = (int)(currentDay * 1000.0) ^ 0x504F4C4C;

                foreach (var d in districts)
                {
                    var (seniors, adults, wealth) = m_ElectionSystem.GetDistrictDemographicsForPoll(d);
                    if (seniors + adults <= 0) continue;

                    var activePolicies = m_ElectionSystem.GetActivePoliciesForPoll(d);
                    int seed = d.Index ^ seedBase;

                    var (pollTaxPopuliste, pollTaxGauche) = m_TaxSystem.GetTaxDiscontentBonus();
                    var result = VoteCalculator.ComputeRound1(
                        seniors, adults, wealth, seed, activePolicies,
                        activeEvent?.Effects, cityLeadingParty,
                        isBastion: false, bastionParty: default,
                        offensiveBonusHolders: null,
                        activeCampaigns: activeCampaigns,
                        districtCampaigns: null,
                        illegalCampaigns: null,
                        citySanctions: citySanctions,
                        unemploymentCrisisActive: m_EconomySystem.IsUnemploymentCrisisActive(),
                        taxDiscontentBonusPopuliste: m_TaxSystem.GetTaxDiscontentBonus().populisteBonus,
                        taxDiscontentBonusGaucheRadicale: m_TaxSystem.GetTaxDiscontentBonus().gaucheRadicaleBonus);

                    foreach (var kv in result.m_VoteShares)
                        totals[kv.Key] += kv.Value * result.m_Voters;
                    totalVoters += result.m_Voters;
                }
            }
            finally { districts.Dispose(); }

            var final = new Dictionary<PoliticalParty, float>();
            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty)))
                final[p] = totalVoters > 0 ? totals[p] / totalVoters : 0f;
            return final;
        }

        /// <summary>
        /// Duplication volontaire de la logique de majorité ville entière (même choix de
        /// découplage que documenté ailleurs dans le mod, cf. CouncilBonusSystem.GetCityMajorityParty).
        /// </summary>
        private PoliticalParty? GetCityLeadingPartyForEvent()
        {
            var totals = new Dictionary<PoliticalParty, int>();
            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    if (!EntityManager.HasComponent<CouncilDistrictData>(d)) continue;
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    if (data.m_Phase != ElectionPhase.Completed) continue;
                    foreach (var r in data.m_FinalResults)
                    {
                        totals.TryGetValue(r.m_Party, out int c);
                        totals[r.m_Party] = c + r.m_Seats;
                    }
                }
            }
            finally { districts.Dispose(); }
            if (totals.Count == 0) return null;
            return totals.OrderByDescending(kv => kv.Value).First().Key;
        }
    }
}