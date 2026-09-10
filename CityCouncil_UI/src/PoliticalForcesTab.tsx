import { useMemo, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Scrollable } from "cs2/ui";
import {
  PartyLogo,
  translatePartyName,
  PARTY_COLORS,
  PARTY_ORDER,
  CUSTOM_PARTY_PALETTE_HEX,
  type PartyResultDto,
} from "./PartyResultDto";
import { TreasuryBreakdown, type PartyMembershipDto } from "./TreasuryBreakdown";
import bonusExcluRepublicainIcon from "./images/Bonus_EXCLU_Republicain.png";
import bonusExcluPopulisteIcon from "./images/Bonus_EXCLU_Populiste.png";
import bonusExcluEcologisteIcon from "./images/Bonus_EXCLU_Ecologiste.png";
import bonusExcluGaucheRadicaleIcon from "./images/Bonus_EXCLU_Gauche.png";
import bonusExcluDemocrateIcon from "./images/Bonus_EXCLU_Democrate.png";
import { PartyDescriptionBlock } from "./PartyDescriptionBlock";
import { BonusBadgeIcon } from "./PartyResultDto";

const districtsOverviewJson$ = bindValue<string>("cityCouncil", "districtsOverviewJson");
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
const republicanBureauBonusActive$ = bindValue<boolean>("cityCouncil", "republicanBureauBonusActive");
const centralIntelligenceBureauPresent$ = bindValue<boolean>("cityCouncil", "centralIntelligenceBureauPresent");
const populistPrisonBonusActive$ = bindValue<boolean>("cityCouncil", "populistPrisonBonusActive");
const ecologistNuclearBonusActive$ = bindValue<boolean>("cityCouncil", "ecologistNuclearBonusActive");
const radicalLeftUniversityBonusActive$ = bindValue<boolean>("cityCouncil", "radicalLeftUniversityBonusActive");
const democratDigitalBonusActive$ = bindValue<boolean>("cityCouncil", "democratDigitalBonusActive");
const TAB_HEIGHT = "560rem";
const DISTRICTS_KEY = "__districts__";


interface DistrictOverviewDto {
  districtId: number;
  districtName: string;
  voters: number;
  seats: number;
  percentOfCouncil: number;
  leadingParty: string;
  displayName?: string;
  displayColor?: string;
  isBastion: boolean;
  bastionParty: string;
  bastionDisplayName?: string;
  bastionDisplayColor?: string;
  streakCount: number;
  isReinforcedBastion: boolean;
}

function districtPartyLabel(d: DistrictOverviewDto, translate: any): string {
  return d.displayName && d.displayName.length > 0 ? d.displayName : translatePartyName(d.leadingParty, translate);
}
function districtPartyColor(d: DistrictOverviewDto): string {
  if (d.displayColor && d.displayColor.length > 0) return CUSTOM_PARTY_PALETTE_HEX[d.displayColor] ?? "#888";
  return PARTY_COLORS[d.leadingParty] ?? "#888";
}
function bastionPartyLabel(d: DistrictOverviewDto, translate: any): string {
  return d.bastionDisplayName && d.bastionDisplayName.length > 0 ? d.bastionDisplayName : translatePartyName(d.bastionParty, translate);
}

// Mini barre de série Bastion (3 cases), version compacte de BastionProgressBar (AdministrationSection.tsx)
function MiniBastionBar({ streakCount, color }: { streakCount: number; color: string }) {
  return (
    <div style={{ display: "flex", marginTop: "4rem", maxWidth: "80rem" }}>
      {[0, 1, 2].map((i) => (
        <div
          key={i}
          style={{
            flex: 1,
            height: "5rem",
            borderRadius: "2rem",
            background: i < streakCount ? color : "rgba(255,255,255,0.10)",
            marginRight: i < 2 ? "3rem" : 0,
          }}
        />
      ))}
    </div>
  );
}

function DistrictRow({ d, translate }: { d: DistrictOverviewDto; translate: any }) {
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;
  const [expanded, setExpanded] = useState(false);

  const votersWord = t("CityCouncil.Hemicycle.POWERFUL_DISTRICT_VOTERS", "personnes en âge de voter");
  const seatsWord = d.seats > 1
    ? t("CityCouncil.Admin.SEATS_PLURAL", "sièges")
    : t("CityCouncil.Admin.SEATS_SINGULAR", "siège");
  const councilSuffix = t("CityCouncil.Hemicycle.POWERFUL_DISTRICT_COUNCIL_SUFFIX", "du Conseil Municipal");
  const ledByLabel = t("CityCouncil.Hemicycle.POWERFUL_DISTRICT_LED_BY", "Dirigé par ");
  const bastionLabel = t("CityCouncil.Admin.BASTION_LABEL", "Bastion : ");

  const pct = Math.round(d.percentOfCouncil * 10) / 10;
  const detailLine = `${d.voters.toLocaleString()} ${votersWord}, ${d.seats} ${seatsWord}, ${pct}% ${councilSuffix}`;
  const leaderColor = districtPartyColor(d);

  // Aplati en une seule chaîne — le moteur casse la ligne si plusieurs enfants JSX
  // adjacents (texte + span) sont utilisés à la place d'une seule string.
  const ledByLine = `${ledByLabel}« ${districtPartyLabel(d, translate)} »`;
    const bastionLine = d.isBastion ? `${bastionLabel}« ${bastionPartyLabel(d, translate)} »` : "";
  const reinforcedLabel = t("CityCouncil.Admin.BASTION_REINFORCED_LABEL", "Bastion Renforcé : ");
  const reinforcedLine = d.isReinforcedBastion
    ? `${reinforcedLabel}« ${bastionPartyLabel(d, translate)} »`
    : "";

  return (
    <div
      style={{
        marginBottom: "6rem",
        borderRadius: "6rem",
        border: "1rem solid rgba(255,255,255,0.15)",
        background: "rgba(255,255,255,0.05)",
        overflow: "hidden",
      }}
    >
      <div
        onClick={() => setExpanded((v) => !v)}
        style={{
          display: "flex",
          alignItems: "center",
          padding: "8rem 10rem",
          cursor: "pointer",
        }}
      >
        <div style={{ marginRight: "8rem", flexShrink: 0 }}>
          <PartyLogo party={d.leadingParty} color={leaderColor} isCustom={!!(d.displayName && d.displayName.length > 0)} sizeRem={30} />
        </div>
        <div style={{ flex: 1, minWidth: 0, color: "white", fontSize: "15rem", fontWeight: 700, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
          {d.districtName}
        </div>
        <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "11rem", flexShrink: 0, marginLeft: "8rem" }}>
          {expanded ? "▲" : "▼"}
        </div>
      </div>

       {expanded && (
        <div style={{ padding: "0 10rem 10rem" }}>
          <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", lineHeight: "15rem", marginBottom: "6rem" }}>
            {detailLine}
          </div>

          <div style={{ color: "rgba(255,255,255,0.85)", fontSize: "12rem", fontWeight: 700 }}>
            {ledByLine}
          </div>

          {d.isBastion && (
            <div style={{ marginTop: "6rem" }}>
              <div style={{ color: "rgba(150,190,255,0.9)", fontSize: "12rem", fontWeight: 700 }}>
                {bastionLine}
              </div>
              <MiniBastionBar streakCount={d.streakCount} color={PARTY_COLORS[d.bastionParty] ?? "#888"} />
              {d.isReinforcedBastion && (
                <div style={{ color: "rgba(255,200,120,0.9)", fontSize: "11rem", fontWeight: 700, marginTop: "4rem" }}>
                  {reinforcedLine}
                </div>
              )}
            </div>
          )}
          {!d.isBastion && d.streakCount > 0 && (
            <MiniBastionBar streakCount={d.streakCount} color={leaderColor} />
          )}
        </div>
      )}
    </div>
  );
}

function DistrictsPanel() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const districtsJson = useValue(districtsOverviewJson$);
  const districts: DistrictOverviewDto[] = useMemo(() => {
    try {
      const p = JSON.parse(districtsJson ?? "[]");
      return Array.isArray(p) ? p.filter((d) => d && typeof d.districtId === "number") : [];
    } catch {
      return [];
    }
  }, [districtsJson]);

  return (
    <div style={{ padding: "16rem" }}>
      <div style={{ color: "white", fontSize: "18rem", fontWeight: 700, marginBottom: "12rem" }}>
        {t("CityCouncil.Forces.DISTRICTS_HEADER", "Districts")}
      </div>
      {districts.length === 0 ? (
        <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "12rem" }}>
          {t("CityCouncil.Forces.DISTRICTS_EMPTY", "Aucun district avec des votants pour le moment.")}
        </div>
      ) : (
        districts.map((d) => <DistrictRow key={d.districtId} d={d} translate={translate} />)
      )}
    </div>
  );
}


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
  const bureauBonusActive = useValue(republicanBureauBonusActive$);
  const bureauPresent = useValue(centralIntelligenceBureauPresent$);
  const populistBonusActive = useValue(populistPrisonBonusActive$);
  const ecologistBonusActive = useValue(ecologistNuclearBonusActive$);
  const radicalLeftBonusActive = useValue(radicalLeftUniversityBonusActive$);
  const democratBonusActive = useValue(democratDigitalBonusActive$);

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
   <div style={{ display: "flex", width: "100%", height: TAB_HEIGHT, boxSizing: "border-box" }}>
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
                {e.bonus !== "None" && <BonusBadgeIcon bonus={e.bonus} widthRem={12} />}

          {e.key === "Republicain" && bureauBonusActive && (
            <img
              src={bonusExcluRepublicainIcon}
              alt=""
              style={{ width: "12rem", height: "12rem", flexShrink: 0 }}
            />
          )}

          {e.key === "Populiste" && populistBonusActive && (
            <img src={bonusExcluPopulisteIcon} alt="" style={{ width: "12rem", height: "12rem", flexShrink: 0 }} />
          )}

            {e.key === "Ecologiste" && ecologistBonusActive && (
              <img src={bonusExcluEcologisteIcon} alt="" style={{ width: "12rem", height: "12rem", flexShrink: 0 }} />
            )}

            {e.key === "GaucheRadicale" && radicalLeftBonusActive && (
              <img src={bonusExcluGaucheRadicaleIcon} alt="" style={{ width: "12rem", height: "12rem", flexShrink: 0 }} />
            )}

            {e.key === "Democrate" && democratBonusActive && (
              <img src={bonusExcluDemocrateIcon} alt="" style={{ width: "12rem", height: "12rem", flexShrink: 0 }} />
            )}

        </div>
      </div>
    );
  })}

        {/* Séparateur + entrée Districts */}
      <div style={{ height: "1rem", background: "rgba(255,255,255,0.12)", margin: "6rem 10rem" }} />
      <div
        onClick={() => setSelectedKey(DISTRICTS_KEY)}
        style={{
          display: "flex",
          alignItems: "center",
          padding: "8rem 10rem",
          cursor: "pointer",
          background: selectedKey === DISTRICTS_KEY ? "rgba(255,255,255,0.10)" : "transparent",
          borderLeft: selectedKey === DISTRICTS_KEY ? "3rem solid white" : "3rem solid transparent",
        }}
      >
        <span style={{ color: "white", fontSize: "13rem" }}>
          {t("CityCouncil.Forces.DISTRICTS_ENTRY", "Districts")}
        </span>
      </div>
    </div>

      {/* Colonne droite : détail du parti sélectionné */}

                  {selectedKey === DISTRICTS_KEY ? (
              <Scrollable style={{ flex: 1 }}>
            <DistrictsPanel />
          </Scrollable>
        ) : selected && (
          <Scrollable style={{ flex: 1 }}>
          <div style={{ padding: "16rem" }}>
          <div style={{ display: "flex", alignItems: "center", marginBottom: "10rem" }}>
            <div style={{ marginRight: "10rem" }}>
              <PartyLogo party={selected.key} color={selected.color} isCustom={selected.isPlayerParty} sizeRem={90} />
            </div>
            <div style={{ color: "white", fontSize: "25rem", fontWeight: 700, whiteSpace: "nowrap" }}>
              {selected.label}
            </div>
          </div>

                  <div style={{ marginBottom: "14rem" }}>
                      <PartyDescriptionBlock
                          partyKey={selected.key}
                          fallbackText={selected.description}
                          isPlayerParty={selected.isPlayerParty}
                        />
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
  <div style={{ marginBottom: "10rem" }}>
    <div style={{ color: "rgba(150,190,255,0.9)", fontSize: "12rem", fontWeight: 700, marginBottom: "6rem", whiteSpace: "nowrap" }}>
      {bonusLabel}
    </div>
    {selected.bonus !== "None" && <BonusBadgeIcon bonus={selected.bonus} widthRem={100} />}
  </div>
)}

          {selected.key === "Republicain" && bureauBonusActive && (
           <div style={{ display: "flex", alignItems: "flex-start", marginBottom: "10rem" }}>
                        <img
                          src={bonusExcluRepublicainIcon}
                          alt=""
                          style={{ width: "150rem", height: "212.46rem", objectFit: "contain", marginRight: "10rem", flexShrink: 0 }}
                        />
                <span style={{ color: "rgba(150,190,255,0.9)", fontSize: "12rem", fontWeight: 700, whiteSpace: "nowrap" }}>
                  {t("CityCouncil.Forces.BUREAU_BONUS_ACTIVE", "Central Intelligence Bureau : +50% cotisations")}
                </span>
             </div>
)}

            {selected.key === "Republicain" && !bureauBonusActive && bureauPresent && (
              <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "11rem", marginBottom: "10rem", lineHeight: "15rem" }}>
                {t("CityCouncil.Forces.BUREAU_PRESENT_HINT", "Le Central Intelligence Bureau est construit — 3 victoires consécutives à la majorité générale activeront le bonus de cotisation.")}
              </div>
            )}

            {selected.key === "Populiste" && populistBonusActive && (
              <div style={{ display: "flex", alignItems: "center", marginBottom: "10rem" }}>
                <img
                  src={bonusExcluPopulisteIcon}
                  alt=""
                  style={{ width: "18rem", height: "18rem", marginRight: "6rem", flexShrink: 0 }}
                />
                <span style={{ color: "rgba(150,190,255,0.9)", fontSize: "12rem", fontWeight: 700, whiteSpace: "nowrap" }}>
                  {t("CityCouncil.Forces.PRISON_BONUS_ACTIVE", "Prison : campagnes illégales -30% coût / -30% détection")}
                </span>
              </div>
            )}

            {selected.key === "Ecologiste" && ecologistBonusActive && (
              <div style={{ display: "flex", alignItems: "center", marginBottom: "10rem" }}>
                <img
                  src={bonusExcluEcologisteIcon}
                  alt=""
                  style={{ width: "18rem", height: "18rem", marginRight: "6rem", flexShrink: 0 }}
                />
                <span style={{ color: "rgba(150,190,255,0.9)", fontSize: "12rem", fontWeight: 700, whiteSpace: "nowrap" }}>
                  {t("CityCouncil.Forces.NUCLEAR_BONUS_ACTIVE", "Centrale nucléaire : +4% quartiers modestes, +4% séniors ville entière")}
                </span>
              </div>
            )}

            {selected.key === "GaucheRadicale" && radicalLeftBonusActive && (
              <div style={{ display: "flex", alignItems: "center", marginBottom: "10rem" }}>
                <img
                  src={bonusExcluGaucheRadicaleIcon}
                  alt=""
                  style={{ width: "18rem", height: "18rem", marginRight: "6rem", flexShrink: 0 }}
                />
                <span style={{ color: "rgba(150,190,255,0.9)", fontSize: "12rem", fontWeight: 700, whiteSpace: "nowrap" }}>
                  {t("CityCouncil.Forces.UNIVERSITY_BONUS_ACTIVE", "Université : 5 campagnes de district au lieu de 3")}
                </span>
              </div>
            )}

            {selected.key === "Democrate" && democratBonusActive && (
              <div style={{ display: "flex", alignItems: "center", marginBottom: "10rem" }}>
                <img
                  src={bonusExcluDemocrateIcon}
                  alt=""
                  style={{ width: "18rem", height: "18rem", marginRight: "6rem", flexShrink: 0 }}
                />
                <span style={{ color: "rgba(150,190,255,0.9)", fontSize: "12rem", fontWeight: 700, whiteSpace: "nowrap" }}>
                  {t("CityCouncil.Forces.SATELLITE_BONUS_ACTIVE", "Liaison Satellite : Campagne digitale innovante débloquée")}
                </span>
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
         </Scrollable>
      )}
    </div>
  );
}