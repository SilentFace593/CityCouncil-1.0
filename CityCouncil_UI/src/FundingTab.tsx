import { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { centeredTabWrapperStyle, centeredTabContentStyle } from "./layoutConstants";

const fundingFixedAmount$ = bindValue<number>("cityCouncil", "fundingFixedAmount");
const fundingLocked$ = bindValue<boolean>("cityCouncil", "fundingLocked");

const MAX_AMOUNT = 100000;

function ActionButton({ label, enabled, onClick }: { label: string; enabled: boolean; onClick: () => void }) {
  return (
    <button
      disabled={!enabled}
      onClick={onClick}
      style={{
        background: enabled ? "rgba(70,130,220,0.85)" : "rgba(255,255,255,0.08)",
        color: "white",
        border: "none",
        borderRadius: "4rem",
        padding: "6rem 10rem",
        fontSize: "13rem",
        fontWeight: "bold",
        cursor: enabled ? "pointer" : "default",
        whiteSpace: "nowrap",
      }}
    >
      {label}
    </button>
  );
}

export function FundingTab() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const currentAmount = useValue(fundingFixedAmount$);
  const locked = useValue(fundingLocked$);

  const [amount, setAmount] = useState<number>(0);

  useEffect(() => {
    setAmount(currentAmount);
  }, [currentAmount]);

  const clamp = (v: number) => Math.max(0, Math.min(MAX_AMOUNT, Math.round(v)));

  const handleChange = (raw: string) => {
    const parsed = Number(raw);
    if (Number.isNaN(parsed)) return;
    const clamped = clamp(parsed);
    setAmount(clamped);
    trigger("cityCouncil", "setFundingFixedAmount", String(clamped));
  };

  const handleValidate = () => trigger("cityCouncil", "validateFundingFixedAmount");

  // Toutes les phrases mêlant texte + variable sont construites en une seule chaîne
  // (même contrainte que sur YourPartyTab / AdministrationSection).
  const fixedAmountLabel = t("CityCouncil.Funding.FIXED_AMOUNT_LABEL", "Part fixe (par cycle)");
  const fixedAmountHint = t("CityCouncil.Funding.FIXED_AMOUNT_HINT", "Répartie à parts égales entre les 5 partis à la fin du cycle électoral.");
  const validateLabel = t("CityCouncil.Funding.VALIDATE_BUTTON", "Valider le montant");
  const lockedMessage = t("CityCouncil.Funding.LOCKED_MESSAGE", "Montant verrouillé jusqu'à la prochaine distribution.");
  const variableInfo = t("CityCouncil.Funding.VARIABLE_INFO", "Part variable : 1 000 crédits par siège obtenu, versée automatiquement.");

  return (
  <div style={centeredTabWrapperStyle}>
   <div style={centeredTabContentStyle}>
      <div
        style={{
          marginBottom: "14rem",
          padding: "10rem",
          background: "rgba(255,255,255,0.06)",
          borderRadius: "6rem",
        }}
      >
        <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "11rem", marginBottom: "6rem", textTransform: "uppercase" }}>
          {fixedAmountLabel}
        </div>

        <div style={{ display: "flex", alignItems: "center", marginBottom: "8rem" }}>
          <input
            type="number"
            min={0}
            max={MAX_AMOUNT}
            step={1000}
            value={amount}
            disabled={locked}
            onChange={(e) => handleChange(e.target.value)}
            style={{
              width: "120rem",
              boxSizing: "border-box",
              background: locked ? "rgba(255,255,255,0.04)" : "rgba(255,255,255,0.08)",
              border: "1rem solid rgba(255,255,255,0.15)",
              borderRadius: "4rem",
              color: "white",
              fontSize: "13rem",
              padding: "6rem 8rem",
              marginRight: "16rem",
            }}
          />
          <ActionButton label={validateLabel} enabled={!locked} onClick={handleValidate} />
        </div>

        <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "11rem", lineHeight: "15rem", marginBottom: locked ? "6rem" : 0 }}>
          {fixedAmountHint}
        </div>

        {locked && (
          <div style={{ color: "rgba(255,180,120,0.9)", fontSize: "12rem" }}>
            {lockedMessage}
          </div>
        )}
      </div>

      <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", lineHeight: "17rem" }}>
        {variableInfo}
      </div>
    </div>
   </div>
  );
}