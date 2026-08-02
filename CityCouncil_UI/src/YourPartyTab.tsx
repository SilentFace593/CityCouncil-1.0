import { useLocalization } from "cs2/l10n";
import { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import {
  translatePartyName,
  CUSTOM_PARTY_PALETTE,
  CUSTOM_PARTY_PALETTE_HEX,
  PARTY_LABELS,
  PARTY_ORDER,
} from "./PartyResultDto";
import type { TranslateFn } from "./PartyResultDto";


const customPartyExists$ = bindValue<boolean>("cityCouncil", "customPartyExists");
const customPartyName$ = bindValue<string>("cityCouncil", "customPartyName");
const customPartyColor$ = bindValue<string>("cityCouncil", "customPartyColor");
const customPartySpace$ = bindValue<string>("cityCouncil", "customPartySpace");
const customPartyPendingDeletion$ = bindValue<boolean>("cityCouncil", "customPartyPendingDeletion");

const SPACES = PARTY_ORDER;

// Style de bouton repris tel quel du pattern déjà utilisé ailleurs dans les mods du joueur
// (ex. bouton "Rénover" de NuclearReactor), pour cohérence visuelle entre mods.
function ActionButton({
  label,
  enabled,
  onClick,
}: {
  label: string;
  enabled: boolean;
  onClick: () => void;
}) {
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

// Case à cocher stylée (carré + coche), sélection unique parmi SPACES mais rendue comme
// une liste de cases à cocher plutôt que des radios natifs (rendu ambigu constaté par
// le joueur) : clic sur une ligne coche celle-ci et décoche implicitement les autres.
function SpaceCheckboxRow({
  spaceKey,
  checked,
  onSelect,
  translate
}: {
  spaceKey: string;
  checked: boolean;
  onSelect: () => void;
  translate: TranslateFn;
}) {
  // NOTE : PARTY_LABELS reste en dur en français pour l'instant, volontairement — les noms de
  // partis seront traduits dans une passe dédiée (cf. plan de traduction), séparée des libellés
  // d'UI courts traités ici.
  const label = translatePartyName(spaceKey, translate);
  return (
    <div
      onClick={onSelect}
      style={{
        display: "flex",
        alignItems: "center",
        cursor: "pointer",
        padding: "4rem 0",
      }}
    >
      <div
        style={{
          width: "16rem",
          height: "16rem",
          borderRadius: "3rem",
          border: checked ? "1rem solid rgba(120,170,255,0.9)" : "1rem solid rgba(255,255,255,0.35)",
          background: checked ? "rgba(70,130,220,0.85)" : "rgba(255,255,255,0.06)",
          marginRight: "8rem",
          flexShrink: 0,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        {checked && (
          <div style={{ color: "white", fontSize: "11rem", fontWeight: "bold", lineHeight: "11rem" }}>
            ✓
          </div>
        )}
      </div>
      <div style={{ color: "rgba(255,255,255,0.9)", fontSize: "13rem", whiteSpace: "nowrap" }}>{label}</div>
    </div>
  );
}

export function YourPartyTab() {
  const { translate } = useLocalization();

  const exists = useValue(customPartyExists$);
  const currentName = useValue(customPartyName$);
  const currentColor = useValue(customPartyColor$);
  const currentSpace = useValue(customPartySpace$);
  const pendingDeletion = useValue(customPartyPendingDeletion$);

  const [name, setName] = useState("");
  const [color, setColor] = useState<string>("Bleu");
  const [space, setSpace] = useState<string>("Democrate");

  useEffect(() => {
    if (exists) {
      setName(currentName);
      setColor(currentColor || "Bleu");
      setSpace(currentSpace || "Democrate");
    }
  }, [exists, currentName, currentColor, currentSpace]);

  const trimmed = name.trim();
  const canSubmit = trimmed.length > 0 && trimmed.length <= 40;

  const handleSubmit = () => {
    if (!canSubmit) return;
    trigger("cityCouncil", "createOrUpdateCustomParty", trimmed, color, space);
  };

  const handleRequestDelete = () => trigger("cityCouncil", "requestDeleteCustomParty");
  const handleCancelDelete = () => trigger("cityCouncil", "cancelDeleteCustomParty");

  // Petit helper local pour ne pas répéter le pattern translate(...) ?? fallback à chaque ligne.
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  // Toutes les phrases mêlant texte + variable sont construites en UNE SEULE chaîne
  // (template literal) avant d'être passées à JSX, pour éviter le retour à la ligne
  // intempestif que provoquent plusieurs enfants JSX adjacents sur ce moteur.
  const spacePrefix = t("CityCouncil.YourPartyTab.SPACE_PREFIX", "Bord politique : ");
  const currentPartyLine = `${spacePrefix}${translatePartyName(currentSpace, translate)}`;

  const pendingDeletionLine = t("CityCouncil.YourPartyTab.PENDING_DELETION", "Suppression prévue à la prochaine élection.");
  const deleteButtonLabel = t("CityCouncil.YourPartyTab.DELETE_BUTTON", "Supprimer le parti");
  const cancelButtonLabel = t("CityCouncil.YourPartyTab.CANCEL_BUTTON", "Annuler");

  const sectionHeaderLabel = exists
    ? t("CityCouncil.YourPartyTab.SECTION_HEADER_EDIT", "Modifier votre parti")
    : t("CityCouncil.YourPartyTab.SECTION_HEADER_CREATE", "Créer votre parti politique");

  const nameLabel = t("CityCouncil.YourPartyTab.NAME_LABEL", "Nom du parti");
  const namePlaceholder = t("CityCouncil.YourPartyTab.NAME_PLACEHOLDER", "Ex : Renouveau Citoyen");
  const colorLabel = t("CityCouncil.YourPartyTab.COLOR_LABEL", "Couleur");
  const spaceLabel = t("CityCouncil.YourPartyTab.SPACE_LABEL", "Espace politique");

  const createButtonLabel = exists
    ? t("CityCouncil.YourPartyTab.UPDATE_BUTTON", "Mettre à jour le parti")
    : t("CityCouncil.YourPartyTab.CREATE_BUTTON", "Créer un parti politique");

  return (
    <div style={{ padding: "10rem", width: "100%", boxSizing: "border-box" }}>
      {exists && (
        <div
          style={{
            marginBottom: "14rem",
            padding: "10rem",
            background: "rgba(255,255,255,0.06)",
            borderRadius: "6rem",
          }}
        >
          <div style={{ display: "flex", alignItems: "center", marginBottom: "4rem" }}>
            <div
              style={{
                width: "14rem",
                height: "14rem",
                borderRadius: "50%",
                background: CUSTOM_PARTY_PALETTE_HEX[currentColor] ?? "#888",
                marginRight: "8rem",
                flexShrink: 0,
              }}
            />
            <div style={{ color: "white", fontSize: "14rem", fontWeight: 600, whiteSpace: "nowrap" }}>
              {currentName}
            </div>
          </div>
          <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "12rem", whiteSpace: "nowrap" }}>
            {currentPartyLine}
          </div>

          {pendingDeletion ? (
            <div
              style={{
                marginTop: "8rem",
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
              }}
            >
              <div style={{ color: "rgba(255,180,120,0.9)", fontSize: "12rem", whiteSpace: "nowrap" }}>
                {pendingDeletionLine}
              </div>
              <ActionButton label={cancelButtonLabel} enabled={true} onClick={handleCancelDelete} />
            </div>
          ) : (
            <div style={{ marginTop: "8rem" }}>
              <ActionButton label={deleteButtonLabel} enabled={true} onClick={handleRequestDelete} />
            </div>
          )}
        </div>
      )}

      <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "13rem", marginBottom: "10rem", whiteSpace: "nowrap" }}>
        {sectionHeaderLabel}
      </div>

      <div style={{ marginBottom: "12rem" }}>
        <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "11rem", marginBottom: "4rem", textTransform: "uppercase" }}>
          {nameLabel}
        </div>
        <input
          value={name}
          maxLength={40}
          onChange={(e) => setName(e.target.value)}
          placeholder={namePlaceholder}
          style={{
            width: "100%",
            boxSizing: "border-box",
            background: "rgba(255,255,255,0.08)",
            border: "1rem solid rgba(255,255,255,0.15)",
            borderRadius: "4rem",
            color: "white",
            fontSize: "13rem",
            padding: "6rem 8rem",
          }}
        />
      </div>

      <div style={{ marginBottom: "12rem" }}>
        <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "11rem", marginBottom: "6rem", textTransform: "uppercase" }}>
          {colorLabel}
        </div>
        <div style={{ display: "flex", flexWrap: "wrap", gap: "10rem" }}>
          {CUSTOM_PARTY_PALETTE.map((c) => {
            const selected = color === c;
            return (
              <div
                key={c}
                onClick={() => setColor(c)}
                title={c}
                style={{
                  width: "28rem",
                  height: "28rem",
                  borderRadius: "50%",
                  background: CUSTOM_PARTY_PALETTE_HEX[c],
                  cursor: "pointer",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  border: selected ? "2rem solid white" : "2rem solid transparent",
                  boxShadow: selected ? "0 0 0 2rem rgba(255,255,255,0.25)" : "none",
                }}
              >
                {selected && (
                  <div style={{ color: "white", fontSize: "13rem", fontWeight: "bold" }}>✓</div>
                )}
              </div>
            );
          })}
        </div>
      </div>

      <div style={{ marginBottom: "14rem" }}>
        <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "11rem", marginBottom: "4rem", textTransform: "uppercase" }}>
          {spaceLabel}
        </div>
        <div style={{ display: "flex", flexDirection: "column" }}>
          {SPACES.map((s) => (
            <SpaceCheckboxRow key={s} spaceKey={s} checked={space === s} onSelect={() => setSpace(s)} translate={translate} />
          ))}
        </div>
      </div>

      <ActionButton
        label={createButtonLabel}
        enabled={canSubmit}
        onClick={handleSubmit}
      />
    </div>
  );
}