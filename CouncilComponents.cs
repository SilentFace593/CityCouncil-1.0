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
        Seniors = 1,
        Toute = 2
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
        { CampaignIntensity.Petite, (20000, 0.02f) },
        { CampaignIntensity.Moyenne, (50000, 0.04f) },
        { CampaignIntensity.Forte, (75000, 0.06f) },
    };

        public const double CampaignDurationDays = 7.0;
        public const int DigitalCampaignCost = 90000;
        public const float DigitalCampaignBonusMin = 0.03f;
        public const float DigitalCampaignBonusMax = 0.07f;
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
        public bool m_IsDigital;
    }

    public struct CouncilPropagandaData : IComponentData, ISerializable
    {
        public FixedList512Bytes<CampaignEntry> m_Entries;

        private const int kVersion = 3;

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
                writer.Write(m_Entries[i].m_IsDigital);
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
                var intensity = CampaignIntensity.Petite;

                if (version >= 2)
                {
                    reader.Read(out autoRenew);
                    reader.Read(out byte intensityByte);
                    intensity = (CampaignIntensity)intensityByte;
                }

                bool isDigital = false; // compat < v3
                if (version >= 3)
                {
                    reader.Read(out isDigital);
                }

                m_Entries.Add(new CampaignEntry
                {
                    m_Party = (PoliticalParty)party,
                    m_Active = active,
                    m_Target = (CampaignTarget)target,
                    m_Intensity = intensity,
                    m_BonusPercent = bonus,
                    m_ExpiryDay = expiry,
                    m_AutoRenew = autoRenew,
                    m_IsDigital = isDigital
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
        { CampaignIntensity.Petite, (3000, 0.02f) },
        { CampaignIntensity.Moyenne, (6000, 0.03f) },
        { CampaignIntensity.Forte, (10000, 0.04f) },
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
        public const int MaxActiveCampaignsPerPartyWithUniversityBonus = 5;
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

    public enum PartyStructureType : byte
    {
        Cadres = 0,
        Masse = 1
    }

    /// <summary>
    /// Règles de cotisation/adhérents selon le type de structure d'un parti. Indépendant de
    /// l'espace politique (PoliticalParty) : c'est un paramètre du SLOT actif, choisi librement
    /// par le joueur à la création de son parti, ou fixé par défaut pour les IA (cf.
    /// GetDefaultForSpace, utilisé aussi pour réinitialiser un slot après suppression du parti joueur).
    /// </summary>
    public static class PartyStructureCatalog
    {
        public static readonly System.Collections.Generic.Dictionary<PartyStructureType, (float membersPerSeatGained, float duesPerMember)> Rules = new()
    {
        { PartyStructureType.Cadres, (10f, 45f) },
        { PartyStructureType.Masse, (30f, 10f) },
    };

        /// <summary>Type par défaut d'un parti IA, selon l'espace politique vanilla qu'il occupe.</summary>
        public static PartyStructureType GetDefaultForSpace(PoliticalParty party)
        {
            return party switch
            {
                PoliticalParty.Populiste => PartyStructureType.Masse,
                PoliticalParty.GaucheRadicale => PartyStructureType.Masse,
                _ => PartyStructureType.Cadres, // Democrate, Republicain, Ecologiste
            };
        }
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
    public struct ExclusiveBonusEntry
    {
        public PoliticalParty m_Party;
        public bool m_Unlocked; // AJOUT — vrai dès que la série de 3 a été atteinte au moins une fois pour ce parti/slot
    }

    public struct CouncilBonusData : IComponentData, ISerializable
    {
        public bool m_HasStreak;
        public PoliticalParty m_StreakParty;
        public int m_StreakCount;

        public FixedList512Bytes<CouncilBonusEntry> m_Entries;

        public bool m_PlayerChoicePending;
        public PoliticalParty m_PlayerChoiceSpace;
        public FixedList512Bytes<ExclusiveBonusEntry> m_ExclusiveBonusEntries;

        private const int kVersion = 2; // AJOUT m_ExclusiveBonusEntries -> bump version

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

            writer.Write(m_ExclusiveBonusEntries.Length); // AJOUT
            for (int i = 0; i < m_ExclusiveBonusEntries.Length; i++)
            {
                writer.Write((byte)m_ExclusiveBonusEntries[i].m_Party);
                writer.Write(m_ExclusiveBonusEntries[i].m_Unlocked);
            }
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int version);
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

            m_ExclusiveBonusEntries = new FixedList512Bytes<ExclusiveBonusEntry>();
            if (version >= 2)
            {
                reader.Read(out int exclusiveCount);
                for (int i = 0; i < exclusiveCount; i++)
                {
                    reader.Read(out byte party);
                    reader.Read(out bool unlocked);
                    m_ExclusiveBonusEntries.Add(new ExclusiveBonusEntry { m_Party = (PoliticalParty)party, m_Unlocked = unlocked });
                }
            }
            // Compat v1 : liste vide -> aucun bonus exclusif déverrouillé pour l'instant, EnsureSingleton
            // ne recrée pas l'entité (elle existe déjà), donc CouncilBonusSystem doit gérer une liste
            // possiblement vide/incomplète à la lecture (cf. IsExclusiveBonusUnlocked ci-dessous).
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
        public bool m_ReinforcedBastionEligiblePending;
        public FixedList512Bytes<DistrictCampaignEntry> m_DistrictCampaigns;
        public FixedList512Bytes<IllegalCampaignEntry> m_IllegalCampaigns;


        private const int kCurrentDataVersion = 5;

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
            writer.Write(m_ReinforcedBastionEligiblePending);

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

            m_ReinforcedBastionEligiblePending = false;
            if (dataVersion >= 5)
            {
                reader.Read(out m_ReinforcedBastionEligiblePending);
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
            public long m_TotalSpentPolls; // AJOUT — cumul débité par CouncilPollSystem.TryOrderPoll
            public PartyStructureType m_StructureType;
        }

        /// <summary>
        /// Composant SINGLETON (même pattern que CouncilCustomPartyData) contenant les 5 entrées
        /// d'adhérents/trésorerie, gérées par CouncilPartyMembershipSystem.
        /// </summary>
        public struct CouncilPartyMembershipData : IComponentData, ISerializable
        {
            public FixedList512Bytes<PartyMembershipEntry> m_Entries;

            private const int kVersion = 4; 

            public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
            {
                writer.Write(kVersion);
                writer.Write(m_Entries.Length);
                for (int i = 0; i < m_Entries.Length; i++)
                {
                    writer.Write((byte)m_Entries[i].m_Party);
                    writer.Write(m_Entries[i].m_Members);
                    writer.Write(m_Entries[i].m_Treasury);
                    writer.Write(m_Entries[i].m_TotalFromCityFunding);
                    writer.Write(m_Entries[i].m_TotalFromDues);
                    writer.Write(m_Entries[i].m_TotalSpentPropaganda);
                    writer.Write(m_Entries[i].m_TotalSpentPolls); 
                    writer.Write((byte)m_Entries[i].m_StructureType);
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

                    long fromCity = 0, fromDues = 0, spentPropaganda = 0, spentPolls = 0;
                    if (version >= 2)
                    {
                        reader.Read(out fromCity);
                        reader.Read(out fromDues);
                        reader.Read(out spentPropaganda);
                    }
                    if (version >= 3)
                    {
                        reader.Read(out spentPolls);
                    }

                    var partyEnum = (PoliticalParty)party;
                    var structureType = PartyStructureCatalog.GetDefaultForSpace(partyEnum); // compat < v4
                    if (version >= 4)
                    {
                        reader.Read(out byte structureByte);
                        structureType = (PartyStructureType)structureByte;
                    }

                    m_Entries.Add(new PartyMembershipEntry
                    {
                        m_Party = partyEnum,
                        m_Members = members,
                        m_Treasury = treasury,
                        m_TotalFromCityFunding = fromCity,
                        m_TotalFromDues = fromDues,
                        m_TotalSpentPropaganda = spentPropaganda,
                        m_TotalSpentPolls = spentPolls,
                        m_StructureType = structureType
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
        public PartyStructureType m_StructureType;

        private const int kVersion = 3;

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
            writer.Write((byte)m_StructureType);
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
                m_SubstitutionActive = m_Exists;
                m_ActiveSpace = m_Space;
            }

            if (version >= 3)
            {
                reader.Read(out byte structureByte);
                m_StructureType = (PartyStructureType)structureByte;
            }
            else
            {
                // Compat < v3 : un parti joueur déjà existant était forcément un parti de cadres
                // (comportement d'avant l'introduction de cette mécanique).
                m_StructureType = PartyStructureType.Cadres;
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
        public bool m_FixedAmountPendingDistribution; // true entre validation et 1er FinalizeResults qui suit
       public bool m_AutoRenew; // AJOUT — true = la part fixe se re-verrouille automatiquement chaque cycle, même montant

        private const int kVersion = 3;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(kVersion);
            writer.Write(m_FixedAmount);
            writer.Write(m_FixedAmountLocked);
            writer.Write(m_FixedAmountPendingDistribution);
            writer.Write(m_AutoRenew);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int version);
            reader.Read(out m_FixedAmount);
            reader.Read(out m_FixedAmountLocked);
            m_FixedAmountPendingDistribution = version >= 2 && ReadPending(reader);
            m_AutoRenew = version >= 3 && ReadPending(reader);
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
        public const float MaxMalus = 0.06f;
        public const int MaxActiveCampaignsPerParty = 3;
        public const double CampaignDurationDays = 7.0;

        // Bonus Populiste (Prison01 + 3 victoires) : réduction de 30% sur coût et détection.
        public const float PopulistBonusCostMultiplier = 0.70f;
        public const float PopulistBonusDetectionMultiplier = 0.70f;

        public static (float detectionChance, float escalateChance, VigilanceLevel maxLevelThisCheck) GetRisk(int activeCount)
        {
            return activeCount switch
            {
                1 => (0.33f, 0.50f, VigilanceLevel.Medium),
                2 => (0.70f, 0.85f, VigilanceLevel.High),
                _ => (0.90f, 1.00f, VigilanceLevel.High),
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

    // --- Sondages ---

public struct PollResultEntry
{
    public PoliticalParty m_Party;
    public float m_SharePercent; // 0..1, part parmi les exprimés (même normalisation que VoteCalculator)
}

/// <summary>
/// Composant SINGLETON (même pattern que CouncilBonusData) portant le résultat du DERNIER
/// sondage commandé, géré par CouncilPollSystem. Pas d'historique multi-sondages : un seul
/// jeu de résultats, écrasé à chaque nouveau sondage — cohérent avec le reste du mod qui ne
/// conserve aucune série temporelle (cf. remarque CouncilCityEventSystem).
/// </summary>
public struct CouncilPollData : IComponentData, ISerializable
{
    public bool m_HasResults;
    public double m_LastPollDay; // -1 si aucun sondage n'a jamais été commandé
    public FixedList512Bytes<PollResultEntry> m_LastResults;

    private const int kVersion = 1;

    public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
    {
        writer.Write(kVersion);
        writer.Write(m_HasResults);
        writer.Write(m_LastPollDay);
        writer.Write(m_LastResults.Length);
        for (int i = 0; i < m_LastResults.Length; i++)
        {
            writer.Write((byte)m_LastResults[i].m_Party);
            writer.Write(m_LastResults[i].m_SharePercent);
        }
    }

    public void Deserialize<TReader>(TReader reader) where TReader : IReader
    {
        reader.Read(out int _);
        reader.Read(out m_HasResults);
        reader.Read(out m_LastPollDay);
        reader.Read(out int count);
        m_LastResults = new FixedList512Bytes<PollResultEntry>();
        for (int i = 0; i < count; i++)
        {
            reader.Read(out byte party);
            reader.Read(out float share);
            m_LastResults.Add(new PollResultEntry { m_Party = (PoliticalParty)party, m_SharePercent = share });
        }
    }
}

    // --- Score ---

    public struct ScoreEntry
    {
        public PoliticalParty m_Party;
        public long m_TrophyScore; // cumulatif, jamais décrémenté
    }

    /// <summary>
    /// Composant SINGLETON portant les points de TROPHÉES des 5 partis (conquêtes de district,
    /// conquêtes de majorité générale — jamais retirés), géré par CouncilScoreSystem. La partie
    /// "possession" du score (sièges/bastions/districts actuellement détenus) N'EST PAS stockée
    /// ici : elle est recalculée à la volée par scan des districts à chaque lecture, pour rester
    /// toujours exacte même si le système est ajouté en cours de partie (pas de delta fragile à
    /// faire dériver).
    /// </summary>
    public struct CouncilScoreData : IComponentData, ISerializable
    {
        public FixedList512Bytes<ScoreEntry> m_TrophyEntries;

        // Dernier détenteur connu de la majorité générale — sert à ne déclencher le trophée
        // "élection générale remportée" que sur un CHANGEMENT de majorité (même logique que la
        // conquête de district), pas à chaque cycle de 7 jours où le même parti reste majoritaire.
        public bool m_HasLastGeneralMajority;
        public PoliticalParty m_LastGeneralMajorityParty;

        private const int kVersion = 1;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(kVersion);
            writer.Write(m_TrophyEntries.Length);
            for (int i = 0; i < m_TrophyEntries.Length; i++)
            {
                writer.Write((byte)m_TrophyEntries[i].m_Party);
                writer.Write(m_TrophyEntries[i].m_TrophyScore);
            }
            writer.Write(m_HasLastGeneralMajority);
            writer.Write((byte)m_LastGeneralMajorityParty);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int _);
            reader.Read(out int count);
            m_TrophyEntries = new FixedList512Bytes<ScoreEntry>();
            for (int i = 0; i < count; i++)
            {
                reader.Read(out byte party);
                reader.Read(out long trophy);
                m_TrophyEntries.Add(new ScoreEntry { m_Party = (PoliticalParty)party, m_TrophyScore = trophy });
            }
            reader.Read(out m_HasLastGeneralMajority);
            reader.Read(out byte lastMajority); m_LastGeneralMajorityParty = (PoliticalParty)lastMajority;
        }
    }

    /// <summary>Curseurs de gameplay du système de score — ajustables librement.</summary>
    public static class ScoreCatalog
    {
        public const long PointsPerSeatHeld = 50;
        public const long PointsPerBastionHeld = 600;
        public const long PointsPerReinforcedBastionHeld = 800;
        public const long PointsPerDistrictHeld = 300;
        public const long PointsPerMember = 1;
        public const long PointsGeneralElectionWon = 500;  // trophée, sur changement de majorité générale
        public const long PointsDistrictWon = 150;          // trophée, sur changement de leader d'un district
    }

    /// <summary>Bastion Renforcé détenu par un parti : un seul district à la fois par parti.</summary>
    public struct ReinforcedBastionEntry
    {
        public PoliticalParty m_Party;
        public bool m_Active;        // false = pas de Bastion Renforcé actif pour ce parti
        public int m_DistrictId;     // Entity.Index du district, valide seulement si m_Active
        public int m_Population;
    }

    /// <summary>
    /// Composant SINGLETON (même pattern que CouncilBonusData) portant le Bastion Renforcé des
    /// 5 partis, géré par CouncilElectionSystem (attribution/perte) et lu par CouncilUISystem
    /// (liste d'éligibilité) + VoteCalculator (bonus +1% supplémentaire).
    /// </summary>
    public struct CouncilReinforcedBastionData : IComponentData, ISerializable
    {
        public FixedList512Bytes<ReinforcedBastionEntry> m_Entries;

        private const int kVersion = 2;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(kVersion);
            writer.Write(m_Entries.Length);
            for (int i = 0; i < m_Entries.Length; i++)
            {
                writer.Write((byte)m_Entries[i].m_Party);
                writer.Write(m_Entries[i].m_Active);
                writer.Write(m_Entries[i].m_DistrictId);
                writer.Write(m_Entries[i].m_Population);
            }
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int version);
            reader.Read(out int count);
            m_Entries = new FixedList512Bytes<ReinforcedBastionEntry>();
            for (int i = 0; i < count; i++)
            {
                reader.Read(out byte party);
                reader.Read(out bool active);
                reader.Read(out int districtId);

                int population = 0;
                if (version >= 2)
                {
                    reader.Read(out population);
                }

                m_Entries.Add(new ReinforcedBastionEntry
                {
                    m_Party = (PoliticalParty)party,
                    m_Active = active,
                    m_DistrictId = districtId,
                    m_Population = population
                });
            }
        }
    }

}