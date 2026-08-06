import { Component, useMemo, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Panel } from "cs2/ui";
import { useLocalization } from "cs2/l10n";
import { translatePartyName, PARTY_LABELS, PARTY_ORDER, resolvePartyColor, resolvePartyLabel, type PartyResultDto } from "./PartyResultDto";
import { YourPartyTab } from "./YourPartyTab";
import { PoliticalForcesTab } from "./PoliticalForcesTab";
import { FundingTab } from "./FundingTab";
import { PropagandaTab } from "./PropagandaTab"; 

// --- Bindings exposés par CouncilUISystem.cs (group "cityCouncil") ---
const hemicycleSeatsJson$ = bindValue<string>("cityCouncil", "hemicycleSeatsJson");
const hemicycleLeader$ = bindValue<string>("cityCouncil", "hemicycleLeader");
const playerBonusChoicePending$ = bindValue<boolean>("cityCouncil", "playerBonusChoicePending"); // AJOUT
const playerBonusChoiceSpace$ = bindValue<string>("cityCouncil", "playerBonusChoiceSpace"); // AJOUT


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
    <div style={{ padding: "10rem", width: "100%", boxSizing: "border-box" }}>
      <div style={{ color: "rgba(255,255,255,0.8)", fontSize: "14rem", marginBottom: "10rem" }}>
        {statusLine}
      </div>

      <HemicycleFan results={results} />

      <div style={{ display: "flex", flexDirection: "column", marginTop: "10rem" }}>
        {results.map((r) => (
          <div
            key={r.party}
            style={{
              display: "flex",
              alignItems: "center",
              fontSize: "13rem",
              color: "rgba(255,255,255,0.9)",
              marginBottom: "5rem",
            }}
          >
            <div
              style={{
                width: "12rem",
                height: "12rem",
                borderRadius: "50%",
                background: resolvePartyColor(r),
                flexShrink: 0,
                marginRight: "8rem",
              }}
            />
            <span style={{ flex: 1, minWidth: 0, whiteSpace: "nowrap", wordBreak: "keep-all", overflowWrap: "normal" }}>
              {resolvePartyLabel(r, translate)}
            </span>
            <span style={{ whiteSpace: "nowrap", wordBreak: "keep-all", overflowWrap: "normal" }}>{r.seats}</span>
          </div>
        ))}
      </div>

       <BonusChoicePrompt />
       //* OUTIL DE DEBUG TEMPORAIRE — force l'avancement de toutes les élections en cours.
       //* À retirer avant release.
      <button
        onClick={() => trigger("cityCouncil", "debugForceNextElection")}
        style={{
          marginTop: "14rem",
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
        [DEBUG] Forcer l'étape électorale suivante
      </button>

      {/* OUTIL DE DEBUG TEMPORAIRE — force le contrôle de majorité (bonus permanent) sans
    attendre le cycle réel de 7 jours in-game, nécessaire quand on enchaîne les élections
    via le bouton ci-dessus. À retirer avant release, comme l'autre bouton [DEBUG]. */}
<button
  onClick={() => trigger("cityCouncil", "debugForceMajorityCheck")}
  style={{
    marginTop: "8rem",
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
  [DEBUG] Forcer le contrôle de majorité (bonus)
</button>

{/* OUTIL DE DEBUG TEMPORAIRE — force le cycle de cotisation des adhérents (trésorerie),
    même remarque que les deux boutons précédents. À retirer avant release. */}
<button
  onClick={() => trigger("cityCouncil", "debugForceCycleCheck")}
  style={{
    marginTop: "8rem",
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
  [DEBUG] Forcer le cycle de cotisation (trésorerie)
</button>

<button
  onClick={() => trigger("cityCouncil", "debugExpireCampaigns")}
  style={{ marginTop: "8rem", width: "100%", background: "rgba(220,80,80,0.85)", color: "white", border: "none", borderRadius: "4rem", padding: "8rem 10rem", fontSize: "12rem", fontWeight: "bold", cursor: "pointer" }}
>
  [DEBUG] Forcer l'expiration des campagnes
</button>

    </div>
  );
}

type TabKey = "results" | "yourParty" | "forces" | "funding" | "propaganda";

const PANEL_WIDTH = "520rem";
const PANEL_CONTENT_HEIGHT = "560rem";

function HemicycleTabs() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const [tab, setTab] = useState<TabKey>("results");

  const tabStyle = (key: TabKey) => ({
    padding: "8rem 14rem",
    cursor: "pointer",
    color: tab === key ? "white" : "rgba(255,255,255,0.55)",
    borderBottom: tab === key ? "2rem solid white" : "2rem solid transparent",
    fontSize: "13rem",
    fontWeight: 600,
  });

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
      </div>

      <div style={{ height: PANEL_CONTENT_HEIGHT, overflowY: "auto", boxSizing: "border-box" }}>
  {tab === "results" && <SafeBoundary><HemicycleResultsContent /></SafeBoundary>}
  {tab === "yourParty" && <SafeBoundary><YourPartyTab /></SafeBoundary>}
  {tab === "forces" && <SafeBoundary><PoliticalForcesTab /></SafeBoundary>}
  {tab === "funding" && <SafeBoundary><FundingTab /></SafeBoundary>}
  {tab === "propaganda" && <SafeBoundary><PropagandaTab /></SafeBoundary>}
</div>
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