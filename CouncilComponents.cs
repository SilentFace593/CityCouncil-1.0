using Colossal.Serialization.Entities;
using Unity.Collections;
using Unity.Entities;

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
        Pauvre = 0,
        Moyen = 1,
        Riche = 2
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

    /// <summary>
    /// Mappe l'enum vanilla à 5 paliers (Game.UI.InGame.HouseholdWealthKey) sur nos 3 paliers.
    /// </summary>
    public static class WealthMapping
    {
        public static WealthLevel FromHouseholdWealthKey(Game.UI.InGame.HouseholdWealthKey key)
        {
            return key switch
            {
                Game.UI.InGame.HouseholdWealthKey.Wretched => WealthLevel.Pauvre,
                Game.UI.InGame.HouseholdWealthKey.Poor => WealthLevel.Pauvre,
                Game.UI.InGame.HouseholdWealthKey.Modest => WealthLevel.Moyen,
                Game.UI.InGame.HouseholdWealthKey.Comfortable => WealthLevel.Moyen,
                Game.UI.InGame.HouseholdWealthKey.Wealthy => WealthLevel.Riche,
                _ => WealthLevel.Moyen
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

        private const int kCurrentDataVersion = 1;

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

        private const int kVersion = 1;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(kVersion);
            writer.Write(m_Exists);
            writer.Write(m_Name.ToString());
            writer.Write((byte)m_Color);
            writer.Write((byte)m_Space);
            writer.Write(m_PendingDeletion);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int _);
            reader.Read(out m_Exists);
            reader.Read(out string name); m_Name = name;
            reader.Read(out byte color); m_Color = (PartyColor)color;
            reader.Read(out byte space); m_Space = (PoliticalParty)space;
            reader.Read(out m_PendingDeletion);
        }
    }
}