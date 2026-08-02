import { ModRegistrar } from "cs2/modding";
import { createElement, Fragment } from "react";
import { HemicyclePanel } from "./HemicyclePanel";
import { AdministrationSection } from "./AdministrationSection";

const register: ModRegistrar = (moduleRegistry) => {
  // Icône + panneau hémicycle, en haut à gauche (résultats ville entière).
  moduleRegistry.append("GameTopLeft", HemicyclePanel);

  const InfoSection = moduleRegistry.registry.get(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx"
  )?.["InfoSection"];

  moduleRegistry.extend(
    "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx",
    "selectedInfoSectionComponents",
    (original: any) => {
      // ResidentsSection est la section vanilla affichée pour un district sélectionné
      // (elle porte déjà isDistrict, wealthKey, ageData — cf. décompilation confirmée).
      // Fragment plutôt que <div> : évite d'ajouter un nœud DOM qui perturberait le
      // système de navigation clavier/manette (même contrainte que sur DistrictNotesMod).
      const originalResidentsSection = original["Game.UI.InGame.ResidentsSection"];
      const WrappedResidentsSection = (props: any) =>
        createElement(
          Fragment,
          null,
          originalResidentsSection ? createElement(originalResidentsSection, props) : null,
          createElement(AdministrationSection(InfoSection), props)
        );

      return {
        ...original,
        "Game.UI.InGame.ResidentsSection": WrappedResidentsSection,
      };
    }
  );
};

export default register;
