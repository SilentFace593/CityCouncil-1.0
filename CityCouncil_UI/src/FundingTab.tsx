import { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { centeredTabWrapperStyle, centeredTabContentStyle } from "./layoutConstants";
import doodleFundingImg from "./images/doodle_finances.png";

const fundingFixedAmount$ = bindValue<number>("cityCouncil", "fundingFixedAmount");
const fundingLocked$ = bindValue<boolean>("cityCouncil", "fundingLocked");
const fundingAutoRenew$ = bindValue<boolean>("cityCouncil", "fundingAutoRenew");
const TAB_IMAGES = { doodleFunding: doodleFundingImg };

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
  const autoRenew = useValue(fundingAutoRenew$);

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

  const handleValidate = () =>
    trigger("cityCouncil", "validateFundingFixedAmount", autoRenew ? "true" : "false");

  // Le toggle marche que le montant soit verrouillé ou non — on peut désactiver une
  // reconduction déjà programmée sans attendre que le cycle en cours se termine (même
  // esprit que "Annuler la campagne" en propagande).
  const handleToggleAutoRenew = () =>
    trigger("cityCouncil", "setFundingAutoRenew", autoRenew ? "false" : "true");

  // Toutes les phrases mêlant texte + variable sont construites en une seule chaîne
  // (même contrainte que sur YourPartyTab / AdministrationSection).
  const fixedAmountLabel = t("CityCouncil.Funding.FIXED_AMOUNT_LABEL", "Part fixe (par cycle)");
  const fixedAmountHint = t("CityCouncil.Funding.FIXED_AMOUNT_HINT", "Répartie à parts égales entre les 5 partis à la fin du cycle électoral.");
  const validateLabel = t("CityCouncil.Funding.VALIDATE_BUTTON", "Valider le montant");
  const lockedMessage = t("CityCouncil.Funding.LOCKED_MESSAGE", "Montant verrouillé jusqu'à la prochaine distribution.");
  const variableInfo = t("CityCouncil.Funding.VARIABLE_INFO", "Part variable : 1 000 crédits par siège obtenu, versée automatiquement.");
  const autoRenewLabel = t("CityCouncil.Funding.AUTO_RENEW_LABEL", "Reconduire automatiquement");

  return (
  <div style={{ ...centeredTabWrapperStyle, position: "relative", minHeight: "100%" }}>
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

        <div
          onClick={handleToggleAutoRenew}
          style={{ display: "flex", alignItems: "center", cursor: "pointer", marginBottom: "8rem" }}
        >
          <div
            style={{
              width: "16rem",
              height: "16rem",
              borderRadius: "3rem",
              border: autoRenew ? "1rem solid rgba(120,170,255,0.9)" : "1rem solid rgba(255,255,255,0.35)",
              background: autoRenew ? "rgba(70,130,220,0.85)" : "rgba(255,255,255,0.06)",
              marginRight: "8rem",
              flexShrink: 0,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
            }}
          >
            {autoRenew && <div style={{ color: "white", fontSize: "11rem", fontWeight: "bold" }}>✓</div>}
          </div>
          <div style={{ color: "rgba(255,255,255,0.9)", fontSize: "13rem", whiteSpace: "nowrap" }}>
            {autoRenewLabel}
          </div>
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

    <div
  style={{
    position: "absolute",
    bottom: "10rem",
    right: "10rem",
    width: "110rem", //rapport:1.01
    height: "111rem",
    backgroundImage: `url(${TAB_IMAGES.doodleFunding})`,
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