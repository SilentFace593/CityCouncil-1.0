import { useLocalization } from "cs2/l10n";
import ecologistePortrait from "./images/ecologiste_portrait.png";
import democratePortrait from "./images/democrate_portrait.png";
import republicainPortrait from "./images/republicain_portrait.png";
import gaucheRadicalePortrait from "./images/gauche_radicale_portrait.png";
import populistePortrait from "./images/populiste_portrait.png";
import { PARTY_DESCRIPTIONS } from "./PartyDescriptions";

// Mapping explicite requis par webpack (les imports d'images doivent être statiques,
// même pattern que PARTY_LOGOS dans PartyResultDto.tsx).
const PORTRAIT_IMAGES: Record<string, string> = {
  "ecologiste_portrait.png": ecologistePortrait,
  "democrate_portrait.png" : democratePortrait,
  "republicain_portrait.png": republicainPortrait,
  "gauche_radicale_portrait.png": gaucheRadicalePortrait,
  "populiste_portrait.png": populistePortrait,
};

/**
 * Parse une syntaxe légère **gras** en segments texte/gras, même principe que
 * RuleText (RulesTab.tsx) qui parse déjà [[id|libellé]] avec une regex + boucle.
 */
function FormattedParagraph({ text }: { text: string }) {
  const boldPattern = /\*\*(.+?)\*\*/g;
  const parts: (string | { bold: string })[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  while ((match = boldPattern.exec(text)) !== null) {
    if (match.index > lastIndex) parts.push(text.slice(lastIndex, match.index));
    parts.push({ bold: match[1] });
    lastIndex = match.index + match[0].length;
  }
  if (lastIndex < text.length) parts.push(text.slice(lastIndex));

  return (
    <>
      {parts.map((p, i) =>
        typeof p === "string" ? (
          <span key={i}>{p}</span>
        ) : (
          <span key={i} style={{ fontWeight: 700 }}>{p.bold}</span>
        )
      )}
    </>
  );
}

function FormattedText({ text }: { text: string }) {
  // Un paragraphe = un bloc séparé par une ligne vide dans le texte source.
  const paragraphs = text.split(/\n\s*\n/).filter((p) => p.trim().length > 0);

  return (
    <>
      {paragraphs.map((p, i) => (
        <div key={i} style={{ marginBottom: i < paragraphs.length - 1 ? "10rem" : 0 }}>
          <FormattedParagraph text={p.trim()} />
        </div>
      ))}
    </>
  );
}

export function PartyDescriptionBlock({
  partyKey,
  fallbackText,
  isPlayerParty,
}: {
  partyKey: string;
  fallbackText: string;
  isPlayerParty?: boolean;
}) {
  const { translate } = useLocalization();

  // AJOUT — un parti joueur actif n'affiche jamais le texte/portrait du bord vanilla qu'il
  // remplace visuellement : on retombe systématiquement sur le fallback (déjà prévu pour ça,
  // cf. Forces_CustomPartyDesc), sans chercher de config dans PARTY_DESCRIPTIONS.
  const config = !isPlayerParty ? PARTY_DESCRIPTIONS[partyKey] : undefined;

  if (!config) {
    return <div style={{ color: "rgba(255,255,255,0.8)", fontSize: "13rem", lineHeight: "18rem" }}>{fallbackText}</div>;
  }

  const resolvedText = translate(config.localeKey, fallbackText) ?? fallbackText;
  const portraitSrc = config.portrait ? PORTRAIT_IMAGES[config.portrait] : null;

  return (
    <div style={{ display: "flex", alignItems: "flex-start" }}>
      {portraitSrc && (
        <img
          src={portraitSrc}
          style={{
            width: "200rem",
            height: "250rem",
            objectFit: "contain",
            flexShrink: 0,
            marginRight: "30rem",
            borderRadius: "4rem",
            border: "1rem solid rgba(255,255,255,0.15)",
          }}
        />
      )}
      <div style={{
        flex: 1,
        minWidth: 0,
        color: "rgba(255,255,255,0.8)",
        fontSize: "13rem",
        lineHeight: "18rem",
        textAlign: "justify",
      }}>
        <FormattedText text={resolvedText} />
      </div>
    </div>
  );
}