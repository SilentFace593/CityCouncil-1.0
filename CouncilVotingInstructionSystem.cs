using System.Collections.Generic;
using Colossal.Logging;
using Game;

namespace CityCouncil
{
    /// <summary>
    /// Gère les consignes de vote données par le joueur pour le 2e tour, district par district.
    /// Mécanique 100% joueur (aucune IA n'y a accès). Volontairement NON sauvegardé (même
    /// justification que CouncilCityEventSystem) : une consigne n'a de sens que pendant la
    /// fenêtre très courte de l'entre-deux-tours, et disparaît proprement à chaque nouveau cycle
    /// électoral sans qu'aucune ISerializable ne soit nécessaire.
    ///
    /// Cycle de vie d'un district éligible :
    ///   1. Round1Done, parti joueur éliminé -> district "sélectionnable" côté UI.
    ///   2. Le joueur peut cocher/décocher librement un finaliste avant de valider.
    ///   3. SubmitInstructions() fige définitivement les choix envoyés : après cet appel, plus
    ///      aucune modification n'est acceptée pour les districts concernés tant que le cycle
    ///      électoral n'a pas avancé (RunRound2 consomme puis nettoie l'entrée, remise à zéro
    ///      naturelle au districts qui repasse à Round1Scheduled).
    /// </summary>
    public partial class CouncilVotingInstructionSystem : GameSystemBase
    {
        private static readonly ILog s_Log = LogManager.GetLogger("CityCouncil").SetShowsErrorsInUI(false);

        // districtId (Entity.Index) -> parti finaliste choisi par le joueur.
        private readonly Dictionary<int, PoliticalParty> m_Instructions = new();

        // districtId -> true dès que le joueur a cliqué "Valider" pour ce lot (verrouille l'UI,
        // empêche toute modification ultérieure tant que non consommé/nettoyé).
        private readonly HashSet<int> m_SubmittedDistricts = new();

        protected override void OnUpdate() { } // purement passif, piloté par CouncilUISystem/CouncilElectionSystem

        /// <summary>
        /// Fige un lot de consignes en une seule fois (bouton "Valider" côté UI). Écrase toute
        /// consigne précédente non encore consommée pour les districts fournis. Les districts
        /// déjà validés lors d'un appel précédent (non encore consommés) sont ignorés : une
        /// validation est définitive tant qu'elle n'a pas été traitée par RunRound2.
        /// </summary>
        public void SubmitInstructions(IEnumerable<KeyValuePair<int, PoliticalParty>> choices)
        {
            foreach (var kv in choices)
            {
                if (m_SubmittedDistricts.Contains(kv.Key)) continue; // déjà verrouillé, on ne réécrit pas

                m_Instructions[kv.Key] = kv.Value;
                m_SubmittedDistricts.Add(kv.Key);
                s_Log.Info($"[CouncilVotingInstructionSystem] Consigne validée pour district {kv.Key} : {kv.Value}.");
            }
        }

        public bool IsSubmitted(int districtId) => m_SubmittedDistricts.Contains(districtId);

        /// <summary>Lit (sans consommer) la consigne d'un district, si elle existe et a été validée.</summary>
        public bool TryGetInstruction(int districtId, out PoliticalParty target)
        {
            if (m_SubmittedDistricts.Contains(districtId) && m_Instructions.TryGetValue(districtId, out target))
                return true;
            target = default;
            return false;
        }

        /// <summary>
        /// Consomme (retire) la consigne d'un district après résolution du 2e tour — que la
        /// consigne ait été appliquée ou non (ex. parti joueur changé entre-temps), l'entrée ne
        /// doit pas survivre au-delà d'un seul 2e tour.
        /// </summary>
        public void ConsumeInstruction(int districtId)
        {
            m_Instructions.Remove(districtId);
            m_SubmittedDistricts.Remove(districtId);
        }
    }
}