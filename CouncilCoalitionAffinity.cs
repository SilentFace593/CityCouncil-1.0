using System.Collections.Generic;

namespace CityCouncil
{
    /// <summary>
    /// Matrice d'affinité politique entre partis, dérivée de VoteCalculator.TransferMatrix
    /// (moyenne des deux sens, puisque le report de voix n'est pas symétrique par nature mais
    /// reste le meilleur proxy déjà présent dans le mod pour représenter une proximité idéologique).
    /// Valeur dans [0,1] : 0 = aucune affinité, 1 = affinité maximale. Sert de probabilité de
    /// base pour la formation d'une coalition IA et de repère visuel pour le joueur.
    /// </summary>
    public static class CouncilCoalitionAffinity
    {
        // Recalculée une seule fois au premier accès (les paires sont figées, pas de dépendance
        // au temps ou à l'état de partie) — coût négligeable de toute façon (10 paires).
        private static readonly Dictionary<(PoliticalParty, PoliticalParty), float> s_Affinity = Build();

        private static Dictionary<(PoliticalParty, PoliticalParty), float> Build()
        {
            var result = new Dictionary<(PoliticalParty, PoliticalParty), float>();
            var parties = (PoliticalParty[])System.Enum.GetValues(typeof(PoliticalParty));

            foreach (var a in parties)
            {
                foreach (var b in parties)
                {
                    if (a == b) continue;
                    if (result.ContainsKey((a, b)) || result.ContainsKey((b, a))) continue;

                    float ab = VoteCalculator.GetTransferShareForCoalition(a, b);
                    float ba = VoteCalculator.GetTransferShareForCoalition(b, a);
                    float affinity = (ab + ba) / 2f;

                    result[(a, b)] = affinity;
                    result[(b, a)] = affinity;
                }
            }
            return result;
        }

        /// <summary>Affinité symétrique entre deux partis distincts, dans [0,1]. 0 si a == b (non défini).</summary>
        public static float GetAffinity(PoliticalParty a, PoliticalParty b)
        {
            if (a == b) return 0f;
            return s_Affinity.TryGetValue((a, b), out var v) ? v : 0f;
        }

        /// <summary>
        /// Affinité moyenne d'un parti candidat vis-à-vis de TOUS les membres déjà présents dans
        /// une coalition en formation — utilisée pour évaluer si l'IA rejoint un groupe existant.
        /// </summary>
        public static float GetAverageAffinity(PoliticalParty candidate, IEnumerable<PoliticalParty> group)
        {
            float sum = 0f;
            int count = 0;
            foreach (var member in group)
            {
                sum += GetAffinity(candidate, member);
                count++;
            }
            return count > 0 ? sum / count : 0f;
        }
    }
}