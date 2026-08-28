export interface PartyDescriptionConfig {
  localeKey: string;       // remplace "text" — clé de traduction au lieu du texte brut
  portrait?: string;
}

export const PARTY_DESCRIPTIONS: Record<string, PartyDescriptionConfig> = {
  Ecologiste: {
    localeKey: "CityCouncil.PartyDesc.ECOLOGISTE",
    portrait: "ecologiste_portrait.png",
  },

  Democrate: {
    localeKey: "CityCouncil.PartyDesc.DEMOCRATE",
    portrait: "democrate_portrait.png",
  },

   Populiste: {
    localeKey: "CityCouncil.PartyDesc.POPULISTE",
    portrait: "populiste_portrait.png",
  },

   Republicain: {
    localeKey: "CityCouncil.PartyDesc.REPUBLICAIN",
    portrait: "republicain_portrait.png",
  },

   GaucheRadicale: {
    localeKey: "CityCouncil.PartyDesc.GAUCHE_RADICALE",
    portrait: "gauche_radicale_portrait.png",
  },
};