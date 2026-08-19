import logoDemocrate from "./images/logo_democrate.png";
import logoEcologiste from "./images/logo_ecologiste.png";
import logoRepublicain from "./images/logo_republicain.png";
import logoGauche from "./images/logo_gauche.png";
import logoPopuliste from "./images/logo_populiste.png";

export interface PartyResultDto {
  party: string;
  seats: number;
  voteShare: number;
  displayName?: string;
  displayColor?: string;
}

// Fallback français, utilisé si translate() ne trouve rien (comportement identique au reste
// du mod : le français en dur reste le filet de sécurité final).
export const PARTY_LABELS: Record<string, string> = {
  Ecologiste: "Écologistes",
  Democrate: "Démocrates",
  Populiste: "Populistes",
  Republicain: "Républicains",
  GaucheRadicale: "Gauche radicale",
};

// Clés de traduction associées, enregistrées côté C# dans CityCouncilLocaleEN/FR
// (cf. LocaleKeys.Party_*).
export const PARTY_LABEL_KEYS: Record<string, string> = {
  Ecologiste: "CityCouncil.Party.ECOLOGISTE",
  Democrate: "CityCouncil.Party.DEMOCRATE",
  Populiste: "CityCouncil.Party.POPULISTE",
  Republicain: "CityCouncil.Party.REPUBLICAIN",
  GaucheRadicale: "CityCouncil.Party.GAUCHE_RADICALE",
};

export const PARTY_COLORS: Record<string, string> = {
  Ecologiste: "#2E7D32",
  Democrate: "#1565C0",
  Populiste: "#F9A825",
  Republicain: "#5C1A9C",
  GaucheRadicale: "#B71C1C",
};

export const PARTY_LOGOS: Record<string, string> = {
  Democrate: logoDemocrate,
  Ecologiste: logoEcologiste,
  Republicain: logoRepublicain,
  GaucheRadicale: logoGauche,
  Populiste: logoPopuliste,
};

// Ordre idéologique gauche → droite, partagé entre AdministrationSection et HemicyclePanel.
export const PARTY_ORDER = ["GaucheRadicale", "Ecologiste", "Democrate", "Populiste", "Republicain"];

// Palette prédéfinie du parti joueur — même ordre/valeurs que l'enum PartyColor côté C#.
export const CUSTOM_PARTY_PALETTE = [
  "Rouge", "Bleu", "Vert", "Orange", "Violet", "Jaune", "Cyan", "Rose", "Gris", "Noir",
] as const;

export const CUSTOM_PARTY_PALETTE_HEX: Record<string, string> = {
  Rouge: "#D32F2F",
  Bleu: "#1976D2",
  Vert: "#388E3C",
  Orange: "#F57C00",
  Violet: "#7B1FA2",
  Jaune: "#FBC02D",
  Cyan: "#0097A7",
  Rose: "#D81B60",
  Gris: "#757575",
  Noir: "#212121",
};

/** Signature attendue pour le paramètre translate, tel que renvoyé par useLocalization(). */
export type TranslateFn = (key: string, fallback: string | null) => string | null;

/**
 * Nom affiché pour une CLÉ de parti brute (ex. dans SpaceCheckboxRow, où l'on n'a pas de
 * PartyResultDto complet mais juste le nom du parti). Ne gère PAS le parti custom du joueur
 * (pas de displayName ici) — pour ça, utiliser resolvePartyLabel avec un PartyResultDto.
 */
export function translatePartyName(partyKey: string, translate: TranslateFn): string {
  const key = PARTY_LABEL_KEYS[partyKey];
  const fallback = PARTY_LABELS[partyKey] ?? partyKey;
  if (!key) return fallback;
  return translate(key, fallback) ?? fallback;
}

/**
 * Nom affiché : celui du joueur si ce résultat a été habillé côté C# (displayName), sinon
 * le nom du parti vanilla traduit via translatePartyName.
 */
export function resolvePartyLabel(r: PartyResultDto, translate: TranslateFn): string {
  if (r.displayName && r.displayName.length > 0) return r.displayName;
  return translatePartyName(r.party, translate);
}

/** Couleur affichée : celle du joueur si ce résultat a été habillé côté C#, sinon la couleur vanilla. */
export function resolvePartyColor(r: PartyResultDto): string {
  if (r.displayColor && r.displayColor.length > 0) {
    return CUSTOM_PARTY_PALETTE_HEX[r.displayColor] ?? "#888";
  }
  return PARTY_COLORS[r.party] ?? "#888";
}

export const BONUS_BADGE: Record<string, string> = {
  Defensif: "🛡️",
  Offensif: "⚔️",

};

/**
 * Logo de parti à taille fixée en rem. Toujours en rem (jamais px) : cohtml redimensionne
 * automatiquement selon l'échelle UI du joueur, donc pas de gestion par résolution d'écran.
 * `party` est la clé technique (Ecologiste, Democrate, ...) utilisée pour choisir le PNG ;
 * si `isCustom` est vrai (parti du joueur), retombe sur une pastille de la couleur `color`
 * fournie, puisqu'aucun logo dédié n'existe pour un parti custom.
 */
export function PartyLogo({
  party,
  color,
  isCustom,
  sizeRem,
}: {
  party: string;
  color: string;
  isCustom: boolean;
  sizeRem: number;
}) {
  const logo = !isCustom ? PARTY_LOGOS[party] : null;

  if (logo) {
    return (
      <img
        src={logo}
        style={{
          width: `${sizeRem}rem`,
          height: `${sizeRem}rem`,
          flexShrink: 0,
          objectFit: "contain",
        }}
      />
    );
  }

  return (
    <div
      style={{
        width: `${sizeRem}rem`,
        height: `${sizeRem}rem`,
        borderRadius: "50%",
        background: color,
        flexShrink: 0,
      }}
    />
  );
}