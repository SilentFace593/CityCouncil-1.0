import { Component, useMemo, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Panel } from "cs2/ui";
import { useLocalization } from "cs2/l10n";
import { translatePartyName, PARTY_LABELS, PARTY_ORDER, resolvePartyColor, resolvePartyLabel, type PartyResultDto } from "./PartyResultDto";
import { YourPartyTab } from "./YourPartyTab";
import { PoliticalForcesTab } from "./PoliticalForcesTab";

// --- Bindings exposés par CouncilUISystem.cs (group "cityCouncil") ---
const hemicycleSeatsJson$ = bindValue<string>("cityCouncil", "hemicycleSeatsJson");
const hemicycleLeader$ = bindValue<string>("cityCouncil", "hemicycleLeader");

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

function HemicycleResultsContent() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const seatsJson = useValue(hemicycleSeatsJson$);
  const results: PartyResultDto[] = useMemo(() => {
    try {
      const parsed = JSON.parse(seatsJson);
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }, [seatsJson]);
  const leader = useValue(hemicycleLeader$);
  const totalSeats = results.reduce((sum, r) => sum + r.seats, 0);
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
    </div>
  );
}

type TabKey = "results" | "yourParty" | "forces";

const PANEL_WIDTH = "440rem";
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
      </div>

      <div style={{ height: PANEL_CONTENT_HEIGHT, overflowY: "auto", boxSizing: "border-box" }}>
        {tab === "results" && <HemicycleResultsContent />}
        {tab === "yourParty" && <YourPartyTab />}
        {tab === "forces" && <PoliticalForcesTab />}
      </div>
    </div>
  );
}

function HemicycleEntry() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const [open, setOpen] = useState(false);

  const handleOpen = () => {
    const next = !open;
    setOpen(next);
    if (next) {
      trigger("cityCouncil", "refreshHemicycle");
    }
  };

  return (
    <>
      <Button variant="flat" onSelect={handleOpen}>
        <div style={{ fontSize: "16rem" }}>🏛</div>
      </Button>

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