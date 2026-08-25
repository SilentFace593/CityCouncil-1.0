import { useMemo } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import {
  resolvePartyLabel,
  resolvePartyColor,
  PARTY_ORDER,
  type PartyResultDto,
  type TranslateFn,
  translatePartyName,
} from "./PartyResultDto";
import { centeredTabWrapperStyle, centeredTabContentStyle } from "./layoutConstants";

const pollResultsJson$ = bindValue<string>("cityCouncil", "pollResultsJson");
const pollAllowed$ = bindValue<boolean>("cityCouncil", "pollAllowed");
const partyMembershipJson$ = bindValue<string>("cityCouncil", "partyMembershipJson");
const customPartyExists$ = bindValue<boolean>("cityCouncil", "customPartyExists");
const customPartySpace$ = bindValue<string>("cityCouncil", "customPartySpace");
const customPartyPendingActivation$ = bindValue<boolean>("cityCouncil", "customPartyPendingActivation");
const electoralContextJson$ = bindValue<string>("cityCouncil", "electoralContextJson");
const cityEventHeadline$ = bindValue<string>("cityCouncil", "cityEventHeadline");

interface PartyMembershipDto {
  party: string;
  treasury: number;
}

interface ElectoralContextPropagandaDto {
  party: string;
  target: string;
  bonusPercent: number;
}
interface ElectoralContextSanctionDto {
  party: string;
  malusPercent: number;
}
interface ElectoralContextDto {
  hasEvent: boolean;
  eventHeadlineKey: string;
  unemploymentActive: boolean;
  taxPoorActive: boolean;
  taxRichActive: boolean;
  propaganda: ElectoralContextPropagandaDto[];
  sanctions: ElectoralContextSanctionDto[];
}

// Coût fixe — dupliqué depuis CouncilPollSystem.PollCost (même choix de duplication de
// constante que MAX_AMOUNT/INTENSITY_TIERS ailleurs dans le mod).
const POLL_COST = 4000;

// Marge d'erreur affichée entre parenthèses — dupliquée depuis
// VoteCalculator.MarginOfErrorPct (±2.5 points de pourcentage absolus).
const MARGIN_OF_ERROR_PCT = 2.5;

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

function PollBar({ result, translate }: { result: PartyResultDto; translate: TranslateFn }) {
  const label = resolvePartyLabel(result, translate);
  const color = resolvePartyColor(result);
  const share = result.voteShare ?? 0;
  const pct = Math.round(share * 1000) / 10; // 1 décimale
  const widthPct = Math.max(0, Math.min(100, share * 100));
  const line = `${pct}% (±${MARGIN_OF_ERROR_PCT}%)`;

  return (
    <div style={{ display: "flex", alignItems: "center", marginBottom: "10rem" }}>
      <div style={{ width: "110rem", flexShrink: 0, display: "flex", alignItems: "center" }}>
        <div
          style={{
            width: "10rem", height: "10rem", borderRadius: "50%",
            background: color, flexShrink: 0, marginRight: "6rem",
          }}
        />
        <span style={{ color: "white", fontSize: "12rem", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
          {label}
        </span>
      </div>

      <div style={{ flex: 1, height: "16rem", background: "rgba(255,255,255,0.06)", borderRadius: "3rem", overflow: "hidden", marginRight: "8rem" }}>
        <div style={{ width: `${widthPct}%`, height: "100%", background: color }} />
      </div>

      <div style={{ width: "90rem", flexShrink: 0, color: "rgba(255,255,255,0.85)", fontSize: "12rem", whiteSpace: "nowrap" }}>
        {line}
      </div>
    </div>
  );
}

function ContextLine({ text }: { text: string }) {
  return (
    <div style={{ display: "flex", alignItems: "flex-start", marginBottom: "6rem" }}>
      <div
        style={{
          width: "6rem", height: "6rem", borderRadius: "50%",
          background: "rgba(255,180,120,0.9)", flexShrink: 0,
          marginTop: "5rem", marginRight: "8rem",
        }}
      />
      <span style={{ color: "rgba(255,255,255,0.85)", fontSize: "12rem", lineHeight: "16rem" }}>
        {text}
      </span>
    </div>
  );
}

export function PollTab() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const resultsJson = useValue(pollResultsJson$);
  const pollAllowed = useValue(pollAllowed$);
  const membershipJson = useValue(partyMembershipJson$);
  const customExists = useValue(customPartyExists$);
  const customSpace = useValue(customPartySpace$);
  const customPending = useValue(customPartyPendingActivation$);
  const electoralContextJson = useValue(electoralContextJson$);
  const cityEventHeadline = useValue(cityEventHeadline$);

  const playerControlsAvailable = !!customExists && !customPending && !!customSpace;

  const electoralContext: ElectoralContextDto | null = useMemo(() => {
  try {
    const p = JSON.parse(electoralContextJson ?? "{}");
    return {
      hasEvent: !!p.hasEvent,
      eventHeadlineKey: p.eventHeadlineKey ?? "",
      unemploymentActive: !!p.unemploymentActive,
      taxPoorActive: !!p.taxPoorActive,
      taxRichActive: !!p.taxRichActive,
      propaganda: Array.isArray(p.propaganda) ? p.propaganda : [],
      sanctions: Array.isArray(p.sanctions) ? p.sanctions : [],
    };
  } catch {
    return null;
  }
}, [electoralContextJson]);

const contextLines: string[] = useMemo(() => {
  if (!electoralContext) return [];
  const lines: string[] = [];

  if (electoralContext.hasEvent && cityEventHeadline) {
    lines.push(t(cityEventHeadline, cityEventHeadline));
  }
  if (electoralContext.unemploymentActive) {
    lines.push(t("CityCouncil.Poll.CONTEXT_UNEMPLOYMENT", "Chômage élevé"));
  }
  if (electoralContext.taxPoorActive) {
    lines.push(t("CityCouncil.Poll.CONTEXT_TAX_POOR", "Impôts trop élevés pour les ménages modestes"));
  }
  if (electoralContext.taxRichActive) {
    lines.push(t("CityCouncil.Poll.CONTEXT_TAX_RICH", "Avantages fiscaux pour les plus riches"));
  }
  for (const c of electoralContext.propaganda) {
    const prefix = t("CityCouncil.Poll.CONTEXT_PROPAGANDA_PREFIX", "Campagne de propagande : ");
    const targetLabel = c.target === "Adultes"
      ? t("CityCouncil.Propaganda.TARGET_ADULTS", "Adultes")
      : t("CityCouncil.Propaganda.TARGET_SENIORS", "Séniors");
    lines.push(`${prefix}${translatePartyName(c.party, translate)} (${targetLabel}, +${Math.round(c.bonusPercent * 100)}%)`);
  }
  for (const s of electoralContext.sanctions) {
    const prefix = t("CityCouncil.Poll.CONTEXT_SANCTION_PREFIX", "Sanction ville entière : ");
    lines.push(`${prefix}${translatePartyName(s.party, translate)} (-${Math.round(s.malusPercent * 100)}%)`);
  }

  return lines;
}, [electoralContext, cityEventHeadline, translate]);

  const results: PartyResultDto[] = useMemo(() => {
    try {
      const parsed = JSON.parse(resultsJson ?? "[]");
      return Array.isArray(parsed) ? parsed.filter((r) => r && typeof r.party === "string") : [];
    } catch {
      return [];
    }
  }, [resultsJson]);

  const membership: PartyMembershipDto[] = useMemo(() => {
    try {
      const parsed = JSON.parse(membershipJson ?? "[]");
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }, [membershipJson]);

  const treasury = playerControlsAvailable
    ? (membership.find((m) => m.party === customSpace)?.treasury ?? 0)
    : 0;
  const canAfford = treasury >= POLL_COST;

  const orderedResults = PARTY_ORDER
    .map((key) => results.find((r) => r.party === key))
    .filter((r): r is PartyResultDto => !!r);

  const handleOrder = () => trigger("cityCouncil", "orderPoll");

  const costLine = `${t("CityCouncil.Poll.COST_LABEL", "Coût : ")}${POLL_COST.toLocaleString()}`;

  return (
  <div style={centeredTabWrapperStyle}>
    <div style={centeredTabContentStyle}>
      {!playerControlsAvailable && (
        <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "12rem", lineHeight: "17rem", marginBottom: "12rem" }}>
          {t("CityCouncil.Poll.NO_PLAYER_PARTY", "Créez votre propre parti (onglet \"Votre Parti\") pour commander des sondages.")}
        </div>
      )}

      {playerControlsAvailable && (
        <div style={{ padding: "10rem", background: "rgba(255,255,255,0.06)", borderRadius: "6rem", marginBottom: "14rem" }}>
          <div style={{ color: "white", fontSize: "13rem", marginBottom: "8rem" }}>{costLine}</div>

          {!pollAllowed && (
            <div style={{ color: "rgba(255,180,120,0.9)", fontSize: "12rem", marginBottom: "8rem", lineHeight: "16rem" }}>
              {t("CityCouncil.Poll.BLACKOUT_MESSAGE", "Sondages interdits de la veille du 1er tour jusqu'à l'issue du 2e tour.")}
            </div>
          )}

          {pollAllowed && !canAfford && (
            <div style={{ color: "rgba(255,120,120,0.9)", fontSize: "12rem", marginBottom: "8rem" }}>
              {t("CityCouncil.Poll.INSUFFICIENT_FUNDS", "Réserves insuffisantes.")}
            </div>
          )}

          <ActionButton
            label={t("CityCouncil.Poll.ORDER_BUTTON", "Commander un sondage")}
            enabled={pollAllowed && canAfford}
            onClick={handleOrder}
          />
        </div>
      )}

      <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", marginBottom: "10rem", textTransform: "uppercase" }}>
        {t("CityCouncil.Poll.RESULTS_HEADER", "Derniers résultats")}
      </div>

      {orderedResults.length === 0 ? (
        <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "12rem" }}>
          {t("CityCouncil.Poll.NO_RESULTS", "Aucun sondage n'a encore été commandé.")}
        </div>
      ) : (
        <div>
          {orderedResults.map((r) => (
            <PollBar key={r.party} result={r} translate={translate} />
          ))}
        </div>
      )}

            <div style={{ marginTop: "20rem", paddingTop: "16rem", borderTop: "1rem solid rgba(255,255,255,0.15)" }}>
        <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", marginBottom: "10rem", textTransform: "uppercase" }}>
          {t("CityCouncil.Poll.CONTEXT_HEADER", "Contexte électoral")}
        </div>

        {contextLines.length === 0 ? (
          <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "12rem" }}>
            {t("CityCouncil.Poll.CONTEXT_EMPTY", "Aucun facteur city-wide notable pour le moment.")}
          </div>
        ) : (
          <div style={{ padding: "10rem", background: "rgba(255,255,255,0.06)", borderRadius: "6rem" }}>
            {contextLines.map((line, i) => (
              <ContextLine key={i} text={line} />
            ))}
          </div>
        )}
      </div>

    </div>
  </div>
  );
}