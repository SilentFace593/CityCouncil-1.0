import { useMemo, useState } from "react";
import { bindValue, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { translatePartyName, PARTY_COLORS, CUSTOM_PARTY_PALETTE_HEX, PartyLogo } from "./PartyResultDto";
import { centeredTabWrapperStyle, centeredTabContentStyle } from "./layoutConstants";
import doodleScoreImg from "./images/doodle_score.png";

const scoreJson$ = bindValue<string>("cityCouncil", "scoreJson");
const TAB_IMAGES = {
  doodleScore: doodleScoreImg,
};

interface ScoreDto {
  party: string;
  score: number;
  displayName?: string;
  displayColor?: string;
  trophyScore: number;
  seatsHeld: number;
  districtsHeld: number;
  bastionsHeld: number;
  reinforcedBastionsHeld: number;
  membersCount: number;
  possessionScore: number;
}

function resolveLabel(d: ScoreDto, translate: (k: string, f: string | null) => string | null): string {
  if (d.displayName && d.displayName.length > 0) return d.displayName;
  return translatePartyName(d.party, translate);
}

function resolveColor(d: ScoreDto): string {
  if (d.displayColor && d.displayColor.length > 0) {
    return CUSTOM_PARTY_PALETTE_HEX[d.displayColor] ?? "#888";
  }
  return PARTY_COLORS[d.party] ?? "#888";
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "5rem" }}>
      <span style={{ color: "rgba(255,255,255,0.75)", fontSize: "12rem", whiteSpace: "nowrap" }}>{label}</span>
      <span style={{ color: "white", fontSize: "12rem", fontWeight: 600, whiteSpace: "nowrap" }}>{value}</span>
    </div>
  );
}

export function ScoreTab() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const scoreJson = useValue(scoreJson$);

  const scores: ScoreDto[] = useMemo(() => {
    try {
      const parsed = JSON.parse(scoreJson ?? "[]");
      return Array.isArray(parsed) ? parsed.filter((s) => s && typeof s.party === "string") : [];
    } catch {
      return [];
    }
  }, [scoreJson]);

  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const [hoveredParty, setHoveredParty] = useState<string | null>(null);

  const toggleExpanded = (party: string) => {
    setExpanded((prev) => ({ ...prev, [party]: !prev[party] }));
  };

  const rankLabel = (index: number) => `#${index + 1}`;

  return (
    <div style={{ ...centeredTabWrapperStyle, position: "relative", minHeight: "100%" }}>
     <div style={centeredTabContentStyle}>
      <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", marginBottom: "12rem", textTransform: "uppercase" }}>
        {t("CityCouncil.Score.HEADER", "Classement")}
      </div>

      <div style={{ display: "flex", flexDirection: "column" }}>
        {scores.map((s, i) => {
          const color = resolveColor(s);
          const label = resolveLabel(s, translate);
          const isLeader = i === 0;
          const isHovered = hoveredParty === s.party;
          const isExpanded = !!expanded[s.party];

          const baseBorderColor = isLeader ? "rgba(255,215,120,0.35)" : "rgba(255,255,255,0.15)";
          const borderColor = isHovered ? "rgba(255,215,120,0.9)" : baseBorderColor;

          return (
            <div
              key={s.party}
              onMouseEnter={() => setHoveredParty(s.party)}
              onMouseLeave={() => setHoveredParty(null)}
              style={{
                marginBottom: "6rem",
                borderRadius: "6rem",
                border: `1rem solid ${borderColor}`,
                background: isLeader ? "rgba(255,215,120,0.10)" : "rgba(255,255,255,0.05)",
                overflow: "hidden",
              }}
            >
              <div
                onClick={() => toggleExpanded(s.party)}
                style={{
                  display: "flex",
                  alignItems: "center",
                  padding: "10rem",
                  cursor: "pointer",
                }}
              >
                <div style={{ width: "34rem", color: "rgba(255,255,255,0.55)", fontSize: "13rem", fontWeight: 700, flexShrink: 0 }}>
                  {rankLabel(i)}
                </div>
                <div style={{ marginRight: "8rem" }}>
                  <PartyLogo
                    party={s.party}
                    color={resolveColor(s)}
                    isCustom={!!(s.displayName && s.displayName.length > 0)}
                    sizeRem={50}
                  />
                </div>
                <div style={{ flex: 1, minWidth: 0, color: "white", fontSize: "13rem", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                  {label}
                </div>
                <div style={{ color: "white", fontSize: "14rem", fontWeight: 700, whiteSpace: "nowrap", marginRight: "10rem" }}>
                  {s.score.toLocaleString()}
                </div>
                <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "11rem", flexShrink: 0 }}>
                  {isExpanded ? "▲" : "▼"}
                </div>
              </div>

              {isExpanded && (
                <div style={{ padding: "4rem 14rem 14rem", borderTop: "1rem solid rgba(255,255,255,0.12)" }}>
                  <DetailRow
                    label={t("CityCouncil.Score.DETAIL_TROPHY", "Trophées (conquêtes définitives)")}
                    value={s.trophyScore.toLocaleString()}
                  />
                  <DetailRow
                    label={`${t("CityCouncil.Score.DETAIL_SEATS", "Sièges détenus")} (${s.seatsHeld} × 50)`}
                    value={(s.seatsHeld * 50).toLocaleString()}
                  />
                  <DetailRow
                    label={`${t("CityCouncil.Score.DETAIL_DISTRICTS", "Districts dirigés")} (${s.districtsHeld} × 300)`}
                    value={(s.districtsHeld * 300).toLocaleString()}
                  />
                  <DetailRow
                    label={`${t("CityCouncil.Score.DETAIL_BASTIONS", "Bastions détenus")} (${s.bastionsHeld} × 600)`}
                    value={(s.bastionsHeld * 600).toLocaleString()}
                  />
                    {s.reinforcedBastionsHeld > 0 && (
                      <DetailRow
                        label={`${t("CityCouncil.Score.DETAIL_REINFORCED_BASTIONS", "Bastions renforcés")} (${s.reinforcedBastionsHeld} × 800)`}
                        value={(s.reinforcedBastionsHeld * 800).toLocaleString()}
                      />
                    )}
                    <DetailRow
                      label={`${t("CityCouncil.Score.DETAIL_MEMBERS", "Adhérents")} (${s.membersCount} × 1)`}
                      value={s.membersCount.toLocaleString()}
                    />
                  <div style={{ borderTop: "1rem solid rgba(255,255,255,0.10)", marginTop: "4rem", paddingTop: "6rem" }}>
                    <DetailRow
                      label={t("CityCouncil.Score.DETAIL_POSSESSION_TOTAL", "Total possession")}
                      value={s.possessionScore.toLocaleString()}
                    />
                  </div>
                  <div style={{ borderTop: "1rem solid rgba(255,255,255,0.15)", marginTop: "6rem", paddingTop: "6rem" }}>
                    <DetailRow
                      label={t("CityCouncil.Score.DETAIL_GRAND_TOTAL", "Score total")}
                      value={s.score.toLocaleString()}
                    />
                  </div>
                </div>
              )}
            </div>
          );
        })}
      </div>

    </div>

    <div
  style={{
    position: "absolute",
    bottom: "10rem",
    right: "10rem",
    width: "100rem", //rapport:1.44
    height: "144rem",
    backgroundImage: `url(${TAB_IMAGES.doodleScore})`,
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