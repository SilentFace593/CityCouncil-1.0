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
    /// Gère les adhérents et la trésorerie des 5 partis. Deux mécanismes distincts :
    ///   - ApplyDistrictSeatDelta : appelé par CouncilElectionSystem à CHAQUE district qui
    ///     obtient de nouveaux résultats finaux -> ±10 adhérents par siège gagné/perdu dans
    ///     CE district précisément (signal local, immédiat).
    ///   - RunCycleCheck (interne, périodique) : une fois par cycle électoral complet (~7 jours
    ///     in-game), calcule la cotisation des adhérents (trésorerie) et applique le bonus de
    ///     +3% (parti vainqueur d'au moins un district, ou majoritaire ville) / malus de -2%
    ///     (sinon) — notion VILLE ENTIÈRE, donc volontairement découplée du traitement
    ///     par-district ci-dessus pour ne pas s'appliquer plusieurs fois par cycle.
    /// </summary>
    public partial class CouncilPartyMembershipSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        // --- Curseurs de gameplay, ajustables librement sans toucher au reste de la logique ---
        private const float CreditsPerMemberPerCycle = 1f;
        private const float WinBonusPct = 0.03f;
        private const float LoseMalusPct = 0.02f;
        private const float MembersPerSeatGained = 10f;
        private const float MembersPerSeatLost = 10f;

        // Même durée que le cycle électoral (CouncilElectionSystem.ElectionCycleDays) : le
        // contrôle "gagne au moins un district / est majoritaire" est réévalué au rythme d'un
        // mandat complet, pas plus souvent.
        private const double CycleCheckIntervalDays = 7.0;

        private EntityQuery m_SingletonQuery;
        private EntityQuery m_DistrictQuery;
        private SimulationSystem m_SimulationSystem;
        private Entity m_SingletonEntity = Entity.Null;

        private double m_LastCycleCheckDay = -1;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_SingletonQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilPartyMembershipData>());
            m_DistrictQuery = GetEntityQuery(ComponentType.ReadOnly<CouncilDistrictData>());
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
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

        // Même remarque que sur CouncilElectionSystem : valeur non confirmée par décompilation.
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 4096;

        protected override void OnUpdate()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();

            double currentDay = (double)m_SimulationSystem.frameIndex / 262144.0;

            if (m_LastCycleCheckDay < 0)
            {
                // Amorce le compteur sans rien appliquer à la toute première frame utile
                // (évite un déclenchement immédiat et arbitraire au chargement).
                m_LastCycleCheckDay = currentDay;
                return;
            }

            if (currentDay - m_LastCycleCheckDay >= CycleCheckIntervalDays)
            {
                m_LastCycleCheckDay = currentDay;
                RunCycleCheck();
            }
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
                    // Même garde-fou que CouncilCustomPartySystem.EnsureSingleton : on garde
                    // l'entité dont le total d'adhérents cumulés est le plus élevé (signe de
                    // données réellement restaurées), on détruit le reste.
                    Entity keep = existing[0];
                    float bestTotal = -1f;
                    foreach (var e in existing)
                    {
                        var data = EntityManager.GetComponentData<CouncilPartyMembershipData>(e);
                        float total = 0f;
                        foreach (var entry in data.m_Entries) total += entry.m_Members;
                        if (total > bestTotal) { bestTotal = total; keep = e; }
                    }
                    foreach (var e in existing)
                    {
                        if (e != keep)
                        {
                            s_Log.Warn($"[CouncilPartyMembershipSystem] Entité singleton en doublon détruite : {e}");
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

            var initial = new CouncilPartyMembershipData { m_Entries = new FixedList512Bytes<PartyMembershipEntry>() };
            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty)))
                initial.m_Entries.Add(new PartyMembershipEntry { m_Party = p, m_Members = 0f, m_Treasury = 0 });

            m_SingletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(m_SingletonEntity, initial);
            s_Log.Info("[CouncilPartyMembershipSystem] Entité singleton créée (5 partis à 0 adhérent).");
        }

        public CouncilPartyMembershipData GetData()
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            return EntityManager.GetComponentData<CouncilPartyMembershipData>(m_SingletonEntity);
        }

        private void SetData(CouncilPartyMembershipData data)
        {
            if (m_SingletonEntity == Entity.Null) EnsureSingleton();
            EntityManager.SetComponentData(m_SingletonEntity, data);
        }

        /// <summary>
        /// Appliqué à chaque district qui obtient de nouveaux résultats finaux (appelé par
        /// CouncilElectionSystem.FinalizeResults, AVANT écrasement de l'ancien résultat) :
        /// ±10 adhérents par siège gagné/perdu dans ce district précisément.
        /// </summary>
        public void ApplyDistrictSeatDelta(IEnumerable<PartyResult> oldResults, IEnumerable<PartyResult> newResults)
        {
            // CORRECTIF — remplace .ToDictionary() (qui plante en cas de clé dupliquée, ex. état
            // corrompu hérité d'une manipulation antérieure) par une accumulation manuelle tolérante :
            // si un parti apparaît plusieurs fois, on garde le dernier sièges rencontré plutôt que de
            // planter. Ne change rien au comportement normal (jamais de doublon en usage standard).
            var oldSeats = new Dictionary<PoliticalParty, int>();
            foreach (var r in oldResults) oldSeats[r.m_Party] = r.m_Seats;

            var newSeats = new Dictionary<PoliticalParty, int>();
            foreach (var r in newResults) newSeats[r.m_Party] = r.m_Seats;

            var data = GetData();
            var entries = data.m_Entries;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                oldSeats.TryGetValue(entry.m_Party, out int oldCount);
                newSeats.TryGetValue(entry.m_Party, out int newCount);
                int delta = newCount - oldCount;

                if (delta > 0)
                    entry.m_Members += delta * MembersPerSeatGained;
                else if (delta < 0)
                    entry.m_Members = MathF.Max(0f, entry.m_Members + delta * MembersPerSeatLost);

                entries[i] = entry;
            }

            data.m_Entries = entries;
            SetData(data);
        }

        public enum TreasurySource
        {
            CityFunding,
            Dues,
            Propaganda // non utilisé par AddTreasury (débit géré dans TrySpendTreasury), gardé pour clarté
        }

        /// <summary>
        /// Crédite (ou débite, si amount négatif) la trésorerie d'un seul parti, en traçant la
        /// provenance dans les compteurs cumulatifs correspondants (uniquement pour un crédit positif
        /// depuis une source suivie — cf. TreasuryBreakdown.tsx côté UI).
        /// </summary>
        public void AddTreasury(PoliticalParty party, int amount, TreasurySource source = TreasurySource.CityFunding)
        {
            if (amount == 0) return;

            var data = GetData();
            var entries = data.m_Entries;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].m_Party != party) continue;
                var entry = entries[i];
                entry.m_Treasury += amount;

                if (amount > 0)
                {
                    switch (source)
                    {
                        case TreasurySource.CityFunding: entry.m_TotalFromCityFunding += amount; break;
                        case TreasurySource.Dues: entry.m_TotalFromDues += amount; break;
                    }
                }

                entries[i] = entry;
                break;
            }

            data.m_Entries = entries;
            SetData(data);
        }

        /// <summary>
        /// Contrôle ville entière périodique : cotisation des adhérents (trésorerie) puis
        /// bonus +3% (parti vainqueur d'au moins un district, ou majoritaire en sièges ville
        /// entière) / malus -2% (sinon). Ne fait rien si aucune élection n'est encore terminée
        /// nulle part (état de tout début de partie).
        /// </summary>
        private void RunCycleCheck()
        {
            var partiesWithAtLeastOneDistrict = new HashSet<PoliticalParty>();
            var totalSeatsByParty = new Dictionary<PoliticalParty, int>();

            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    var districtData = EntityManager.GetComponentData<CouncilDistrictData>(d);
                    if (districtData.m_Phase != ElectionPhase.Completed) continue;

                    partiesWithAtLeastOneDistrict.Add(districtData.m_LeadingParty);

                    foreach (var r in districtData.m_FinalResults)
                    {
                        totalSeatsByParty.TryGetValue(r.m_Party, out int current);
                        totalSeatsByParty[r.m_Party] = current + r.m_Seats;
                    }
                }
            }
            finally
            {
                districts.Dispose();
            }

            if (totalSeatsByParty.Count == 0) return;

            PoliticalParty cityMajority = totalSeatsByParty.OrderByDescending(kv => kv.Value).First().Key;

            var data = GetData();
            var entries = data.m_Entries;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];

                int duesAmount = (int)MathF.Floor(entry.m_Members * CreditsPerMemberPerCycle);
                entry.m_Treasury += duesAmount;
                entry.m_TotalFromDues += duesAmount; // AJOUT — traçage cotisations

                bool wonAtLeastOneDistrict = partiesWithAtLeastOneDistrict.Contains(entry.m_Party);
                bool isCityMajority = entry.m_Party == cityMajority;

                entry.m_Members *= (wonAtLeastOneDistrict || isCityMajority) ? (1f + WinBonusPct) : (1f - LoseMalusPct);
                entry.m_Members = MathF.Max(0f, entry.m_Members);

                entries[i] = entry;
            }

            data.m_Entries = entries;
            SetData(data);

            s_Log.Info("[CouncilPartyMembershipSystem] Cycle d'adhérents traité (cotisations + bonus/malus).");
        }

        /// <summary>
        /// Remet à zéro adhérents et trésorerie d'un parti donné. Utilisé par CouncilCustomPartySystem
        /// au moment où une substitution de parti joueur devient effective (le nouveau parti ne
        /// n'hérite pas du passif de celui qu'il remplace).
        /// </summary>
        public void ResetPartyTreasuryAndMembers(PoliticalParty party)
        {
            var data = GetData();
            var entries = data.m_Entries;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].m_Party != party) continue;
                var entry = entries[i];
                entry.m_Members = 0f;
                entry.m_Treasury = 0;
                entry.m_TotalFromCityFunding = 0;   // AJOUT
                entry.m_TotalFromDues = 0;          // AJOUT
                entry.m_TotalSpentPropaganda = 0;   // AJOUT
                entries[i] = entry;
                break;
            }

            data.m_Entries = entries;
            SetData(data);
        }

        /// <summary>
        /// OUTIL DE DEBUG TEMPORAIRE — force l'exécution immédiate du cycle de cotisation/trésorerie,
        /// sans attendre les 7 jours in-game réels. Même remarque que sur CouncilBonusSystem : ce
        /// système dépend du temps de simulation réel, jamais avancé par le bouton d'élection accélérée.
        /// </summary>
        public void DebugForceCycleCheck()
        {
            double currentDay = (double)m_SimulationSystem.frameIndex / 262144.0;
            m_LastCycleCheckDay = currentDay;
            RunCycleCheck();
            s_Log.Info("[CouncilPartyMembershipSystem] DEBUG : cycle de cotisation exécuté immédiatement.");
        }

        /// <summary>
        /// Débite la trésorerie d'un parti si les fonds sont suffisants. Utilisé par
        /// CouncilPropagandaSystem pour financer les campagnes — trace donc systématiquement dans
        /// m_TotalSpentPropaganda (seul appelant actuel de cette méthode).
        /// </summary>
        public bool TrySpendTreasury(PoliticalParty party, int amount)
        {
            if (amount <= 0) return true;

            var data = GetData();
            var entries = data.m_Entries;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].m_Party != party) continue;
                if (entries[i].m_Treasury < amount) return false;

                var entry = entries[i];
                entry.m_Treasury -= amount;
                entry.m_TotalSpentPropaganda += amount; // AJOUT
                entries[i] = entry;

                data.m_Entries = entries;
                SetData(data);
                return true;
            }

            return false;
        }

    }
}