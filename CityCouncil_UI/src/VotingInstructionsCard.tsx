import { useMemo, useState, useEffect } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { PartyLogo, PARTY_COLORS, translatePartyName } from "./PartyResultDto";

const votingInstructionDistrictsJson$ = bindValue<string>("cityCouncil", "votingInstructionDistrictsJson");

interface VotingInstructionDistrictDto {
  districtId: number;
  districtName: string;
  finalist1: string;
  finalist2: string;
  submitted: boolean;
  selectedParty: string;
}

export function VotingInstructionsCard() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const districtsJson = useValue(votingInstructionDistrictsJson$);

  const districts: VotingInstructionDistrictDto[] = useMemo(() => {
    try {
      const p = JSON.parse(districtsJson ?? "[]");
      return Array.isArray(p) ? p.filter((d) => d && typeof d.districtId === "number") : [];
    } catch {
      return [];
    }
  }, [districtsJson]);

  // Sélections locales : districtId -> parti choisi (ou undefined). Réinitialisé quand la
  // liste de districts change (nouveau cycle), pré-rempli depuis le serveur si déjà soumis.
  const [selections, setSelections] = useState<Record<number, string>>({});

  useEffect(() => {
    const initial: Record<number, string> = {};
    for (const d of districts) {
      if (d.selectedParty) initial[d.districtId] = d.selectedParty;
    }
    setSelections(initial);
  }, [districtsJson]);

  if (districts.length === 0) return null;

  const anySubmitted = districts.some((d) => d.submitted);

  const handleClick = (districtId: number, party: string, locked: boolean) => {
    if (locked) return;
    setSelections((prev) => {
      const next = { ...prev };
      if (next[districtId] === party) delete next[districtId]; // reclic = décoche
      else next[districtId] = party;
      return next;
    });
  };

  const handleSubmit = () => {
    // N'envoie que les districts non déjà verrouillés.
    const pairs = districts
      .filter((d) => !d.submitted && selections[d.districtId])
      .map((d) => `${d.districtId}:${selections[d.districtId]}`);
    trigger("cityCouncil", "submitVotingInstructions", pairs.join(","));
  };

  const title = t("CityCouncil.VotingInstruction.HEADER", "Consignes de vote");
  const intro = t(
    "CityCouncil.VotingInstruction.INTRO",
    "Vous pouvez donner une consigne de vote dans les districts suivants, toutefois il est possible que vos partisans ne la respectent pas..."
  );
  const validateLabel = t("CityCouncil.VotingInstruction.VALIDATE_BUTTON", "Valider");

  const logoStyle = (selected: boolean, locked: boolean) => ({
    padding: "3rem",
    borderRadius: "6rem",
    border: selected ? "2rem solid white" : "2rem solid transparent",
    cursor: locked ? "default" : "pointer",
    opacity: locked && !selected ? 0.4 : 1,
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
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
          const locked = d.submitted;
          const selected = selections[d.districtId];
          return (
            <div
              key={d.districtId}
              style={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                padding: "6rem 8rem",
                background: "rgba(255,255,255,0.04)",
                borderRadius: "4rem",
              }}
            >
              <span style={{ color: "white", fontSize: "12rem", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis", marginRight: "10rem" }}>
                {d.districtName}
              </span>
              <div style={{ display: "flex", gap: "8rem", flexShrink: 0 }}>
                <div
                  title={translatePartyName(d.finalist1, translate)}
                  onClick={() => handleClick(d.districtId, d.finalist1, locked)}
                  style={logoStyle(selected === d.finalist1, locked)}
                >
                  <PartyLogo party={d.finalist1} color={PARTY_COLORS[d.finalist1] ?? "#888"} isCustom={false} sizeRem={28} />
                </div>
                <div
                  title={translatePartyName(d.finalist2, translate)}
                  onClick={() => handleClick(d.districtId, d.finalist2, locked)}
                  style={logoStyle(selected === d.finalist2, locked)}
                >
                  <PartyLogo party={d.finalist2} color={PARTY_COLORS[d.finalist2] ?? "#888"} isCustom={false} sizeRem={28} />
                </div>
              </div>
            </div>
          );
        })}
      </div>

      <button
        disabled={anySubmitted}
        onClick={handleSubmit}
        style={{
          width: "100%",
          background: anySubmitted ? "rgba(50,95,165,0.9)" : "rgba(70,130,220,0.85)",
          color: "white",
          border: "none",
          borderRadius: "4rem",
          padding: "8rem 10rem",
          fontSize: "13rem",
          fontWeight: "bold",
          cursor: anySubmitted ? "default" : "pointer",
        }}
      >
        {validateLabel}
      </button>
    </div>
  );
}