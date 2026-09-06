import { useMemo, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { PARTY_ORDER, PARTY_COLORS, PartyLogo, translatePartyName } from "./PartyResultDto";

const coalitionJson$ = bindValue<string>("cityCouncil", "coalitionJson");
const customPartyExists$ = bindValue<boolean>("cityCouncil", "customPartyExists");
const customPartySpace$ = bindValue<string>("cityCouncil", "customPartySpace");
const customPartyPendingActivation$ = bindValue<boolean>("cityCouncil", "customPartyPendingActivation");

interface CoalitionDto {
  hasActiveCoalition: boolean;
  hasAbsoluteMajorityParty: boolean;
  awaitingPlayerDecision: boolean;
}

export function CoalitionProposalCard() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const json = useValue(coalitionJson$);
  const customExists = useValue(customPartyExists$);
  const customSpace = useValue(customPartySpace$);
  const customPending = useValue(customPartyPendingActivation$);

 const dto: CoalitionDto | null = useMemo(() => {
  try {
    const p = JSON.parse(json ?? "{}");
    // MODIFIÉ — vérifie la présence d'un champ qui existe réellement dans le DTO actuel
    return typeof p.awaitingPlayerDecision === "boolean" ? p : null;
  } catch {
    return null;
  }
}, [json]);

  const playerAvailable = !!customExists && !customPending && !!customSpace;
  const [selected, setSelected] = useState<Record<string, boolean>>({});

  // La carte ne s'affiche QUE si le système attend explicitement une décision
 if (!playerAvailable || !dto || !dto.awaitingPlayerDecision) return null;

  const eligibleParties = PARTY_ORDER.filter((p) => p !== customSpace);

  const toggle = (party: string) =>
    setSelected((prev) => ({ ...prev, [party]: !prev[party] }));

  const handlePropose = () => {
    const targets = eligibleParties.filter((p) => selected[p]);
    if (targets.length === 0) return;
    trigger("cityCouncil", "proposeCoalition", targets.join(","));
    setSelected({});
  };

  const handleDecline = () => {
  trigger("cityCouncil", "declineCoalitionProposal");
  setSelected({});
};

  const anySelected = eligibleParties.some((p) => selected[p]);

  return (
    <div style={{ marginTop: "14rem", padding: "10rem", background: "rgba(255,255,255,0.06)", borderRadius: "6rem" }}>
      <div style={{ color: "rgba(150,190,255,0.9)", fontSize: "13rem", fontWeight: 700, marginBottom: "6rem" }}>
        {t("CityCouncil.Coalition.PROPOSAL_HEADER", "Proposer une coalition")}
      </div>
      <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", lineHeight: "16rem", marginBottom: "12rem" }}>
        {t("CityCouncil.Coalition.PROPOSAL_INTRO", "Aucun parti ne détient la majorité absolue. Sélectionnez un ou plusieurs partis à solliciter — chacun répondra selon sa proximité politique avec votre parti et les autres membres déjà partants.")}
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: "8rem", marginBottom: "12rem" }}>
        {eligibleParties.map((p) => {
          const isSelected = !!selected[p];
          return (
            <div
              key={p}
              onClick={() => toggle(p)}
              style={{
                display: "flex", alignItems: "center", justifyContent: "space-between",
                padding: "6rem 8rem", background: isSelected ? "rgba(150,190,255,0.12)" : "rgba(255,255,255,0.04)",
                borderRadius: "4rem", cursor: "pointer",
                border: isSelected ? "1rem solid rgba(150,190,255,0.6)" : "1rem solid transparent",
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: "8rem" }}>
                <PartyLogo party={p} color={PARTY_COLORS[p] ?? "#888"} isCustom={false} sizeRem={24} />
                <span style={{ color: "white", fontSize: "12rem" }}>{translatePartyName(p, translate)}</span>
              </div>
              <div style={{
                width: "16rem", height: "16rem", borderRadius: "3rem",
                border: isSelected ? "1rem solid rgba(120,170,255,0.9)" : "1rem solid rgba(255,255,255,0.35)",
                background: isSelected ? "rgba(70,130,220,0.85)" : "rgba(255,255,255,0.06)",
                display: "flex", alignItems: "center", justifyContent: "center",
              }}>
                {isSelected && <div style={{ color: "white", fontSize: "11rem", fontWeight: "bold" }}>✓</div>}
              </div>
            </div>
          );
        })}
      </div>

        <div style={{ display: "flex", gap: "10rem" }}>
          <div style={{ flex: 1 }}>
            <button
              disabled={!anySelected}
              onClick={handlePropose}
              style={{
                width: "100%", background: anySelected ? "rgba(70,130,220,0.85)" : "rgba(255,255,255,0.08)",
                color: "white", border: "none", borderRadius: "4rem", padding: "8rem 10rem",
                fontSize: "13rem", fontWeight: "bold", cursor: anySelected ? "pointer" : "default",
              }}
            >
              {t("CityCouncil.Coalition.PROPOSE_BUTTON", "Proposer")}
            </button>
          </div>
          <div style={{ flex: 1 }}>
            <button
              onClick={handleDecline}
              style={{
                width: "100%", background: "rgba(255,255,255,0.08)",
                color: "rgba(255,255,255,0.8)", border: "1rem solid rgba(255,255,255,0.2)",
                borderRadius: "4rem", padding: "8rem 10rem", fontSize: "13rem", fontWeight: "bold",
                cursor: "pointer",
              }}
            >
              {t("CityCouncil.Coalition.DECLINE_BUTTON", "Renoncer")}
            </button>
          </div>
        </div>
    </div>
  );
}