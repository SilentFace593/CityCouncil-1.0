import type { TranslateFn } from "./PartyResultDto";

export interface PartyMembershipDto {
  party: string;
  members: number;
  treasury: number;
  fromCityFunding: number;
  fromDues: number;
  spentPropaganda: number;
  spentPolls: number;
}

interface TreasuryRow {
  label: string;
  value: number;
  sign: "+" | "-";
}

/**
 * Détail des flux cumulatifs de trésorerie d'un parti (pas d'historique par cycle : le mod
 * ne conserve aucune série temporelle ailleurs, cf. remarque CouncilComponents.cs). Les 3
 * premières lignes sont des CUMULS depuis la création du parti (ou depuis le dernier reset,
 * cf. CouncilCustomPartySystem.ApplyPendingChangesForNewElection) ; la ligne du bas est le
 * SOLDE ACTUEL — volontairement distingués visuellement pour ne pas laisser penser que
 * cumuls - dépenses = solde (faux après un reset ou si le montant municipal a changé).
 */
export function TreasuryBreakdown({
  entry,
  t,
}: {
  entry: PartyMembershipDto;
  t: (key: string, fallback: string) => string;
}) {
  const rows: TreasuryRow[] = [
    { label: t("CityCouncil.Treasury.CITY_FUNDING", "Financement municipal"), value: entry.fromCityFunding, sign: "+" },
    { label: t("CityCouncil.Treasury.DUES", "Cotisations des adhérents"), value: entry.fromDues, sign: "+" },
    { label: t("CityCouncil.Treasury.PROPAGANDA_SPENT", "Dépenses de propagande"), value: entry.spentPropaganda, sign: "-" },
    { label: t("CityCouncil.Treasury.POLLS_SPENT", "Dépenses de sondages"), value: entry.spentPolls, sign: "-" },
  ];

  return (
    <div style={{ marginTop: "10rem" }}>
      <div
        style={{
          color: "rgba(255,255,255,0.6)",
          fontSize: "11rem",
          marginBottom: "6rem",
          textTransform: "uppercase",
          whiteSpace: "nowrap",
        }}
      >
        {t("CityCouncil.Treasury.HEADER", "Détail de la trésorerie")}
      </div>

      <div
        style={{
          background: "rgba(255,255,255,0.06)",
          borderRadius: "6rem",
          padding: "8rem 10rem",
        }}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "5rem" }}>
          {rows.map((r) => {
            const line = `${r.sign}${r.value.toLocaleString()}`;
            return (
              <div
                key={r.label}
                style={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  fontSize: "12rem",
                }}
              >
                <span style={{ color: "rgba(255,255,255,0.75)", whiteSpace: "nowrap" }}>{r.label}</span>
                <span
                  style={{
                    color: r.sign === "-" ? "rgba(255,140,140,0.9)" : "rgba(150,220,150,0.9)",
                    whiteSpace: "nowrap",
                    fontWeight: 600,
                  }}
                >
                  {line}
                </span>
              </div>
            );
          })}
        </div>

        <div
          style={{
            borderTop: "1rem solid rgba(255,255,255,0.15)",
            marginTop: "8rem",
            paddingTop: "8rem",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
          }}
        >
          <span style={{ color: "white", fontSize: "13rem", fontWeight: 700, whiteSpace: "nowrap" }}>
            {t("CityCouncil.Treasury.CURRENT_TOTAL", "Solde actuel")}
          </span>
          <span style={{ color: "white", fontSize: "13rem", fontWeight: 700, whiteSpace: "nowrap" }}>
            {entry.treasury.toLocaleString()}
          </span>
        </div>
      </div>
    </div>
  );
}