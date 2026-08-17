import { useMemo, useState } from "react";
import { bindValue, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { translatePartyName, PARTY_ORDER, PARTY_COLORS } from "./PartyResultDto";
import eyeClosedIcon from "./images/eye_closed.png";
import eyeSemiClosedIcon from "./images/eye_semiclosed.png";
import eyeOpenIcon from "./images/eye_open.png";

const commissionReportJson$ = bindValue<string>("cityCouncil", "commissionReportJson");
const customPartyExists$ = bindValue<boolean>("cityCouncil", "customPartyExists");
const customPartyName$ = bindValue<string>("cityCouncil", "customPartyName");
const customPartySpace$ = bindValue<string>("cityCouncil", "customPartySpace");
const customPartyPendingActivation$ = bindValue<boolean>("cityCouncil", "customPartyPendingActivation");

interface CommissionReportDto {
  party: string;
  vigilanceLevel: string; // "Low" | "Medium" | "High"
  isPlayer: boolean;
  activeIllegalCount: number;
  hasSanction: boolean;
  sanctionExpiryDay: number;
}

// Icônes 40x40px, cf. CityCouncil_UI/src/images/
const VIGILANCE_ICONS: Record<string, string> = {
  Low: eyeClosedIcon,
  Medium: eyeSemiClosedIcon,
  High: eyeOpenIcon,
};

function VigilanceIcon({ level }: { level: string }) {
  const src = VIGILANCE_ICONS[level] ?? VIGILANCE_ICONS.Low;
  return <img src={src} alt={level} style={{ width: "40rem", height: "40rem", flexShrink: 0 }} />;
}

export function ElectoralCommissionTab() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const reportJson = useValue(commissionReportJson$);
  const customExists = useValue(customPartyExists$);
  const customName = useValue(customPartyName$);
  const customSpace = useValue(customPartySpace$);
  const customPending = useValue(customPartyPendingActivation$);

  const playerControlsAvailable = !!customExists && !customPending && !!customSpace;

  const partyLabel = (partyKey: string): string => {
    const isPlayerHere = playerControlsAvailable && customSpace === partyKey;
    return isPlayerHere ? (customName || partyKey) : translatePartyName(partyKey, translate);
  };
  const report: CommissionReportDto[] = useMemo(() => {
    try {
      const p = JSON.parse(reportJson ?? "[]");
      return Array.isArray(p) ? p.filter((r) => r && typeof r.party === "string") : [];
    } catch {
      return [];
    }
  }, [reportJson]);

  const [selectedKey, setSelectedKey] = useState<string>(PARTY_ORDER[0]);
  const orderedReport = PARTY_ORDER
    .map((key) => report.find((r) => r.party === key))
    .filter((r): r is CommissionReportDto => !!r);
  const selected = orderedReport.find((r) => r.party === selectedKey) ?? orderedReport[0];

  const levelLabel = (level: string): string => {
    switch (level) {
      case "Medium": return t("CityCouncil.Commission.LEVEL_MEDIUM", "Sous surveillance");
      case "High": return t("CityCouncil.Commission.LEVEL_HIGH", "En Alerte !");
      default: return t("CityCouncil.Commission.LEVEL_LOW", "Peu vigilant");
    }
  };

  return (
    <div style={{ display: "flex", width: "100%", height: "100%", boxSizing: "border-box" }}>
      {/* Colonne gauche : liste des partis, cliquable */}
      <div style={{ width: "150rem", flexShrink: 0, borderRight: "1rem solid rgba(255,255,255,0.12)", overflowY: "auto" }}>
        {orderedReport.map((r) => {
          const isSelected = selected && r.party === selected.party;
          const color = PARTY_COLORS[r.party] ?? "#888";
          return (
            <div
              key={r.party}
              onClick={() => setSelectedKey(r.party)}
              style={{
                display: "flex", alignItems: "center", padding: "8rem 10rem", cursor: "pointer",
                background: isSelected ? "rgba(255,255,255,0.10)" : "transparent",
                borderLeft: isSelected ? "3rem solid white" : "3rem solid transparent",
              }}
            >
              <div style={{ width: "10rem", height: "10rem", borderRadius: "50%", background: color, flexShrink: 0, marginRight: "8rem" }} />
              <span style={{ color: "white", fontSize: "12rem", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
  {partyLabel(r.party)}
</span>
            </div>
          );
        })}
      </div>

      {/* Colonne droite : rapport d'activités */}
      <div style={{ flex: 1, padding: "12rem", overflowY: "auto", boxSizing: "border-box" }}>
        {selected && (
          <>
            <div style={{ display: "flex", alignItems: "center", marginBottom: "14rem" }}>
              <VigilanceIcon level={selected.vigilanceLevel} />
              <div style={{ marginLeft: "10rem" }}>
                <div style={{ color: "white", fontSize: "16rem", fontWeight: 700 }}>
  {partyLabel(selected.party)}
                </div>
                <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "13rem" }}>
                  {levelLabel(selected.vigilanceLevel)}
                </div>
              </div>
            </div>

            <div style={{ color: "white", fontSize: "14rem", fontWeight: 600, marginBottom: "10rem" }}>
              {t("CityCouncil.Commission.REPORT_TITLE", "Rapport d'Activités")}
            </div>

            {selected.isPlayer && (
              <div style={{ color: "rgba(255,255,255,0.85)", fontSize: "12rem", marginBottom: "8rem" }}>
                {`${t("CityCouncil.Commission.ACTIVE_ILLEGAL_COUNT", "Campagnes illégales actives")} : ${selected.activeIllegalCount} / 3`}
              </div>
            )}

            {selected.hasSanction ? (
              <div style={{ color: "rgba(255,140,140,0.9)", fontSize: "12rem", fontWeight: 700 }}>
                {t("CityCouncil.Commission.ACTIVE_SANCTION", "Sanction active")}
              </div>
            ) : (
              <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "12rem" }}>
                {t("CityCouncil.Commission.NO_SANCTION", "Aucune sanction active.")}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}