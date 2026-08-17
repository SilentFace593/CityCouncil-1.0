using Colossal.Serialization.Entities;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using static Colossal.IO.AssetDatabase.AtlasFrame;

namespace CityCouncil
{
    public enum PoliticalParty : byte
    {
        Ecologiste = 0,
        Democrate = 1,
        Populiste = 2,
        Republicain = 3,
        GaucheRadicale = 4
    }

    public enum WealthLevel : byte
    {
        Wretched = 0,
        Poor = 1,
        Modest = 2,
        Comfortable = 3,
        Wealthy = 4
    }

    public enum CampaignTarget : byte
    {
        Adultes = 0,
        Seniors = 1
    }

    public enum CampaignIntensity : byte
    {
        Petite = 0,
        Moyenne = 1,
        Forte = 2
    }

    /// <summary>Catalogue statique coût/bonus par palier — ajustable librement sans toucher au reste.</summary>
    public static class CampaignCatalog
    {
        public static readonly System.Collections.Generic.Dictionary<CampaignIntensity, (int cost, float bonusPct)> Tiers = new()
    {
        { CampaignIntensity.Petite, (25000, 0.02f) },
        { CampaignIntensity.Moyenne, (75000, 0.04f) },
        { CampaignIntensity.Forte, (150000, 0.06f) },
    };

        // Même durée qu'un cycle électoral complet — ajustable indépendamment.
        public const double CampaignDurationDays = 7.0;
    }

    public struct CampaignEntry
    {
        public PoliticalParty m_Party;
        public bool m_Active;
        public CampaignTarget m_Target;
        public CampaignIntensity m_Intensity; // AJOUT — nécessaire pour la reconduction automatique
        public float m_BonusPercent;
        public double m_ExpiryDay;
        public bool m_AutoRenew;
    }

    public struct CouncilPropagandaData : IComponentData, ISerializable
    {
        public FixedList512Bytes<CampaignEntry> m_Entries;

        private const int kVersion = 2; // AJOUT de champ -> bump version

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(kVersion);
            writer.Write(m_Entries.Length);
            for (int i = 0; i < m_Entries.Length; i++)
            {
                writer.Write((byte)m_Entries[i].m_Party);
                writer.Write(m_Entries[i].m_Active);
                writer.Write((byte)m_Entries[i].m_Target);
                writer.Write(m_Entries[i].m_BonusPercent);
                writer.Write(m_Entries[i].m_ExpiryDay);
                writer.Write(m_Entries[i].m_AutoRenew); // AJOUT
                writer.Write((byte)m_Entries[i].m_Intensity);
            }
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int version);
            reader.Read(out int count);
            m_Entries = new FixedList512Bytes<CampaignEntry>();
            for (int i = 0; i < count; i++)
            {
                reader.Read(out byte party);
                reader.Read(out bool active);
                reader.Read(out byte target);
                reader.Read(out float bonus);
                reader.Read(out double expiry);

                bool autoRenew = false;
                var intensity = CampaignIntensity.Petite; // valeur par défaut pour compat v1

                if (version >= 2)
                {
                    reader.Read(out autoRenew);
                    reader.Read(out byte intensityByte);
                    intensity = (CampaignIntensity)intensityByte;
                }

                m_Entries.Add(new CampaignEntry
                {
                    m_Party = (PoliticalParty)party,
                    m_Active = active,
                    m_Target = (CampaignTarget)target,
                    m_Intensity = intensity,
                    m_BonusPercent = bonus,
                    m_ExpiryDay = expiry,
                    m_AutoRenew = autoRenew
                });
            }
        }
    }

    public enum DistrictCampaignType : byte
    {
        Boost = 0,        // classique : boost général du parti dans ce district
        AttackClean = 1,  // ciblée propre : malus sur le parti visé, aucun risque
        AttackDirty = 2   // ciblée sale : malus plus fort sur le parti visé + risque de retour de bâton
    }

    /// <summary>Catalogue coût/effet pour les campagnes de district, indépendant du catalogue ville.</summary>
    public static class DistrictCampaignCatalog
    {
        // Palier "classique" (Boost) : coût -> bonus pour le parti lançant la campagne.
        public static readonly System.Collections.Generic.Dictionary<CampaignIntensity, (int cost, float bonusPct)> BoostTiers = new()
    {
        { CampaignIntensity.Petite, (5000, 0.02f) },
        { CampaignIntensity.Moyenne, (10000, 0.03f) },
        { CampaignIntensity.Forte, (20000, 0.04f) },
    };

        // Attaque propre : coût fixe, malus fixe sur la cible, aucun risque.
        public const int AttackCleanCost = 10000;
        public const float AttackCleanMalus = 0.02f;

        // Attaque sale : coût fixe, malus fixe sur la cible + malus aléatoire [0;5%] sur le lanceur.
        public const int AttackDirtyCost = 15000;
        public const float AttackDirtyMalus = 0.04f;
        public const float AttackDirtySelfMalusMax = 0.05f;

        public const double DistrictCampaignDurationDays = 7.0; // même rythme que le cycle électoral
        public const int MaxActiveCampaignsPerParty = 3;        // point 4 : jusqu'à 3 districts simultanés
    }

    public struct DistrictCampaignEntry
    {
        public PoliticalParty m_Party;          // parti qui a lancé la campagne
        public DistrictCampaignType m_Type;
        public PoliticalParty m_TargetParty;    // valide seulement si AttackClean/AttackDirty
        public float m_BonusPercent;            // Boost : bonus au lanceur. Attack* : malus à la cible (positif, appliqué en négatif)
        public float m_SelfMalusPercent;        // AttackDirty seulement : malus tiré une fois au lancement (0 si Clean/Boost)
        public double m_ExpiryDay;
    }

    /// <summary>
    /// Palette prédéfinie et fermée pour le parti du joueur (pas de color picker libre) :
    /// simple à sérialiser/valider, et évite les couleurs illisibles ou trop proches des
    /// 5 couleurs de partis déjà utilisées dans PARTY_COLORS côté React.
    /// </summary>
    public enum PartyColor : byte
    {
        Rouge = 0,
        Bleu = 1,
        Vert = 2,
        Orange = 3,
        Violet = 4,
        Jaune = 5,
        Cyan = 6,
        Rose = 7,
        Gris = 8,
        Noir = 9
    }

    public enum PermanentBonusType : byte
    {
        None = 0,
        Defensif = 1,
        Offensif = 2
    }
    /// <summary>Bonus permanent détenu par un slot politique (parti vanilla, ou hôte du parti joueur).</summary>
    public struct CouncilBonusEntry
    {
        public PoliticalParty m_Party;
        public PermanentBonusType m_Bonus;
    }

    /// <summary>
    /// Composant SINGLETON (même pattern que CouncilCustomPartyData) géré par CouncilBonusSystem.
    /// Suit la série de victoires consécutives à la majorité du conseil municipal (vérifiée au même
    /// rythme que le cycle de cotisation, cf. CouncilPartyMembershipSystem.RunCycleCheck — duplication
    /// volontaire de la logique de calcul de majorité, même choix de découplage que documenté ailleurs
    /// dans le mod), et les bonus permanents effectivement attribués par slot.
    /// </summary>
    public struct CouncilBonusData : IComponentData, ISerializable
    {
        public bool m_HasStreak;             // false tant qu'aucune majorité n'a encore été observée
        public PoliticalParty m_StreakParty; // valide seulement si m_HasStreak
        public int m_StreakCount;

        public FixedList512Bytes<CouncilBonusEntry> m_Entries; // jusqu'à 5, un par PoliticalParty

        // Choix en attente pour le parti joueur (si le slot en série appartient à sa substitution active).
        public bool m_PlayerChoicePending;
        public PoliticalParty m_PlayerChoiceSpace; // valide seulement si m_PlayerChoicePending

        private const int kVersion = 1;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(kVersion);
            writer.Write(m_HasStreak);
            writer.Write((byte)m_StreakParty);
            writer.Write(m_StreakCount);

            writer.Write(m_Entries.Length);
            for (int i = 0; i < m_Entries.Length; i++)
            {
                writer.Write((byte)m_Entries[i].m_Party);
                writer.Write((byte)m_Entries[i].m_Bonus);
            }

            writer.Write(m_PlayerChoicePending);
            writer.Write((byte)m_PlayerChoiceSpace);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int _);
            reader.Read(out m_HasStreak);
            reader.Read(out byte streakParty); m_StreakParty = (PoliticalParty)streakParty;
            reader.Read(out m_StreakCount);

            reader.Read(out int count);
            m_Entries = new FixedList512Bytes<CouncilBonusEntry>();
            for (int i = 0; i < count; i++)
            {
                reader.Read(out byte party);
                reader.Read(out byte bonus);
                m_Entries.Add(new CouncilBonusEntry { m_Party = (PoliticalParty)party, m_Bonus = (PermanentBonusType)bonus });
            }

            reader.Read(out m_PlayerChoicePending);
            reader.Read(out byte choiceSpace); m_PlayerChoiceSpace = (PoliticalParty)choiceSpace;
        }
    }


    /// <summary>
    /// Mappe l'enum vanilla à 5 paliers (Game.UI.InGame.HouseholdWealthKey) sur nos 3 paliers.
    /// </summary>
    public static class WealthMapping
    {
        public static WealthLevel FromHouseholdWealthKey(Game.UI.InGame.HouseholdWealthKey key)
        {
            return key switch
            {
                Game.UI.InGame.HouseholdWealthKey.Wretched => WealthLevel.Wretched,
                Game.UI.InGame.HouseholdWealthKey.Poor => WealthLevel.Poor,
                Game.UI.InGame.HouseholdWealthKey.Modest => WealthLevel.Modest,
                Game.UI.InGame.HouseholdWealthKey.Comfortable => WealthLevel.Comfortable,
                Game.UI.InGame.HouseholdWealthKey.Wealthy => WealthLevel.Wealthy,
                _ => WealthLevel.Modest
            };
        }
    }

    public enum ElectionPhase : byte
    {
        NoElection = 0,       // 0 habitant -> commission spéciale
        Round1Scheduled = 1,
        Round1Done = 2,       // en attente du 2e tour (ou terminé si majorité dès le 1er tour)
        Round2Scheduled = 3,
        Completed = 4
    }

    public struct PartyResult
    {
        public PoliticalParty m_Party;
        public float m_VoteShare; // 0..1
        public int m_Seats;
    }

    /// <summary>
    /// Composant persistant attaché à chaque entité Game.Areas.District.
    /// </summary>
    public struct CouncilDistrictData : IComponentData, ISerializable
    {
        public ElectionPhase m_Phase;
        public PoliticalParty m_LeadingParty;
        public int m_TotalSeats;

        public int m_VotersRound1;
        public int m_AbstentionRound1;
        public int m_VotersRound2;
        public int m_AbstentionRound2;

        public double m_NextRound1Day;   // jour in-game (simulation time) du prochain 1er tour
        public double m_Round1CompletedDay;
        public bool m_WonInRound1;       // true si un parti a eu >=50% dès le 1er tour

        // Résultats du 1er tour, jusqu'à 5 partis
        public FixedList128Bytes<PartyResult> m_Round1Results;
        // Résultats finaux (après 2e tour ou victoire au 1er tour), jusqu'à 5 partis
        public FixedList128Bytes<PartyResult> m_FinalResults;
        // AJOUT — système Bastion : série de victoires consécutives d'un même parti dans ce district.
        public PoliticalParty m_StreakParty;  // parti actuellement en série (valide seulement si m_StreakCount > 0)
        public int m_StreakCount;             // 0..3, remis à 1 dès qu'un autre parti gagne
        public bool m_IsBastion;              // true dès que m_StreakCount atteint 3
        public PoliticalParty m_BastionParty; // parti détenteur (valide seulement si m_IsBastion)
        public FixedList64Bytes<DistrictCampaignEntry> m_DistrictCampaigns;
        public FixedList64Bytes<IllegalCampaignEntry> m_IllegalCampaigns;


        private const int kCurrentDataVersion = 4; // bump version

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(kCurrentDataVersion);

            writer.Write((byte)m_Phase);
            writer.Write((byte)m_LeadingParty);
            writer.Write(m_TotalSeats);

            writer.Write(m_VotersRound1);
            writer.Write(m_AbstentionRound1);
            writer.Write(m_VotersRound2);
            writer.Write(m_AbstentionRound2);

            writer.Write(m_NextRound1Day);
            writer.Write(m_Round1CompletedDay);
            writer.Write(m_WonInRound1);

            WriteResults(writer, m_Round1Results);
            WriteResults(writer, m_FinalResults);

            writer.Write((byte)m_StreakParty);
            writer.Write(m_StreakCount);
            writer.Write(m_IsBastion);
            writer.Write((byte)m_BastionParty);

            writer.Write(m_DistrictCampaigns.Length);
            for (int i = 0; i < m_DistrictCampaigns.Length; i++)
            {
                var c = m_DistrictCampaigns[i];
                writer.Write((byte)c.m_Party);
                writer.Write((byte)c.m_Type);
                writer.Write((byte)c.m_TargetParty);
                writer.Write(c.m_BonusPercent);
                writer.Write(c.m_SelfMalusPercent);
                writer.Write(c.m_ExpiryDay);
            }

            writer.Write(m_IllegalCampaigns.Length);
            for (int i = 0; i < m_IllegalCampaigns.Length; i++)
            {
                var c = m_IllegalCampaigns[i];
                writer.Write((byte)c.m_Party);
                writer.Write((byte)c.m_TargetParty);
                writer.Write(c.m_MalusPercent);
                writer.Write(c.m_ExpiryDay);
            }
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int dataVersion); // réservé pour absorber de futurs changements de format sans casser les vieilles sauvegardes

            reader.Read(out byte phase); m_Phase = (ElectionPhase)phase;
            reader.Read(out byte leading); m_LeadingParty = (PoliticalParty)leading;
            reader.Read(out m_TotalSeats);

            reader.Read(out m_VotersRound1);
            reader.Read(out m_AbstentionRound1);
            reader.Read(out m_VotersRound2);
            reader.Read(out m_AbstentionRound2);

            reader.Read(out m_NextRound1Day);
            reader.Read(out m_Round1CompletedDay);
            reader.Read(out m_WonInRound1);

            m_Round1Results = ReadResults(reader);
            m_FinalResults = ReadResults(reader);

            if (dataVersion >= 2)
            {
                reader.Read(out byte streakParty); m_StreakParty = (PoliticalParty)streakParty;
                reader.Read(out m_StreakCount);
                reader.Read(out m_IsBastion);
                reader.Read(out byte bastionParty); m_BastionParty = (PoliticalParty)bastionParty;
            }
            else
            {
                m_StreakParty = default;
                m_StreakCount = 0;
                m_IsBastion = false;
                m_BastionParty = default;
            }

            m_DistrictCampaigns = new FixedList64Bytes<DistrictCampaignEntry>();
            if (dataVersion >= 3)
            {
                reader.Read(out int campaignCount);
                for (int i = 0; i < campaignCount; i++)
                {
                    reader.Read(out byte party);
                    reader.Read(out byte type);
                    reader.Read(out byte target);
                    reader.Read(out float bonus);
                    reader.Read(out float selfMalus);
                    reader.Read(out double expiry);
                    m_DistrictCampaigns.Add(new DistrictCampaignEntry
                    {
                        m_Party = (PoliticalParty)party,
                        m_Type = (DistrictCampaignType)type,
                        m_TargetParty = (PoliticalParty)target,
                        m_BonusPercent = bonus,
                        m_SelfMalusPercent = selfMalus,
                        m_ExpiryDay = expiry
                    });
                }
            }

            m_IllegalCampaigns = new FixedList64Bytes<IllegalCampaignEntry>();
            if (dataVersion >= 4)
            {
                reader.Read(out int illegalCount);
                for (int i = 0; i < illegalCount; i++)
                {
                    reader.Read(out byte party);
                    reader.Read(out byte target);
                    reader.Read(out float malus);
                    reader.Read(out double expiry);
                    m_IllegalCampaigns.Add(new IllegalCampaignEntry
                    {
                        m_Party = (PoliticalParty)party,
                        m_TargetParty = (PoliticalParty)target,
                        m_MalusPercent = malus,
                        m_ExpiryDay = expiry
                    });
                }
            }
            // Compat v1/v2/v3 : aucune campagne illégale connue avant ce système, liste vide par défaut.
        }

        /// <summary>
        /// Un parti (adhérents + trésorerie). 5 entrées fixes, une par PoliticalParty — le parti
        /// joueur ne possède pas d'entrée séparée : il hérite adhérents/trésorerie du parti qu'il
        /// remplace visuellement (cf. m_Space dans CouncilCustomPartyData), cohérent avec le choix
        /// déjà fait de "remplacement d'affichage" plutôt que 6e parti indépendant.
        /// </summary>
        public struct PartyMembershipEntry
        {
            public PoliticalParty m_Party;
            public float m_Members;
            public int m_Treasury;

            // AJOUT — compteurs cumulatifs par source, jamais décrémentés (sauf reset complet du
            // parti via ResetPartyTreasuryAndMembers). Ne représentent PAS le solde courant : servent
            // uniquement à l'affichage détaillé côté "Forces Politiques" (cf. TreasuryBreakdown.tsx).
            public long m_TotalFromCityFunding;   // versé par CouncilFundingSystem (part fixe + variable)
            public long m_TotalFromDues;          // cotisations, cf. CouncilPartyMembershipSystem.RunCycleCheck
            public long m_TotalSpentPropaganda;   // débité par CouncilPropagandaSystem.TryLaunchCampaign
        }

        /// <summary>
        /// Composant SINGLETON (même pattern que CouncilCustomPartyData) contenant les 5 entrées
        /// d'adhérents/trésorerie, gérées par CouncilPartyMembershipSystem.
        /// </summary>
        public struct CouncilPartyMembershipData : IComponentData, ISerializable
        {
            public FixedList512Bytes<PartyMembershipEntry> m_Entries;

            private const int kVersion = 2; // AJOUT des 3 compteurs -> bump version

            public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
            {
                writer.Write(kVersion);
                writer.Write(m_Entries.Length);
                for (int i = 0; i < m_Entries.Length; i++)
                {
                    writer.Write((byte)m_Entries[i].m_Party);
                    writer.Write(m_Entries[i].m_Members);
                    writer.Write(m_Entries[i].m_Treasury);
                    writer.Write(m_Entries[i].m_TotalFromCityFunding);   // AJOUT
                    writer.Write(m_Entries[i].m_TotalFromDues);          // AJOUT
                    writer.Write(m_Entries[i].m_TotalSpentPropaganda);   // AJOUT
                }
            }

            public void Deserialize<TReader>(TReader reader) where TReader : IReader
            {
                reader.Read(out int version);
                reader.Read(out int count);
                m_Entries = new FixedList512Bytes<PartyMembershipEntry>();
                for (int i = 0; i < count; i++)
                {
                    reader.Read(out byte party);
                    reader.Read(out float members);
                    reader.Read(out int treasury);

                    long fromCity = 0, fromDues = 0, spentPropaganda = 0;
                    if (version >= 2)
                    {
                        reader.Read(out fromCity);
                        reader.Read(out fromDues);
                        reader.Read(out spentPropaganda);
                    }
                    // Compat sauvegardes v1 : aucun historique connu avant ce système, on repart à 0
                    // plutôt que de deviner une provenance rétroactive au solde déjà accumulé.

                    m_Entries.Add(new PartyMembershipEntry
                    {
                        m_Party = (PoliticalParty)party,
                        m_Members = members,
                        m_Treasury = treasury,
                        m_TotalFromCityFunding = fromCity,
                        m_TotalFromDues = fromDues,
                        m_TotalSpentPropaganda = spentPropaganda
                    });
                }
            }
        }

        private static void WriteResults<TWriter>(TWriter writer, FixedList128Bytes<PartyResult> results)
            where TWriter : IWriter
        {
            writer.Write(results.Length);
            for (int i = 0; i < results.Length; i++)
            {
                writer.Write((byte)results[i].m_Party);
                writer.Write(results[i].m_VoteShare);
                writer.Write(results[i].m_Seats);
            }
        }

        private static FixedList128Bytes<PartyResult> ReadResults<TReader>(TReader reader)
            where TReader : IReader
        {
            var list = new FixedList128Bytes<PartyResult>();
            reader.Read(out int count);
            for (int i = 0; i < count; i++)
            {
                reader.Read(out byte party);
                reader.Read(out float share);
                reader.Read(out int seats);
                list.Add(new PartyResult
                {
                    m_Party = (PoliticalParty)party,
                    m_VoteShare = share,
                    m_Seats = seats
                });
            }
            return list;
        }
    }

    /// <summary>
    /// Tag ajouté pour marquer les districts sans habitant (gérés par une commission spéciale).
    /// </summary>
    public struct CouncilNoElectionTag : IComponentData { }

    /// <summary>
    /// Données du parti créé par le joueur. Composant SINGLETON : une seule entité dans le
    /// monde, créée et gérée par CouncilCustomPartySystem (même pattern que CouncilDistrictData,
    /// mais sur une entité dédiée plutôt que sur un district).
    ///
    /// Choix design : le parti du joueur REMPLACE l'affichage (nom + couleur) du parti existant
    /// correspondant à son bord politique (m_Space), plutôt que d'être un 6e parti indépendant.
    /// Aucun impact sur VoteCalculator (bases de vote, transferts de voix, modificateurs de
    /// richesse/politiques/évènements) — uniquement un habillage visuel résolu côté
    /// CouncilUISystem au moment de sérialiser les résultats vers React.
    /// </summary>
    public struct CouncilCustomPartyData : IComponentData, ISerializable
    {
        public bool m_Exists;              // false = pas de parti joueur créé
        public FixedString64Bytes m_Name;
        public PartyColor m_Color;
        public PoliticalParty m_Space;     // bord politique choisi = parti "hôte" habillé

        // Une suppression demandée par le joueur ne doit prendre effet qu'à la PROCHAINE
        // élection (cf. énoncé), pas immédiatement en pleine mandature. On distingue donc
        // l'état "vivant" affiché (m_Exists) de l'intention en attente (m_PendingDeletion),
        // résolue par CouncilCustomPartySystem.ApplyPendingChangesForNewElection().
        public bool m_PendingDeletion;
        // AJOUT — substitution différée : la décoration visuelle (nom/couleur remplaçant l'hôte)
        // ne s'active qu'à la prochaine élection (cf. ApplyPendingChangesForNewElection), pas
        // immédiatement à la création/au changement de bord.
        public bool m_SubstitutionActive;
        public PoliticalParty m_ActiveSpace;   // bord réellement substitué (valide seulement si m_SubstitutionActive)

        private const int kVersion = 2;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(kVersion);
            writer.Write(m_Exists);
            writer.Write(m_Name.ToString());
            writer.Write((byte)m_Color);
            writer.Write((byte)m_Space);
            writer.Write(m_PendingDeletion);
            writer.Write(m_SubstitutionActive);
            writer.Write((byte)m_ActiveSpace);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int version);
            reader.Read(out m_Exists);
            reader.Read(out string name); m_Name = name;
            reader.Read(out byte color); m_Color = (PartyColor)color;
            reader.Read(out byte space); m_Space = (PoliticalParty)space;
            reader.Read(out m_PendingDeletion);

            if (version >= 2)
            {
                reader.Read(out m_SubstitutionActive);
                reader.Read(out byte activeSpace); m_ActiveSpace = (PoliticalParty)activeSpace;
            }
            else
            {
                // Compat sauvegardes v1 : un parti déjà existant à l'époque était immédiatement actif
                // (ancien comportement), on préserve ce comportement pour ne pas casser une partie en cours.
                m_SubstitutionActive = m_Exists;
                m_ActiveSpace = m_Space;
            }
        }
    }


    /// <summary>
    /// Composant SINGLETON (même pattern que CouncilCustomPartyData) portant le paramètre de
    /// financement fixe de la vie politique, géré par CouncilFundingSystem.
    /// </summary>
    public struct CouncilFundingData : IComponentData, ISerializable
    {
        public int m_FixedAmount;
        public bool m_FixedAmountLocked;
        public bool m_FixedAmountPendingDistribution; // AJOUT — true entre validation et 1er FinalizeResults qui suit

        private const int kVersion = 2; // AJOUT champ -> bump version

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(kVersion);
            writer.Write(m_FixedAmount);
            writer.Write(m_FixedAmountLocked);
            writer.Write(m_FixedAmountPendingDistribution);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int version);
            reader.Read(out m_FixedAmount);
            reader.Read(out m_FixedAmountLocked);
            // Compat sauvegardes v1 : le champ n'existait pas, donc rien en attente par défaut.
            m_FixedAmountPendingDistribution = version >= 2 && ReadPending(reader);
        }

        private static bool ReadPending<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out bool pending);
            return pending;
        }

    }

    /// <summary>
    /// Catalogue des libellés de "fausses factures" affichés sur les mouvements de caisse noire.
    /// Tiré aléatoirement à chaque transfert (point 10), sans lien avec le sens du mouvement.
    /// Les clés de localisation vivent dans LocaleKeys (BlackFund_Invoice1..4).
    /// </summary>
    public static class BlackFundInvoiceCatalog
    {
        public static readonly string[] InvoiceLocaleKeys =
        {
        LocaleKeys.BlackFund_Invoice1,
        LocaleKeys.BlackFund_Invoice2,
        LocaleKeys.BlackFund_Invoice3,
        LocaleKeys.BlackFund_Invoice4,
    };
    }

    /// <summary>Entrée caisse noire d'un parti (5 entrées fixes, une par PoliticalParty).</summary>
    public struct BlackFundEntry
    {
        public PoliticalParty m_Party;
        public bool m_Active;
        public int m_Balance;
    }

    /// <summary>
    /// Composant SINGLETON (même pattern que CouncilPartyMembershipData) portant la caisse noire
    /// des 5 partis, géré par CouncilBlackFundSystem. Volontairement séparé de
    /// CouncilPartyMembershipData : la caisse noire est un compte "hors livre" distinct de la
    /// trésorerie officielle, avec ses propres règles (fermeture = perte totale, pas de cotisation
    /// automatique dessus, etc.) — mélanger les deux complexifierait inutilement la structure
    /// existante pour un concept fondamentalement différent.
    /// </summary>
    public struct CouncilBlackFundData : IComponentData, ISerializable
    {
        public FixedList512Bytes<BlackFundEntry> m_Entries;

        private const int kVersion = 1;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(kVersion);
            writer.Write(m_Entries.Length);
            for (int i = 0; i < m_Entries.Length; i++)
            {
                writer.Write((byte)m_Entries[i].m_Party);
                writer.Write(m_Entries[i].m_Active);
                writer.Write(m_Entries[i].m_Balance);
            }
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int _);
            reader.Read(out int count);
            m_Entries = new FixedList512Bytes<BlackFundEntry>();
            for (int i = 0; i < count; i++)
            {
                reader.Read(out byte party);
                reader.Read(out bool active);
                reader.Read(out int balance);
                m_Entries.Add(new BlackFundEntry
                {
                    m_Party = (PoliticalParty)party,
                    m_Active = active,
                    m_Balance = balance
                });
            }
        }
    }

    public enum VigilanceLevel : byte
    {
        Low = 0,     // "Peu vigilant"
        Medium = 1,  // "Sous surveillance"
        High = 2     // "En Alerte !"
    }

    public struct VigilanceEntry
    {
        public PoliticalParty m_Party;
        public VigilanceLevel m_Level;
    }

    /// <summary>Sanction city-wide temporaire infligée à un parti détecté (malus -10%, 7 jours).</summary>
    public struct SanctionEntry
    {
        public PoliticalParty m_Party;
        public float m_MalusPercent; // positif, appliqué en négatif par VoteCalculator
        public double m_ExpiryDay;
    }

    /// <summary>
    /// Composant SINGLETON (même pattern que CouncilBonusData) portant la vigilance des 5 partis
    /// et les sanctions city-wide actives, géré par CouncilElectoralCommissionSystem.
    /// </summary>
    public struct CouncilElectoralCommissionData : IComponentData, ISerializable
    {
        public FixedList512Bytes<VigilanceEntry> m_VigilanceEntries; // 5 entrées, une par PoliticalParty
        public FixedList512Bytes<SanctionEntry> m_Sanctions;         // sanctions actives (rarement plus d'une ou deux à la fois)

        private const int kVersion = 1;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(kVersion);

            writer.Write(m_VigilanceEntries.Length);
            for (int i = 0; i < m_VigilanceEntries.Length; i++)
            {
                writer.Write((byte)m_VigilanceEntries[i].m_Party);
                writer.Write((byte)m_VigilanceEntries[i].m_Level);
            }

            writer.Write(m_Sanctions.Length);
            for (int i = 0; i < m_Sanctions.Length; i++)
            {
                writer.Write((byte)m_Sanctions[i].m_Party);
                writer.Write(m_Sanctions[i].m_MalusPercent);
                writer.Write(m_Sanctions[i].m_ExpiryDay);
            }
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int _);

            reader.Read(out int vigilanceCount);
            m_VigilanceEntries = new FixedList512Bytes<VigilanceEntry>();
            for (int i = 0; i < vigilanceCount; i++)
            {
                reader.Read(out byte party);
                reader.Read(out byte level);
                m_VigilanceEntries.Add(new VigilanceEntry { m_Party = (PoliticalParty)party, m_Level = (VigilanceLevel)level });
            }

            reader.Read(out int sanctionCount);
            m_Sanctions = new FixedList512Bytes<SanctionEntry>();
            for (int i = 0; i < sanctionCount; i++)
            {
                reader.Read(out byte party);
                reader.Read(out float malus);
                reader.Read(out double expiry);
                m_Sanctions.Add(new SanctionEntry { m_Party = (PoliticalParty)party, m_MalusPercent = malus, m_ExpiryDay = expiry });
            }
        }
    }

    /// <summary>Catalogue de la campagne illégale de district (coût fixe, malus aléatoire).</summary>
    public static class IllegalCampaignCatalog
    {
        public const int Cost = 10000;
        public const float MaxMalus = 0.06f; // malus tiré aléatoirement entre 0 et 6%
        public const int MaxActiveCampaignsPerParty = 3; // compteur SÉPARÉ des campagnes classiques (point 4)
        public const double CampaignDurationDays = 7.0;

        /// <summary>
        /// Probabilité de détection et probabilité/plafond d'escalade de vigilance en fonction du
        /// nombre de campagnes illégales actives par le parti au moment du contrôle (1, 2 ou 3+).
        /// </summary>
        public static (float detectionChance, float escalateChance, VigilanceLevel maxLevelThisCheck) GetRisk(int activeCount)
        {
            return activeCount switch
            {
                1 => (0.33f, 0.50f, VigilanceLevel.Medium), // point : "reste en niveau 1 ou 2"
                2 => (0.70f, 0.85f, VigilanceLevel.High),
                _ => (0.90f, 1.00f, VigilanceLevel.High),   // 3 campagnes ou plus : escalade quasi garantie
            };
        }
    }

    public struct IllegalCampaignEntry
    {
        public PoliticalParty m_Party;
        public PoliticalParty m_TargetParty;
        public float m_MalusPercent; // tiré une fois au lancement (0 à 6%), fixe pour la durée de la campagne
        public double m_ExpiryDay;
    }

}