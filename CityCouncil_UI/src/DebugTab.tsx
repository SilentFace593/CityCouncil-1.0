import { trigger } from "cs2/api";
import { centeredTabWrapperStyle, centeredTabContentStyle } from "./layoutConstants";

interface DebugButtonDef {
  label: string;
  triggerName: string;
}

// Regroupe tous les boutons [DEBUG] du mod, auparavant dispersés dans HemicyclePanel/ScoreTab.
// Un seul point d'entrée : ajouter un futur bouton debug se fait ici uniquement.
const DEBUG_BUTTONS: DebugButtonDef[] = [
  { label: "Forcer l'étape électorale suivante", triggerName: "debugForceNextElection" },
  { label: "Forcer le contrôle de majorité (bonus)", triggerName: "debugForceMajorityCheck" },
  { label: "Forcer le cycle de cotisation (trésorerie)", triggerName: "debugForceCycleCheck" },
  { label: "Forcer l'expiration des campagnes", triggerName: "debugExpireCampaigns" },
  { label: "Forcer le cycle IA de propagande", triggerName: "debugForceAiCycle" },
  { label: "Forcer le contrôle de la Commission Électorale", triggerName: "debugForceCommissionCheck" },
  { label: "Forcer le contrôle d'élection générale (score)", triggerName: "debugForceGeneralElectionCheck" },
  { label: "Lister tous les bâtiments présents (logs)", triggerName: "debugLogAllBuildings" },
];

function DebugButton({ label, triggerName }: DebugButtonDef) {
  return (
    <button
      onClick={() => trigger("cityCouncil", triggerName)}
      style={{
        marginBottom: "8rem",
        width: "100%",
        background: "rgba(220,80,80,0.85)",
        color: "white",
        border: "none",
        borderRadius: "4rem",
        padding: "8rem 10rem",
        fontSize: "12rem",
        fontWeight: "bold",
        cursor: "pointer",
      }}
    >
      [DEBUG] {label}
    </button>
  );
}

export function DebugTab() {
  return (
   <div style={centeredTabWrapperStyle}>
    <div style={centeredTabContentStyle}>
      <div
        style={{
          color: "rgba(255,180,120,0.9)",
          fontSize: "12rem",
          lineHeight: "16rem",
          marginBottom: "14rem",
          padding: "8rem",
          background: "rgba(255,180,120,0.08)",
          borderRadius: "4rem",
        }}
      >
        Ces outils forcent l'exécution immédiate de cycles normalement basés sur le temps de
        simulation réel. Réservé aux tests — à désactiver dans les Options avant une partie normale.
      </div>

      {DEBUG_BUTTONS.map((b) => (
        <DebugButton key={b.triggerName} {...b} />
      ))}
    </div>
    </div>
  );
}