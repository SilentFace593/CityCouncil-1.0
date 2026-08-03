import { useMemo, useState } from "react";
import { bindValue, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import {
  translatePartyName,
  PARTY_LABELS,
  PARTY_COLORS,
  PARTY_ORDER,
  CUSTOM_PARTY_PALETTE_HEX,
  type PartyResultDto,
} from "./PartyResultDto";

const hemicycleSeatsJson$ = bindValue<string>("cityCouncil", "hemicycleSeatsJson");
const partyMembershipJson$ = bindValue<string>("cityCouncil", "partyMembershipJson");
const customPartyExists$ = bindValue<boolean>("cityCouncil", "customPartyExists");
const customPartyName$ = bindValue<string>("cityCouncil", "customPartyName");
const customPartyColor$ = bindValue<string>("cityCouncil", "customPartyColor");
const customPartySpace$ = bindValue<string>("cityCouncil", "customPartySpace");
const customPartyPendingActivation$ = bindValue<boolean>("cityCouncil", "customPartyPendingActivation");

interface PartyMembershipDto {
  party: string;
  members: number;
  treasury: number;
}

function partyPhotoSrc(_party: string): string | null {
  return null;
}

interface ForceEntry {
  key: string;
  label: string;
  color: string;
  description: string;
  members: number;
  seats: number;
  treasury: number;
  pendingReplacement: boolean;
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

  const partyDescriptions: Record<string, string> = {
    Ecologiste: t("CityCouncil.Forces.DESC_ECOLOGISTE", "Défend une transition écologique ambitieuse et la préservation des espaces naturels."),
    Democrate: t("CityCouncil.Forces.DESC_DEMOCRATE", "Parti de centre, favorable au dialogue social et à une gestion pragmatique de la ville."),
    Populiste: t("CityCouncil.Forces.DESC_POPULISTE", "Porte-voix des mécontentements populaires, critique des taxes et des élites locales."),
    Republicain: t("CityCouncil.Forces.DESC_REPUBLICAIN", "Défend l'ordre, la sécurité et une gestion rigoureuse des finances municipales."),
    GaucheRadicale: t("CityCouncil.Forces.DESC_GAUCHERADICALE", "Milite pour une redistribution radicale des richesses et des services publics renforcés."),
  };

  const seatResults: PartyResultDto[] = useMemo(() => {
    try {
      const parsed = JSON.parse(seatsJson);
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }, [seatsJson]);

  const membership: PartyMembershipDto[] = useMemo(() => {
    try {
      const parsed = JSON.parse(membershipJson);
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }, [membershipJson]);

  const entries: ForceEntry[] = useMemo(() => {
    return PARTY_ORDER.map((partyKey) => {
      const seatEntry = seatResults.find((r) => r.party === partyKey);
      const memberEntry = membership.find((m) => m.party === partyKey);
   // Décoré uniquement si la substitution est ACTIVE (le serveur ne remplit displayName/
      // displayColor sur seatResults que dans ce cas, mais on garde une logique cohérente
      // côté client pour label/couleur/description qui ne passent pas par le DTO).
      const isCustomHere = customExists && !customPendingActivation && customSpace === partyKey;
      const isPendingReplacement = customExists && customPendingActivation && customSpace === partyKey; // AJOUT

      return {
        key: partyKey,
        label: isCustomHere ? customName : (translatePartyName(partyKey, translate)),
        color: isCustomHere ? (CUSTOM_PARTY_PALETTE_HEX[customColor] ?? "#888") : (PARTY_COLORS[partyKey] ?? "#888"),
        description: isCustomHere
          ? t("CityCouncil.Forces.CUSTOM_PARTY_DESC", "Votre parti politique. La personnalisation de la description est prévue dans une prochaine étape.")
          : (partyDescriptions[partyKey] ?? ""),
        members: memberEntry?.members ?? 0,
        seats: seatEntry?.seats ?? 0,
        treasury: memberEntry?.treasury ?? 0,
        pendingReplacement: isPendingReplacement, // AJOUT
      };
    });
  }, [seatResults, membership, customExists, customName, customColor, customSpace, customPendingActivation, partyDescriptions]);

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

  const treasurySuffix = t("CityCouncil.Forces.TREASURY", "crédits en caisse");
  const treasuryLine = selected ? `${selected.treasury.toLocaleString()} ${treasurySuffix}` : "";
  const pendingReplacementLabel = t("CityCouncil.Forces.PENDING_REPLACEMENT", "Parti remplacé à la prochaine élection !");


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
              <div style={{ color: "white", fontSize: "12rem", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                {e.label}
              </div>
            </div>
          );
        })}
      </div>

      {/* Colonne droite : détail du parti sélectionné */}
      <div style={{ flex: 1, padding: "12rem", overflowY: "auto", boxSizing: "border-box" }}>
        {selected && (
          <>
            <div style={{ display: "flex", alignItems: "center", marginBottom: "10rem" }}>
              {partyPhotoSrc(selected.key) ? (
                <img
                  src={partyPhotoSrc(selected.key)!}
                  style={{ width: "48rem", height: "48rem", borderRadius: "6rem", marginRight: "10rem", flexShrink: 0 }}
                />
              ) : (
                <div
                  style={{
                    width: "48rem",
                    height: "48rem",
                    borderRadius: "6rem",
                    background: selected.color,
                    marginRight: "10rem",
                    flexShrink: 0,
                  }}
                />
              )}
              <div style={{ color: "white", fontSize: "16rem", fontWeight: 700, whiteSpace: "nowrap" }}>
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

            <div style={{ display: "flex", flexDirection: "column", gap: "6rem" }}>
              <div style={{ color: "rgba(255,255,255,0.9)", fontSize: "13rem", whiteSpace: "nowrap" }}>{membersLine}</div>
              <div style={{ color: "rgba(255,255,255,0.9)", fontSize: "13rem", whiteSpace: "nowrap" }}>{seatsLine}</div>
              <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "12rem", whiteSpace: "nowrap" }}>{treasuryLine}</div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}