using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow; // AJOUT, pour GameManager

namespace CityCouncil
{
    public class Mod : IMod
    {
        public static readonly ILog log = LogManager.GetLogger(nameof(CityCouncil))
            .SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info("CityCouncil : chargement.");

            // Enregistrement de la source de localisation en-US : condition nécessaire pour
            // qu'I18n EveryWhere détecte CityCouncil comme mod localisable (cf. panneau
            // Options d'I18n EveryWhere, qui liste les mods ayant appelé AddSource).
            GameManager.instance.localizationManager.AddSource("en-US", new CityCouncilLocaleEN()); // AJOUT
            GameManager.instance.localizationManager.AddSource("fr-FR", new CityCouncilLocaleFR()); // AJOUT

            updateSystem.UpdateAt<CouncilElectionSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilPolicyRegistry>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilCityEventSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilCustomPartySystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilPartyMembershipSystem>(SystemUpdatePhase.GameSimulation);

            updateSystem.UpdateAt<CityCouncil.Systems.CouncilUISystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            log.Info("CityCouncil : déchargement.");
        }
    }
}