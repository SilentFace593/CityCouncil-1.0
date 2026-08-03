using System.Collections.Generic;
using System.Linq;
using Colossal.Entities;
using Colossal.Logging;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Prefabs;
using Game.Simulation; // pour SimulationSystem / TimeSystem selon version SDK
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace CityCouncil
{
    /// <summary>
    /// Orchestre le cycle électoral : planifie le 1er tour, le déclenche, planifie et
    /// déclenche le 2e tour si nécessaire, calcule et persiste les résultats.
    /// Fréquence : vérifié une fois par jour in-game (pas besoin de tourner à chaque frame).
    /// </summary>
    public partial class CouncilElectionSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        private const int ElectionCycleDays = 7;   // 1er tour tous les 7 jours in-game

        // ⚠️ RÉGLAGE DE TEST — 2e tour 1h in-game après le 1er (1.0/24.0 jour), au lieu de
        // Round2DelayDays = 1.0 (24h) en usage normal. Remettre à 1.0 avant release.
        private const double Round2DelayDays = 1.0 / 24.0;

        private EntityQuery m_DistrictQuery;
        private EntityQuery m_HappinessParameterQuery;
        private SimulationSystem m_SimulationSystem;
        private CouncilPolicyRegistry m_PolicyRegistry;
        private CouncilCityEventSystem m_CityEventSystem;
        private CouncilCustomPartySystem m_CustomPartySystem; // AJOUT
        private CouncilPartyMembershipSystem m_MembershipSystem;
        private CouncilFundingSystem m_FundingSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_PolicyRegistry = World.GetOrCreateSystemManaged<CouncilPolicyRegistry>();
            m_CityEventSystem = World.GetOrCreateSystemManaged<CouncilCityEventSystem>();
            m_CustomPartySystem = World.GetOrCreateSystemManaged<CouncilCustomPartySystem>(); // AJOUT
            m_MembershipSystem = World.GetOrCreateSystemManaged<CouncilPartyMembershipSystem>(); // AJOUT
            m_FundingSystem = World.GetOrCreateSystemManaged<CouncilFundingSystem>();

            m_DistrictQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<District>() },
            });

            // Même query que Game.UI.InGame.CitizenSection : nécessaire pour
            // CitizenUIUtils.GetHouseholdWealth / GetAverageHouseholdWealth.
            m_HappinessParameterQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Prefabs.CitizenHappinessParameterData>());

            CreateDistrictBuildingQuery();

            RequireForUpdate(m_DistrictQuery);
            RequireForUpdate(m_HappinessParameterQuery);
        }

        // Fréquence de vérification du système : pas besoin de tourner à chaque frame de simulation
        // (262144 = kTicksPerDay du jeu). Une fraction de cet intervalle suffit largement
        // (ex. toutes les ~4096 frames, ~16x/jour in-game) pour ne pas manquer une échéance
        // électorale tout en restant très léger en perf. À ajuster si besoin.
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 4096;

        protected override void OnUpdate()
        {
            double currentDay = GetCurrentSimulationDay();

            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var districtEntity in districts)
                {
                    ProcessDistrict(districtEntity, currentDay);
                }
            }
            finally
            {
                districts.Dispose();
            }
        }

        private void ProcessDistrict(Entity districtEntity, double currentDay)
        {
            bool hasData = EntityManager.HasComponent<CouncilDistrictData>(districtEntity);
            var data = hasData
                ? EntityManager.GetComponentData<CouncilDistrictData>(districtEntity)
                : CreateInitialData(currentDay);

            var (seniors, adults, wealth) = GetDistrictDemographics(districtEntity);
            int totalPopulation = seniors + adults;

            if (totalPopulation <= 0)
            {
                data.m_Phase = ElectionPhase.NoElection;
                SetData(districtEntity, data);
                return;
            }

            // Si le district vient de repasser au-dessus de 0 habitant après avoir été vide,
            // on (re)planifie une élection.
            if (data.m_Phase == ElectionPhase.NoElection)
            {
                data.m_Phase = ElectionPhase.Round1Scheduled;
                data.m_NextRound1Day = currentDay; // élection au prochain cycle
            }

            switch (data.m_Phase)
            {
                case ElectionPhase.Round1Scheduled:
                    if (currentDay >= data.m_NextRound1Day)
                        RunRound1(districtEntity, ref data, seniors, adults, wealth);
                    break;

                case ElectionPhase.Round1Done:
                    if (!data.m_WonInRound1 && currentDay >= data.m_Round1CompletedDay + Round2DelayDays)
                        RunRound2(districtEntity, ref data, totalPopulation);
                    break;

                case ElectionPhase.Completed:
                    if (currentDay >= data.m_NextRound1Day)
                    {
                        data.m_Phase = ElectionPhase.Round1Scheduled;
                        RunRound1(districtEntity, ref data, seniors, adults, wealth);
                    }
                    break;
            }

            SetData(districtEntity, data);
        }

        private CouncilDistrictData CreateInitialData(double currentDay)
        {
            return new CouncilDistrictData
            {
                m_Phase = ElectionPhase.Round1Scheduled,
                m_NextRound1Day = currentDay,
                m_Round1Results = new FixedList128Bytes<PartyResult>(),
                m_FinalResults = new FixedList128Bytes<PartyResult>(),
            };
        }

        /// <summary>
        /// Liste les noms des politiques (parmi les 9 suivies) actuellement actives sur ce district.
        /// </summary>
        private List<string> GetActivePolicies(Entity districtEntity)
        {
            var active = new List<string>();
            foreach (var kv in m_PolicyRegistry.PolicyPrefabs)
            {
                if (CouncilPolicyUtils.IsPolicyActive(EntityManager, districtEntity, kv.Value))
                    active.Add(kv.Key);
            }
            return active;
        }

        /// <summary>
        /// Détermine le parti actuellement majoritaire à l'échelle de la ville, en agrégeant
        /// les sièges de tous les districts ayant une élection terminée. Retourne null si aucun
        /// district n'a encore de résultat. Même principe que CouncilUISystem.RefreshHemicycle,
        /// dupliqué volontairement ici pour ne pas coupler les deux systèmes (l'un tourne côté
        /// simulation, l'autre à la demande côté UI) — un léger doublon de logique simple plutôt
        /// qu'une dépendance croisée entre les deux.
        /// </summary>
        private PoliticalParty? GetCityLeadingParty()
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
                        totals.TryGetValue(r.m_Party, out int current);
                        totals[r.m_Party] = current + r.m_Seats;
                    }
                }
            }
            finally
            {
                districts.Dispose();
            }

            if (totals.Count == 0) return null;
            return totals.OrderByDescending(kv => kv.Value).First().Key;
        }

        private void RunRound1(Entity districtEntity, ref CouncilDistrictData data, int seniors, int adults, WealthLevel wealth)
        {
            // Applique une suppression de parti joueur en attente AVANT de calculer les résultats,
            // pour que le nouveau cycle (celui qu'on est en train de lancer) reflète déjà le retrait.
            // Idempotent : sans effet si rien n'est en attente, donc pas grave d'être appelé une fois
            // par district traité dans le même tick plutôt qu'une seule fois globalement.
            m_CustomPartySystem.ApplyPendingChangesForNewElection(); // AJOUT

            // Seed dérivée du district + jour courant : varie par district et par élection,
            // tout en restant reproductible pour une même combinaison (utile en debug).
            int seed = districtEntity.Index ^ (int)(GetCurrentSimulationDay() * 1000.0);
            var activePolicies = GetActivePolicies(districtEntity);

            var activeEvent = m_CityEventSystem.GetActiveEventDefinition();
            // Coût du calcul du parti majoritaire évité quand aucun évènement n'est actif.
            PoliticalParty? cityLeadingParty = activeEvent != null ? GetCityLeadingParty() : null;

            var result = VoteCalculator.ComputeRound1(
                seniors, adults, wealth, seed, activePolicies,
                activeEvent?.Effects, cityLeadingParty);

            data.m_VotersRound1 = result.m_Voters;
            data.m_AbstentionRound1 = result.m_Abstention;
            data.m_Round1CompletedDay = GetCurrentSimulationDay();

            int seatCount = VoteCalculator.ComputeSeatCount(seniors + adults);
            data.m_TotalSeats = seatCount;

            data.m_Round1Results.Clear();
            foreach (var kv in result.m_VoteShares.OrderByDescending(kv => kv.Value))
            {
                data.m_Round1Results.Add(new PartyResult
                {
                    m_Party = kv.Key,
                    m_VoteShare = kv.Value,
                    m_Seats = 0
                });
            }

            if (result.m_MajorityReached)
            {
                data.m_WonInRound1 = true;
                data.m_LeadingParty = result.m_Leader;
                FinalizeResults(ref data, result.m_VoteShares, seatCount);
                data.m_Phase = ElectionPhase.Completed;
                data.m_NextRound1Day = data.m_Round1CompletedDay + ElectionCycleDays;

                s_Log.Info($"District {districtEntity.Index}: victoire dès le 1er tour ({data.m_LeadingParty}).");
            }
            else
            {
                data.m_WonInRound1 = false;
                data.m_Phase = ElectionPhase.Round1Done;

                s_Log.Info($"District {districtEntity.Index}: pas de majorité au 1er tour, " +
                           $"2e tour prévu dans {Round2DelayDays} jour(s).");
            }
        }

        private void RunRound2(Entity districtEntity, ref CouncilDistrictData data, int totalPopulation)
        {
            var round1Shares = data.m_Round1Results
                .ToArray()
                .ToDictionary(r => r.m_Party, r => r.m_VoteShare);

            var ordered = round1Shares.OrderByDescending(kv => kv.Value).ToList();
            var finalist1 = ordered[0].Key;
            var finalist2 = ordered[1].Key;

            var round1Result = new RoundResult
            {
                m_VoteShares = round1Shares,
                m_Voters = data.m_VotersRound1,
                m_Abstention = data.m_AbstentionRound1,
            };

            var result = VoteCalculator.ComputeRound2(round1Result, totalPopulation, finalist1, finalist2);

            data.m_VotersRound2 = result.m_Voters;
            data.m_AbstentionRound2 = result.m_Abstention;
            data.m_LeadingParty = result.m_Leader;

            FinalizeResults(ref data, result.m_VoteShares, data.m_TotalSeats);

            data.m_Phase = ElectionPhase.Completed;
            data.m_NextRound1Day = GetCurrentSimulationDay() + ElectionCycleDays;

            s_Log.Info($"District {districtEntity.Index}: 2e tour terminé, vainqueur {data.m_LeadingParty}.");
        }

        private void FinalizeResults(ref CouncilDistrictData data, Dictionary<PoliticalParty, float> shares, int seatCount)
        {
            var oldResults = data.m_FinalResults.ToArray();

            var allocated = VoteCalculator.AllocateSeats(shares, seatCount);
            data.m_FinalResults.Clear();
            foreach (var r in allocated)
                data.m_FinalResults.Add(r);

            m_MembershipSystem.ApplyDistrictSeatDelta(oldResults, allocated);
            m_FundingSystem.DistributeForFinalizedDistrict(allocated); // AJOUT
        }

        private void SetData(Entity districtEntity, CouncilDistrictData data)
        {
            if (EntityManager.HasComponent<CouncilDistrictData>(districtEntity))
                EntityManager.SetComponentData(districtEntity, data);
            else
                EntityManager.AddComponentData(districtEntity, data);

            bool shouldTagNoElection = data.m_Phase == ElectionPhase.NoElection;
            bool hasTag = EntityManager.HasComponent<CouncilNoElectionTag>(districtEntity);
            if (shouldTagNoElection && !hasTag)
                EntityManager.AddComponent<CouncilNoElectionTag>(districtEntity);
            else if (!shouldTagNoElection && hasTag)
                EntityManager.RemoveComponent<CouncilNoElectionTag>(districtEntity);
        }

        // --- Accès aux données exposées par le jeu ---
        //
        // Basé sur la décompilation de Game.UI.InGame.ResidentsSection :
        //   - Game.Areas.CurrentDistrict.m_District porté par les BÂTIMENTS (pas les citoyens)
        //     relie chaque bâtiment résidentiel à son district.
        //   - Les occupants d'un bâtiment résidentiel s'obtiennent via son buffer Renter
        //     (Game.Buildings.Renter -> m_Renter = entité Household), puis le buffer
        //     Game.Citizens.HouseholdCitizen de chaque household.
        //   - CitizenUIUtils.GetAverageHouseholdWealth(EntityManager, households, happinessData)
        //     donne DIRECTEMENT la richesse moyenne du district (même calcul que le panneau vanilla) —
        //     plus besoin de la déduire nous-mêmes.
        //   - Citizen.GetAge() renvoie Game.Citizens.CitizenAge (Child/Teen/Adult/Elderly) —
        //     à ne pas confondre avec Game.UI.InGame.CitizenAgeKey (Elder, sans 'ly') utilisé
        //     côté UI ; on utilise CitizenAge ici car on lit directement le component ECS.

        private EntityQuery m_DistrictBuildingQuery;

        // (à ajouter dans OnCreate, cf. note plus bas)
        private void CreateDistrictBuildingQuery()
        {
            m_DistrictBuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Game.Buildings.Building>(),
                    ComponentType.ReadOnly<Game.Buildings.ResidentialProperty>(),
                    ComponentType.ReadOnly<Game.Prefabs.PrefabRef>(),
                    ComponentType.ReadOnly<Game.Buildings.Renter>(),
                    ComponentType.ReadOnly<CurrentDistrict>(),
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Deleted>(),
                }
            });
        }

        private (int seniors, int adults, WealthLevel wealth) GetDistrictDemographics(Entity districtEntity)
        {
            int seniors = 0;
            int adults = 0;
            var households = new NativeList<Entity>(Allocator.Temp);

            try
            {
                var buildings = m_DistrictBuildingQuery.ToEntityArray(Allocator.Temp);
                try
                {
                    foreach (var buildingEntity in buildings)
                    {
                        var currentDistrict = EntityManager.GetComponentData<CurrentDistrict>(buildingEntity);
                        if (currentDistrict.m_District != districtEntity) continue;

                        var renters = EntityManager.GetBuffer<Game.Buildings.Renter>(buildingEntity, isReadOnly: true);
                        for (int i = 0; i < renters.Length; i++)
                        {
                            Entity householdEntity = renters[i].m_Renter;
                            if (!EntityManager.HasComponent<Household>(householdEntity)) continue;
                            if (!EntityManager.TryGetBuffer<HouseholdCitizen>(householdEntity, true, out var citizens)) continue;

                            households.Add(householdEntity);

                            for (int j = 0; j < citizens.Length; j++)
                            {
                                if (!EntityManager.TryGetComponent<Citizen>(citizens[j].m_Citizen, out var citizen)) continue;

                                switch (citizen.GetAge())
                                {
                                    case CitizenAge.Elderly: seniors++; break;
                                    case CitizenAge.Adult: adults++; break;
                                        // Child / Teen ignorés : pas d'âge de vote
                                }
                            }
                        }
                    }
                }
                finally
                {
                    buildings.Dispose();
                }

                WealthLevel wealth = WealthLevel.Moyen;
                if (households.Length > 0)
                {
                    var happinessData = m_HappinessParameterQuery.GetSingleton<Game.Prefabs.CitizenHappinessParameterData>();
                    var wealthKey = Game.UI.InGame.CitizenUIUtils.GetAverageHouseholdWealth(
                        EntityManager, households, happinessData);
                    wealth = WealthMapping.FromHouseholdWealthKey(wealthKey);
                }

                return (seniors, adults, wealth);
            }
            finally
            {
                households.Dispose();
            }
        }

        private double GetCurrentSimulationDay()
        {
            // Game.Simulation.TimeSystem.kTicksPerDay = 262144 (confirmé par décompilation).
            // On suppose que SimulationSystem expose le compteur de frames brut via une
            // propriété publique "frameIndex" (nom à confirmer/ajuster si différent) :
            //   jour = frameIndex / kTicksPerDay
            // Si le vrai nom diverge (ex. m_SimulationSystem.frameIndex n'existe pas publiquement),
            // remplacer par la propriété équivalente trouvée dans SimulationSystem ou TimeSystem
            // lui-même (ex. TimeSystem.normalizedDate * kTicksPerDay, ou une méthode GetDate()).
            const int ticksPerDay = 262144; // Game.Simulation.TimeSystem.kTicksPerDay

            uint frameIndex = m_SimulationSystem.frameIndex;
            return (double)frameIndex / ticksPerDay;
        }
    }
}