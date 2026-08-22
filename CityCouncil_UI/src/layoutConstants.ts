/**
 * Largeur maximale du contenu pour les onglets "colonne unique" du panneau hémicycle.
 * Centré via flexbox plutôt que margin:auto — cohtml ne centre pas toujours fiablement
 * un bloc avec margin:0 auto selon le parent (cf. quirks Scrollable déjà documentés
 * ailleurs dans le mod), un conteneur flex explicite est plus robuste.
 */
export const TAB_CONTENT_MAX_WIDTH = "480rem";

/** Conteneur EXTÉRIEUR (prend toute la largeur, centre son enfant). */
export const centeredTabWrapperStyle = {
  display: "flex" as const,
  justifyContent: "center" as const,
  width: "100%",
  boxSizing: "border-box" as const,
};

/** Conteneur INTÉRIEUR (largeur contrainte, contenu réel de l'onglet). */
export const centeredTabContentStyle = {
  width: "100%",
  maxWidth: TAB_CONTENT_MAX_WIDTH,
  boxSizing: "border-box" as const,
  padding: "10rem",
};