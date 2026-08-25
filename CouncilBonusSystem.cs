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
    /// Gère l'attribution des bonus permanents : suit les victoires consécutives à la majorité
    /// du conseil municipal (vérifiée périodiquement, même cycle que CouncilPartyMembershipSystem),
    /// et attribue un bonus (aléatoire pour l'IA, au choix pour le parti joueur) à chaque palier
    /// ≥2 victoires consécutives.
    /// </summary>
    public partial class CouncilBonusSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        private const double CycleIntervalDays = 7.0; // même durée qu'un cycle électoral

        private EntityQuery m_SingletonQuery;
        private EntityQuery m_DistrictQuery;
        private SimulationSystem m_SimulationSystem;
        private CouncilCustomPartySystem m_CustomPartySystem;
        private readonly Random m_Rng = new Random();

        private Entity m_SingletonEntity = Entity.Null;
        private double m_LastCycleDay = -1;
        private CouncilInstitutionSystem m_InstitutionSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilBonusData>());
            m_DistrictQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilDistrictData>());
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_CustomPartySystem = World.GetOrCreateSystemManaged<CouncilCustomPartySystem>();
            m_InstitutionSystem = World.GetOrCreateSystemManaged<CouncilInstitutionSystem>();
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

            double currentDay = (double)m_SimulationSystem.frameIndex / 262144.0;

            if (m_LastCycleDay < 0)
            {
                m_LastCycleDay = currentDay;
                return;
            }

            if (currentDay - m_LastCycleDay >= CycleIntervalDays)
            {
                m_LastCycleDay = currentDay;
                RunMajorityCheck();
            }
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
                        var d = EntityManager.GetComponentData<CouncilBonusData>(e);
                        if (d.m_HasStreak) { keep = e; break; }
                    }
                    foreach (var e in existing)
                    {
                        if (e != keep)
                        {
                            s_Log.Warn($"[CouncilBonusSystem] Entité singleton en doublon détruite : {e}");
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
            EntityManager.AddComponentData(m_SingletonEntity, new CouncilBonusData
            {
                m_Entries = new FixedList512Bytes<CouncilBonusEntry>()
            });
            s_Log.Info("[CouncilBonusSystem] Entité singleton créée (aucune trouvée).");
        }

        public CouncilBonusData GetData()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            return EntityManager.GetComponentData<CouncilBonusData>(m_SingletonEntity);
        }

        private void SetData(CouncilBonusData data)
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            EntityManager.SetComponentData(m_SingletonEntity, data);
        }

        public PermanentBonusType GetBonus(PoliticalParty party)
        {
            var data = GetData();
            foreach (var e in data.m_Entries)
                if (e.m_Party == party) return e.m_Bonus;
            return PermanentBonusType.None;
        }

        public bool IsPlayerChoicePending() => GetData().m_PlayerChoicePending;
        public PoliticalParty GetPlayerChoiceSpace() => GetData().m_PlayerChoiceSpace;

        /// <summary>Appelé par CouncilUISystem quand le joueur valide son choix.</summary>
        public void ChoosePlayerBonus(PermanentBonusType type)
        {
            var data = GetData();
            if (!data.m_PlayerChoicePending) return;
            if (type != PermanentBonusType.Defensif && type != PermanentBonusType.Offensif) return;

            var space = data.m_PlayerChoiceSpace;
            SetBonus(space, type);

            data = GetData(); // relu après SetBonus (qui a modifié m_Entries)
            data.m_PlayerChoicePending = false;
            SetData(data);

            s_Log.Info($"[CouncilBonusSystem] Joueur a choisi le bonus {type} pour {space}.");
        }

        private void SetBonus(PoliticalParty party, PermanentBonusType bonus)
        {
            var data = GetData();
            var entries = data.m_Entries;
            bool found = false;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].m_Party != party) continue;
                var e = entries[i];
                e.m_Bonus = bonus;
                entries[i] = e;
                found = true;
                break;
            }
            if (!found)
                entries.Add(new CouncilBonusEntry { m_Party = party, m_Bonus = bonus });

            data.m_Entries = entries;
            SetData(data);
        }

        private void RunMajorityCheck()
        {
            var majority = GetCityMajorityParty();
            if (majority == null) return; // aucune élection terminée nulle part encore

            var data = GetData();
            bool sameAsBefore = data.m_HasStreak && data.m_StreakParty == majority.Value;

            if (sameAsBefore)
                data.m_StreakCount++;
            else
            {
                data.m_StreakParty = majority.Value;
                data.m_StreakCount = 1;
                data.m_HasStreak = true;
            }
            SetData(data);

            if (data.m_StreakCount >= 2)
                HandleBonusOpportunity(majority.Value);
        }

        /// <summary>
        /// À chaque palier ≥2 victoires consécutives : propose le choix au joueur si ce slot
        /// est occupé par sa substitution active, sinon tire un bonus aléatoire pour l'IA.
        /// </summary>
        private void HandleBonusOpportunity(PoliticalParty party)
        {
            var custom = m_CustomPartySystem.GetData();
            bool isPlayerSlot = custom.m_Exists && custom.m_SubstitutionActive && custom.m_ActiveSpace == party;

            if (isPlayerSlot)
            {
                var data = GetData();
                data.m_PlayerChoicePending = true;
                data.m_PlayerChoiceSpace = party;
                SetData(data);
                s_Log.Info($"[CouncilBonusSystem] Choix du bonus permanent proposé au joueur pour {party}.");
            }
            else
            {
                var chosen = m_Rng.Next(2) == 0 ? PermanentBonusType.Defensif : PermanentBonusType.Offensif;
                SetBonus(party, chosen);
                s_Log.Info($"[CouncilBonusSystem] Bonus permanent attribué aléatoirement à {party} : {chosen}.");
            }
        }

        /// <summary>
        /// Duplication volontaire du calcul de majorité (même logique que
        /// CouncilUISystem.RefreshHemicycle / CouncilElectionSystem.GetCityLeadingParty),
        /// pour ne pas coupler ce système aux autres.
        /// </summary>
        private PoliticalParty? GetCityMajorityParty()
        {
            var totals = new Dictionary<PoliticalParty, int>();
            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    if (!EntityManager.HasComponent<CouncilDistrictData>(d)) continue;
                    var districtData = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    if (districtData.m_Phase != ElectionPhase.Completed) continue;

                    foreach (var r in districtData.m_FinalResults)
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

        /// <summary>
        /// OUTIL DE DEBUG TEMPORAIRE — force le prochain OnUpdate à exécuter immédiatement le
        /// contrôle de majorité, sans attendre les 7 jours in-game réels. Nécessaire car le bouton
        /// [DEBUG] Forcer l'étape électorale suivante n'avance QUE les échéances par district, pas
        /// le temps de simulation réel (m_SimulationSystem.frameIndex) dont dépend ce cycle — même
        /// remarque que le bug initial de trésorerie/financement.
        /// </summary>
        public void DebugForceMajorityCheck()
        {
            double currentDay = (double)m_SimulationSystem.frameIndex / 262144.0;
            m_LastCycleDay = currentDay - CycleIntervalDays - 0.001;
            s_Log.Info("[CouncilBonusSystem] DEBUG : contrôle de majorité forcé au prochain update.");
        }

        private const int RepublicanBureauStreakThreshold = 3;
        public bool IsRepublicanBureauBonusActive()
        {
            var data = GetData();
            bool hasStreak = data.m_HasStreak
                && data.m_StreakParty == PoliticalParty.Republicain
                && data.m_StreakCount >= RepublicanBureauStreakThreshold;

            if (!hasStreak) return false;

            return m_InstitutionSystem.IsCentralIntelligenceBureauPresent();
        }

        private const int PrisonBonusStreakThreshold = 3;
        public bool IsPopulistPrisonBonusActive()
        {
            var data = GetData();
            bool hasStreak = data.m_HasStreak
                && data.m_StreakParty == PoliticalParty.Populiste
                && data.m_StreakCount >= PrisonBonusStreakThreshold;

            if (!hasStreak) return false;

            return m_InstitutionSystem.IsPrisonPresent();
        }

        private const int NuclearBonusStreakThreshold = 3;
        public bool IsEcologistNuclearBonusActive()
        {
            var data = GetData();
            bool hasStreak = data.m_HasStreak
                && data.m_StreakParty == PoliticalParty.Ecologiste
                && data.m_StreakCount >= NuclearBonusStreakThreshold;

            if (!hasStreak) return false;

            return m_InstitutionSystem.IsNuclearPowerPlantPresent();
        }

        private const int UniversityBonusStreakThreshold = 3;
        public bool IsRadicalLeftUniversityBonusActive()
        {
            var data = GetData();
            bool hasStreak = data.m_HasStreak
                && data.m_StreakParty == PoliticalParty.GaucheRadicale
                && data.m_StreakCount >= UniversityBonusStreakThreshold;

            if (!hasStreak) return false;

            return m_InstitutionSystem.IsUniversityPresent();
        }

    }
}