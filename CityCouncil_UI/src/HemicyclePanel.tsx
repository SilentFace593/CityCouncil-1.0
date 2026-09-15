import { Component, useMemo, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Panel, Scrollable } from "cs2/ui";
import { useLocalization } from "cs2/l10n";

import { 
  translatePartyName, 
  PARTY_LABELS, 
  PARTY_ORDER, 
  PARTY_COLORS,
  CUSTOM_PARTY_PALETTE_HEX,
  resolvePartyColor, 
  resolvePartyLabel, 
  PartyLogo, 
  type PartyResultDto,
  BonusBadgeIcon 
} from "./PartyResultDto";

import { LawsTab } from "./LawsTab";
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
import { VotingInstructionsCard } from "./VotingInstructionsCard";
import { ReinforcedBastionCard } from "./ReinforcedBastionCard";
import { CoalitionProposalCard } from "./CoalitionProposalCard"; // Ajout de l'import manquant
import doodleResultatsImg from "./images/doodle_resultats.png";
import iconeHemicycleIcon from "./images/iconeHemicycle.svg";
import iconeVotrePartiIcon from "./images/iconeVotreParti.svg";
import iconeForcesIcon from "./images/icone_forces.svg";
import iconeFinancesIcon from "./images/icone_finances.svg";
import iconePropagandeIcon from "./images/icone_propagande.svg";
import iconeCommissionIcon from "./images/icone_commission.svg";
import iconeSondageIcon from "./images/icone_sondage.svg";
import iconeLoisIcon from "./images/icone_lois.svg";
import iconeScoreIcon from "./images/icone_score.svg";
import iconeReglesIcon from "./images/icone_regles.svg";

const TAB_IMAGES = { doodleResultats: doodleResultatsImg };
const RESULTS_FAN_MAX_WIDTH = "760rem";

// --- Bindings exposés par CouncilUISystem.cs (group "cityCouncil") ---
const hemicycleSeatsJson$ = bindValue<string>("cityCouncil", "hemicycleSeatsJson");
const hemicycleLeader$ = bindValue<string>("cityCouncil", "hemicycleLeader");
const playerBonusChoicePending$ = bindValue<boolean>("cityCouncil", "playerBonusChoicePending");
const playerBonusChoiceSpace$ = bindValue<string>("cityCouncil", "playerBonusChoiceSpace");
const showDebugTab$ = bindValue<boolean>("cityCouncil", "showDebugTab");
const coalitionJson$ = bindValue<string>("cityCouncil", "coalitionJson");

const lawActiveVotesJson$ = bindValue<string>("cityCouncil", "lawActiveVotesJson");
const votingInstructionDistrictsJson$ = bindValue<string>("cityCouncil", "votingInstructionDistrictsJson");

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
        <div style={optionStyle("Defensif")} onClick={() => setSelection("Defensif")}>
          <div style={{ marginBottom: "4rem", display: "flex", justifyContent: "center" }}>
            <BonusBadgeIcon bonus="Defensif" widthRem={150} />
          </div>
          <div>{defensifLabel}</div>
        </div>
        <div style={optionStyle("Offensif")} onClick={() => setSelection("Offensif")}>
          <div style={{ marginBottom: "4rem", display: "flex", justifyContent: "center" }}>
            <BonusBadgeIcon bonus="Offensif" widthRem={150} />
          </div>
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

interface CoalitionDto {
  leadingIsCoalition: boolean;
  leadingMembers: string[];
  leadingHasAbsoluteMajority: boolean;
  awaitingPlayerDecision: boolean;
  playerCustomName: string;
  customPartyName: string;
  customPartyColor: string;
  leadingBlocLawsVoted: number;     
  leadingBlocLawsAbrogated: number;
}

function CoalitionStatusLine() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;
  const json = useValue(coalitionJson$);

  const dto: CoalitionDto | null = useMemo(() => {
    try {
      const p = JSON.parse(json ?? "{}");
      return typeof p.leadingIsCoalition === "boolean" ? p : null;
    } catch { return null; }
  }, [json]);

  // MODIFIÉ — ne s'affiche QUE si le bloc en tête est une coalition (2+ membres), pas
  // n'importe quelle coalition existante par ailleurs.
  if (!dto || !dto.leadingIsCoalition) return null;

  const color = (partyKey: string): string =>
    dto.playerCustomName && partyKey === dto.playerCustomName
      ? (CUSTOM_PARTY_PALETTE_HEX[dto.customPartyColor] ?? "#888")
      : (PARTY_COLORS[partyKey] ?? "#888");

  // AJOUT — précise si la coalition dispose ou non de la majorité absolue.
  // MODIFIÉ — titre + suffixe aplatis en UNE SEULE chaîne (contrainte du moteur : plusieurs
  // enfants JSX adjacents pour du texte provoquent un retour à la ligne intempestif, même
  // pattern que le reste du fichier).
  const majoritySuffix = dto.leadingHasAbsoluteMajority
    ? t("CityCouncil.Coalition.MAJORITY_SUFFIX", " (majorité absolue)")
    : t("CityCouncil.Coalition.PLURALITY_SUFFIX", " (majorité relative)");
  const titleLine = `${t("CityCouncil.Coalition.HEADER", "Coalition")}${majoritySuffix}`;

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        marginBottom: "10rem",
        padding: "10rem 16rem",
        border: "1rem solid rgba(150,190,255,0.35)",
        borderRadius: "6rem",
        background: "rgba(150,190,255,0.06)",
      }}
    >
      <div style={{ color: "rgba(150,190,255,0.95)", fontSize: "16rem", fontWeight: 700, marginBottom: "8rem", whiteSpace: "nowrap" }}>
        {titleLine}
      </div>

      {/* MODIFIÉ — logo seul par membre, séparé par un tiret gras en cas de coalition (2+
          membres). Rangée d'éléments JSX (pas du texte fluide) : pas soumise à la contrainte
          d'aplatissement en chaîne unique, même principe que HemicycleFan/ScoreTab plus bas. */}
      <div style={{ display: "flex", alignItems: "center", gap: "10rem" }}>
        {dto.leadingMembers.flatMap((m, i) => {
          const logo = (
            <PartyLogo
              key={`logo-${m}`}
              party={m}
              color={color(m)}
              isCustom={!!dto.playerCustomName && m === dto.playerCustomName}
              sizeRem={40}
            />
          );
          if (i === 0) return [logo];

          const dash = (
            <span
              key={`dash-${m}`}
              style={{ color: "rgba(255,255,255,0.7)", fontSize: "22rem", fontWeight: 900, lineHeight: "1" }}
            >
              -
            </span>
          );
          return [dash, logo];
        })}
      </div>
    </div>
  );
}


function LeadingBlocLawSummary() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;
  const json = useValue(coalitionJson$);

  const dto: CoalitionDto | null = useMemo(() => {
    try {
      const p = JSON.parse(json ?? "{}");
      return typeof p.leadingIsCoalition === "boolean" ? p : null;
    } catch { return null; }
  }, [json]);

  if (!dto || dto.leadingMembers.length === 0) return null;

  const prefix = t("CityCouncil.Hemicycle.LEADING_BLOC_LAW_SUMMARY_PREFIX", "Bilan de l'actuel parti ou coalition au pouvoir : ");
  const votedWord = t("CityCouncil.Hemicycle.LAWS_VOTED_WORD", "lois votées");
  const abrogatedWord = t("CityCouncil.Hemicycle.LAWS_ABROGATED_WORD", "lois abrogées");

  // Une seule chaîne (contrainte du moteur) : pas de mélange texte + expressions adjacentes.
  const line = `${prefix}${dto.leadingBlocLawsVoted} ${votedWord}, ${dto.leadingBlocLawsAbrogated} ${abrogatedWord}`;

  return (
    <div
      style={{
        marginTop: "12rem",
        padding: "10rem",
        background: "rgba(255,255,255,0.06)",
        borderRadius: "6rem",
        textAlign: "center",
      }}
    >
      <div style={{ color: "rgba(255,255,255,0.85)", fontSize: "12rem", lineHeight: "16rem" }}>
        {line}
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
      const parsed = JSON.parse(seatsJson ?? "[]");
      return Array.isArray(parsed) ? parsed.filter((r) => r && typeof r.party === "string") : [];
    } catch {
      return [];
    }
  }, [seatsJson]);

 const leader = useValue(hemicycleLeader$);
const coalitionJsonRaw = useValue(coalitionJson$);

const coalitionLeadingIsCoalition = useMemo(() => {
  try {
    return !!JSON.parse(coalitionJsonRaw ?? "{}").leadingIsCoalition;
  } catch {
    return false;
  }
}, [coalitionJsonRaw]);

  const totalSeats = results.reduce((sum, r) => sum + (r.seats ?? 0), 0);
  const leaderResult = results.find((r) => r.party === leader);

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
    <div style={{ position: "relative", width: "100%", boxSizing: "border-box", minHeight: "100%" }}>
      <div style={centeredTabWrapperStyle}>
        <div style={centeredTabContentStyle}>

          <CoalitionStatusLine />
{!coalitionLeadingIsCoalition && (
  <div style={{ color: "rgba(255,255,255,0.8)", fontSize: "18rem", marginBottom: "10rem", textAlign: "center" }}>
    {statusLine}
  </div>
)}

        
        </div>
      </div>

           {/* Bloc élargi : graphique + légende */}
      <div style={{ display: "flex", justifyContent: "center", width: "100%", boxSizing: "border-box" }}>
        <div style={{ width: "100%", maxWidth: RESULTS_FAN_MAX_WIDTH, boxSizing: "border-box", padding: "0 10rem" }}>
          <div style={{ display: "flex", alignItems: "center" }}>
            <div style={{ flex: "1 1 auto", minWidth: 0 }}>
              <HemicycleFan results={results} />
            </div>

            <div style={{ flex: "0 0 auto", maxWidth: "220rem", display: "flex", flexDirection: "column", marginLeft: "16rem" }}>
              {results.map((r) => {
                const seatsWord = r.seats > 1
                  ? t("CityCouncil.Hemicycle.LEGEND_SEATS_PLURAL", "sièges")
                  : t("CityCouncil.Hemicycle.LEGEND_SEATS_SINGULAR", "siège");
                const bastionCount = r.bastions ?? 0;
                const bastionWord = bastionCount > 1
                  ? t("CityCouncil.Hemicycle.LEGEND_BASTIONS_PLURAL", "bastions")
                  : t("CityCouncil.Hemicycle.LEGEND_BASTIONS_SINGULAR", "bastion");
                const bastionSuffix = bastionCount > 0 ? ` — ${bastionCount} ${bastionWord}` : "";
                const reinforcedCount = r.reinforcedBastions ?? 0;
                const reinforcedWord = reinforcedCount > 1
                  ? t("CityCouncil.Hemicycle.LEGEND_REINFORCED_BASTIONS_PLURAL", "bastions renforcés")
                  : t("CityCouncil.Hemicycle.LEGEND_REINFORCED_BASTIONS_SINGULAR", "bastion renforcé");
                const reinforcedSuffix = reinforcedCount > 0 ? ` (dont ${reinforcedCount} ${reinforcedWord})` : "";

                const legendLine = `${resolvePartyLabel(r, translate)} : ${r.seats} ${seatsWord}${bastionSuffix}${reinforcedSuffix}`;

                return (
                  <div
                    key={r.party}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      fontSize: "13rem",
                      lineHeight: "16rem",
                      color: "rgba(255,255,255,0.9)",
                      marginBottom: "6rem",
                    }}
                  >
                    <div
                      style={{
                        width: "12rem",
                        height: "12rem",
                        borderRadius: "50%",
                        background: resolvePartyColor(r),
                        flexShrink: 0,
                        marginRight: "6rem",
                      }}
                    />
                    <span style={{ minWidth: 0, wordBreak: "break-word", overflowWrap: "break-word" }}>
                      {legendLine}
                    </span>
                  </div>
                );
              })}
            </div>
          </div>

          <LeadingBlocLawSummary />
        </div>
      </div>

      {/* Bloc étroit : contenu annexe */}
      <div style={centeredTabWrapperStyle}>
        <div style={centeredTabContentStyle}>
          <BonusChoicePrompt />
          <VotingInstructionsCard />
          <ReinforcedBastionCard />
          <CoalitionProposalCard />
        </div>
      </div>

      <div
        style={{
          position: "absolute",
          bottom: "10rem",
          right: "10rem",
          width: "120rem",
          height: "154rem",
          backgroundImage: `url(${TAB_IMAGES.doodleResultats})`,
          backgroundSize: "contain",
          backgroundRepeat: "no-repeat",
          backgroundPosition: "center",
          pointerEvents: "none",
          zIndex: 10,
        }}
      />
    </div>
  );
}

type TabKey = "results" | "yourParty" | "forces" | "funding" | "propaganda" | "commission" | "poll" | "laws" | "score" | "rules" | "debug";

const PANEL_WIDTH = "820rem";
const PANEL_CONTENT_HEIGHT = "560rem";

function HemicycleTabs() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const [tab, setTab] = useState<TabKey>("results");
  const [resultsTabHovered, setResultsTabHovered] = useState(false);
  const [yourPartyTabHovered, setYourPartyTabHovered] = useState(false);
    const [forcesTabHovered, setForcesTabHovered] = useState(false);
  const [fundingTabHovered, setFundingTabHovered] = useState(false);
  const [propagandaTabHovered, setPropagandaTabHovered] = useState(false);
  const [commissionTabHovered, setCommissionTabHovered] = useState(false);
  const [pollTabHovered, setPollTabHovered] = useState(false);
  const [lawsTabHovered, setLawsTabHovered] = useState(false);
  const [scoreTabHovered, setScoreTabHovered] = useState(false);
  const [rulesTabHovered, setRulesTabHovered] = useState(false);
  const showDebugTab = useValue(showDebugTab$);

  const tabStyle = (key: TabKey) => ({
    padding: "8rem 9rem",
    cursor: "pointer",
    color: tab === key ? "white" : "rgba(255,255,255,0.55)",
    borderBottom: tab === key ? "2rem solid white" : "2rem solid transparent",
    fontSize: "13rem",
    fontWeight: 600,
  });

  const effectiveTab = tab === "debug" && !showDebugTab ? "results" : tab;

  return (
    <div style={{ width: PANEL_WIDTH }}>
           <div style={{ display: "flex", borderBottom: "1rem solid rgba(255,255,255,0.15)", height: "34rem", overflow: "visible" }}>
                        <div
          style={{
            ...tabStyle("results"),
            position: "relative",
            height: "100%",
            padding: "0 9rem",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            overflow: "visible",
          }}
          onClick={() => setTab("results")}
          onMouseEnter={() => setResultsTabHovered(true)}
          onMouseLeave={() => setResultsTabHovered(false)}
        >
          <img
            src={iconeHemicycleIcon}
            style={{ width: "35rem", height: "35rem", display: "block", flexShrink: 0 }}
          />
                    {resultsTabHovered && (
            <div
              style={{
                position: "absolute",
                top: "100%",
                left: 0,
                marginTop: "4rem",
                background: "rgba(20,20,28,0.97)",
                border: "1rem solid rgba(255,255,255,0.15)",
                borderRadius: "4rem",
                padding: "4rem 8rem",
                color: "white",
                fontSize: "11rem",
                whiteSpace: "nowrap",
                zIndex: 20,
                pointerEvents: "none",
              }}
            >
              {t("CityCouncil.Hemicycle.ICON_TOOLTIP", "Hémicycle")}
            </div>
          )}
        </div>
                <div
          style={{
            ...tabStyle("yourParty"),
            position: "relative",
            height: "100%",
            padding: "0 9rem",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            overflow: "visible",
          }}
          onClick={() => setTab("yourParty")}
          onMouseEnter={() => setYourPartyTabHovered(true)}
          onMouseLeave={() => setYourPartyTabHovered(false)}
        >
          <img
            src={iconeVotrePartiIcon}
            style={{ width: "35rem", height: "35rem", display: "block", flexShrink: 0 }}
          />
          {yourPartyTabHovered && (
            <div
              style={{
                position: "absolute",
                top: "100%",
                left: "50%",
                transform: "translateX(-50%)",
                marginTop: "4rem",
                background: "rgba(20,20,28,0.97)",
                border: "1rem solid rgba(255,255,255,0.15)",
                borderRadius: "4rem",
                padding: "4rem 8rem",
                color: "white",
                fontSize: "11rem",
                whiteSpace: "nowrap",
                zIndex: 20,
                pointerEvents: "none",
              }}
            >
              {t("CityCouncil.Hemicycle.YOUR_PARTY_ICON_TOOLTIP", "Votre Parti")}
            </div>
          )}
        </div>
                <div
          style={{ ...tabStyle("forces"), position: "relative", height: "100%", padding: "0 9rem", display: "flex", alignItems: "center", justifyContent: "center", overflow: "visible" }}
          onClick={() => setTab("forces")}
          onMouseEnter={() => setForcesTabHovered(true)}
          onMouseLeave={() => setForcesTabHovered(false)}
        >
          <img src={iconeForcesIcon} style={{ width: "35rem", height: "35rem", display: "block", flexShrink: 0 }} />
          {forcesTabHovered && (
            <div style={{ position: "absolute", top: "100%", left: "50%", transform: "translateX(-50%)", marginTop: "4rem", background: "rgba(20,20,28,0.97)", border: "1rem solid rgba(255,255,255,0.15)", borderRadius: "4rem", padding: "4rem 8rem", color: "white", fontSize: "11rem", whiteSpace: "nowrap", zIndex: 20, pointerEvents: "none" }}>
              {t("CityCouncil.Hemicycle.FORCES_ICON_TOOLTIP", "Forces Politiques")}
            </div>
          )}
        </div>

        <div
          style={{ ...tabStyle("funding"), position: "relative", height: "100%", padding: "0 9rem", display: "flex", alignItems: "center", justifyContent: "center", overflow: "visible" }}
          onClick={() => setTab("funding")}
          onMouseEnter={() => setFundingTabHovered(true)}
          onMouseLeave={() => setFundingTabHovered(false)}
        >
          <img src={iconeFinancesIcon} style={{ width: "35rem", height: "35rem", display: "block", flexShrink: 0 }} />
          {fundingTabHovered && (
            <div style={{ position: "absolute", top: "100%", left: "50%", transform: "translateX(-50%)", marginTop: "4rem", background: "rgba(20,20,28,0.97)", border: "1rem solid rgba(255,255,255,0.15)", borderRadius: "4rem", padding: "4rem 8rem", color: "white", fontSize: "11rem", whiteSpace: "nowrap", zIndex: 20, pointerEvents: "none" }}>
              {t("CityCouncil.Hemicycle.FUNDING_ICON_TOOLTIP", "Financement")}
            </div>
          )}
        </div>

        <div
          style={{ ...tabStyle("propaganda"), position: "relative", height: "100%", padding: "0 9rem", display: "flex", alignItems: "center", justifyContent: "center", overflow: "visible" }}
          onClick={() => setTab("propaganda")}
          onMouseEnter={() => setPropagandaTabHovered(true)}
          onMouseLeave={() => setPropagandaTabHovered(false)}
        >
          <img src={iconePropagandeIcon} style={{ width: "35rem", height: "35rem", display: "block", flexShrink: 0 }} />
          {propagandaTabHovered && (
            <div style={{ position: "absolute", top: "100%", left: "50%", transform: "translateX(-50%)", marginTop: "4rem", background: "rgba(20,20,28,0.97)", border: "1rem solid rgba(255,255,255,0.15)", borderRadius: "4rem", padding: "4rem 8rem", color: "white", fontSize: "11rem", whiteSpace: "nowrap", zIndex: 20, pointerEvents: "none" }}>
              {t("CityCouncil.Hemicycle.PROPAGANDA_ICON_TOOLTIP", "Propagande")}
            </div>
          )}
        </div>

        <div
          style={{ ...tabStyle("commission"), position: "relative", height: "100%", padding: "0 9rem", display: "flex", alignItems: "center", justifyContent: "center", overflow: "visible" }}
          onClick={() => setTab("commission")}
          onMouseEnter={() => setCommissionTabHovered(true)}
          onMouseLeave={() => setCommissionTabHovered(false)}
        >
          <img src={iconeCommissionIcon} style={{ width: "35rem", height: "35rem", display: "block", flexShrink: 0 }} />
          {commissionTabHovered && (
            <div style={{ position: "absolute", top: "100%", left: "50%", transform: "translateX(-50%)", marginTop: "4rem", background: "rgba(20,20,28,0.97)", border: "1rem solid rgba(255,255,255,0.15)", borderRadius: "4rem", padding: "4rem 8rem", color: "white", fontSize: "11rem", whiteSpace: "nowrap", zIndex: 20, pointerEvents: "none" }}>
              {t("CityCouncil.Hemicycle.COMMISSION_ICON_TOOLTIP", "Commission Électorale")}
            </div>
          )}
        </div>

        <div
          style={{ ...tabStyle("poll"), position: "relative", height: "100%", padding: "0 9rem", display: "flex", alignItems: "center", justifyContent: "center", overflow: "visible" }}
          onClick={() => setTab("poll")}
          onMouseEnter={() => setPollTabHovered(true)}
          onMouseLeave={() => setPollTabHovered(false)}
        >
          <img src={iconeSondageIcon} style={{ width: "35rem", height: "35rem", display: "block", flexShrink: 0 }} />
          {pollTabHovered && (
            <div style={{ position: "absolute", top: "100%", left: "50%", transform: "translateX(-50%)", marginTop: "4rem", background: "rgba(20,20,28,0.97)", border: "1rem solid rgba(255,255,255,0.15)", borderRadius: "4rem", padding: "4rem 8rem", color: "white", fontSize: "11rem", whiteSpace: "nowrap", zIndex: 20, pointerEvents: "none" }}>
              {t("CityCouncil.Hemicycle.POLL_ICON_TOOLTIP", "Sondages")}
            </div>
          )}
        </div>

        <div
          style={{ ...tabStyle("laws"), position: "relative", height: "100%", padding: "0 9rem", display: "flex", alignItems: "center", justifyContent: "center", overflow: "visible" }}
          onClick={() => setTab("laws")}
          onMouseEnter={() => setLawsTabHovered(true)}
          onMouseLeave={() => setLawsTabHovered(false)}
        >
          <img src={iconeLoisIcon} style={{ width: "35rem", height: "35rem", display: "block", flexShrink: 0 }} />
          {lawsTabHovered && (
            <div style={{ position: "absolute", top: "100%", left: "50%", transform: "translateX(-50%)", marginTop: "4rem", background: "rgba(20,20,28,0.97)", border: "1rem solid rgba(255,255,255,0.15)", borderRadius: "4rem", padding: "4rem 8rem", color: "white", fontSize: "11rem", whiteSpace: "nowrap", zIndex: 20, pointerEvents: "none" }}>
              {t("CityCouncil.Hemicycle.LAWS_ICON_TOOLTIP", "Lois")}
            </div>
          )}
        </div>

        <div
          style={{ ...tabStyle("score"), position: "relative", height: "100%", padding: "0 9rem", display: "flex", alignItems: "center", justifyContent: "center", overflow: "visible" }}
          onClick={() => setTab("score")}
          onMouseEnter={() => setScoreTabHovered(true)}
          onMouseLeave={() => setScoreTabHovered(false)}
        >
          <img src={iconeScoreIcon} style={{ width: "35rem", height: "35rem", display: "block", flexShrink: 0 }} />
          {scoreTabHovered && (
            <div style={{ position: "absolute", top: "100%", left: "50%", transform: "translateX(-50%)", marginTop: "4rem", background: "rgba(20,20,28,0.97)", border: "1rem solid rgba(255,255,255,0.15)", borderRadius: "4rem", padding: "4rem 8rem", color: "white", fontSize: "11rem", whiteSpace: "nowrap", zIndex: 20, pointerEvents: "none" }}>
              {t("CityCouncil.Hemicycle.SCORE_ICON_TOOLTIP", "Score")}
            </div>
          )}
        </div>

        <div
          style={{ ...tabStyle("rules"), position: "relative", height: "100%", padding: "0 9rem", display: "flex", alignItems: "center", justifyContent: "center", overflow: "visible" }}
          onClick={() => setTab("rules")}
          onMouseEnter={() => setRulesTabHovered(true)}
          onMouseLeave={() => setRulesTabHovered(false)}
        >
          <img src={iconeReglesIcon} style={{ width: "35rem", height: "35rem", display: "block", flexShrink: 0 }} />
          {rulesTabHovered && (
            <div style={{ position: "absolute", top: "100%", left: "50%", transform: "translateX(-50%)", marginTop: "4rem", background: "rgba(20,20,28,0.97)", border: "1rem solid rgba(255,255,255,0.15)", borderRadius: "4rem", padding: "4rem 8rem", color: "white", fontSize: "11rem", whiteSpace: "nowrap", zIndex: 20, pointerEvents: "none" }}>
              {t("CityCouncil.Hemicycle.RULES_ICON_TOOLTIP", "Règles")}
            </div>
          )}
        </div>
        {showDebugTab && (
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
        {effectiveTab === "laws" && <SafeBoundary><LawsTab /></SafeBoundary>}
        {effectiveTab === "score" && <SafeBoundary><ScoreTab /></SafeBoundary>}
        {effectiveTab === "rules" && <SafeBoundary><RulesTab /></SafeBoundary>}
        {effectiveTab === "debug" && showDebugTab && <SafeBoundary><DebugTab /></SafeBoundary>}
      </Scrollable>
    </div>
  );
}

function NotificationDot({ color, tooltip, offset }: { color: string; tooltip: string; offset: number }) {
  const [hovered, setHovered] = useState(false);

  return (
    <div
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      style={{
        position: "absolute",
        top: "2rem",
        right: `${2 + offset * 11}rem`,
        width: "8rem",
        height: "8rem",
        borderRadius: "50%",
        background: color,
        border: "1rem solid white",
        pointerEvents: "auto",
      }}
    >
      {hovered && tooltip && (
        <div
          style={{
            position: "absolute",
            bottom: "130%",
            right: 0,
            background: "rgba(20,20,28,0.97)",
            border: "1rem solid rgba(255,255,255,0.15)",
            borderRadius: "4rem",
            padding: "6rem 8rem",
            color: "white",
            fontSize: "11rem",
            lineHeight: "15rem",
            whiteSpace: "nowrap",
            zIndex: 20,
            pointerEvents: "none",
          }}
        >
          {tooltip}
        </div>
      )}
    </div>
  );
}

function HemicycleEntry() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const [open, setOpen] = useState(false);
  const bonusPending = useValue(playerBonusChoicePending$);
  const coalitionJsonRaw = useValue(coalitionJson$);
  const lawActiveVotesJsonRaw = useValue(lawActiveVotesJson$);
  const votingInstructionJsonRaw = useValue(votingInstructionDistrictsJson$);

  const coalitionPending = useMemo(() => {
    try {
      return !!JSON.parse(coalitionJsonRaw ?? "{}").awaitingPlayerDecision;
    } catch {
      return false;
    }
  }, [coalitionJsonRaw]);

  const lawPending = useMemo(() => {
    try {
      const arr = JSON.parse(lawActiveVotesJsonRaw ?? "[]");
      return Array.isArray(arr) && arr.some((v: any) => v && v.hasPlayerBloc && !v.playerHasAnswered && !v.isPlayerProposer);
    } catch {
      return false;
    }
  }, [lawActiveVotesJsonRaw]);

  const votingInstructionPending = useMemo(() => {
    try {
      const arr = JSON.parse(votingInstructionJsonRaw ?? "[]");
      return Array.isArray(arr) && arr.some((d: any) => d && d.submitted === false);
    } catch {
      return false;
    }
  }, [votingInstructionJsonRaw]);

  const notifications = [
    bonusPending && {
      key: "bonus",
      color: "rgba(220,80,80,0.95)",
      tooltip: t("CityCouncil.Hemicycle.BONUS_PENDING_TOOLTIP", "Bonus Permanent à choisir !"),
    },
    coalitionPending && {
      key: "coalition",
      color: "rgba(70,130,220,0.95)",
      tooltip: t("CityCouncil.Hemicycle.COALITION_PENDING_TOOLTIP", "Décision de coalition en attente !"),
    },
    lawPending && {
      key: "law",
      color: "rgba(160,90,220,0.95)",
      tooltip: t("CityCouncil.Hemicycle.LAW_PENDING_TOOLTIP", "Votre parti est sollicité pour une loi !"),
    },
    votingInstructionPending && {
      key: "votingInstruction",
      color: "rgba(90,200,120,0.95)",
      tooltip: t("CityCouncil.Hemicycle.VOTING_INSTRUCTION_PENDING_TOOLTIP", "Consigne de vote disponible !"),
    },
  ].filter((n): n is { key: string; color: string; tooltip: string } => !!n);

  const handleOpen = () => {
    const next = !open;
    setOpen(next);
    if (next) {
      trigger("cityCouncil", "refreshHemicycle");
    }
  };

  return (
    <>
      <div style={{ position: "relative", display: "inline-block" }}>
        <Button variant="flat" onSelect={handleOpen}>
          <div style={{ fontSize: "16rem" }}>CityCouncil</div>
        </Button>
        {notifications.map((n, i) => (
          <NotificationDot key={n.key} color={n.color} tooltip={n.tooltip} offset={i} />
        ))}
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