import { useMemo, useState, useRef } from "react";
import { RULES_SECTIONS } from "./RulesCatalog";

/**
 * Parse un paragraphe contenant des liens internes au format [[id|libellé]] et rend le texte
 * avec des <span> cliquables à la place — simule un lien wiki à l'intérieur du panneau,
 * puisque cohtml ne propose pas de composant hyperlien natif avec ancre de page.
 */
function RuleText({ text, onNavigate }: { text: string; onNavigate: (id: string) => void }) {
  const linkPattern = /\[\[([a-z0-9_]+)\|([^\]]+)\]\]/g;

  const parts: (string | { linkId: string; label: string })[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  while ((match = linkPattern.exec(text)) !== null) {
    if (match.index > lastIndex) parts.push(text.slice(lastIndex, match.index));
    parts.push({ linkId: match[1], label: match[2] });
    lastIndex = match.index + match[0].length;
  }
  if (lastIndex < text.length) parts.push(text.slice(lastIndex));

  return (
    <>
      {parts.map((p, i) =>
        typeof p === "string" ? (
          <span key={i}>{p}</span>
        ) : (
          <span
            key={i}
            onClick={() => onNavigate(p.linkId)}
            style={{
              color: "rgba(120,180,255,0.95)",
              cursor: "pointer",
              textDecoration: "underline",
            }}
          >
            {p.label}
          </span>
        )
      )}
    </>
  );
}

export function RulesTab() {
  const [selectedId, setSelectedId] = useState<string>(RULES_SECTIONS[0].id);
  const detailRef = useRef<HTMLDivElement>(null);

  const selected = useMemo(
    () => RULES_SECTIONS.find((s) => s.id === selectedId) ?? RULES_SECTIONS[0],
    [selectedId]
  );

  const handleNavigate = (id: string) => {
    const exists = RULES_SECTIONS.some((s) => s.id === id);
    if (!exists) return; // lien vers une section inconnue, ignoré silencieusement
    setSelectedId(id);
    if (detailRef.current) detailRef.current.scrollTop = 0;
  };

  return (
    <div style={{ display: "flex", width: "100%", height: "100%", boxSizing: "border-box" }}>
      {/* Colonne gauche : sommaire, même pattern que PoliticalForcesTab/ElectoralCommissionTab */}
      <div style={{ width: "170rem", flexShrink: 0, borderRight: "1rem solid rgba(255,255,255,0.12)", overflowY: "auto" }}>
        {RULES_SECTIONS.map((s) => {
          const isSelected = s.id === selected.id;
          return (
            <div
              key={s.id}
              onClick={() => handleNavigate(s.id)}
              style={{
                padding: "8rem 10rem",
                cursor: "pointer",
                background: isSelected ? "rgba(255,255,255,0.10)" : "transparent",
                borderLeft: isSelected ? "3rem solid white" : "3rem solid transparent",
                color: "white",
                fontSize: "12rem",
                whiteSpace: "nowrap",
                overflow: "hidden",
                textOverflow: "ellipsis",
              }}
            >
              {s.title}
            </div>
          );
        })}
      </div>

      {/* Colonne droite : contenu détaillé de la section sélectionnée */}
      <div ref={detailRef} style={{ flex: 1, padding: "14rem", overflowY: "auto", boxSizing: "border-box" }}>
        <div style={{ color: "white", fontSize: "16rem", fontWeight: 700, marginBottom: "14rem" }}>
          {selected.title}
        </div>

        {selected.paragraphs.map((p, i) => (
          <div
            key={i}
            style={{
              color: "rgba(255,255,255,0.85)",
              fontSize: "13rem",
              lineHeight: "19rem",
              marginBottom: "12rem",
            }}
          >
            <RuleText text={p} onNavigate={handleNavigate} />
          </div>
        ))}
      </div>
    </div>
  );
}