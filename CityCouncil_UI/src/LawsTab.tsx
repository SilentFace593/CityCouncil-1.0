import { useMemo, useState, useRef, useEffect } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { translatePartyName, PARTY_COLORS } from "./PartyResultDto";
import { centeredTabWrapperStyle, centeredTabContentStyle } from "./layoutConstants";

const lawCatalogJson$ = bindValue<string>("cityCouncil", "lawCatalogJson");
const lawActiveVotesJson$ = bindValue<string>("cityCouncil", "lawActiveVotesJson");
const lawHistoryJson$ = bindValue<string>("cityCouncil", "lawHistoryJson");
const lawPlayerMalusPercent$ = bindValue<number>("cityCouncil", "lawPlayerMalusPercent");
const lawPlayerCanPropose$ = bindValue<boolean>("cityCouncil", "lawPlayerCanPropose");
const customPartyExists$ = bindValue<boolean>("cityCouncil", "customPartyExists");
const customPartySpace$ = bindValue<string>("cityCouncil", "customPartySpace");
const customPartyName$ = bindValue<string>("cityCouncil", "customPartyName");
const customPartyPendingActivation$ = bindValue<boolean>("cityCouncil", "customPartyPendingActivation");

const MAX_NAME_LENGTH = 50;

interface LawAdherenceDto { party: string; adherence: string; }
interface LawCatalogDto { id: string; titleLocaleKey: string; theme: string; adherence: LawAdherenceDto[]; }
interface LawActiveVoteDto {
  lawId: string; customName: string; titleLocaleKey: string; isRepeal: boolean;
  proposerParty: string; proposerIsCoalition: boolean; proposerMembers: string[];
  expiryDay: number; hasPlayerBloc: boolean; playerHasAnswered: boolean; isPlayerProposer: boolean;
}
interface LawHistoryDto {
  recordIndex: number; lawId: string; customName: string; titleLocaleKey: string;
  proposerParty: string; proposerIsCoalition: boolean; outcome: string; resolvedDay: number;
  repealed: boolean; repealerParty: string; repealerIsCoalition: boolean; repealedDay: number;
  canPlayerRepeal: boolean;
}

const ADHERENCE_LABELS: Record<string, string> = {
  TresFavorable: "++",
  PlutotFavorable: "+",
  Pragmatique: "=",
  PlutotDefavorable: "-",
  TresDefavorable: "--",
};
const ADHERENCE_COLORS: Record<string, string> = {
  TresFavorable: "rgba(120,220,120,0.9)",
  PlutotFavorable: "rgba(170,220,120,0.9)",
  Pragmatique: "rgba(200,200,200,0.7)",
  PlutotDefavorable: "rgba(230,160,110,0.9)",
  TresDefavorable: "rgba(230,110,110,0.9)",
};

interface DropdownOption {
  value: string;
  label: string;
}

interface CustomDropdownProps {
  options: DropdownOption[];
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
  disabled?: boolean;
}

function CustomDropdown({ options, value, onChange, placeholder, disabled }: CustomDropdownProps) {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const selectedOption = options.find((opt) => opt.value === value);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  return (
    <div ref={dropdownRef} style={{ position: "relative", width: "100%", marginBottom: "8rem" }}>
      <div
        onClick={() => !disabled && setIsOpen((prev) => !prev)}
        style={{
          width: "100%",
          boxSizing: "border-box",
          background: disabled ? "rgba(255,255,255,0.04)" : "rgba(255,255,255,0.08)",
          border: "1rem solid rgba(255,255,255,0.15)",
          borderRadius: "4rem",
          color: disabled ? "rgba(255,255,255,0.3)" : "white",
          fontSize: "13rem",
          padding: "6rem 8rem",
          cursor: disabled ? "default" : "pointer",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
        }}
      >
        <span>{selectedOption ? selectedOption.label : placeholder}</span>
        <span style={{ fontSize: "10rem", marginLeft: "8rem" }}>{isOpen ? "▲" : "▼"}</span>
      </div>

      {isOpen && !disabled && (
        <div
          style={{
            position: "absolute",
            top: "100%",
            left: 0,
            right: 0,
            marginTop: "2rem",
            background: "rgba(30, 35, 45, 0.95)",
            border: "1rem solid rgba(255,255,255,0.2)",
            borderRadius: "4rem",
            maxHeight: "200rem",
            overflowY: "auto",
            zIndex: 1000,
            boxShadow: "0 4rem 12rem rgba(0,0,0,0.5)",
          }}
        >
          {options.map((opt) => (
            <div
              key={opt.value}
              onClick={() => {
                onChange(opt.value);
                setIsOpen(false);
              }}
              style={{
                padding: "8rem 10rem",
                fontSize: "12rem",
                color: opt.value === value ? "#70a6ff" : "white",
                background: opt.value === value ? "rgba(255,255,255,0.1)" : "transparent",
                cursor: "pointer",
                borderBottom: "1rem solid rgba(255,255,255,0.05)",
              }}
            >
              {opt.label}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function ActionButton({ label, enabled, onClick, danger }: { label: string; enabled: boolean; onClick: () => void; danger?: boolean }) {
  return (
    <button
      disabled={!enabled}
      onClick={onClick}
      style={{
        background: !enabled ? "rgba(255,255,255,0.08)" : danger ? "rgba(200,80,80,0.85)" : "rgba(70,130,220,0.85)",
        color: "white",
        border: "none",
        borderRadius: "4rem",
        padding: "6rem 10rem",
        fontSize: "12rem",
        fontWeight: "bold",
        cursor: enabled ? "pointer" : "default",
        whiteSpace: "nowrap",
      }}
    >
      {label}
    </button>
  );
}

export function LawsTab() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const catalogJson = useValue(lawCatalogJson$);
  const activeVotesJson = useValue(lawActiveVotesJson$);
  const historyJson = useValue(lawHistoryJson$);
  const malusPercent = useValue(lawPlayerMalusPercent$);
  const canPropose = useValue(lawPlayerCanPropose$);
  const customExists = useValue(customPartyExists$);
  const customSpace = useValue(customPartySpace$);
  const customName = useValue(customPartyName$);
  const customPending = useValue(customPartyPendingActivation$);

  const playerAvailable = !!customExists && !customPending && !!customSpace;

  const catalog: LawCatalogDto[] = useMemo(() => {
    try { const p = JSON.parse(catalogJson ?? "[]"); return Array.isArray(p) ? p : []; }
    catch { return []; }
  }, [catalogJson]);

  const activeVotes: LawActiveVoteDto[] = useMemo(() => {
    try { const p = JSON.parse(activeVotesJson ?? "[]"); return Array.isArray(p) ? p : []; }
    catch { return []; }
  }, [activeVotesJson]);

  const history: LawHistoryDto[] = useMemo(() => {
    try { const p = JSON.parse(historyJson ?? "[]"); return Array.isArray(p) ? p : []; }
    catch { return []; }
  }, [historyJson]);

  const [selectedLawId, setSelectedLawId] = useState<string>("");
  const [lawName, setLawName] = useState<string>("");
  const [historyExpanded, setHistoryExpanded] = useState(false);

  const selectedLaw = catalog.find((l) => l.id === selectedLawId);
  const trimmedName = lawName.trim();
  const canSubmit = playerAvailable && canPropose && !!selectedLaw && trimmedName.length > 0 && trimmedName.length <= MAX_NAME_LENGTH;

  const dropdownOptions: DropdownOption[] = useMemo(() => {
    return catalog.map((l) => ({
      value: l.id,
      label: t(l.titleLocaleKey, l.id),
    }));
  }, [catalog, translate]);

  const handlePropose = () => {
    if (!canSubmit) return;
    trigger("cityCouncil", "proposeLaw", selectedLawId, trimmedName);
    setSelectedLawId("");
    setLawName("");
  };

  const partyLabel = (key: string): string =>
    playerAvailable && key === customSpace ? customName : translatePartyName(key, translate);

  const proposerLine = (party: string, isCoalition: boolean, members: string[]): string => {
    if (!isCoalition) return partyLabel(party);
    return members.map(partyLabel).join(" + ");
  };

  const handleRespond = (accept: boolean) => trigger("cityCouncil", "respondToLawVote", accept ? "true" : "false");
  const handleRepeal = (recordIndex: number) => trigger("cityCouncil", "proposeLawRepeal", String(recordIndex));

  const outcomeLabel = (outcome: string): string => {
    switch (outcome) {
      case "Adopted": return t("CityCouncil.Law.OUTCOME_ADOPTED", "Adoptée");
      case "Rejected": return t("CityCouncil.Law.OUTCOME_REJECTED", "Rejetée");
      case "CancelledElection": return t("CityCouncil.Law.OUTCOME_CANCELLED", "Annulée (élection)");
      default: return outcome;
    }
  };

  const pendingPlayerDecisionVote = activeVotes.find((v) => v.hasPlayerBloc && !v.playerHasAnswered && !v.isPlayerProposer);

  return (
    <div style={centeredTabWrapperStyle}>
      <div style={centeredTabContentStyle}>
        <div style={{ color: "white", fontSize: "16rem", fontWeight: 700, marginBottom: "6rem" }}>
          {t("CityCouncil.Law.TAB_TITLE", "Lois")}
        </div>
        <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", lineHeight: "17rem", marginBottom: "14rem" }}>
          {t("CityCouncil.Law.TAB_INTRO", "Proposez une loi au vote du conseil municipal, ou répondez aux propositions des autres partis. Chaque bloc politique (parti seul ou coalition) ne peut porter qu'un seul vote à la fois. La votation dure 12h in-game.")}
        </div>

        {malusPercent > 0 && (
          <div style={{ padding: "8rem 10rem", background: "rgba(230,110,110,0.12)", border: "1rem solid rgba(230,110,110,0.4)", borderRadius: "6rem", marginBottom: "14rem" }}>
            <span style={{ color: "rgba(255,180,180,0.95)", fontSize: "12rem", fontWeight: 600 }}>
              {t("CityCouncil.Law.PLAYER_MALUS_ACTIVE", "Malus actif : ")}-{Math.round(malusPercent * 100)}% {t("CityCouncil.Law.PLAYER_MALUS_SUFFIX", "d'intention de vote (vote contradictoire avec votre bord politique, jusqu'au prochain cycle électoral)")}
            </span>
          </div>
        )}

        {/* --- Décision du joueur en attente sur un vote lancé par un autre bloc --- */}
        {pendingPlayerDecisionVote && (
          <div style={{ padding: "10rem", background: "rgba(150,190,255,0.10)", border: "1rem solid rgba(150,190,255,0.4)", borderRadius: "6rem", marginBottom: "14rem" }}>
            <div style={{ color: "rgba(150,190,255,0.95)", fontSize: "13rem", fontWeight: 700, marginBottom: "6rem" }}>
              {t("CityCouncil.Law.DECISION_HEADER", "Votre parti est sollicité")}
            </div>
            <div style={{ color: "white", fontSize: "12rem", marginBottom: "10rem" }}>
              {proposerLine(pendingPlayerDecisionVote.proposerParty, pendingPlayerDecisionVote.proposerIsCoalition, pendingPlayerDecisionVote.proposerMembers)}
              {" "}{pendingPlayerDecisionVote.isRepeal
                ? t("CityCouncil.Law.PROPOSES_REPEAL_OF", "propose l'abrogation de ")
                : t("CityCouncil.Law.PROPOSES_LAW", "propose la loi ")}
              « {pendingPlayerDecisionVote.customName} »
            </div>
            <div style={{ display: "flex", gap: "10rem" }}>
              <ActionButton label={t("CityCouncil.Law.VOTE_FOR", "Voter pour")} enabled={true} onClick={() => handleRespond(true)} />
              <ActionButton label={t("CityCouncil.Law.VOTE_AGAINST", "Voter contre")} enabled={true} onClick={() => handleRespond(false)} danger />
            </div>
          </div>
        )}

        {/* --- Proposition d'une nouvelle loi --- */}
        {!playerAvailable ? (
          <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "12rem", marginBottom: "14rem" }}>
            {t("CityCouncil.Law.NO_PLAYER_PARTY", "Créez votre propre parti (onglet \"Votre Parti\") pour proposer des lois.")}
          </div>
        ) : (
          <div style={{ padding: "10rem", background: "rgba(255,255,255,0.06)", borderRadius: "6rem", marginBottom: "14rem" }}>
            <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", marginBottom: "8rem", textTransform: "uppercase" }}>
              {t("CityCouncil.Law.PROPOSE_HEADER", "Proposer une loi")}
            </div>

            {!canPropose && (
              <div style={{ color: "rgba(255,180,120,0.9)", fontSize: "12rem", marginBottom: "8rem" }}>
                {t("CityCouncil.Law.BLOC_BUSY", "Votre bloc politique a déjà un vote en cours.")}
              </div>
            )}

            <CustomDropdown
              options={dropdownOptions}
              value={selectedLawId}
              onChange={setSelectedLawId}
              placeholder={t("CityCouncil.Law.SELECT_PLACEHOLDER", "-- Choisir une loi --")}
              disabled={!canPropose}
            />

            {selectedLaw && (
              <div style={{ display: "flex", flexWrap: "wrap", gap: "6rem", marginBottom: "8rem" }}>
                {selectedLaw.adherence.map((a) => (
                  <div key={a.party} style={{ display: "flex", alignItems: "center", gap: "4rem", padding: "3rem 6rem", background: "rgba(255,255,255,0.05)", borderRadius: "4rem" }}>
                    <div style={{ width: "8rem", height: "8rem", borderRadius: "50%", background: PARTY_COLORS[a.party] ?? "#888" }} />
                    <span style={{ color: "rgba(255,255,255,0.75)", fontSize: "11rem" }}>{translatePartyName(a.party, translate)}</span>
                    <span style={{ color: ADHERENCE_COLORS[a.adherence] ?? "white", fontSize: "11rem", fontWeight: 700 }}>
                      {ADHERENCE_LABELS[a.adherence] ?? a.adherence}
                    </span>
                  </div>
                ))}
              </div>
            )}

            <input
              value={lawName}
              maxLength={MAX_NAME_LENGTH}
              onChange={(e: any) => setLawName(e.target.value)}
              disabled={!canPropose}
              placeholder={t("CityCouncil.Law.NAME_PLACEHOLDER", "Nom de la loi (50 caractères max, définitif)")}
              style={{
                width: "100%", boxSizing: "border-box", background: "rgba(255,255,255,0.08)",
                border: "1rem solid rgba(255,255,255,0.15)", borderRadius: "4rem", color: "white",
                fontSize: "13rem", padding: "6rem 8rem", marginBottom: "8rem",
              }}
            />

            <ActionButton label={t("CityCouncil.Law.PROPOSE_BUTTON", "Proposer au vote")} enabled={canSubmit} onClick={handlePropose} />
          </div>
        )}

        {/* --- Votes en cours (tous blocs) --- */}
        <div style={{ marginBottom: "14rem" }}>
          <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", marginBottom: "8rem", textTransform: "uppercase" }}>
            {t("CityCouncil.Law.ACTIVE_VOTES_HEADER", "Votes en cours")}
          </div>
          {activeVotes.length === 0 ? (
            <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "12rem" }}>
              {t("CityCouncil.Law.NO_ACTIVE_VOTES", "Aucun vote en cours.")}
            </div>
          ) : (
            activeVotes.map((v, i) => (
              <div key={i} style={{ padding: "8rem 10rem", background: "rgba(255,255,255,0.05)", borderRadius: "4rem", marginBottom: "6rem" }}>
                <div style={{ color: "white", fontSize: "12rem" }}>
                  {proposerLine(v.proposerParty, v.proposerIsCoalition, v.proposerMembers)}
                  {" — "}
                  {v.isRepeal ? t("CityCouncil.Law.LABEL_REPEAL", "Abrogation : ") : t("CityCouncil.Law.LABEL_PROPOSAL", "Proposition : ")}
                  « {v.customName} »
                </div>
              </div>
            ))
          )}
        </div>

        {/* --- Historique repliable --- */}
        <div>
          <div
            onClick={() => setHistoryExpanded((v) => !v)}
            style={{ display: "flex", alignItems: "center", justifyContent: "space-between", cursor: "pointer", color: "rgba(255,255,255,0.7)", fontSize: "12rem", marginBottom: "8rem", textTransform: "uppercase" }}
          >
            <span>{t("CityCouncil.Law.HISTORY_HEADER", "Historique")}</span>
            <span style={{ fontSize: "11rem" }}>{historyExpanded ? "▲" : "▼"}</span>
          </div>

          {historyExpanded && (
            history.length === 0 ? (
              <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "12rem" }}>
                {t("CityCouncil.Law.NO_HISTORY", "Aucune loi votée pour l'instant.")}
              </div>
            ) : (
              [...history].reverse().map((r) => (
                <div key={r.recordIndex} style={{ padding: "8rem 10rem", background: "rgba(255,255,255,0.05)", borderRadius: "4rem", marginBottom: "6rem" }}>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                    <span style={{ color: "white", fontSize: "12rem" }}>
                      « {r.customName} » — {proposerLine(r.proposerParty, r.proposerIsCoalition, [r.proposerParty])} — {outcomeLabel(r.outcome)}
                      {r.repealed && ` — ${t("CityCouncil.Law.REPEALED_BY", "abrogée par")} ${partyLabel(r.repealerParty)}`}
                    </span>
                    {r.canPlayerRepeal && (
                      <ActionButton label={t("CityCouncil.Law.REPEAL_BUTTON", "Proposer l'abrogation")} enabled={true} onClick={() => handleRepeal(r.recordIndex)} />
                    )}
                  </div>
                </div>
              ))
            )
          )}
        </div>
      </div>
    </div>
  );
}