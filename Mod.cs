using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;

namespace CityCouncil
{
    public class Mod : IMod
    {
        public static readonly ILog log = LogManager.GetLogger(nameof(CityCouncil))
            .SetShowsErrorsInUI(false);

        /// <summary>Instance statique du Setting, accessible depuis CouncilUISystem pour lire ShowDebugTab.</summary>
        public static Setting Instance { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info("CityCouncil : chargement.");

            GameManager.instance.localizationManager.AddSource("en-US", new CityCouncilLocaleEN());
            GameManager.instance.localizationManager.AddSource("fr-FR", new CityCouncilLocaleFR());

            // AJOUT — création et enregistrement du Setting dans le menu Options du jeu.
            Instance = new Setting(this);
            Instance.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Instance));
            GameManager.instance.localizationManager.AddSource("fr-FR", new LocaleFR(Instance));
            AssetDatabase.global.LoadSettings(nameof(CityCouncil), Instance, new Setting(this));

            updateSystem.UpdateAt<CouncilElectionSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilPolicyRegistry>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilCityEventSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilCustomPartySystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilPartyMembershipSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilBonusSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilPropagandaSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilFundingSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilBlackFundSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilElectoralCommissionSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilPollSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilScoreSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilEconomySystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilTaxSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilInstitutionSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CouncilVotingInstructionSystem>(SystemUpdatePhase.GameSimulation);

            updateSystem.UpdateAt<CityCouncil.Systems.CouncilUISystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            log.Info("CityCouncil : déchargement.");
            if (Instance != null)
            {
                Instance.UnregisterInOptionsUI();
                Instance = null;
            }
        }
    }
}