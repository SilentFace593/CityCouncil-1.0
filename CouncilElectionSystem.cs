using System;
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
        private CouncilBonusSystem m_BonusSystem;
        private CouncilPropagandaSystem m_PropagandaSystem;
        private CouncilElectoralCommissionSystem m_CommissionSystem;
        private CouncilScoreSystem m_ScoreSystem;
        private CouncilEconomySystem m_EconomySystem;
        private CouncilTaxSystem m_TaxSystem;
        private CouncilVotingInstructionSystem m_VotingInstructionSystem;
        private readonly Random m_InstructionRng = new Random();

        protected override void OnCreate()
        {
            base.OnCreate();

            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_PolicyRegistry = World.GetOrCreateSystemManaged<CouncilPolicyRegistry>();
            m_CityEventSystem = World.GetOrCreateSystemManaged<CouncilCityEventSystem>();
            m_CustomPartySystem = World.GetOrCreateSystemManaged<CouncilCustomPartySystem>(); // AJOUT
            m_MembershipSystem = World.GetOrCreateSystemManaged<CouncilPartyMembershipSystem>(); // AJOUT
            m_FundingSystem = World.GetOrCreateSystemManaged<CouncilFundingSystem>();
            m_BonusSystem = World.GetOrCreateSystemManaged<CouncilBonusSystem>();
            m_PropagandaSystem = World.GetOrCreateSystemManaged<CouncilPropagandaSystem>();
            m_CommissionSystem = World.GetOrCreateSystemManaged<CouncilElectoralCommissionSystem>();
            m_ScoreSystem = World.GetOrCreateSystemManaged<CouncilScoreSystem>();
            m_EconomySystem = World.GetOrCreateSystemManaged<CouncilEconomySystem>();
            m_TaxSystem = World.GetOrCreateSystemManaged<CouncilTaxSystem>();
            m_VotingInstructionSystem = World.GetOrCreateSystemManaged<CouncilVotingInstructionSystem>();


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
            data = EntityManager.GetComponentData<CouncilDistrictData>(districtEntity);

            // Seed dérivée du district + jour courant : varie par district et par élection,
            // tout en restant reproductible pour une même combinaison (utile en debug).
            int seed = districtEntity.Index ^ (int)(GetCurrentSimulationDay() * 1000.0);
            var activePolicies = GetActivePolicies(districtEntity);

            var activeEvent = m_CityEventSystem.GetActiveEventDefinition();
            // Coût du calcul du parti majoritaire évité quand aucun évènement n'est actif.
            PoliticalParty? cityLeadingParty = activeEvent != null ? GetCityLeadingParty() : null;

            var offensiveHolders = GetOffensiveBonusHolders(data);
            var activeCampaigns = m_PropagandaSystem.GetActiveCampaigns();
            var districtCampaigns = m_PropagandaSystem.GetActiveDistrictCampaigns(districtEntity);
            var illegalCampaigns = m_PropagandaSystem.GetActiveIllegalCampaigns(districtEntity);
            bool unemploymentCrisis = m_EconomySystem.IsUnemploymentCrisisActive();
            var (taxDiscontentPopuliste, taxDiscontentGauche) = m_TaxSystem.GetTaxDiscontentBonus();
            bool ecologistNuclearBonus = m_BonusSystem.IsEcologistNuclearBonusActive();

            // AJOUT — sanctions city-wide : combine celles de tous les partis (rarement plusieurs à la
            // fois), la fonction GetActiveSanctionsForParty filtrant déjà par expiry.
            var citySanctions = new List<SanctionEntry>();
            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty)))
                citySanctions.AddRange(m_CommissionSystem.GetActiveSanctionsForParty(p, GetCurrentSimulationDay()));


            // AJOUT — l'état Bastion utilisé ici est celui d'AVANT cette élection (m_IsBastion/
            // m_BastionParty ne sont mis à jour qu'après, dans FinalizeResults), donc le bonus profite
            // bien au détenteur actuel pour DÉFENDRE son district, pas à un futur vainqueur.
            var result = VoteCalculator.ComputeRound1(
            seniors, adults, wealth, seed, activePolicies,
            activeEvent?.Effects, cityLeadingParty,
            data.m_IsBastion, data.m_BastionParty,
            offensiveHolders,
            activeCampaigns,
            districtCampaigns,
            illegalCampaigns,
            citySanctions,
            unemploymentCrisis,
            taxDiscontentPopuliste,
            taxDiscontentGauche,
            ecologistNuclearBonus);

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
                var previousLeader = data.m_LeadingParty;
                bool wasAlreadyLeading = data.m_Phase == ElectionPhase.Completed && previousLeader == result.m_Leader;

                data.m_WonInRound1 = true;
                data.m_LeadingParty = result.m_Leader;
                FinalizeResults(ref data, result.m_VoteShares, seatCount, isNewConquest: !wasAlreadyLeading);
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

        /// <summary>
        /// Liste les partis détenant le bonus Offensif dont ce district (Bastion ou non) n'est PAS
        /// le fief : n'a d'effet que si data.m_IsBastion est vrai et appartient à un autre parti.
        /// Utilise l'état du district AVANT cette élection (FinalizeResults/UpdateBastionStreak ne
        /// modifient m_IsBastion/m_BastionParty qu'après ce calcul).
        /// </summary>
        private List<PoliticalParty> GetOffensiveBonusHolders(in CouncilDistrictData data)
        {
            var holders = new List<PoliticalParty>();
            if (!data.m_IsBastion) return holders; // aucun effet hors Bastion

            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty)))
            {
                if (p == data.m_BastionParty) continue; // le détenteur du Bastion n'est jamais visé
                if (m_BonusSystem.GetBonus(p) == PermanentBonusType.Offensif)
                    holders.Add(p);
            }
            return holders;
        }



        private void RunRound2(Entity districtEntity, ref CouncilDistrictData data, int totalPopulation)
        {
            var previousLeader = data.m_LeadingParty;

            var round1Shares = data.m_Round1Results.ToArray().ToDictionary(r => r.m_Party, r => r.m_VoteShare);
            var ordered = round1Shares.OrderByDescending(kv => kv.Value).ToList();
            var finalist1 = ordered[0].Key;
            var finalist2 = ordered[1].Key;

            var round1Result = new RoundResult
            {
                m_VoteShares = round1Shares,
                m_Voters = data.m_VotersRound1,
                m_Abstention = data.m_AbstentionRound1,
            };

            var activeEvent = m_CityEventSystem.GetActiveEventDefinition();

            // AJOUT — consigne de vote : ne s'applique que si le parti joueur actif était bien éliminé
            // dans CE district (ni finaliste 1 ni finaliste 2), et qu'une consigne validée existe encore.
            PoliticalParty? instructedEliminated = null;
            PoliticalParty? instructedTarget = null;
            float complianceRoll = 0f;

            var custom = m_CustomPartySystem.GetData();
            if (custom.m_Exists && custom.m_SubstitutionActive)
            {
                var playerParty = custom.m_ActiveSpace;
                bool playerEliminated = playerParty != finalist1 && playerParty != finalist2
                                      && round1Shares.ContainsKey(playerParty);

                if (playerEliminated && m_VotingInstructionSystem.TryGetInstruction(districtEntity.Index, out var target))
                {
                    instructedEliminated = playerParty;
                    instructedTarget = target;
                    complianceRoll = VoteCalculator.VotingInstructionComplianceMin
                        + (float)m_InstructionRng.NextDouble()
                          * (VoteCalculator.VotingInstructionComplianceMax - VoteCalculator.VotingInstructionComplianceMin);

                    s_Log.Info($"[CouncilElectionSystem] Consigne de vote appliquée district {districtEntity.Index} : " +
                               $"{playerParty} -> {target}, compliance {complianceRoll:P0}.");
                }

                // Toujours consommée, appliquée ou non (parti changé entre-temps, etc.) : ne doit pas
                // survivre au-delà de ce 2e tour.
                m_VotingInstructionSystem.ConsumeInstruction(districtEntity.Index);
            }

            var result = VoteCalculator.ComputeRound2(
                round1Result, totalPopulation, finalist1, finalist2, activeEvent?.Effects,
                instructedEliminated, instructedTarget, complianceRoll);

            data.m_VotersRound2 = result.m_Voters;
            data.m_AbstentionRound2 = result.m_Abstention;
            data.m_LeadingParty = result.m_Leader;

            bool isNewConquest = previousLeader != result.m_Leader;

            FinalizeResults(ref data, result.m_VoteShares, data.m_TotalSeats, isNewConquest);

            data.m_Phase = ElectionPhase.Completed;
            data.m_NextRound1Day = GetCurrentSimulationDay() + ElectionCycleDays;

            s_Log.Info($"District {districtEntity.Index}: 2e tour terminé, vainqueur {data.m_LeadingParty}.");
        }

        private void FinalizeResults(ref CouncilDistrictData data, Dictionary<PoliticalParty, float> shares, int seatCount, bool isNewConquest)
        {
            var oldResultsRaw = data.m_FinalResults.ToArray();
            var oldResults = new Dictionary<PoliticalParty, PartyResult>();
            foreach (var r in oldResultsRaw) oldResults[r.m_Party] = r;
            var dedupedOldResults = oldResults.Values;

            var allocated = VoteCalculator.AllocateSeats(shares, seatCount);
            data.m_FinalResults.Clear();
            foreach (var r in allocated)
                data.m_FinalResults.Add(r);

            m_MembershipSystem.ApplyDistrictSeatDelta(dedupedOldResults, allocated);

            if (isNewConquest)
                m_ScoreSystem.ApplyDistrictConquest(data.m_LeadingParty); // seul hook de score restant ici

            m_FundingSystem.DistributeForFinalizedDistrict(allocated);

            UpdateBastionStreak(ref data); // inchangé, plus aucun appel au score system dedans
        }

        /// <summary>
        /// Met à jour la série de victoires consécutives du district à partir de data.m_LeadingParty
        /// (déjà renseigné par l'appelant avant FinalizeResults, pour le 1er comme le 2e tour).
        /// Le Bastion est acquis à la 3e victoire consécutive, et perdu IMMÉDIATEMENT dès qu'un autre
        /// parti remporte le district (la série repart à 1 pour le nouveau vainqueur, comme demandé :
        /// "si le parti perd une élection le compteur est remis à zéro et perd le bonus").
        /// </summary>
        private void UpdateBastionStreak(ref CouncilDistrictData data)
        {
            var winner = data.m_LeadingParty;

            bool defenderLosing = data.m_StreakCount > 0
                && data.m_StreakParty != winner
                && m_BonusSystem.GetBonus(data.m_StreakParty) == PermanentBonusType.Defensif;

            if (defenderLosing)
            {
                bool wasBastion = data.m_IsBastion;
                data.m_IsBastion = false;
                data.m_StreakCount = System.Math.Max(0, data.m_StreakCount - 1);

                s_Log.Info($"[CouncilElectionSystem] Bonus défensif : {data.m_StreakParty} " +
                           $"{(wasBastion ? "perd le Bastion et " : "")}conserve {data.m_StreakCount} case(s) restante(s).");
                return;
            }

            if (data.m_StreakCount > 0 && data.m_StreakParty == winner)
            {
                data.m_StreakCount = System.Math.Min(3, data.m_StreakCount + 1);
            }
            else
            {
                // Le parti en série change : s'il détenait le Bastion, il le perd ici (m_IsBastion
                // repassé à false ci-dessous, inconditionnellement — toujours correct qu'il y ait eu
                // Bastion ou non avant ce changement de vainqueur).
                data.m_StreakParty = winner;
                data.m_StreakCount = 1;
                data.m_IsBastion = false;
            }

            if (data.m_StreakCount >= 3 && !data.m_IsBastion)
            {
                data.m_IsBastion = true;
                data.m_BastionParty = winner;
                s_Log.Info($"[CouncilElectionSystem] Bastion : {winner} détient désormais ce district (3 victoires consécutives).");
            }
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

                WealthLevel wealth = WealthLevel.Modest; // était WealthLevel.Moyen
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

        /// <summary>
        /// OUTIL DE DEBUG TEMPORAIRE — force tous les districts à traiter leur prochaine échéance
        /// électorale dès le prochain OnUpdate, en ramenant leurs timers au jour courant (ou légèrement
        /// avant, pour passer les comparaisons ">="). Ne modifie ni ElectionCycleDays ni Round2DelayDays :
        /// on avance seulement les rendez-vous déjà planifiés, ce qui permet de dérouler toute la boucle
        /// (1er tour -> 2e tour si besoin -> cycle suivant) exactement comme en jeu normal, juste sans
        /// attendre. À retirer (ou masquer derrière une build de dev) une fois les tests terminés.
        /// </summary>
        public void DebugForceAllDistrictsToNextStep()
        {
            double currentDay = GetCurrentSimulationDay();
            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var districtEntity in districts)
                {
                    if (!EntityManager.HasComponent<CouncilDistrictData>(districtEntity)) continue;
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(districtEntity);

                    switch (data.m_Phase)
                    {
                        case ElectionPhase.Round1Scheduled:
                        case ElectionPhase.Completed:
                            data.m_NextRound1Day = currentDay - 0.001;
                            break;

                        case ElectionPhase.Round1Done:
                            // Force la condition currentDay >= m_Round1CompletedDay + Round2DelayDays
                            data.m_Round1CompletedDay = currentDay - Round2DelayDays - 0.001;
                            break;
                    }

                    EntityManager.SetComponentData(districtEntity, data);
                }
            }
            finally
            {
                districts.Dispose();
            }

            s_Log.Info("[CouncilElectionSystem] DEBUG : toutes les échéances électorales forcées au jour courant.");
        }

        /// <summary>
        /// Wrapper public exposant GetDistrictDemographics à CouncilPollSystem. La méthode privée
        /// reste inchangée (utilisée aussi par ProcessDistrict) — ce n'est qu'un point d'accès en
        /// lecture seule pour un système externe, même esprit que PolicyPrefabs exposé en lecture
        /// seule par CouncilPolicyRegistry.
        /// </summary>
        public (int seniors, int adults, WealthLevel wealth) GetDistrictDemographicsForPoll(Entity districtEntity)
            => GetDistrictDemographics(districtEntity);

        public List<string> GetActivePoliciesForPoll(Entity districtEntity)
            => GetActivePolicies(districtEntity);

    }
}