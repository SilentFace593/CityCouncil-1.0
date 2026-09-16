import { useMemo, useState } from "react";
import { bindValue, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { centeredTabWrapperStyle, centeredTabContentStyle } from "./layoutConstants";

import bonusDefensifIcon from "./images/bonus_defensif.png";
import bonusOffensifIcon from "./images/bonus_offensif.png";
import bonusExcluRepublicainIcon from "./images/Bonus_EXCLU_Republicain.png";
import bonusExcluPopulisteIcon from "./images/Bonus_EXCLU_Populiste.png";
import bonusExcluEcologisteIcon from "./images/Bonus_EXCLU_Ecologiste.png";
import bonusExcluGaucheRadicaleIcon from "./images/Bonus_EXCLU_Gauche.png";
import bonusExcluDemocrateIcon from "./images/Bonus_EXCLU_Democrate.png";

const partyBonusesJson$ = bindValue<string>("cityCouncil", "partyBonusesJson");
const customPartyExists$ = bindValue<boolean>("cityCouncil", "customPartyExists");
const customPartySpace$ = bindValue<string>("cityCouncil", "customPartySpace");
const customPartyPendingActivation$ = bindValue<boolean>("cityCouncil", "customPartyPendingActivation");
const republicanBureauBonusActive$ = bindValue<boolean>("cityCouncil", "republicanBureauBonusActive");
const populistPrisonBonusActive$ = bindValue<boolean>("cityCouncil", "populistPrisonBonusActive");
const ecologistNuclearBonusActive$ = bindValue<boolean>("cityCouncil", "ecologistNuclearBonusActive");
const radicalLeftUniversityBonusActive$ = bindValue<boolean>("cityCouncil", "radicalLeftUniversityBonusActive");
const democratDigitalBonusActive$ = bindValue<boolean>("cityCouncil", "democratDigitalBonusActive");

interface PartyBonusDto {
  party: string;
  bonus: string; // "None" | "Defensif" | "Offensif"
}

// Taille de départ demandée pour les images des cartes de bonus. Les PNG source font
// 175x248px (même ratio que BONUS_BADGE_ASPECT_RATIO dans PartyResultDto.tsx) : on calcule
// la hauteur du cadre à partir de ce ratio plutôt que de forcer un carré, pour ne pas
// déformer l'image.
const CARD_IMAGE_WIDTH = 130;
const CARD_IMAGE_ASPECT_RATIO = 175 / 248; // largeur / hauteur
const CARD_IMAGE_WIDTH_STYLE = `${CARD_IMAGE_WIDTH}rem`;
const CARD_IMAGE_HEIGHT_STYLE = `${CARD_IMAGE_WIDTH / CARD_IMAGE_ASPECT_RATIO}rem`;

function BonusCard({
  image,
  title,
  description,
  isActive,
}: {
  image: string;
  title: string;
  description: string;
  isActive: boolean;
}) {
  const [hovered, setHovered] = useState(false);

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        width: CARD_IMAGE_WIDTH_STYLE,
        marginRight: "18rem",
        marginBottom: "20rem",
      }}
    >
      <div
        onMouseEnter={() => setHovered(true)}
        onMouseLeave={() => setHovered(false)}
        style={{
          position: "relative",
          width: CARD_IMAGE_WIDTH_STYLE,
          height: CARD_IMAGE_HEIGHT_STYLE,
          borderRadius: "8rem",
          border: "1rem solid rgba(120,170,255,0.9)",
          background: "rgba(255,255,255,0.04)",
          overflow: "hidden",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          boxSizing: "border-box",
        }}
      >
        {/* Cadre déjà au bon ratio (175x248) : l'image remplit tout l'espace sans déformation. */}
        <img
          src={image}
          style={{ width: "100%", height: "100%", display: "block", boxSizing: "border-box" }}
        />

        {hovered && (
          <div
            style={{
              position: "absolute",
              top: 0,
              left: 0,
              right: 0,
              bottom: 0,
              background: "rgba(10,12,22,0.78)",
            }}
          >
            {/* Un <div> bloc positionné en absolu + translateY, plutôt qu'un centrage flex :
                sous cohtml, un enfant flex refuse de se réduire en dessous de la largeur de son
                contenu non-wrappé (même avec minWidth:0), et le texte reste sur une seule ligne.
                Un div bloc dont la largeur est bornée par left/right wrap normalement. */}
            <div
              style={{
                position: "absolute",
                top: "50%",
                left: 0,
                right: 0,
                transform: "translateY(-50%)",
                padding: "0 10rem",
                boxSizing: "border-box",
                color: "white",
                fontSize: "12rem",
                lineHeight: "16rem",
                textAlign: "center",
              }}
            >
              {description}
            </div>
          </div>
        )}
      </div>

      <div
        style={{
          color: "white",
          fontSize: "13rem",
          fontWeight: 700,
          marginTop: "8rem",
          textAlign: "center",
        }}
      >
        {title}
      </div>

      {/* Réserve toujours la place de la coche pour aligner les titres entre eux */}
      <div style={{ height: "18rem", marginTop: "5rem", display: "flex", alignItems: "center", justifyContent: "center" }}>
        {isActive && (
          <div
            style={{
              width: "16rem",
              height: "16rem",
              borderRadius: "50%",
              background: "rgba(70,130,220,0.95)",
              border: "1rem solid white",
              flexShrink: 0,
            }}
          />
        )}
      </div>
    </div>
  );
}

export function BonusTab() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const bonusesJson = useValue(partyBonusesJson$);
  const customExists = useValue(customPartyExists$);
  const customSpace = useValue(customPartySpace$);
  const customPending = useValue(customPartyPendingActivation$);

  const bureauBonusActive = useValue(republicanBureauBonusActive$);
  const prisonBonusActive = useValue(populistPrisonBonusActive$);
  const nuclearBonusActive = useValue(ecologistNuclearBonusActive$);
  const universityBonusActive = useValue(radicalLeftUniversityBonusActive$);
  const digitalBonusActive = useValue(democratDigitalBonusActive$);

  const playerControlsAvailable = !!customExists && !customPending && !!customSpace;

  const bonuses: PartyBonusDto[] = useMemo(() => {
    try {
      const p = JSON.parse(bonusesJson ?? "[]");
      return Array.isArray(p) ? p : [];
    } catch {
      return [];
    }
  }, [bonusesJson]);

  const playerPermanentBonus = playerControlsAvailable
    ? (bonuses.find((b) => b.party === customSpace)?.bonus ?? "None")
    : "None";

  const tabTitle = t("CityCouncil.Bonus.TAB_TITLE", "Bonus");
  const permanentHeader = t("CityCouncil.Bonus.PERMANENT_HEADER", "Bonus permanents");
  const exclusiveHeader = t("CityCouncil.Bonus.EXCLUSIVE_HEADER", "Bonus exclusifs");

  return (
    <div style={centeredTabWrapperStyle}>
      <div style={centeredTabContentStyle}>
        <div style={{ color: "white", fontSize: "18rem", fontWeight: 700, marginBottom: "14rem" }}>
          {tabTitle}
        </div>

        <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", marginBottom: "10rem", textTransform: "uppercase" }}>
          {permanentHeader}
        </div>
        <div style={{ display: "flex", flexWrap: "wrap" }}>
          <BonusCard
            image={bonusDefensifIcon}
            title={t("CityCouncil.Forces.BONUS_DEFENSIF_LABEL", "Bonus permanent : Défensif")}
            description={t(
              "CityCouncil.Admin.BONUS_DEFENSIF_TOOLTIP",
              "Bonus Défensif permanent (protège une case de barre de Bastion)"
            )}
            isActive={playerControlsAvailable && playerPermanentBonus === "Defensif"}
          />
          <BonusCard
            image={bonusOffensifIcon}
            title={t("CityCouncil.Forces.BONUS_OFFENSIF_LABEL", "Bonus permanent : Offensif")}
            description={t(
              "CityCouncil.Admin.BONUS_OFFENSIF_TOOLTIP",
              "Bonus Offensif permanent (+3% d'intention de vote dans les bastions adverses)"
            )}
            isActive={playerControlsAvailable && playerPermanentBonus === "Offensif"}
          />
        </div>

        <div
          style={{
            color: "rgba(255,255,255,0.7)",
            fontSize: "12rem",
            marginBottom: "10rem",
            marginTop: "22rem",
            textTransform: "uppercase",
          }}
        >
          {exclusiveHeader}
        </div>
        <div style={{ display: "flex", flexWrap: "wrap" }}>
          <BonusCard
            image={bonusExcluRepublicainIcon}
            title={t("CityCouncil.Bonus.REPUBLICAIN_TITLE", "Central Intelligence Bureau")}
            description={t(
              "CityCouncil.Forces.BUREAU_BONUS_ACTIVE",
              "Central Intelligence Bureau : +50% cotisations"
            )}
            isActive={playerControlsAvailable && customSpace === "Republicain" && bureauBonusActive}
          />
          <BonusCard
            image={bonusExcluPopulisteIcon}
            title={t("CityCouncil.Bonus.POPULISTE_TITLE", "Prison")}
            description={t(
              "CityCouncil.Forces.PRISON_BONUS_ACTIVE",
              "Prison : campagnes illégales -30% coût / -30% détection"
            )}
            isActive={playerControlsAvailable && customSpace === "Populiste" && prisonBonusActive}
          />
          <BonusCard
            image={bonusExcluEcologisteIcon}
            title={t("CityCouncil.Bonus.ECOLOGISTE_TITLE", "Centrale nucléaire")}
            description={t(
              "CityCouncil.Forces.NUCLEAR_BONUS_ACTIVE",
              "Centrale nucléaire : +4% quartiers modestes, +4% séniors ville entière"
            )}
            isActive={playerControlsAvailable && customSpace === "Ecologiste" && nuclearBonusActive}
          />
          <BonusCard
            image={bonusExcluGaucheRadicaleIcon}
            title={t("CityCouncil.Bonus.GAUCHE_RADICALE_TITLE", "Université")}
            description={t(
              "CityCouncil.Forces.UNIVERSITY_BONUS_ACTIVE",
              "Université : 5 campagnes de district au lieu de 3"
            )}
            isActive={playerControlsAvailable && customSpace === "GaucheRadicale" && universityBonusActive}
          />
          <BonusCard
            image={bonusExcluDemocrateIcon}
            title={t("CityCouncil.Bonus.DEMOCRATE_TITLE", "Liaison Satellite")}
            description={t(
              "CityCouncil.Forces.SATELLITE_BONUS_ACTIVE",
              "Liaison Satellite : Campagne digitale innovante débloquée"
            )}
            isActive={playerControlsAvailable && customSpace === "Democrate" && digitalBonusActive}
          />
        </div>
      </div>
    </div>
  );
}
