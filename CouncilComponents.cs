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


        private const int kCurrentDataVersion = 2;

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
                // Compat sauvegardes v1 : aucune série connue avant ce système, on repart à zéro
                // plutôt que de deviner un historique — cohérent avec un ajout de fonctionnalité,
                // pas une régression pour les parties déjà en cours.
                m_StreakParty = default;
                m_StreakCount = 0;
                m_IsBastion = false;
                m_BastionParty = default;
            }
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
            public float m_Members;  // flottant : permet aux pourcentages successifs (+3%/-2%) de s'accumuler proprement sans arrondi prématuré
            public int m_Treasury;   // crédits accumulés
        }

        /// <summary>
        /// Composant SINGLETON (même pattern que CouncilCustomPartyData) contenant les 5 entrées
        /// d'adhérents/trésorerie, gérées par CouncilPartyMembershipSystem.
        /// </summary>
        public struct CouncilPartyMembershipData : IComponentData, ISerializable
        {
            public FixedList512Bytes<PartyMembershipEntry> m_Entries;

            private const int kVersion = 1;

            public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
            {
                writer.Write(kVersion);
                writer.Write(m_Entries.Length);
                for (int i = 0; i < m_Entries.Length; i++)
                {
                    writer.Write((byte)m_Entries[i].m_Party);
                    writer.Write(m_Entries[i].m_Members);
                    writer.Write(m_Entries[i].m_Treasury);
                }
            }

            public void Deserialize<TReader>(TReader reader) where TReader : IReader
            {
                reader.Read(out int _);
                reader.Read(out int count);
                m_Entries = new FixedList512Bytes<PartyMembershipEntry>();
                for (int i = 0; i < count; i++)
                {
                    reader.Read(out byte party);
                    reader.Read(out float members);
                    reader.Read(out int treasury);
                    m_Entries.Add(new PartyMembershipEntry
                    {
                        m_Party = (PoliticalParty)party,
                        m_Members = members,
                        m_Treasury = treasury
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

}