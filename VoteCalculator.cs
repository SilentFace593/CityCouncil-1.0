using System;
using System.Collections.Generic;
using System.Linq;

namespace CityCouncil
{
    public struct RoundResult
    {
        public Dictionary<PoliticalParty, float> m_VoteShares; // parmi les exprimés, somme = 1
        public int m_Voters;
        public int m_Abstention;
        public bool m_MajorityReached; // >= 50% dès ce tour
        public PoliticalParty m_Leader;
    }

    public static class VoteCalculator
    {
        // Bases par tranche d'âge (avant modificateurs de richesse), somme = 1 par tranche
        private static readonly Dictionary<PoliticalParty, float> SeniorBase = new()
        {
            { PoliticalParty.Republicain, 0.50f },
            { PoliticalParty.Populiste, 0.15f },
            { PoliticalParty.GaucheRadicale, 0.05f },
            { PoliticalParty.Democrate, 0.20f },
            { PoliticalParty.Ecologiste, 0.10f },
        };
        private const float SeniorAbstentionBase = 0.05f;

        private static readonly Dictionary<PoliticalParty, float> AdultBase = new()
        {
            { PoliticalParty.Democrate, 0.30f },
            { PoliticalParty.Ecologiste, 0.20f },
            { PoliticalParty.Republicain, 0.20f },
            { PoliticalParty.Populiste, 0.15f },
            { PoliticalParty.GaucheRadicale, 0.15f },
        };
        private const float AdultAbstentionBase = 0.25f;

        /// <summary>
        /// Matrice de report des voix au 2e tour : part des voix du parti éliminé
        /// qui se reporte sur chaque finaliste. Le reste va à l'abstention.
        /// Clé : (parti éliminé, parti finaliste destinataire) -> part [0..1]
        /// </summary>
        private static readonly Dictionary<(PoliticalParty eliminated, PoliticalParty finalist), float> TransferMatrix =
            new()
            {
                // Gauche radicale éliminée
                { (PoliticalParty.GaucheRadicale, PoliticalParty.Democrate), 0.55f },
                { (PoliticalParty.GaucheRadicale, PoliticalParty.Republicain), 0.10f },
                { (PoliticalParty.GaucheRadicale, PoliticalParty.Ecologiste), 0.50f },
                { (PoliticalParty.GaucheRadicale, PoliticalParty.Populiste), 0.20f },

                // Ecologiste éliminé
                { (PoliticalParty.Ecologiste, PoliticalParty.Democrate), 0.60f },
                { (PoliticalParty.Ecologiste, PoliticalParty.Republicain), 0.15f },
                { (PoliticalParty.Ecologiste, PoliticalParty.Populiste), 0.15f },
                { (PoliticalParty.Ecologiste, PoliticalParty.GaucheRadicale), 0.55f },

                // Democrate éliminé
                { (PoliticalParty.Democrate, PoliticalParty.Republicain), 0.20f },
                { (PoliticalParty.Democrate, PoliticalParty.Ecologiste), 0.55f },
                { (PoliticalParty.Democrate, PoliticalParty.Populiste), 0.20f },
                { (PoliticalParty.Democrate, PoliticalParty.GaucheRadicale), 0.40f },

                // Populiste éliminé
                { (PoliticalParty.Populiste, PoliticalParty.Democrate), 0.15f },
                { (PoliticalParty.Populiste, PoliticalParty.Republicain), 0.55f },
                { (PoliticalParty.Populiste, PoliticalParty.Ecologiste), 0.10f },
                { (PoliticalParty.Populiste, PoliticalParty.GaucheRadicale), 0.25f },

                // Republicain éliminé
                { (PoliticalParty.Republicain, PoliticalParty.Democrate), 0.15f },
                { (PoliticalParty.Republicain, PoliticalParty.Ecologiste), 0.05f },
                { (PoliticalParty.Republicain, PoliticalParty.Populiste), 0.45f },
                { (PoliticalParty.Republicain, PoliticalParty.GaucheRadicale), 0.05f },
            };

        /// <summary>
        /// Applique les modificateurs de richesse à une base de vote pour une tranche d'âge donnée.
        /// isAdult=true applique les bonus adultes, sinon les bonus séniors.
        /// Retourne des parts renormalisées à 1 (hors abstention) + l'abstention ajustée.
        /// </summary>
        private static (Dictionary<PoliticalParty, float> shares, float abstention) ApplyWealthModifiers(
            Dictionary<PoliticalParty, float> baseShares, float baseAbstention, WealthLevel wealth, bool isAdult)
        {
            var shares = new Dictionary<PoliticalParty, float>(baseShares);
            float abstention = baseAbstention;

            switch (wealth)
            {
                case WealthLevel.Pauvre:
                    // Populiste et Gauche radicale +75% pour toutes tranches d'âge
                    Boost(shares, PoliticalParty.Populiste, 0.75f);
                    Boost(shares, PoliticalParty.GaucheRadicale, 0.75f);
                    // Abstention +50% chez les adultes uniquement
                    if (isAdult)
                        abstention *= 1.50f;
                    break;

                case WealthLevel.Moyen:
                    if (isAdult)
                    {
                        Boost(shares, PoliticalParty.Democrate, 0.02f);
                        Boost(shares, PoliticalParty.Ecologiste, 0.25f);
                    }
                    else
                    {
                        Boost(shares, PoliticalParty.Republicain, 0.10f);
                    }
                    break;

                case WealthLevel.Riche:
                    Boost(shares, PoliticalParty.Democrate, 0.05f);
                    Boost(shares, PoliticalParty.Republicain, 0.05f);
                    Boost(shares, PoliticalParty.Ecologiste, 0.25f);
                    break;
            }

            // Renormalisation : la part "exprimée" totale (hors abstention) doit rester cohérente.
            // On renormalise les parts entre partis pour qu'elles somment à 1, l'abstention
            // étant gérée séparément (elle réduit le nombre de votants, pas les proportions entre partis).
            float sum = shares.Values.Sum();
            var keys = shares.Keys.ToList();
            foreach (var k in keys)
                shares[k] /= sum;

            abstention = Math.Clamp(abstention, 0f, 0.95f);

            return (shares, abstention);
        }

        /// <summary>
        /// Applique une perturbation aléatoire de ±marginPct (en points de pourcentage absolus)
        /// à chaque parti, puis renormalise pour que la somme reste 1. Simule une marge
        /// d'erreur/incertitude électorale sur les bases de vote. Coût négligeable : quelques
        /// tirages aléatoires par tranche d'âge, une fois par district par tour d'élection.
        /// </summary>
        private static Dictionary<PoliticalParty, float> ApplyMarginOfError(
            Dictionary<PoliticalParty, float> shares, Random rng, float marginPct)
        {
            var result = new Dictionary<PoliticalParty, float>(shares);
            var keys = result.Keys.ToList();

            foreach (var k in keys)
            {
                float delta = ((float)rng.NextDouble() * 2f - 1f) * marginPct; // [-marginPct, +marginPct]
                result[k] = Math.Max(0f, result[k] + delta);
            }

            float sum = result.Values.Sum();
            if (sum > 0f)
            {
                foreach (var k in keys)
                    result[k] /= sum;
            }

            return result;
        }

        /// <summary>
        /// Booste un parti de `percent` (ex 0.75 = +75%) au détriment proportionnel des autres.
        /// </summary>
        private static void Boost(Dictionary<PoliticalParty, float> shares, PoliticalParty party, float percent)
        {
            float original = shares[party];
            float gain = original * percent;

            // Le gain est prélevé proportionnellement sur les autres partis
            var others = shares.Keys.Where(k => k != party).ToList();
            float othersSum = others.Sum(k => shares[k]);
            if (othersSum <= 0f) return;

            foreach (var k in others)
                shares[k] -= gain * (shares[k] / othersSum);

            shares[party] = original + gain;
        }

        /// <summary>
        /// Bonus/malus par politique de district active, appliqués sur les parts de voix
        /// combinées (après fusion séniors/adultes), via la même fonction Boost que les
        /// modificateurs de richesse — Boost gère déjà correctement les pourcentages négatifs
        /// (malus) : le montant est simplement prélevé sur le parti ciblé et redistribué
        /// proportionnellement aux autres, au lieu de l'inverse.
        /// </summary>
        private static readonly Dictionary<string, (PoliticalParty party, float percent)[]> PolicyModifiers = new()
        {
            ["Energy Consumption Awareness"] = new[] { (PoliticalParty.Ecologiste, 0.01f) },
            ["Recycling"] = new[] { (PoliticalParty.Ecologiste, 0.01f), (PoliticalParty.Populiste, 0.01f) },
            ["Roadside Parking Fee"] = new[] { (PoliticalParty.Populiste, 0.05f), (PoliticalParty.GaucheRadicale, 0.05f) },
            ["Speed Bumps"] = new[] { (PoliticalParty.Populiste, 0.05f), (PoliticalParty.GaucheRadicale, 0.05f) },
            ["Heavy Traffic Ban"] = new[] { (PoliticalParty.Ecologiste, 0.01f) },
            ["Gated Community"] = new[] { (PoliticalParty.Populiste, 0.20f), (PoliticalParty.Republicain, 0.20f) },
            ["Combustion Engine Ban"] = new[] { (PoliticalParty.Ecologiste, -0.20f) },
            ["Urban Cycling Initiative"] = new[] { (PoliticalParty.Ecologiste, 0.01f) },
            ["Bicycle Traffic Restriction"] = new[] { (PoliticalParty.Populiste, 0.05f), (PoliticalParty.Republicain, 0.05f) },
        };

        private static void ApplyPolicyModifiers(Dictionary<PoliticalParty, float> shares, IEnumerable<string> activePolicies)
        {
            foreach (var policyName in activePolicies)
            {
                if (!PolicyModifiers.TryGetValue(policyName, out var mods)) continue;
                foreach (var (party, percent) in mods)
                    Boost(shares, party, percent);
            }
        }

        /// <summary>
        /// Applique les effets d'un évènement de ville actif (cf. CouncilEventCatalog), par
        /// tranche d'âge — même point du pipeline que les modificateurs de richesse, pour que
        /// les effets AdultsOnly/SeniorsOnly puissent cibler une seule tranche.
        /// </summary>
        private static void ApplyEventEffects(
            Dictionary<PoliticalParty, float> shares, ref float abstention, bool isAdult,
            EventEffect[] effects, PoliticalParty? cityLeadingParty)
        {
            if (effects == null) return;

            foreach (var effect in effects)
            {
                bool scopeMatches = effect.AgeScope == EventAgeScope.All
                    || (effect.AgeScope == EventAgeScope.AdultsOnly && isAdult)
                    || (effect.AgeScope == EventAgeScope.SeniorsOnly && !isAdult);
                if (!scopeMatches) continue;

                switch (effect.Target)
                {
                    case EventEffectTarget.SpecificParty:
                        Boost(shares, effect.Party, effect.Percent);
                        break;

                    case EventEffectTarget.LeadingPartyCityWide:
                        if (cityLeadingParty.HasValue)
                            Boost(shares, cityLeadingParty.Value, effect.Percent);
                        break;

                    case EventEffectTarget.Abstention:
                        abstention = Math.Clamp(abstention + effect.Percent, 0f, 0.95f);
                        break;
                }
            }
        }

        // Marge d'erreur/incertitude électorale : ±2.5 points de pourcentage absolus,
        // tirés aléatoirement par tranche d'âge et par élection. Ajustable ici sans
        // toucher au reste de la logique.
        private const float MarginOfErrorPct = 0.025f;
        // AJOUT — bonus fixe du système Bastion, appliqué symétriquement chez séniors et adultes.
        private const float BastionBonusPct = 0.04f;

        /// <summary>
        /// Calcule le résultat du 1er tour pour un district.
        /// </summary>
        /// <param name="seed">Graine aléatoire pour la marge d'erreur (ex. dérivée du district +
        /// du jour de l'élection) — assure une variation entre districts/élections tout en restant
        /// reproductible pour une même graine.</param>
        /// <param name="activeEventEffects">Effets de l'évènement de ville actif, ou null si aucun.</param>
        /// <param name="cityLeadingParty">Parti actuellement majoritaire à l'échelle de la ville,
        /// nécessaire uniquement si un effet cible EventEffectTarget.LeadingPartyCityWide ; null sinon.</param>
        public static RoundResult ComputeRound1(
       int seniors, int adults, WealthLevel wealth, int seed,
       IEnumerable<string> activePolicies,
       EventEffect[] activeEventEffects = null,
       PoliticalParty? cityLeadingParty = null,
       bool isBastion = false,                        // AJOUT
       PoliticalParty bastionParty = default)          // AJOUT
        {
            var rng = new Random(seed);
            var seniorBaseWithMargin = ApplyMarginOfError(SeniorBase, rng, MarginOfErrorPct);
            var adultBaseWithMargin = ApplyMarginOfError(AdultBase, rng, MarginOfErrorPct);

            var (seniorShares, seniorAbst) = ApplyWealthModifiers(seniorBaseWithMargin, SeniorAbstentionBase, wealth, isAdult: false);
            var (adultShares, adultAbst) = ApplyWealthModifiers(adultBaseWithMargin, AdultAbstentionBase, wealth, isAdult: true);

            ApplyEventEffects(seniorShares, ref seniorAbst, isAdult: false, activeEventEffects, cityLeadingParty);
            ApplyEventEffects(adultShares, ref adultAbst, isAdult: true, activeEventEffects, cityLeadingParty);

            // AJOUT — bonus Bastion, appliqué après évènements/richesse, avant fusion des tranches.
            if (isBastion)
            {
                Boost(seniorShares, bastionParty, BastionBonusPct);
                Boost(adultShares, bastionParty, BastionBonusPct);
            }

            int seniorVoters = (int)Math.Round(seniors * (1f - seniorAbst));
            int adultVoters = (int)Math.Round(adults * (1f - adultAbst));
            int totalVoters = seniorVoters + adultVoters;
            int totalAbstention = (seniors - seniorVoters) + (adults - adultVoters);

            var combined = new Dictionary<PoliticalParty, float>();
            foreach (PoliticalParty p in Enum.GetValues(typeof(PoliticalParty)))
            {
                float votesFromSeniors = seniorShares[p] * seniorVoters;
                float votesFromAdults = adultShares[p] * adultVoters;
                combined[p] = totalVoters > 0 ? (votesFromSeniors + votesFromAdults) / totalVoters : 0f;
            }

            ApplyPolicyModifiers(combined, activePolicies);

            var leader = combined.OrderByDescending(kv => kv.Value).First();

            return new RoundResult
            {
                m_VoteShares = combined,
                m_Voters = totalVoters,
                m_Abstention = totalAbstention,
                m_MajorityReached = leader.Value >= 0.50f,
                m_Leader = leader.Key
            };
        }

        /// <summary>
        /// Calcule le résultat du 2e tour à partir des scores du 1er tour et des deux finalistes.
        /// Redistribue les voix des partis éliminés selon TransferMatrix ; le reste s'abstient.
        /// Les votants du 1er tour qui avaient déjà voté pour un finaliste restent acquis.
        /// </summary>
        public static RoundResult ComputeRound2(
            RoundResult round1, int round1TotalPopulation,
            PoliticalParty finalist1, PoliticalParty finalist2)
        {
            float votesF1 = round1.m_VoteShares[finalist1];
            float votesF2 = round1.m_VoteShares[finalist2];
            float newAbstentionShare = 0f;

            foreach (var kv in round1.m_VoteShares)
            {
                var eliminated = kv.Key;
                if (eliminated == finalist1 || eliminated == finalist2) continue;

                float share = kv.Value;
                float toF1 = TransferMatrix.TryGetValue((eliminated, finalist1), out var t1) ? t1 : 0f;
                float toF2 = TransferMatrix.TryGetValue((eliminated, finalist2), out var t2) ? t2 : 0f;
                // Clamp pour éviter de dépasser 100% par erreur de saisie dans la matrice
                toF1 = Math.Clamp(toF1, 0f, 1f);
                toF2 = Math.Clamp(toF2, 0f, 1f - toF1);

                votesF1 += share * toF1;
                votesF2 += share * toF2;
                newAbstentionShare += share * (1f - toF1 - toF2);
            }

            // Nombre de votants au 1er tour qui participent effectivement au 2e tour
            int baseVoters = round1.m_Voters;
            int abstainingNow = (int)Math.Round(baseVoters * newAbstentionShare);
            int votersRound2 = baseVoters - abstainingNow;

            float total = votesF1 + votesF2;
            var shares = new Dictionary<PoliticalParty, float>
            {
                { finalist1, total > 0 ? votesF1 / total : 0.5f },
                { finalist2, total > 0 ? votesF2 / total : 0.5f },
            };

            var leader = shares[finalist1] >= shares[finalist2] ? finalist1 : finalist2;

            return new RoundResult
            {
                m_VoteShares = shares,
                m_Voters = votersRound2,
                // Abstention totale = ceux qui n'avaient déjà pas voté au 1er tour + les nouveaux abstentionnistes
                m_Abstention = (round1TotalPopulation - baseVoters) + abstainingNow,
                m_MajorityReached = true, // le 2e tour est toujours tranché
                m_Leader = leader
            };
        }

        /// <summary>
        /// Répartition proportionnelle des sièges par la méthode du quotient + plus forts restes.
        /// </summary>
        public static List<PartyResult> AllocateSeats(Dictionary<PoliticalParty, float> voteShares, int totalSeats)
        {
            var quotas = voteShares.ToDictionary(kv => kv.Key, kv => kv.Value * totalSeats);
            var seats = quotas.ToDictionary(kv => kv.Key, kv => (int)Math.Floor(kv.Value));
            int allocated = seats.Values.Sum();
            int remaining = totalSeats - allocated;

            var remainders = quotas
                .OrderByDescending(kv => kv.Value - Math.Floor(kv.Value))
                .Select(kv => kv.Key)
                .ToList();

            for (int i = 0; i < remaining && i < remainders.Count; i++)
                seats[remainders[i]]++;

            return voteShares.Select(kv => new PartyResult
            {
                m_Party = kv.Key,
                m_VoteShare = kv.Value,
                m_Seats = seats.TryGetValue(kv.Key, out var s) ? s : 0
            })
            .OrderByDescending(r => r.m_Seats)
            .ThenByDescending(r => r.m_VoteShare)
            .ToList();
        }

        // Seuil abaissé de 5000 à 2000 hab/siège (test) pour favoriser une meilleure
        // représentation proportionnelle des petits partis via la méthode du plus fort
        // reste — plus de sièges au total = plus de chances qu'un petit parti en obtienne
        // au moins un. Si le monopole Démocrate/Républicain persiste malgré ce changement,
        // le problème vient des pourcentages de boost de richesse (VoteCalculator), pas
        // du nombre de sièges — à revoir dans ce cas.
        private const int SeatPopulationThreshold = 2000;

        public static int ComputeSeatCount(int population)
        {
            if (population <= 0) return 0;
            return 1 + (population - 1) / SeatPopulationThreshold;
        }
    }
}