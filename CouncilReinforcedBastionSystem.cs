using System.Linq;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Unity.Collections;
using Unity.Entities;

namespace CityCouncil
{
    public partial class CouncilReinforcedBastionSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        private EntityQuery m_SingletonQuery;
        private EntityQuery m_DistrictQuery;
        private Entity m_SingletonEntity = Entity.Null;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilReinforcedBastionData>());
            m_DistrictQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilDistrictData>());
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

        protected override void OnUpdate() { } // purement passif, piloté par CouncilElectionSystem/CouncilUISystem

        private void DestroyExistingSingleton()
        {
            var existing = m_SingletonQuery.ToEntityArray(Allocator.Temp);
            try { foreach (var e in existing) EntityManager.DestroyEntity(e); }
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
                        if (e != keep) { s_Log.Warn($"[CouncilReinforcedBastionSystem] Doublon détruit : {e}"); EntityManager.DestroyEntity(e); }
                    m_SingletonEntity = keep;
                    return;
                }
            }
            finally { existing.Dispose(); }

            var initial = new CouncilReinforcedBastionData { m_Entries = new FixedList512Bytes<ReinforcedBastionEntry>() };
            foreach (PoliticalParty p in System.Enum.GetValues(typeof(PoliticalParty)))
                initial.m_Entries.Add(new ReinforcedBastionEntry { m_Party = p, m_Active = false, m_DistrictEntity = Entity.Null });

            m_SingletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(m_SingletonEntity, initial);
            s_Log.Info("[CouncilReinforcedBastionSystem] Entité singleton créée (5 partis, aucun Bastion Renforcé).");
        }

        public CouncilReinforcedBastionData GetData()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            return EntityManager.GetComponentData<CouncilReinforcedBastionData>(m_SingletonEntity);
        }

        private void SetData(CouncilReinforcedBastionData data)
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            EntityManager.SetComponentData(m_SingletonEntity, data);
        }

        /// <summary>true si `party` a un Bastion Renforcé actif dans CE district précis (comparaison
        /// d'Entity complète — Index + Version — donc fiable après un rechargement).</summary>
        public bool IsReinforcedBastion(PoliticalParty party, Entity districtEntity)
        {
            foreach (var e in GetData().m_Entries)
                if (e.m_Party == party) return e.m_Active && e.m_DistrictEntity == districtEntity;
            return false;
        }

        public bool TryGetReinforcedDistrict(PoliticalParty party, out Entity districtEntity)
        {
            foreach (var e in GetData().m_Entries)
            {
                if (e.m_Party != party) continue;
                districtEntity = e.m_DistrictEntity;
                return e.m_Active;
            }
            districtEntity = Entity.Null;
            return false;
        }

        /// <summary>Districts éligibles pour `party` : Bastion qui vient de se prolonger au-delà de 3
        /// victoires ce cycle-ci et pas encore résolu (choisi ou expiré), à l'exception du district déjà
        /// choisi comme Bastion Renforcé.</summary>
        public System.Collections.Generic.List<Entity> GetEligibleDistricts(PoliticalParty party)
        {
            var result = new System.Collections.Generic.List<Entity>();
            TryGetReinforcedDistrict(party, out Entity currentRbDistrict);

            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    if (!EntityManager.HasComponent<CouncilDistrictData>(d)) continue;
                    var data = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    if (!data.m_ReinforcedBastionEligiblePending) continue;
                    if (data.m_BastionParty != party) continue;
                    if (d == currentRbDistrict) continue; // MODIFIÉ — comparaison d'Entity, plus fiable que .Index
                    result.Add(d);
                }
            }
            finally { districts.Dispose(); }
            return result;
        }

        /// <summary>Écriture brute, sans validation — utilisée en interne par le choix joueur (déjà
        /// validé) et par l'arbitrage IA.</summary>
        private void SetReinforcedBastion(PoliticalParty party, Entity districtEntity, int population)
        {
            var data = GetData();
            var entries = data.m_Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].m_Party != party) continue;
                var e = entries[i];
                e.m_Active = true;
                e.m_DistrictEntity = districtEntity; // MODIFIÉ
                e.m_Population = population;
                entries[i] = e;
                break;
            }
            data.m_Entries = entries;
            SetData(data);
        }

        /// <summary>Choix (ou changement) du Bastion Renforcé par le joueur.</summary>
        public bool TryChooseReinforcedBastion(PoliticalParty party, Entity districtEntity, int population, out string error)
        {
            error = null;
            if (!EntityManager.HasComponent<CouncilDistrictData>(districtEntity))
            {
                error = "District invalide.";
                return false;
            }
            var districtData = EntityManager.GetComponentData<CouncilDistrictData>(districtEntity);
            if (!districtData.m_ReinforcedBastionEligiblePending || districtData.m_BastionParty != party)
            {
                error = "Ce district n'est pas (ou plus) éligible au Bastion Renforcé.";
                return false;
            }

            SetReinforcedBastion(party, districtEntity, population);

            districtData.m_ReinforcedBastionEligiblePending = false;
            EntityManager.SetComponentData(districtEntity, districtData);

            s_Log.Info($"[CouncilReinforcedBastionSystem] Bastion Renforcé choisi : {party} sur district {districtEntity.Index} (population {population}).");
            return true;
        }

        /// <summary>
        /// Arbitrage IA : décision immédiate dès qu'un Bastion IA devient éligible, sur le
        /// seul critère de la population du district (le plus peuplé l'emporte). Aucun changement si
        /// le nouveau district est moins (ou aussi) peuplé que celui déjà détenu.
        /// </summary>
        public void ConsiderAiChoice(PoliticalParty party, Entity districtEntity, int population)
        {
            var data = GetData();
            int currentPopulation = -1;
            bool hasCurrent = false;

            foreach (var e in data.m_Entries)
            {
                if (e.m_Party != party) continue;
                hasCurrent = e.m_Active;
                currentPopulation = e.m_Population;
                break;
            }

            if (hasCurrent && currentPopulation >= population)
            {
                s_Log.Info($"[CouncilReinforcedBastionSystem] IA {party} : district {districtEntity.Index} (pop {population}) " +
                           $"moins intéressant que le Bastion Renforcé actuel (pop {currentPopulation}), aucun changement.");
                return;
            }

            SetReinforcedBastion(party, districtEntity, population);
            s_Log.Info($"[CouncilReinforcedBastionSystem] IA {party} : Bastion Renforcé " +
                       $"{(hasCurrent ? "changé pour" : "attribué à")} le district {districtEntity.Index} (pop {population}).");
        }

        /// <summary>Retire le Bastion Renforcé d'un parti (perte du district, ou reset de slot).</summary>
        public void ClearReinforcedBastion(PoliticalParty party)
        {
            var data = GetData();
            var entries = data.m_Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].m_Party != party) continue;
                if (!entries[i].m_Active) return; // déjà vide, pas d'écriture inutile
                var e = entries[i];
                e.m_Active = false;
                e.m_DistrictEntity = Entity.Null; // MODIFIÉ
                entries[i] = e;
                data.m_Entries = entries;
                SetData(data);
                s_Log.Info($"[CouncilReinforcedBastionSystem] Bastion Renforcé retiré pour {party}.");
                return;
            }
        }
    }
}