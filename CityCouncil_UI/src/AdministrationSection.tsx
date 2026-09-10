import { Component, useMemo, useState, useEffect } from "react";
import { bindValue, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import {
  PartyLogo,
  translatePartyName,
  PARTY_LABELS,
  PARTY_ORDER,
  PARTY_COLORS,
  CUSTOM_PARTY_PALETTE_HEX,
  resolvePartyColor,
  resolvePartyLabel,
  type PartyResultDto,
  type TranslateFn,
  BonusBadgeIcon,
} from "./PartyResultDto";
import fanionGaucheIcon from "./images/Fanion_gauche.png";
import fanionPopulisteIcon from "./images/Fanion_populiste.png";
import fanionEcologisteIcon from "./images/Fanion_ecologiste.png";
import fanionDemocrateIcon from "./images/Fanion_democrate.png";
import fanionRepublicainIcon from "./images/Fanion_republicain.png";

// --- Bindings exposés par CouncilUISystem.cs (group "cityCouncil") ---
const adminVisible$ = bindValue<boolean>("cityCouncil", "adminVisible");
const adminPhase$ = bindValue<string>("cityCouncil", "adminPhase");
const adminLeadingParty$ = bindValue<string>("cityCouncil", "adminLeadingParty");
const adminFinalist1$ = bindValue<string>("cityCouncil", "adminFinalist1");
const adminFinalist2$ = bindValue<string>("cityCouncil", "adminFinalist2");
const adminSeats$ = bindValue<number>("cityCouncil", "adminSeats");
const adminVoters$ = bindValue<number>("cityCouncil", "adminVoters");
const adminAbstention$ = bindValue<number>("cityCouncil", "adminAbstention");
const adminResultsJson$ = bindValue<string>("cityCouncil", "adminResultsJson");
const adminRound1ResultsJson$ = bindValue<string>("cityCouncil", "adminRound1ResultsJson");
const cityEventHeadline$ = bindValue<string>("cityCouncil", "cityEventHeadline");
const adminBastionStreakParty$ = bindValue<string>("cityCouncil", "adminBastionStreakParty");
const adminBastionStreakCount$ = bindValue<number>("cityCouncil", "adminBastionStreakCount");
const adminBastionActive$ = bindValue<boolean>("cityCouncil", "adminBastionActive");
const adminBastionReinforcedActive$ = bindValue<boolean>("cityCouncil", "adminBastionReinforcedActive"); // AJOUT
const adminLeadingPartyBonus$ = bindValue<string>("cityCouncil", "adminLeadingPartyBonus");

const FANION_IMAGES: Record<string, string> = {
  GaucheRadicale: fanionGaucheIcon,
  Populiste: fanionPopulisteIcon,
  Ecologiste: fanionEcologisteIcon,
  Democrate: fanionDemocrateIcon,
  Republicain: fanionRepublicainIcon,
};
const FANION_ASPECT_RATIO = 80 / 140; //ratio : 1.75

function partyBadgeSrc(party: string): string | null {
  return null;
}

interface Round1ResultDto {
  party: string;
  voteShare: number;
  votes: number;
  displayName?: string;
  displayColor?: string;
}

function resolveRound1Label(r: Round1ResultDto, translate: TranslateFn): string {
  if (r.displayName && r.displayName.length > 0) return r.displayName;
  return translatePartyName(r.party, translate);
}

function resolveRound1Color(r: Round1ResultDto): string {
  if (r.displayColor && r.displayColor.length > 0) {
    return CUSTOM_PARTY_PALETTE_HEX[r.displayColor] ?? "#888";
  }
  return PARTY_COLORS[r.party] ?? "#888";
}

/**
 * Une ligne d'histogramme horizontal : logo 25rem à gauche, barre fine au centre, voix +
 * pourcentage à droite. Même esprit visuel que PollBar (PollTab.tsx), mais avec le logo du
 * parti plutôt qu'une simple pastille de couleur.
 */
function Round1Bar({ result, translate }: { result: Round1ResultDto; translate: TranslateFn }) {
  const label = resolveRound1Label(result, translate);
  const color = resolveRound1Color(result);
  const isCustom = !!(result.displayName && result.displayName.length > 0);
  const pct = Math.round(result.voteShare * 1000) / 10;
  const widthPct = Math.max(0, Math.min(100, result.voteShare * 100));
  const statsLine = `${result.votes.toLocaleString()} (${pct}%)`;

  return (
    <div style={{ display: "flex", alignItems: "center", marginBottom: "8rem" }}>
      <div style={{ marginRight: "8rem", flexShrink: 0 }}>
        <PartyLogo party={result.party} color={color} isCustom={isCustom} sizeRem={25} />
      </div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "3rem" }}>
          <span style={{ color: "white", fontSize: "11rem", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
            {label}
          </span>
          <span style={{ color: "rgba(255,255,255,0.8)", fontSize: "11rem", whiteSpace: "nowrap", marginLeft: "6rem", flexShrink: 0 }}>
            {statsLine}
          </span>
        </div>
        <div style={{ height: "10rem", borderRadius: "3rem", background: "rgba(255,255,255,0.06)", overflow: "hidden" }}>
          <div style={{ width: `${widthPct}%`, height: "100%", background: color }} />
        </div>
      </div>
    </div>
  );
}

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


// Remplace title="" sur <img>, non fiable en cohtml (même contrainte que :hover CSS).
// Tooltip géré manuellement via onMouseEnter/onMouseLeave + état local, positionné en absolu.
function HoverTooltip({ text, children }: { text: string; children: any }) {
  const [hovered, setHovered] = useState(false);

  return (
    <span
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      style={{ position: "relative", display: "inline-block" }}
    >
      {children}
      {hovered && text && (
        <div
          style={{
            position: "absolute",
            bottom: "110%",
            left: "50%",
            marginBottom: "6rem",
            background: "rgba(20,20,28,0.97)",
            border: "1rem solid rgba(255,255,255,0.15)",
            borderRadius: "4rem",
            padding: "6rem 8rem",
            color: "white",
            fontSize: "11rem",
            lineHeight: "15rem",
            whiteSpace: "normal",
            maxWidth: "200rem",
            zIndex: 10,
            pointerEvents: "none",
          }}
        >
          {text}
        </div>
      )}
    </span>
  );
}


function PartyBadge({ result, bonus, reinforcedBastionActive }: { result: PartyResultDto; bonus?: string; reinforcedBastionActive?: boolean }) {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const label = resolvePartyLabel(result, translate);
  const color = resolvePartyColor(result);
  const isCustom = !!(result.displayName && result.displayName.length > 0);
  const seatsWord = result.seats > 1
    ? t("CityCouncil.Admin.SEATS_PLURAL", "sièges")
    : t("CityCouncil.Admin.SEATS_SINGULAR", "siège");
  const seatsLine = `${result.seats} ${seatsWord}`;

  const bonusTooltip = bonus === "Defensif"
    ? t("CityCouncil.Admin.BONUS_DEFENSIF_TOOLTIP", "Bonus Défensif permanent (protège une case de barre de Bastion)")
    : bonus === "Offensif"
    ? t("CityCouncil.Admin.BONUS_OFFENSIF_TOOLTIP", "Bonus Offensif permanent (+3% d'intention de vote dans les bastions adverses)")
    : "";

  const fanionSrc = reinforcedBastionActive ? FANION_IMAGES[result.party] : null;
  const fanionTooltip = t("CityCouncil.Admin.FANION_TOOLTIP", "Bastion Renforcé actif dans ce district");

  const showBadgeRow = (bonus && bonus !== "None") || !!fanionSrc;

  return (
    <div style={{ display: "flex", alignItems: "center" }}>
      <div style={{ marginRight: "8rem" }}>
        <PartyLogo party={result.party} color={color} isCustom={isCustom} sizeRem={36} />
      </div>
      <div style={{ minWidth: 0 }}>
        <div style={{ position: "relative", display: "flex", alignItems: "center", height: "18rem" }}>
          <span style={{ color: "white", fontSize: "15rem", fontWeight: 600, whiteSpace: "nowrap", wordBreak: "keep-all", overflowWrap: "normal" }}>
            {label}
          </span>

          {showBadgeRow && (
            <span
              style={{
                position: "absolute",
                left: "125%",
                top: "50%",
                transform: "translateY(-25%)",
                marginLeft: "14rem",
                zIndex: 2,
                display: "flex",
                alignItems: "center",
                gap: "10rem",
              }}
            >
              {bonus && bonus !== "None" && (
                <HoverTooltip text={bonusTooltip}>
                  <BonusBadgeIcon bonus={bonus} widthRem={35} />
                </HoverTooltip>
              )}

              {fanionSrc && (
                <HoverTooltip text={fanionTooltip}>
                  <img
                    src={fanionSrc}
                    style={{
                      width: "22rem",
                      height: `${22 / FANION_ASPECT_RATIO}rem`,
                      objectFit: "contain",
                      flexShrink: 0,
                      pointerEvents: "auto",
                    }}
                  />
                </HoverTooltip>
              )}
            </span>
          )}
        </div>
        <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "13rem", whiteSpace: "nowrap", wordBreak: "keep-all", overflowWrap: "normal" }}>
          {seatsLine}
        </div>
      </div>
    </div>
  );
}

function StackedSeatBar({ results }: { results: PartyResultDto[] }) {
  const totalSeats = results.reduce((sum, r) => sum + r.seats, 0);
  if (totalSeats === 0) return null;

  const byParty: Record<string, PartyResultDto> = {};
  for (const r of results) byParty[r.party] = r;

  const segments = PARTY_ORDER.map((party) => byParty[party]).filter(
    (r): r is PartyResultDto => !!r && r.seats > 0
  );

  return (
    <div
      style={{
        display: "flex",
        width: "100%",
        height: "22rem",
        borderRadius: "4rem",
        overflow: "hidden",
        background: "rgba(255,255,255,0.06)",
      }}
    >
      {segments.map((r) => {
        const widthPct = (r.seats / totalSeats) * 100;
        const showNumber = widthPct > 8;
        return (
          <div
            key={r.party}
            style={{
              width: `${widthPct}%`,
              background: resolvePartyColor(r),
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              flexShrink: 0,
            }}
          >
            {showNumber && (
              <span style={{ color: "white", fontSize: "12rem", fontWeight: 700, whiteSpace: "nowrap", wordBreak: "keep-all", overflowWrap: "normal" }}>
                {r.seats}
              </span>
            )}
          </div>
        );
      })}
    </div>
  );
}

// --- Bastion Progress Bar ---
function BastionProgressBar({
  streakParty,
  streakCount,
  active,
  reinforced,
  translate,
}: {
  streakParty: string;
  streakCount: number;
  active: boolean;
reinforced: boolean;
  translate: (key: string, fallback: string | null) => string | null;
}) {
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  if (!streakParty || streakCount <= 0) return null;

  const color = PARTY_COLORS[streakParty] ?? "#888";
  const partyLabel = translatePartyName(streakParty, translate);

  // Une seule chaîne (contrainte du moteur) : label + nom de parti, plus suffixe si Bastion actif.
  const bastionLabelPrefix = reinforced
    ? t("CityCouncil.Admin.BASTION_REINFORCED_LABEL", "Bastion Renforcé : ")
    : t("CityCouncil.Admin.BASTION_LABEL", "Bastion : ");
  const bastionActiveSuffix = active
    ? (reinforced
        ? t("CityCouncil.Admin.BASTION_REINFORCED_ACTIVE_SUFFIX", " — Bonus actif (+5%)")
        : t("CityCouncil.Admin.BASTION_ACTIVE_SUFFIX", " — Bonus actif (+4%)"))
    : "";
  const bastionLine = `${bastionLabelPrefix}${partyLabel}${bastionActiveSuffix}`;

  return (
    <div style={{ marginTop: "10rem" }}>
      <div
        style={{
          color: active ? "rgba(150,190,255,0.95)" : "rgba(255,255,255,0.6)",
          fontSize: "12rem",
          fontWeight: active ? 700 : 400,
          whiteSpace: "nowrap",
          marginBottom: "6rem",
        }}
      >
        {bastionLine}
      </div>
      <div style={{ display: "flex" }}>
          {[0, 1, 2].map((i) => (
            <div
              key={i}
              style={{
                flex: 1,
                height: "8rem",
                borderRadius: "3rem",
                background: i < streakCount ? color : "rgba(255,255,255,0.10)",
                border: "1rem solid rgba(120,170,255,0.4)",
                marginRight: i < 2 ? "4rem" : 0,
              }}
            />
          ))}
      </div>
    </div>
  );
}

// AJOUT — état global partagé (pas useState local) : InfoSection remonte le composant à
// chaque changement de district sélectionné, un state local reviendrait donc à "déplié" à
// chaque clic. Même pattern déjà utilisé sur un autre mod pour ce cas précis.
let collapsedState = false;
const collapsedListeners: Array<(v: boolean) => void> = [];

function setGlobalCollapsed(value: boolean) {
  collapsedState = value;
  collapsedListeners.forEach((listener) => listener(value));
}

function useGlobalCollapsed(): [boolean, (value: boolean) => void] {
  const [value, setValue] = useState(collapsedState);

  useEffect(() => {
    collapsedListeners.push(setValue);
    return () => {
      const idx = collapsedListeners.indexOf(setValue);
      if (idx !== -1) collapsedListeners.splice(idx, 1);
    };
  }, []);

  return [value, setGlobalCollapsed];
}

const AdministrationSectionInner = ({ InfoSection }: { InfoSection: any }) => {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const visible = useValue(adminVisible$);
  const phase = useValue(adminPhase$);
  const leadingParty = useValue(adminLeadingParty$);
  const finalist1 = useValue(adminFinalist1$);
  const finalist2 = useValue(adminFinalist2$);
  const seats = useValue(adminSeats$);
  const voters = useValue(adminVoters$);
  const abstention = useValue(adminAbstention$);
  const resultsJson = useValue(adminResultsJson$);
  const round1ResultsJson = useValue(adminRound1ResultsJson$);
  const bastionStreakParty = useValue(adminBastionStreakParty$);
  const bastionStreakCount = useValue(adminBastionStreakCount$);
  const bastionActive = useValue(adminBastionActive$);
  const bastionReinforcedActive = useValue(adminBastionReinforcedActive$);
  const leadingPartyBonus = useValue(adminLeadingPartyBonus$);
  const [collapsed, setCollapsed] = useGlobalCollapsed();
const results: PartyResultDto[] = useMemo(() => {
  try {
    const parsed = JSON.parse(resultsJson ?? "[]"); // GARDE
    return Array.isArray(parsed) ? parsed.filter((r) => r && typeof r.party === "string") : []; // GARDE
  } catch {
    return [];
  }
}, [resultsJson]);

const round1Results: Round1ResultDto[] = useMemo(() => {
  try {
    const parsed = JSON.parse(round1ResultsJson ?? "[]");
    return Array.isArray(parsed) ? parsed.filter((r) => r && typeof r.party === "string") : [];
  } catch {
    return [];
  }
}, [round1ResultsJson]);

  const cityEventHeadline = useValue(cityEventHeadline$);

  if (!visible || !InfoSection) return null;

  const totalCast = voters + abstention;
  const abstentionPct = totalCast > 0 ? Math.round((abstention / totalCast) * 100) : 0;
  const abstentionLine = `${abstention.toLocaleString()}\u00A0(${abstentionPct}%)`;

  const leadingResult = results.find((r) => r.party === leadingParty);
  const leadingLabel = leadingParty ? translatePartyName(leadingParty, translate) : "";

  const round1WonPrefix = t("CityCouncil.Admin.ROUND1_WON_PREFIX", "District remporté par le Parti \"");
  const round1WonSuffix = t("CityCouncil.Admin.ROUND1_WON_SUFFIX", "\" dès le 1er Tour. Election terminée et en attente de la fin du 2ème tour général.");
  const round1WonLine = `${round1WonPrefix}${leadingLabel}${round1WonSuffix}`;

  const finalist1Label = finalist1 ? translatePartyName(finalist1, translate) : "";
  const finalist2Label = finalist2 ? translatePartyName(finalist2, translate) : "";

  const round1PendingPrefix = t("CityCouncil.Admin.ROUND1_PENDING_PREFIX", "Aucune majorité au 1er tour. Second tour en attente entre ");
  const round1PendingMiddle = t("CityCouncil.Admin.ROUND1_PENDING_MIDDLE", " et ");
  const round1PendingSuffix = t("CityCouncil.Admin.ROUND1_PENDING_SUFFIX", ".");
  const round1PendingLine = `${round1PendingPrefix}${finalist1Label}${round1PendingMiddle}${finalist2Label}${round1PendingSuffix}`;

  return (
    <InfoSection focusKey="cityCouncilAdmin" disableFoldout={true}> {/* MODIFIÉ — repliage géré manuellement */}
       <div style={{ padding: "4rem 8rem", marginTop: "-25rem", width: "100%", boxSizing: "border-box" }}>
          <div
            onClick={() => setCollapsed(!collapsed)}
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              cursor: "pointer",
              color: "rgba(255,255,255,0.7)",
              fontSize: "14rem",
              textTransform: "uppercase",
              marginBottom: collapsed ? 0 : "16rem",
              whiteSpace: "nowrap", wordBreak: "keep-all", overflowWrap: "normal",
            }}
          >
            <span>{t("CityCouncil.Admin.HEADER", "Administration")}</span>
            <span style={{ fontSize: "11rem", flexShrink: 0, marginLeft: "8rem" }}>
              {collapsed ? "▼" : "▲"}
            </span>
          </div>

      {!collapsed && (
        <>
          {phase === "NoElection" && (
                  <div style={{ color: "rgba(255,255,255,0.85)", fontSize: "14rem", lineHeight: "18rem" }}> {/* MODIFIÉ — retrait nowrap, ajout lineHeight */}
          {t("CityCouncil.Admin.NO_ELECTION_DESC", "Pas d'élections dans ce District car aucun habitant. Il est géré par une Commission Spéciale.")}
                    </div>
        )}

        {phase === "Round1Done" && (
          <div style={{ color: "rgba(255,255,255,0.85)", fontSize: "14rem", lineHeight: "18rem" }}> {/* MODIFIÉ — retrait nowrap */}
            {leadingParty
              ? round1WonLine
              : finalist1 && finalist2
              ? round1PendingLine
              : t("CityCouncil.Admin.ROUND1_PENDING_DEFAULT", "Aucune majorité au 1er tour. Le 2e tour est en attente.")}
          </div>
        )}

        {phase === "Round1Done" && round1Results.length > 0 && (
          <div style={{ marginTop: "10rem" }}>
            {round1Results.map((r) => (
              <Round1Bar key={r.party} result={r} translate={translate} />
            ))}
          </div>
        )}

        {phase === "Completed" && leadingParty && (
          <div>
            <PartyBadge
              result={leadingResult ?? { party: leadingParty, seats, voteShare: 0 }}
              bonus={leadingPartyBonus}
              reinforcedBastionActive={bastionReinforcedActive}
            />

                        <div style={{ marginTop: "10rem" }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "6rem" }}>
                <div style={{ color: "white", fontWeight: 400, fontSize: "15rem", whiteSpace: "nowrap", wordBreak: "keep-all", overflowWrap: "normal" }}>
                  {t("CityCouncil.Admin.VOTERS_LABEL", "Votants")}
                </div>
                <div style={{ color: "white", fontSize: "15rem", whiteSpace: "nowrap", wordBreak: "keep-all", overflowWrap: "normal" }}>
                  {voters.toLocaleString()}
                </div>
              </div>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                <div style={{ color: "white", fontWeight: 400, fontSize: "15rem", whiteSpace: "nowrap", wordBreak: "keep-all", overflowWrap: "normal" }}>
                  {t("CityCouncil.Admin.ABSTENTION_LABEL", "Abstention")}
                </div>
                <div style={{ color: "white", fontSize: "15rem", whiteSpace: "nowrap", wordBreak: "keep-all", overflowWrap: "normal" }}>
                  {abstentionLine}
                </div>
              </div>
            </div>

            {results.length > 1 && (
              <div style={{ marginTop: "10rem" }}>
                <StackedSeatBar results={results} />

                <div style={{ display: "flex", flexDirection: "column", marginTop: "8rem" }}>
                  {results.map((r) => (
                    <div
                      key={r.party}
                      style={{
                        display: "flex",
                        alignItems: "center",
                        fontSize: "12rem",
                        color: "rgba(255,255,255,0.8)",
                        marginBottom: "4rem",
                      }}
                    >
                      <div
                        style={{
                          width: "10rem",
                          height: "10rem",
                          borderRadius: "50%",
                          background: resolvePartyColor(r),
                          flexShrink: 0,
                          marginRight: "6rem",
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
            )}
            <BastionProgressBar
      streakParty={bastionStreakParty}
      streakCount={bastionStreakCount}
      active={bastionActive}
      reinforced={bastionReinforcedActive}
      translate={translate}
    />

          </div>
        )}

        {cityEventHeadline && (
          <div
            style={{
              marginTop: "10rem",
              paddingTop: "8rem",
              borderTop: "1rem solid rgba(255,255,255,0.15)",
              color: "rgba(255,220,150,0.9)",
              fontSize: "12rem",
              fontFamily: "Overpass, 'Noto Sans', sans-serif",
            }}
          >
            {t(cityEventHeadline, cityEventHeadline)}
          </div>
          )}
          </>
        )}
      </div>
    </InfoSection>
  );
};

export const AdministrationSection = (InfoSection: any) => (props: any) => (
  <SafeBoundary>
    <AdministrationSectionInner InfoSection={InfoSection} />
  </SafeBoundary>
);