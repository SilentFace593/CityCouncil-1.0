using System;
using System.Collections.Generic;
using System.Linq;
using CityCouncil;
using static Game.Prefabs.ReplacePrefabSystem;

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
            { PoliticalParty.Republicain, 0.60f },
            { PoliticalParty.Populiste, 0.13f },
            { PoliticalParty.GaucheRadicale, 0.05f },
            { PoliticalParty.Democrate, 0.12f },
            { PoliticalParty.Ecologiste, 0.10f },
        };
        private const float SeniorAbstentionBase = 0.05f;

        private static readonly Dictionary<PoliticalParty, float> AdultBase = new()
        {
            { PoliticalParty.Democrate, 0.25f },
            { PoliticalParty.Ecologiste, 0.20f },
            { PoliticalParty.Republicain, 0.23f },
            { PoliticalParty.Populiste, 0.16f },
            { PoliticalParty.GaucheRadicale, 0.16f },
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
                case WealthLevel.Wretched:
                    // Toutes tranches d'âge confondues, d'après l'énoncé.
                    Boost(shares, PoliticalParty.Populiste, 0.80f);
                    Boost(shares, PoliticalParty.GaucheRadicale, 0.40f);
                    abstention *= 1.65f;
                    break;

                case WealthLevel.Poor:
                    // Comportement identique à l'ancien palier "Pauvre" (inchangé).
                    Boost(shares, PoliticalParty.Populiste, 0.75f);
                    Boost(shares, PoliticalParty.GaucheRadicale, 0.75f);
                    if (isAdult)
                        abstention *= 1.50f;
                    break;

                case WealthLevel.Modest:
                    if (isAdult)
                    {
                        Boost(shares, PoliticalParty.Populiste, 0.30f);
                        Boost(shares, PoliticalParty.GaucheRadicale, 0.40f);
                    }
                    else
                    {
                        Boost(shares, PoliticalParty.Republicain, 0.01f);
                        Boost(shares, PoliticalParty.Populiste, 0.20f);
                    }
                    break;

                case WealthLevel.Comfortable:
                    if (isAdult)
                    {
                        Boost(shares, PoliticalParty.Ecologiste, 0.15f);
                        Boost(shares, PoliticalParty.Democrate, 0.04f);
                    }
                    else
                    {
                        Boost(shares, PoliticalParty.Republicain, 0.04f);
                    }
                    break;

                case WealthLevel.Wealthy:
                    if (isAdult)
                    {
                        Boost(shares, PoliticalParty.Democrate, 0.05f);
                        Boost(shares, PoliticalParty.Ecologiste, 0.15f);
                    }
                    else
                    {
                        Boost(shares, PoliticalParty.Republicain, 0.05f);
                    }
                    break;
            }

            // Renormalisation : la part "exprimée" totale (hors abstention) doit rester cohérente.
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
            ["Energy Consumption Awareness"] = new[] { (PoliticalParty.Ecologiste, 0.12f) },
            ["Recycling"] = new[] { (PoliticalParty.Ecologiste, 0.12f), (PoliticalParty.Populiste, 0.01f) },
            ["Roadside Parking Fee"] = new[] { (PoliticalParty.Populiste, 0.05f), (PoliticalParty.GaucheRadicale, 0.15f) },
            ["Speed Bumps"] = new[] { (PoliticalParty.Populiste, 0.05f) },
            ["Heavy Traffic Ban"] = new[] { (PoliticalParty.Ecologiste, 0.02f) },
            ["Gated Community"] = new[] { (PoliticalParty.Populiste, 0.20f), (PoliticalParty.Republicain, 0.18f) },
            ["Combustion Engine Ban"] = new[] { (PoliticalParty.Ecologiste, -0.20f) },
            ["Urban Cycling Initiative"] = new[] { (PoliticalParty.Ecologiste, 0.08f) },
            ["Bicycle Traffic Restriction"] = new[] { (PoliticalParty.Populiste, 0.05f), (PoliticalParty.Republicain, 0.05f) },
        };

        private static void ApplyPolicyModifiers(Dictionary<PoliticalParty, float> shares, IEnumerable<string> activePolicies, PoliticalParty? playerParty) // MODIFIÉ — ajout playerParty
        {
            foreach (var policyName in activePolicies)
            {
                if (!PolicyModifiers.TryGetValue(policyName, out var mods)) continue;
                foreach (var (party, percent) in mods)
                {
                    // le parti joueur n'est plus juge et partie sur ses propres politiques de district.
                    if (playerParty.HasValue && party == playerParty.Value) continue;
                    Boost(shares, party, percent);
                }
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
        private const float ReinforcedBastionBonusPct = 0.05f;

        /// <summary>
        /// Calcule le résultat du 1er tour pour un district.
        /// </summary>
        /// <param name="seed">Graine aléatoire pour la marge d'erreur (ex. dérivée du district +
        /// du jour de l'élection) — assure une variation entre districts/élections tout en restant
        /// reproductible pour une même graine.</param>
        /// <param name="activeEventEffects">Effets de l'évènement de ville actif, ou null si aucun.</param>
        /// <param name="cityLeadingParty">Parti actuellement majoritaire à l'échelle de la ville,
        /// nécessaire uniquement si un effet cible EventEffectTarget.LeadingPartyCityWide ; null sinon.</param>
        /// 
        // AJOUT — bonus offensif : impact de +3 % sur un district Bastion qui n'appartient pas au
        // détenteur du bonus. Même point du pipeline que le bonus Bastion.
        private const float OffensiveBonusPct = 0.03f;
        private const float EcologistNuclearWealthBonusPct = 0.04f; // Wretched/Poor/Modest, toutes tranches
        private const float EcologistNuclearSeniorBonusPct = 0.04f; // ville entière, séniors uniquement
        public const float VotingInstructionComplianceMin = 0.30f;
        public const float VotingInstructionComplianceMax = 0.70f;

        public static RoundResult ComputeRound1(
    int seniors, int adults, WealthLevel wealth, int seed,
    IEnumerable<string> activePolicies,
    EventEffect[] activeEventEffects = null,
    PoliticalParty? cityLeadingParty = null,
    bool isBastion = false,
    PoliticalParty bastionParty = default,
    bool isReinforcedBastion = false,
    IEnumerable<PoliticalParty> offensiveBonusHolders = null,
    IEnumerable<(PoliticalParty party, CampaignTarget target, float percent)> activeCampaigns = null,
    IEnumerable<DistrictCampaignEntry> districtCampaigns = null,
    IEnumerable<IllegalCampaignEntry> illegalCampaigns = null,
    IEnumerable<SanctionEntry> citySanctions = null,
    bool unemploymentCrisisActive = false,
    float taxDiscontentBonusPopuliste = 0f,
    float taxDiscontentBonusGaucheRadicale = 0f,
    bool ecologistNuclearBonusActive = false,
    PoliticalParty? playerParty = null)
        {
            var rng = new Random(seed);
            var seniorBaseWithMargin = ApplyMarginOfError(SeniorBase, rng, MarginOfErrorPct);
            var adultBaseWithMargin = ApplyMarginOfError(AdultBase, rng, MarginOfErrorPct);

            var (seniorShares, seniorAbst) = ApplyWealthModifiers(seniorBaseWithMargin, SeniorAbstentionBase, wealth, isAdult: false);
            var (adultShares, adultAbst) = ApplyWealthModifiers(adultBaseWithMargin, AdultAbstentionBase, wealth, isAdult: true);

            ApplyEventEffects(seniorShares, ref seniorAbst, isAdult: false, activeEventEffects, cityLeadingParty);
            ApplyEventEffects(adultShares, ref adultAbst, isAdult: true, activeEventEffects, cityLeadingParty);

            // AJOUT — bonus Populiste "colère sociale" si le chômage dépasse le seuil (cf.
                       // CouncilEconomySystem.UnemploymentThresholdPct), city-wide et symétrique séniors/adultes,
                       // même mécanisme que le bonus Bastion ci-dessous.
                        if (unemploymentCrisisActive)
                            {
                Boost(seniorShares, PoliticalParty.Populiste, CouncilEconomySystem.PopulisteUnemploymentBonusPct);
                Boost(adultShares, PoliticalParty.Populiste, CouncilEconomySystem.PopulisteUnemploymentBonusPct);
                            }

            // AJOUT — mécontentement fiscal (colère populaire ou anti-inégalité, jamais les deux
            // à la fois, cf. CouncilTaxSystem.GetTaxDiscontentBonus), Populiste ET GaucheRadicale,
            // symétrique séniors/adultes, même mécanisme que le bonus chômage ci-dessus.
            bool playerIsPopuliste = playerParty.HasValue && playerParty.Value == PoliticalParty.Populiste;
            bool playerIsGaucheRadicale = playerParty.HasValue && playerParty.Value == PoliticalParty.GaucheRadicale;

            if (taxDiscontentBonusPopuliste > 0f && !playerIsPopuliste)
            {
                Boost(seniorShares, PoliticalParty.Populiste, taxDiscontentBonusPopuliste);
                Boost(adultShares, PoliticalParty.Populiste, taxDiscontentBonusPopuliste);
            }
            if (taxDiscontentBonusGaucheRadicale > 0f && !playerIsGaucheRadicale)
            {
                Boost(seniorShares, PoliticalParty.GaucheRadicale, taxDiscontentBonusGaucheRadicale);
                Boost(adultShares, PoliticalParty.GaucheRadicale, taxDiscontentBonusGaucheRadicale);
            }

            if (isBastion)
            {
                float bastionBonus = isReinforcedBastion ? ReinforcedBastionBonusPct : BastionBonusPct;
                Boost(seniorShares, bastionParty, BastionBonusPct);
                Boost(adultShares, bastionParty, BastionBonusPct);
            }

            if (ecologistNuclearBonusActive)
            {
                Boost(seniorShares, PoliticalParty.Ecologiste, EcologistNuclearSeniorBonusPct);

                if (wealth == WealthLevel.Wretched || wealth == WealthLevel.Poor || wealth == WealthLevel.Modest)
                {
                    Boost(seniorShares, PoliticalParty.Ecologiste, EcologistNuclearWealthBonusPct);
                    Boost(adultShares, PoliticalParty.Ecologiste, EcologistNuclearWealthBonusPct);
                }
            }

            if (offensiveBonusHolders != null)
            {
                foreach (var party in offensiveBonusHolders)
                {
                    Boost(seniorShares, party, OffensiveBonusPct);
                    Boost(adultShares, party, OffensiveBonusPct);
                }
            }

            // AJOUT — bonus de campagne de propagande, ciblé par tranche d'âge, city-wide (identique
            // dans tous les districts puisque activeCampaigns provient d'un état ville entière, pas
            // par district).
            if (activeCampaigns != null)
            {
                foreach (var (party, target, percent) in activeCampaigns)
                {
                    switch (target)
                    {
                        case CampaignTarget.Adultes:
                            Boost(adultShares, party, percent);
                            break;
                        case CampaignTarget.Seniors:
                            Boost(seniorShares, party, percent);
                            break;
                        case CampaignTarget.Toute: // AJOUT — campagne digitale : symétrique sur les deux tranches
                            Boost(adultShares, party, percent);
                            Boost(seniorShares, party, percent);
                            break;
                    }
                }
            }

            // AJOUT — campagnes de district, appliquées APRÈS la campagne ville, sur les
            // deux tranches d'âge symétriquement (pas de ciblage d'âge pour ce mécanisme, contrairement
            // à la campagne ville).
            if (districtCampaigns != null)
            {
                foreach (var c in districtCampaigns)
                {
                    switch (c.m_Type)
                    {
                        case DistrictCampaignType.Boost:
                            Boost(seniorShares, c.m_Party, c.m_BonusPercent);
                            Boost(adultShares, c.m_Party, c.m_BonusPercent);
                            break;

                        case DistrictCampaignType.AttackClean:
                        case DistrictCampaignType.AttackDirty:
                            Boost(seniorShares, c.m_TargetParty, -c.m_BonusPercent);
                            Boost(adultShares, c.m_TargetParty, -c.m_BonusPercent);
                            if (c.m_Type == DistrictCampaignType.AttackDirty && c.m_SelfMalusPercent > 0f)
                            {
                                Boost(seniorShares, c.m_Party, -c.m_SelfMalusPercent);
                                Boost(adultShares, c.m_Party, -c.m_SelfMalusPercent);
                            }
                            break;
                    }
                }
            }

            // AJOUT — campagnes illégales : malus pur sur le parti visé, aucun effet sur le lanceur
            // (contrairement à AttackDirty qui a un m_SelfMalusPercent). Le risque encouru par le
            // lanceur passe uniquement par la détection/sanction gérée par CouncilElectoralCommissionSystem,
            // pas par un malus immédiat ici.
            if (illegalCampaigns != null)
            {
                foreach (var c in illegalCampaigns)
                {
                    Boost(seniorShares, c.m_TargetParty, -c.m_MalusPercent);
                    Boost(adultShares, c.m_TargetParty, -c.m_MalusPercent);
                }
            }

            // Sanctions city-wide de la Commission Électorale, appliquées en dernier
            // (après tout le reste), symétriquement sur les deux tranches d'âge.
            if (citySanctions != null)
            {
                foreach (var s in citySanctions)
                {
                    Boost(seniorShares, s.m_Party, -s.m_MalusPercent);
                    Boost(adultShares, s.m_Party, -s.m_MalusPercent);
                }
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

            ApplyPolicyModifiers(combined, activePolicies, playerParty);

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
        /// Construit un dictionnaire d'overrides de report (eliminated, finalist) -> nouvelle part,
        /// à partir des effets TransferOverride de l'évènement de ville actif. Retourne null si
        /// aucun override n'est en jeu, pour éviter d'allouer un dictionnaire vide à chaque tour.
        /// </summary>
        private static Dictionary<(PoliticalParty eliminated, PoliticalParty finalist), float> BuildTransferOverrides(
            EventEffect[] activeEventEffects)
        {
            if (activeEventEffects == null) return null;

            Dictionary<(PoliticalParty, PoliticalParty), float> overrides = null;
            foreach (var effect in activeEventEffects)
            {
                if (effect.Target != EventEffectTarget.TransferOverride) continue;
                overrides ??= new Dictionary<(PoliticalParty, PoliticalParty), float>();
                overrides[(effect.Party, effect.TargetParty)] = effect.Percent;
            }
            return overrides;
        }

        /// <summary>
        /// Résout la part de report pour un couple (éliminé, finaliste) : priorité à un override
        /// d'évènement actif, sinon la valeur normale de TransferMatrix (0 si le couple n'y figure
        /// pas, cohérent avec le comportement existant).
        /// </summary>
        private static float ResolveTransferShare(
            PoliticalParty eliminated, PoliticalParty finalist,
            Dictionary<(PoliticalParty eliminated, PoliticalParty finalist), float> overrides)
        {
                if (overrides != null && overrides.TryGetValue((eliminated, finalist), out float overridden))
                        return overridden;
                return TransferMatrix.TryGetValue((eliminated, finalist), out float t) ? t : 0f;
            }

        /// <summary>
        /// Calcule le résultat du 2e tour à partir des scores du 1er tour et des deux finalistes.
        /// Redistribue les voix des partis éliminés selon TransferMatrix ; le reste s'abstient.
        /// Les votants du 1er tour qui avaient déjà voté pour un finaliste restent acquis.
        /// </summary>
        public static RoundResult ComputeRound2(
            RoundResult round1, int round1TotalPopulation,
            PoliticalParty finalist1, PoliticalParty finalist2,
            EventEffect[] activeEventEffects = null,
            PoliticalParty? instructedEliminatedParty = null,
            PoliticalParty? instructedTarget = null,
            float complianceRoll = 0f)
        {
            float votesF1 = round1.m_VoteShares[finalist1];
            float votesF2 = round1.m_VoteShares[finalist2];
            float newAbstentionShare = 0f;

            var transferOverrides = BuildTransferOverrides(activeEventEffects);

            foreach (var kv in round1.m_VoteShares)
            {
                var eliminated = kv.Key;
                if (eliminated == finalist1 || eliminated == finalist2) continue;

                float share = kv.Value;
                float toF1 = ResolveTransferShare(eliminated, finalist1, transferOverrides);
                float toF2 = ResolveTransferShare(eliminated, finalist2, transferOverrides);
                toF1 = Math.Clamp(toF1, 0f, 1f);
                toF2 = Math.Clamp(toF2, 0f, 1f - toF1);

                // AJOUT — consigne de vote du joueur : une fraction (compliance) des voix du parti
                // éliminé suit la consigne intégralement, le reste suit la matrice normale. Ne
                // s'applique qu'au SEUL parti concerné par la consigne (le parti joueur éliminé).
                if (instructedEliminatedParty.HasValue && eliminated == instructedEliminatedParty.Value
                    && instructedTarget.HasValue && (instructedTarget.Value == finalist1 || instructedTarget.Value == finalist2))
                {
                    float compliance = Math.Clamp(complianceRoll, 0f, 1f);
                    float instructedVotes = share * compliance;
                    float remainder = share - instructedVotes;

                    if (instructedTarget.Value == finalist1) votesF1 += instructedVotes;
                    else votesF2 += instructedVotes;

                    votesF1 += remainder * toF1;
                    votesF2 += remainder * toF2;
                    newAbstentionShare += remainder * (1f - toF1 - toF2);
                    continue;
                }

                votesF1 += share * toF1;
                votesF2 += share * toF2;
                newAbstentionShare += share * (1f - toF1 - toF2);
            }

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
                m_Abstention = (round1TotalPopulation - baseVoters) + abstainingNow,
                m_MajorityReached = true,
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