import { useState } from "react";
import { Scrollable } from "cs2/ui";

/**
 * Menu déroulant "maison" (cohtml ne supporte pas <select>/<option>, cf. remarque déjà
 * documentée dans le mod). NOTE — les crashs JS observés lors des tentatives précédentes
 * (overlay absolute/fixed coupé par le clipping du parent, puis suspicion de Scrollable
 * imbriqué non supporté) provenaient en réalité d'un <select> natif mort resté ailleurs
 * dans LawsTab.tsx (jamais remplacé par une édition précédente) — pas de ce composant.
 * Un Scrollable imbriqué dans le Scrollable du panneau fonctionne donc normalement : on
 * l'utilise ici pour un vrai scroll interne (molette/glisser) sur la liste d'options,
 * bornée à une hauteur fixe, plutôt que de dépendre du Scrollable de la page entière.
 */
export interface DropdownOption {
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

export function CustomDropdown({ options, value, onChange, placeholder, disabled }: CustomDropdownProps) {
  const [isOpen, setIsOpen] = useState(false);
  const selectedOption = options.find((opt) => opt.value === value);

  const handleToggle = () => {
    if (disabled) return;
    setIsOpen((prev) => !prev);
  };

  return (
    <div style={{ width: "100%", marginBottom: "8rem" }}>
      <div
        onClick={handleToggle}
        style={{
          width: "100%",
          boxSizing: "border-box",
          background: disabled ? "rgba(255,255,255,0.04)" : "rgba(255,255,255,0.08)",
          border: "1rem solid rgba(255,255,255,0.15)",
          borderRadius: isOpen ? "4rem 4rem 0 0" : "4rem",
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
            width: "100%",
            boxSizing: "border-box",
            background: "rgba(20,22,30,0.98)",
            border: "1rem solid rgba(255,255,255,0.15)",
            borderTop: "none",
            borderRadius: "0 0 4rem 4rem",
          }}
        >
          <Scrollable style={{ height: "220rem" }}>
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
          </Scrollable>
        </div>
      )}
    </div>
  );
}
