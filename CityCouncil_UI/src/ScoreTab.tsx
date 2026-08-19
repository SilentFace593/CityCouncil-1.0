import { useMemo } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { translatePartyName, PARTY_COLORS, CUSTOM_PARTY_PALETTE_HEX, PartyLogo } from "./PartyResultDto";

const scoreJson$ = bindValue<string>("cityCouncil", "scoreJson");

interface ScoreDto {
  party: string;
  score: number;
  displayName?: string;
  displayColor?: string;
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

  const rankLabel = (index: number) => `#${index + 1}`;

  return (
    <div style={{ padding: "10rem", width: "100%", boxSizing: "border-box" }}>
      <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", marginBottom: "12rem", textTransform: "uppercase" }}>
        {t("CityCouncil.Score.HEADER", "Classement")}
      </div>

      <div style={{ display: "flex", flexDirection: "column" }}>
        {scores.map((s, i) => {
          const color = resolveColor(s);
          const label = resolveLabel(s, translate);
          return (
            <div
              key={s.party}
              style={{
                display: "flex",
                alignItems: "center",
                padding: "10rem",
                marginBottom: "6rem",
                background: i === 0 ? "rgba(255,215,120,0.10)" : "rgba(255,255,255,0.05)",
                borderRadius: "6rem",
                border: i === 0 ? "1rem solid rgba(255,215,120,0.35)" : "1rem solid transparent",
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
              <div style={{ flex: 1, color: "white", fontSize: "13rem", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                {label}
              </div>
              <div style={{ color: "white", fontSize: "14rem", fontWeight: 700, whiteSpace: "nowrap" }}>
                {s.score.toLocaleString()}
              </div>
            </div>
          );
        })}
      </div>

    </div>
  );
}