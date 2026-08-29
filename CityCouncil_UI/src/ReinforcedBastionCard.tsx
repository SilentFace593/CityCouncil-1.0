import { useEffect, useMemo, useRef, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";

const reinforcedBastionEligibleDistrictsJson$ = bindValue<string>("cityCouncil", "reinforcedBastionEligibleDistrictsJson");

interface ReinforcedBastionDistrictDto {
  districtId: number;
  districtName: string;
  population: number;
}

export function ReinforcedBastionCard() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const districtsJson = useValue(reinforcedBastionEligibleDistrictsJson$);

  const districts: ReinforcedBastionDistrictDto[] = useMemo(() => {
    try {
      const p = JSON.parse(districtsJson ?? "[]");
      return Array.isArray(p) ? p.filter((d) => d && typeof d.districtId === "number") : [];
    } catch {
      return [];
    }
  }, [districtsJson]);

  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [dismissed, setDismissed] = useState(false);
  const prevCountRef = useRef(0);

  useEffect(() => {
  if (prevCountRef.current === 0 && districts.length > 0) {
    setDismissed(false);
  }
  prevCountRef.current = districts.length;
}, [districts.length]);

if (districts.length === 0 || dismissed) return null;

  const handleToggle = (districtId: number) => {
    setSelectedId((prev) => (prev === districtId ? null : districtId));
  };

  const handleValidate = () => {
    if (selectedId === null) return;
    trigger("cityCouncil", "chooseReinforcedBastion", String(selectedId));
    setSelectedId(null);
    setDismissed(true);
  };

  const title = t("CityCouncil.ReinforcedBastion.HEADER", "Bastion Renforcé");
  const intro = t(
    "CityCouncil.ReinforcedBastion.INTRO",
    "Vous pouvez, ou non, choisir un Bastion Renforcé éligible - Bonus +5% d'intention de vote dans ce District. Vous n'en avez droit qu'à un seul, mais vous pourrez en changer au prochain scrutin s'il y a de nouveau un District éligible."
  );
  const validateLabel = t("CityCouncil.ReinforcedBastion.VALIDATE_BUTTON", "Valider");

  const rowStyle = (selected: boolean) => ({
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    padding: "6rem 8rem",
    background: selected ? "rgba(150,190,255,0.12)" : "rgba(255,255,255,0.04)",
    borderRadius: "4rem",
    cursor: "pointer",
    border: selected ? "1rem solid rgba(150,190,255,0.6)" : "1rem solid transparent",
  });

  return (
    <div style={{ marginTop: "14rem", padding: "10rem", background: "rgba(255,255,255,0.06)", borderRadius: "6rem" }}>
      <div style={{ color: "rgba(150,190,255,0.9)", fontSize: "13rem", fontWeight: 700, marginBottom: "6rem", whiteSpace: "nowrap" }}>
        {title}
      </div>
      <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", lineHeight: "16rem", marginBottom: "12rem" }}>
        {intro}
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: "8rem", marginBottom: "12rem" }}>
        {districts.map((d) => {
          const selected = selectedId === d.districtId;
          const line = `${d.districtName} (${d.population.toLocaleString()})`;
          return (
            <div key={d.districtId} onClick={() => handleToggle(d.districtId)} style={rowStyle(selected)}>
              <span style={{ color: "white", fontSize: "12rem", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis", marginRight: "10rem" }}>
        {line}
      </span>
              <div
                style={{
                  width: "16rem",
                  height: "16rem",
                  borderRadius: "3rem",
                  border: selected ? "1rem solid rgba(120,170,255,0.9)" : "1rem solid rgba(255,255,255,0.35)",
                  background: selected ? "rgba(70,130,220,0.85)" : "rgba(255,255,255,0.06)",
                  flexShrink: 0,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                }}
              >
                {selected && <div style={{ color: "white", fontSize: "11rem", fontWeight: "bold" }}>✓</div>}
              </div>
            </div>
          );
        })}
      </div>

      <button
        disabled={selectedId === null}
        onClick={handleValidate}
        style={{
          width: "100%",
          background: selectedId !== null ? "rgba(70,130,220,0.85)" : "rgba(255,255,255,0.08)",
          color: "white",
          border: "none",
          borderRadius: "4rem",
          padding: "8rem 10rem",
          fontSize: "13rem",
          fontWeight: "bold",
          cursor: selectedId !== null ? "pointer" : "default",
        }}
      >
        {validateLabel}
      </button>
    </div>
  );
}