import { useMemo, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import {
  PartyLogo,
  translatePartyName,
  PARTY_COLORS,
  PARTY_ORDER,
  BONUS_BADGE,
  CUSTOM_PARTY_PALETTE_HEX,
  type PartyResultDto,
} from "./PartyResultDto";
import { TreasuryBreakdown, type PartyMembershipDto } from "./TreasuryBreakdown";

const hemicycleSeatsJson$ = bindValue<string>("cityCouncil", "hemicycleSeatsJson");
const partyMembershipJson$ = bindValue<string>("cityCouncil", "partyMembershipJson");
const customPartyExists$ = bindValue<boolean>("cityCouncil", "customPartyExists");
const customPartyName$ = bindValue<string>("cityCouncil", "customPartyName");
const customPartyColor$ = bindValue<string>("cityCouncil", "customPartyColor");
const customPartySpace$ = bindValue<string>("cityCouncil", "customPartySpace");
const customPartyPendingActivation$ = bindValue<boolean>("cityCouncil", "customPartyPendingActivation");
const partyBonusesJson$ = bindValue<string>("cityCouncil", "partyBonusesJson"); 
const blackFundJson$ = bindValue<string>("cityCouncil", "blackFundJson");
const lastInvoiceLocaleKey$ = bindValue<string>("cityCouncil", "lastInvoiceLocaleKey");

interface BlackFundDto { active: boolean; balance: number; }

interface PartyBonusDto {
  party: string;
  bonus: string;
}

interface ForceEntry {
  key: string;
  label: string;
  color: string;
  description: string;
  members: number;
  seats: number;
  membershipEntry: PartyMembershipDto | null; 
  pendingReplacement: boolean;
  bonus: string;
  isPlayerParty: boolean;
}

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

export function PoliticalForcesTab() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const seatsJson = useValue(hemicycleSeatsJson$);
  const membershipJson = useValue(partyMembershipJson$);
  const customExists = useValue(customPartyExists$);
  const customName = useValue(customPartyName$);
  const customColor = useValue(customPartyColor$);
  const customSpace = useValue(customPartySpace$);
  const customPendingActivation = useValue(customPartyPendingActivation$);
  const bonusesJson = useValue(partyBonusesJson$);
  const blackFundJson = useValue(blackFundJson$);
  const lastInvoiceKey = useValue(lastInvoiceLocaleKey$);

  const blackFund: BlackFundDto = useMemo(() => {
    try {
      const p = JSON.parse(blackFundJson ?? "{}");
      return { active: !!p.active, balance: typeof p.balance === "number" ? p.balance : 0 };
    } catch {
      return { active: false, balance: 0 };
    }
  }, [blackFundJson]);

  const [transferAmount, setTransferAmount] = useState<string>("");
  const [showCloseConfirm, setShowCloseConfirm] = useState(false);

  const partyDescriptions: Record<string, string> = {
    Ecologiste: t("CityCouncil.Forces.DESC_ECOLOGISTE", "Défend une transition écologique ambitieuse et la préservation des espaces naturels."),
    Democrate: t("CityCouncil.Forces.DESC_DEMOCRATE", "Parti de centre, favorable au dialogue social et à une gestion pragmatique de la ville."),
    Populiste: t("CityCouncil.Forces.DESC_POPULISTE", "Porte-voix des mécontentements populaires, critique des taxes et des élites locales."),
    Republicain: t("CityCouncil.Forces.DESC_REPUBLICAIN", "Défend l'ordre, la sécurité et une gestion rigoureuse des finances municipales."),
    GaucheRadicale: t("CityCouncil.Forces.DESC_GAUCHERADICALE", "Milite pour une redistribution radicale des richesses et des services publics renforcés."),
  };

  const seatResults: PartyResultDto[] = useMemo(() => {
    try {
      const parsed = JSON.parse(seatsJson ?? "[]");
      return Array.isArray(parsed) ? parsed.filter((r) => r && typeof r.party === "string") : [];
    } catch {
      return [];
    }
  }, [seatsJson]);

  const membership: PartyMembershipDto[] = useMemo(() => {
    try {
      const parsed = JSON.parse(membershipJson ?? "[]");
      return Array.isArray(parsed) ? parsed.filter((m) => m && typeof m.party === "string") : [];
    } catch {
      return [];
    }
  }, [membershipJson]);

  const bonuses: PartyBonusDto[] = useMemo(() => {
    try {
      const parsed = JSON.parse(bonusesJson ?? "[]");
      return Array.isArray(parsed) ? parsed.filter((b) => b && typeof b.party === "string") : [];
    } catch {
      return [];
    }
  }, [bonusesJson]);

  const entries: ForceEntry[] = useMemo(() => {
    return PARTY_ORDER.map((partyKey) => {
      const seatEntry = seatResults.find((r) => r.party === partyKey);
      const memberEntry = membership.find((m) => m.party === partyKey);
      const bonusEntry = bonuses.find((b) => b.party === partyKey);
      const isCustomHere = customExists && !customPendingActivation && customSpace === partyKey;
      const isPendingReplacement = customExists && customPendingActivation && customSpace === partyKey;

      return {
        key: partyKey,
        label: isCustomHere ? customName : translatePartyName(partyKey, translate),
        color: isCustomHere ? (CUSTOM_PARTY_PALETTE_HEX[customColor] ?? "#888") : (PARTY_COLORS[partyKey] ?? "#888"),
        description: isCustomHere
          ? t("CityCouncil.Forces.CUSTOM_PARTY_DESC", "Votre parti politique. La personnalisation de la description est prévue dans une prochaine étape.")
          : (partyDescriptions[partyKey] ?? ""),
        members: memberEntry?.members ?? 0,
        seats: seatEntry?.seats ?? 0,
        membershipEntry: memberEntry ?? null,
        pendingReplacement: isPendingReplacement,
        bonus: bonusEntry?.bonus ?? "",
        isPlayerParty: isCustomHere,
      };
    });
  }, [seatResults, membership, customExists, customName, customColor, customSpace, customPendingActivation, partyDescriptions, translate, customName, customColor, t]);

  const [selectedKey, setSelectedKey] = useState<string>(PARTY_ORDER[0]);
  const selected = entries.find((e) => e.key === selectedKey) ?? entries[0];

  const membersWord = selected && selected.members > 1
    ? t("CityCouncil.Forces.MEMBERS_PLURAL", "adhérents")
    : t("CityCouncil.Forces.MEMBERS_SINGULAR", "adhérent");
  const membersLine = selected ? `${selected.members.toLocaleString()} ${membersWord}` : "";

  const seatsWord = selected && selected.seats > 1
    ? t("CityCouncil.Forces.SEATS_PLURAL", "sièges au total")
    : t("CityCouncil.Forces.SEATS_SINGULAR", "siège au total");
  const seatsLine = selected ? `${selected.seats} ${seatsWord}` : "";

  const pendingReplacementLabel = t("CityCouncil.Forces.PENDING_REPLACEMENT", "Parti remplacé à la prochaine élection !");
  const bonusLabel = selected && selected.bonus !== "None"
    ? (selected.bonus === "Defensif"
        ? t("CityCouncil.Forces.BONUS_DEFENSIF_LABEL", "Bonus permanent : Défensif")
        : t("CityCouncil.Forces.BONUS_OFFENSIF_LABEL", "Bonus permanent : Offensif"))
    : "";

  return (
    <div style={{ display: "flex", width: "100%", height: "100%", boxSizing: "border-box" }}>
      {/* Colonne gauche : liste des partis */}
      <div style={{ width: "150rem", flexShrink: 0, borderRight: "1rem solid rgba(255,255,255,0.12)", overflowY: "auto" }}>
        {entries.map((e) => {
          const isSelected = selected && e.key === selected.key;
          return (
            <div
              key={e.key}
              onClick={() => setSelectedKey(e.key)}
              style={{
                display: "flex",
                alignItems: "center",
                padding: "8rem 10rem",
                cursor: "pointer",
                background: isSelected ? "rgba(255,255,255,0.10)" : "transparent",
                borderLeft: isSelected ? "3rem solid white" : "3rem solid transparent",
              }}
            >
              <div
                style={{
                  width: "12rem",
                  height: "12rem",
                  borderRadius: "50%",
                  background: e.color,
                  flexShrink: 0,
                  marginRight: "8rem",
                }}
              />
              <div style={{ color: "white", fontSize: "13rem", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis", display: "flex", alignItems: "center", gap: "4rem" }}>
                <span>{e.label}</span>
                {e.bonus !== "None" && <span style={{ fontSize: "12rem" }}>{BONUS_BADGE[e.bonus] ?? ""}</span>}
              </div>
            </div>
          );
        })}
      </div>

      {/* Colonne droite : détail du parti sélectionné */}
      {selected && (
        <div style={{ flex: 1, padding: "16rem", overflowY: "auto" }}>
          <div style={{ display: "flex", alignItems: "center", marginBottom: "10rem" }}>
            <div style={{ marginRight: "10rem" }}>
              <PartyLogo party={selected.key} color={selected.color} isCustom={selected.isPlayerParty} sizeRem={90} />
            </div>
            <div style={{ color: "white", fontSize: "25rem", fontWeight: 700, whiteSpace: "nowrap" }}>
              {selected.label}
            </div>
          </div>

          <div style={{ color: "rgba(255,255,255,0.8)", fontSize: "13rem", marginBottom: "14rem", lineHeight: "18rem" }}>
            {selected.description}
          </div>

          {selected.pendingReplacement && (
            <div
              style={{
                color: "rgba(255,180,120,0.9)",
                fontSize: "12rem",
                fontWeight: 700,
                marginBottom: "10rem",
                whiteSpace: "nowrap",
              }}
            >
              {pendingReplacementLabel}
            </div>
          )}

          {bonusLabel && (
            <div style={{ color: "rgba(150,190,255,0.9)", fontSize: "12rem", fontWeight: 700, marginBottom: "10rem", whiteSpace: "nowrap" }}>
              {bonusLabel}
            </div>
          )}

          <div style={{ display: "flex", flexDirection: "column", gap: "6rem" }}>
            <div style={{ color: "rgba(255,255,255,0.9)", fontSize: "13rem", whiteSpace: "nowrap" }}>{membersLine}</div>
            <div style={{ color: "rgba(255,255,255,0.9)", fontSize: "13rem", whiteSpace: "nowrap" }}>{seatsLine}</div>
            {selected.membershipEntry && <TreasuryBreakdown entry={selected.membershipEntry} t={t} />}
            
            {selected.isPlayerParty && (
              <div style={{ marginTop: "14rem" }}>
                <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "11rem", marginBottom: "6rem", textTransform: "uppercase" }}>
                  {t("CityCouncil.BlackFund.HEADER", "Caisse noire")}
                </div>

                {!blackFund.active ? (
                  <ActionButton
                    label={t("CityCouncil.BlackFund.ACTIVATE_BUTTON", "Ouvrir une caisse noire")}
                    enabled={true}
                    onClick={() => trigger("cityCouncil", "activateBlackFund")}
                  />
                ) : (
                  <div style={{ background: "rgba(255,255,255,0.06)", borderRadius: "6rem", padding: "10rem" }}>
                    <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "10rem" }}>
                      <span style={{ color: "rgba(255,255,255,0.75)", fontSize: "12rem" }}>
                        {t("CityCouncil.BlackFund.BALANCE_LABEL", "Solde de la caisse noire")}
                      </span>
                      <span style={{ color: "white", fontSize: "13rem", fontWeight: 700 }}>
                        {blackFund.balance.toLocaleString()}
                      </span>
                    </div>

                    <input
                      type="number"
                      min={0}
                      value={transferAmount}
                      onChange={(e) => setTransferAmount(e.target.value)}
                      placeholder={t("CityCouncil.BlackFund.AMOUNT_PLACEHOLDER", "Montant")}
                      style={{
                        width: "100%", boxSizing: "border-box", background: "rgba(255,255,255,0.08)",
                        border: "1rem solid rgba(255,255,255,0.15)", borderRadius: "4rem", color: "white",
                        fontSize: "13rem", padding: "6rem 8rem", marginBottom: "8rem",
                      }}
                    />

                    <div style={{ display: "flex", marginBottom: "10rem" }}>
                      <div style={{ marginRight: "16rem" }}>
                        <ActionButton
                          label={t("CityCouncil.BlackFund.TRANSFER_TO_LABEL", "Vers la caisse noire")}
                          enabled={!!transferAmount && Number(transferAmount) > 0}
                          onClick={() => {
                            trigger("cityCouncil", "transferBlackFund", transferAmount, "toBlackFund");
                            setTransferAmount("");
                          }}
                        />
                      </div>
                      <ActionButton
                        label={t("CityCouncil.BlackFund.TRANSFER_FROM_LABEL", "Vers le compte principal")}
                        enabled={!!transferAmount && Number(transferAmount) > 0}
                        onClick={() => {
                          trigger("cityCouncil", "transferBlackFund", transferAmount, "fromBlackFund");
                          setTransferAmount("");
                        }}
                      />
                    </div>

                    {lastInvoiceKey && (
                      <div style={{ color: "rgba(255,220,150,0.85)", fontSize: "11rem", fontStyle: "italic", marginBottom: "10rem" }}>
                        {t(lastInvoiceKey, lastInvoiceKey)}
                      </div>
                    )}

                    {!showCloseConfirm ? (
                      <ActionButton
                        label={t("CityCouncil.BlackFund.CLOSE_BUTTON", "Fermer la caisse noire")}
                        enabled={true}
                        onClick={() => setShowCloseConfirm(true)}
                      />
                    ) : (
                      <div>
                        <div style={{ color: "rgba(255,140,140,0.9)", fontSize: "11rem", marginBottom: "8rem" }}>
                          {t("CityCouncil.BlackFund.CLOSE_WARNING", "Fermer la caisse noire fera perdre tout l'argent qu'elle contient.")}
                        </div>
                        <div style={{ display: "flex" }}>
                              <div style={{ marginRight: "16rem" }}>
                                <ActionButton
                                  label={t("CityCouncil.BlackFund.CLOSE_CONFIRM", "Confirmer la fermeture")}
                                  enabled={true}
                                  onClick={() => { trigger("cityCouncil", "closeBlackFund"); setShowCloseConfirm(false); }}
                                />
                              </div>
                              <ActionButton
                                label={t("CityCouncil.YourPartyTab.CANCEL_BUTTON", "Annuler")}
                                enabled={true}
                                onClick={() => setShowCloseConfirm(false)}
                              />
                        </div>
                      </div>
                    )}
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}