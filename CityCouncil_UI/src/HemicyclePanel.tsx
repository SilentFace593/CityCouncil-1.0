import { Component, useMemo, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Panel } from "cs2/ui";
import { useLocalization } from "cs2/l10n";
import { Scrollable } from "cs2/ui";
import { translatePartyName, PARTY_LABELS, PARTY_ORDER, resolvePartyColor, resolvePartyLabel, PartyLogo, type PartyResultDto } from "./PartyResultDto";
import { YourPartyTab } from "./YourPartyTab";
import { PoliticalForcesTab } from "./PoliticalForcesTab";
import { FundingTab } from "./FundingTab";
import { PropagandaTab } from "./PropagandaTab"; 
import { ElectoralCommissionTab } from "./ElectoralCommissionTab";
import { PollTab } from "./PollTab";
import { ScoreTab } from "./ScoreTab";
import { RulesTab } from "./RulesTab";
import { DebugTab } from "./DebugTab";
import { centeredTabWrapperStyle, centeredTabContentStyle } from "./layoutConstants";

// --- Bindings exposés par CouncilUISystem.cs (group "cityCouncil") ---
const hemicycleSeatsJson$ = bindValue<string>("cityCouncil", "hemicycleSeatsJson");
const hemicycleLeader$ = bindValue<string>("cityCouncil", "hemicycleLeader");
const playerBonusChoicePending$ = bindValue<boolean>("cityCouncil", "playerBonusChoicePending");
const playerBonusChoiceSpace$ = bindValue<string>("cityCouncil", "playerBonusChoiceSpace");
const showDebugTab$ = bindValue<boolean>("cityCouncil", "showDebugTab");

const scrollbarStyle = `
  .cc-scrollable::-webkit-scrollbar {
    width: 8rem;
  }
  .cc-scrollable::-webkit-scrollbar-track {
    background: rgba(255,255,255,0.05);
  }
  .cc-scrollable::-webkit-scrollbar-thumb {
    background: rgba(255,255,255,0.25);
    border-radius: 4rem;
  }
  .cc-scrollable::-webkit-scrollbar-thumb:hover {
    background: rgba(255,255,255,0.4);
  }
`;


// Error Boundary
class SafeBoundary extends Component<{ children: any }, { crashed: boolean }> {
  constructor(props: any) {
    super(props);
    this.state = { crashed: false };
  }
  static getDerivedStateFromError() {
    return { crashed: true };
  }
  render() {
    if (this.state.crashed) return null;
    return this.props.children;
  }
}

function polarPoint(cx: number, cy: number, r: number, angleDeg: number) {
  const rad = (angleDeg * Math.PI) / 180;
  return { x: cx + r * Math.cos(rad), y: cy + r * Math.sin(rad) };
}

function sectorPath(cx: number, cy: number, rInner: number, rOuter: number, startAngle: number, endAngle: number) {
  const p1 = polarPoint(cx, cy, rOuter, startAngle);
  const p2 = polarPoint(cx, cy, rOuter, endAngle);
  const p3 = polarPoint(cx, cy, rInner, endAngle);
  const p4 = polarPoint(cx, cy, rInner, startAngle);
  const largeArc = endAngle - startAngle > 180 ? 1 : 0;
  return [
    `M ${p1.x} ${p1.y}`,
    `A ${rOuter} ${rOuter} 0 ${largeArc} 1 ${p2.x} ${p2.y}`,
    `L ${p3.x} ${p3.y}`,
    `A ${rInner} ${rInner} 0 ${largeArc} 0 ${p4.x} ${p4.y}`,
    "Z",
  ].join(" ");
}

function HemicycleFan({ results }: { results: PartyResultDto[] }) {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const totalSeats = results.reduce((sum, r) => sum + r.seats, 0);
  if (totalSeats === 0) {
    return (
      <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "14rem", padding: "16rem 0" }}>
        {t("CityCouncil.Hemicycle.NO_ELECTION", "Aucune élection terminée pour le moment.")}
      </div>
    );
  }

  const byParty: Record<string, PartyResultDto> = {};
  for (const r of results) byParty[r.party] = r;

  const cx = 275;
  const cy = 220;
  const rInner = 85;
  const rOuter = 195;

  let angle = 180;
  const segments = PARTY_ORDER.map((party) => {
    const r = byParty[party];
    if (!r || r.seats <= 0) return null;
    const width = (r.seats / totalSeats) * 180;
    const seg = { result: r, start: angle, end: angle + width };
    angle += width;
    return seg;
  }).filter((s): s is { result: PartyResultDto; start: number; end: number } => s !== null);

  return (
    <svg viewBox="0 0 550 240" style={{ width: "100%", height: "220rem", display: "block" }}>
      {segments.map((seg) => {
        const midAngle = (seg.start + seg.end) / 2;
        const midRadius = (rInner + rOuter) / 2;
        const labelPos = polarPoint(cx, cy, midRadius, midAngle);
        const showLabel = seg.end - seg.start > 14;

        return (
          <g key={seg.result.party}>
            <path
              d={sectorPath(cx, cy, rInner, rOuter, seg.start, seg.end)}
              fill={resolvePartyColor(seg.result)}
              stroke="rgba(0,0,0,0.35)"
              strokeWidth="1.5"
            />
            {showLabel && (
              <text
                x={labelPos.x}
                y={labelPos.y}
                fill="white"
                fontSize="18"
                fontWeight="700"
                textAnchor="middle"
                dominantBaseline="middle"
              >
                {seg.result.seats}
              </text>
            )}
          </g>
        );
      })}
    </svg>
  );
}

function BonusChoicePrompt() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const pending = useValue(playerBonusChoicePending$);
  const [selection, setSelection] = useState<"Defensif" | "Offensif" | null>(null);

  if (!pending) return null;

  const title = t("CityCouncil.Hemicycle.BONUS_CHOICE_TITLE", "Bonus permanent obtenu !");
  const desc = t("CityCouncil.Hemicycle.BONUS_CHOICE_DESC", "Votre parti a remporté la majorité au conseil municipal plusieurs fois de suite. Choisissez votre bonus permanent :");
  const defensifLabel = t("CityCouncil.Hemicycle.BONUS_CHOICE_DEFENSIF", "Défensif");
  const offensifLabel = t("CityCouncil.Hemicycle.BONUS_CHOICE_OFFENSIF", "Offensif");
  const confirmLabel = t("CityCouncil.Hemicycle.BONUS_CHOICE_CONFIRM", "Valider");
  const hint = t("CityCouncil.Hemicycle.BONUS_CHOICE_HINT", "Pour changer de bonus, vous devrez remporter la majorité au moins une fois de plus.");

  const optionStyle = (key: "Defensif" | "Offensif") => ({
    flex: 1,
    padding: "10rem",
    borderRadius: "6rem",
    cursor: "pointer",
    textAlign: "center" as const,
    background: selection === key ? "rgba(70,130,220,0.85)" : "rgba(255,255,255,0.08)",
    border: selection === key ? "1rem solid rgba(150,190,255,0.9)" : "1rem solid rgba(255,255,255,0.15)",
    color: "white",
    fontSize: "13rem",
  });

  return (
    <div style={{ marginTop: "14rem", padding: "10rem", background: "rgba(255,255,255,0.06)", borderRadius: "6rem" }}>
      <div style={{ color: "rgba(150,190,255,0.9)", fontSize: "13rem", fontWeight: 700, marginBottom: "6rem", whiteSpace: "nowrap" }}>
        {title}
      </div>
      <div style={{ color: "rgba(255,255,255,0.8)", fontSize: "12rem", lineHeight: "16rem", marginBottom: "10rem" }}>
        {desc}
      </div>

      <div style={{ display: "flex", gap: "8rem", marginBottom: "10rem" }}>
        {/* TODO : remplacer les emoji par de vraies icônes .png une fois le design disponible. */}
        <div style={optionStyle("Defensif")} onClick={() => setSelection("Defensif")}>
          <div style={{ fontSize: "20rem", marginBottom: "4rem" }}>🛡️</div>
          <div>{defensifLabel}</div>
        </div>
        <div style={optionStyle("Offensif")} onClick={() => setSelection("Offensif")}>
          <div style={{ fontSize: "20rem", marginBottom: "4rem" }}>⚔️</div>
          <div>{offensifLabel}</div>
        </div>
      </div>

      <button
        disabled={!selection}
        onClick={() => selection && trigger("cityCouncil", "choosePlayerPermanentBonus", selection)}
        style={{
          width: "100%",
          background: selection ? "rgba(70,130,220,0.85)" : "rgba(255,255,255,0.08)",
          color: "white",
          border: "none",
          borderRadius: "4rem",
          padding: "8rem 10rem",
          fontSize: "13rem",
          fontWeight: "bold",
          cursor: selection ? "pointer" : "default",
          marginBottom: "8rem",
        }}
      >
        {confirmLabel}
      </button>

      <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "11rem", lineHeight: "15rem" }}>
        {hint}
      </div>
    </div>
  );
}

function HemicycleResultsContent() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const seatsJson = useValue(hemicycleSeatsJson$);
  const results: PartyResultDto[] = useMemo(() => {
    try {
      const parsed = JSON.parse(seatsJson ?? "[]"); // GARDE
      return Array.isArray(parsed) ? parsed.filter((r) => r && typeof r.party === "string") : []; // GARDE
    } catch {
      return [];
    }
  }, [seatsJson]);
  const leader = useValue(hemicycleLeader$);
  const totalSeats = results.reduce((sum, r) => sum + (r.seats ?? 0), 0); // GARDE
  const leaderResult = results.find((r) => r.party === leader);

  // Construction sécurisée de la ligne de résumé sous forme d'une seule chaîne (template literal)
  let statusLine = t("CityCouncil.Hemicycle.NO_ELECTION", "Aucune élection terminée pour le moment.");
  if (leader) {
   const leaderName = leaderResult
    ? resolvePartyLabel(leaderResult, translate)
    : translatePartyName(leader, translate);
    const leaderPrefix = t("CityCouncil.Hemicycle.LEADER_PREFIX", "Parti en tête : ");
    const seatsSuffix = t("CityCouncil.Hemicycle.SEATS_SUFFIX", " sièges au total");
    statusLine = `${leaderPrefix}${leaderName} — ${totalSeats}${seatsSuffix}`;
  }

  return (
  <div style={centeredTabWrapperStyle}>
    <div style={centeredTabContentStyle}>
      <div style={{ color: "rgba(255,255,255,0.8)", fontSize: "18rem", marginBottom: "10rem", textAlign: "center" }}>
        {statusLine}
      </div>

      {leaderResult && (
        <div style={{ display: "flex", justifyContent: "center", marginBottom: "14rem" }}>
          <PartyLogo
            party={leaderResult.party}
            color={resolvePartyColor(leaderResult)}
            isCustom={!!(leaderResult.displayName && leaderResult.displayName.length > 0)}
            sizeRem={60}
          />
        </div>
      )}

      <HemicycleFan results={results} />

      <div style={{ display: "flex", flexDirection: "column", marginTop: "10rem" }}>
  {results.map((r) => {
    const seatsWord = r.seats > 1
      ? t("CityCouncil.Hemicycle.LEGEND_SEATS_PLURAL", "sièges")
      : t("CityCouncil.Hemicycle.LEGEND_SEATS_SINGULAR", "siège");
    const bastionCount = r.bastions ?? 0;
    const bastionWord = bastionCount > 1
      ? t("CityCouncil.Hemicycle.LEGEND_BASTIONS_PLURAL", "bastions")
      : t("CityCouncil.Hemicycle.LEGEND_BASTIONS_SINGULAR", "bastion");
    const bastionSuffix = bastionCount > 0 ? ` — ${bastionCount} ${bastionWord}` : "";
    const legendLine = `${resolvePartyLabel(r, translate)} : ${r.seats} ${seatsWord}${bastionSuffix}`;
 

    return (
      <div
        key={r.party}
        style={{
          display: "flex",
          alignItems: "center",
          fontSize: "18rem",
          color: "rgba(255,255,255,0.9)",
          marginBottom: "5rem",
        }}
      >
        <div
          style={{
            width: "18rem",
            height: "18rem",
            borderRadius: "50%",
            background: resolvePartyColor(r),
            flexShrink: 0,
            marginRight: "8rem",
          }}
        />
        <span style={{ whiteSpace: "nowrap", wordBreak: "keep-all", overflowWrap: "normal" }}>
          {legendLine}
        </span>
      </div>
    );
  })}
</div>

       <BonusChoicePrompt />

    </div>
    </div>
  );
}


type TabKey = "results" | "yourParty" | "forces" | "funding" | "propaganda" | "commission" | "poll" | "score" | "rules" | "debug";

const PANEL_WIDTH = "820rem";
const PANEL_CONTENT_HEIGHT = "560rem";

function HemicycleTabs() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const [tab, setTab] = useState<TabKey>("results");
  const showDebugTab = useValue(showDebugTab$); // AJOUT

  const tabStyle = (key: TabKey) => ({
    padding: "8rem 9rem",
    cursor: "pointer",
    color: tab === key ? "white" : "rgba(255,255,255,0.55)",
    borderBottom: tab === key ? "2rem solid white" : "2rem solid transparent",
    fontSize: "13rem",
    fontWeight: 600,
  });

  // AJOUT — si l'onglet actif est "debug" mais que le réglage vient d'être désactivé en
  // cours de session, on retombe sur "results" pour éviter un onglet sélectionné invisible.
  const effectiveTab = tab === "debug" && !showDebugTab ? "results" : tab;

  return (
    <div style={{ width: PANEL_WIDTH }}>
      <div style={{ display: "flex", borderBottom: "1rem solid rgba(255,255,255,0.15)" }}>
        <div style={tabStyle("results")} onClick={() => setTab("results")}>
          {t("CityCouncil.Hemicycle.TAB_RESULTS", "Résultats")}
        </div>
        <div style={tabStyle("yourParty")} onClick={() => setTab("yourParty")}>
          {t("CityCouncil.Hemicycle.TAB_YOUR_PARTY", "Votre Parti")}
        </div>
        <div style={tabStyle("forces")} onClick={() => setTab("forces")}>
          {t("CityCouncil.Hemicycle.TAB_FORCES", "Forces Politiques")}
        </div>
        <div style={tabStyle("funding")} onClick={() => setTab("funding")}>
          {t("CityCouncil.Hemicycle.TAB_FUNDING", "Financement")}
        </div>
        <div style={tabStyle("propaganda")} onClick={() => setTab("propaganda")}>
          {t("CityCouncil.Propaganda.TAB_LABEL", "Propagande")}
        </div>
        <div style={tabStyle("commission")} onClick={() => setTab("commission")}>
          {t("CityCouncil.Commission.TAB_LABEL", "Commission Électorale")}
        </div>
        <div style={tabStyle("poll")} onClick={() => setTab("poll")}>
          {t("CityCouncil.Poll.TAB_LABEL", "Sondages")}
        </div>
        <div style={tabStyle("score")} onClick={() => setTab("score")}>
          {t("CityCouncil.Score.TAB_LABEL", "Score")}
        </div>
        <div style={tabStyle("rules")} onClick={() => setTab("rules")}>
          {t("CityCouncil.Rules.TAB_LABEL", "Règles")}
        </div>
        {showDebugTab && ( // AJOUT — onglet visible uniquement si activé dans les Options
          <div style={tabStyle("debug")} onClick={() => setTab("debug")}>
            [DEBUG]
          </div>
        )}
      </div>

      <Scrollable style={{ height: PANEL_CONTENT_HEIGHT }}>
        {effectiveTab === "results" && <SafeBoundary><HemicycleResultsContent /></SafeBoundary>}
        {effectiveTab === "yourParty" && <SafeBoundary><YourPartyTab /></SafeBoundary>}
        {effectiveTab === "forces" && <SafeBoundary><PoliticalForcesTab /></SafeBoundary>}
        {effectiveTab === "funding" && <SafeBoundary><FundingTab /></SafeBoundary>}
        {effectiveTab === "propaganda" && <SafeBoundary><PropagandaTab /></SafeBoundary>}
        {effectiveTab === "commission" && <SafeBoundary><ElectoralCommissionTab /></SafeBoundary>}
        {effectiveTab === "poll" && <SafeBoundary><PollTab /></SafeBoundary>}
        {effectiveTab === "score" && <SafeBoundary><ScoreTab /></SafeBoundary>}
        {effectiveTab === "rules" && <SafeBoundary><RulesTab /></SafeBoundary>}
        {effectiveTab === "debug" && showDebugTab && <SafeBoundary><DebugTab /></SafeBoundary>}
      </Scrollable>
    </div>
  );
}

function HemicycleEntry() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const [open, setOpen] = useState(false);
   const bonusPending = useValue(playerBonusChoicePending$);

  const handleOpen = () => {
    const next = !open;
    setOpen(next);
    if (next) {
      trigger("cityCouncil", "refreshHemicycle");
    }
  };

  // TODO : remplacer ce badge emoji par un vrai remplacement d'icône .png une fois le design
  // disponible (ex. <img src="coui://.../hemicycle_bonus_pending.png" /> à la place du bouton
  // normal). Pour l'instant : icône inchangée + petit indicateur rouge + tooltip natif.
  const tooltip = bonusPending ? t("CityCouncil.Hemicycle.BONUS_PENDING_TOOLTIP", "Bonus Permanent à choisir !") : undefined;

  return (
    <>
      <div title={tooltip} style={{ position: "relative", display: "inline-block" }}>
        <Button variant="flat" onSelect={handleOpen}>
          <div style={{ fontSize: "16rem" }}>CityCouncil</div>
        </Button>
        {bonusPending && (
          <div
            style={{
              position: "absolute",
              top: "2rem",
              right: "2rem",
              width: "8rem",
              height: "8rem",
              borderRadius: "50%",
              background: "rgba(220,80,80,0.95)",
              border: "1rem solid white",
              pointerEvents: "none",
            }}
          />
        )}
      </div>

      {open && (
        <Panel
          draggable
          initialPosition={{ x: 0.5, y: 0.3 }}
          header={
            <div style={{ display: "flex", alignItems: "center" }}>
              <span>{t("CityCouncil.Hemicycle.PANEL_TITLE", "Conseil municipal")}</span>
            </div>
          }
          onClose={() => setOpen(false)}
        >
          <HemicycleTabs />
        </Panel>
      )}
    </>
  );
}

export const HemicyclePanel = () => (
  <SafeBoundary>
    <HemicycleEntry />
  </SafeBoundary>
);