import { useMemo, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { translatePartyName } from "./PartyResultDto";

const partyMembershipJson$ = bindValue<string>("cityCouncil", "partyMembershipJson");
const propagandaStateJson$ = bindValue<string>("cityCouncil", "propagandaStateJson");
const customPartyExists$ = bindValue<boolean>("cityCouncil", "customPartyExists");
const customPartyName$ = bindValue<string>("cityCouncil", "customPartyName");
const customPartySpace$ = bindValue<string>("cityCouncil", "customPartySpace");
const customPartyPendingActivation$ = bindValue<boolean>("cityCouncil", "customPartyPendingActivation");
const districtListJson$ = bindValue<string>("cityCouncil", "districtListJson");
const districtCampaignsJson$ = bindValue<string>("cityCouncil", "districtCampaignsJson");
const illegalCampaignsJson$ = bindValue<string>("cityCouncil", "illegalCampaignsJson");
const blackFundJson$ = bindValue<string>("cityCouncil", "blackFundJson");

interface PartyMembershipDto { party: string; members: number; treasury: number; }
interface PropagandaDto { party: string; active: boolean; target: string; bonusPercent: number; autoRenew: boolean; }
interface DistrictDto { id: number; name: string; }
interface DistrictCampaignDto {
  districtId: number; districtName: string; party: string;
  type: string; targetParty: string; bonusPercent: number; selfMalusPercent: number;
}
interface IllegalCampaignDto {
  districtId: number; districtName: string; targetParty: string; malusPercent: number;
}


const INTENSITY_TIERS: Record<string, { cost: number; bonus: number }> = {
  Petite: { cost: 25000, bonus: 0.02 },
  Moyenne: { cost: 75000, bonus: 0.04 },
  Forte: { cost: 150000, bonus: 0.06 },
};
const INTENSITIES = ["Petite", "Moyenne", "Forte"];
const TARGETS = ["Adultes", "Seniors"];

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

function Dropdown({
  value,
  options,
  labels,
  onChange,
}: {
  value: string;
  options: string[];
  labels: Record<string, string>;
  onChange: (v: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const safeOptions = Array.isArray(options) ? options : [];

  return (
    <div style={{ position: "relative" }}>
      <div
        onClick={() => setOpen((o) => !o)}
        style={{
          width: "100%",
          boxSizing: "border-box",
          background: "rgba(255,255,255,0.08)",
          border: "1rem solid rgba(255,255,255,0.15)",
          borderRadius: "4rem",
          color: "white",
          fontSize: "13rem",
          padding: "6rem 8rem",
          cursor: "pointer",
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
        }}
      >
        <span>{labels?.[value] ?? value}</span>
        <span style={{ fontSize: "10rem", opacity: 0.7 }}>{open ? "▲" : "▼"}</span>
      </div>

      {open && (
        <div
          style={{
            position: "absolute",
            top: "100%",
            left: 0,
            right: 0,
            marginTop: "2rem",
            background: "rgba(30,30,40,0.98)",
            border: "1rem solid rgba(255,255,255,0.15)",
            borderRadius: "4rem",
            zIndex: 10,
            overflow: "hidden",
          }}
        >
          {safeOptions.map((o) => (
            <div
              key={o}
              onClick={() => {
                onChange(o);
                setOpen(false);
              }}
              style={{
                padding: "6rem 8rem",
                fontSize: "13rem",
                color: "white",
                cursor: "pointer",
                background: o === value ? "rgba(70,130,220,0.5)" : "transparent",
              }}
            >
              {labels?.[o] ?? o}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export function PropagandaTab() {
  const { translate } = useLocalization();
  const t = (key: string, fallback: string): string => translate(key, fallback) ?? fallback;

  const membershipJson = useValue(partyMembershipJson$);
  const propagandaJson = useValue(propagandaStateJson$);
  const customExists = useValue(customPartyExists$);
  const customName = useValue(customPartyName$);
  const customSpace = useValue(customPartySpace$);
  const customPending = useValue(customPartyPendingActivation$);
  const districtListJson = useValue(districtListJson$);
  const districtCampaignsJson = useValue(districtCampaignsJson$);
  const illegalCampaignsJson = useValue(illegalCampaignsJson$);
  const blackFundJson = useValue(blackFundJson$);

  const illegalCampaigns: IllegalCampaignDto[] = useMemo(() => {
  try { const p = JSON.parse(illegalCampaignsJson ?? "[]"); return Array.isArray(p) ? p : []; }
  catch { return []; }
}, [illegalCampaignsJson]);

const blackFundActive = useMemo(() => {
  try { return !!JSON.parse(blackFundJson ?? "{}").active; }
  catch { return false; }
}, [blackFundJson]);

const [illegalDistrictId, setIllegalDistrictId] = useState<number | null>(null);
const [illegalTarget, setIllegalTarget] = useState<string>("");

const illegalSlotsFull = illegalCampaigns.length >= 3;

const handleLaunchIllegal = () => {
  if (illegalDistrictId === null || !illegalTarget || illegalSlotsFull) return;
  trigger("cityCouncil", "launchIllegalCampaign", String(illegalDistrictId), illegalTarget);
};

  const playerControlsAvailable = !!customExists && !customPending && !!customSpace;

  const membership: PartyMembershipDto[] = useMemo(() => {
    try {
      const p = JSON.parse(membershipJson ?? "[]");
      return Array.isArray(p) ? p : [];
    } catch {
      return [];
    }
  }, [membershipJson]);

  const campaigns: PropagandaDto[] = useMemo(() => {
    try {
      const p = JSON.parse(propagandaJson ?? "[]");
      return Array.isArray(p) ? p : [];
    } catch {
      return [];
    }
  }, [propagandaJson]);

  const districtList: DistrictDto[] = useMemo(() => {
  try { const p = JSON.parse(districtListJson ?? "[]"); return Array.isArray(p) ? p : []; }
  catch { return []; }
}, [districtListJson]);

const districtCampaigns: DistrictCampaignDto[] = useMemo(() => {
  try { const p = JSON.parse(districtCampaignsJson ?? "[]"); return Array.isArray(p) ? p : []; }
  catch { return []; }
}, [districtCampaignsJson]);

const [selectedDistrictId, setSelectedDistrictId] = useState<number | null>(null);
const [campaignMode, setCampaignMode] = useState<"boost" | "attack">("boost");
const [boostTier, setBoostTier] = useState<string>("Petite");
const [attackTarget, setAttackTarget] = useState<string>("");
const [attackDirty, setAttackDirty] = useState(false);

const slotsUsed = districtCampaigns.length;
const slotsFull = slotsUsed >= 3;

const handleLaunchDistrict = () => {
  if (!playerControlsAvailable || !customSpace || selectedDistrictId === null || slotsFull) return;
  const type = campaignMode === "boost" ? "Boost" : (attackDirty ? "AttackDirty" : "AttackClean");
  const target = campaignMode === "attack" ? attackTarget : "";
  // AJOUT — plus que 4 arguments (districtId, type, target, tier), le 5e "" a été retiré
  trigger("cityCouncil", "launchDistrictCampaign", String(selectedDistrictId), type, target, boostTier);
};

const handleCancelDistrict = (districtId: number) =>
  trigger("cityCouncil", "cancelDistrictCampaign", String(districtId));

  const partyLabel = (key: string): string => {
    if (!key) return "";
    const isCustomHere = playerControlsAvailable && customSpace === key;
    return isCustomHere ? (customName || key) : translatePartyName(key, translate);
  };

  const playerActiveCampaign = playerControlsAvailable
    ? campaigns.find((c) => c && c.party === customSpace && c.active)
    : undefined;

  const handleCancel = () => {
    if (!playerControlsAvailable || !customSpace) return;
    trigger("cityCouncil", "cancelCampaign", customSpace);
  };

  const alreadyActiveLine = t(
    "CityCouncil.Propaganda.ALREADY_ACTIVE",
    "Une campagne est déjà en cours pour votre parti. Annulez-la ou attendez son terme pour en lancer une nouvelle."
  );
  const cancelLabel = t("CityCouncil.Propaganda.CANCEL_BUTTON", "Annuler la campagne");

  const [target, setTarget] = useState<string>("Adultes");
  const [intensity, setIntensity] = useState<string>("Petite");
  const [autoRenew, setAutoRenew] = useState<boolean>(false);

  const targetLabels: Record<string, string> = {
    Adultes: t("CityCouncil.Propaganda.TARGET_ADULTS", "Adultes"),
    Seniors: t("CityCouncil.Propaganda.TARGET_SENIORS", "Séniors"),
  };
  const intensityLabels: Record<string, string> = {
    Petite: t("CityCouncil.Propaganda.INTENSITY_SMALL", "Petite campagne"),
    Moyenne: t("CityCouncil.Propaganda.INTENSITY_MEDIUM", "Campagne moyenne"),
    Forte: t("CityCouncil.Propaganda.INTENSITY_STRONG", "Forte campagne"),
  };

  const selectedMembership = playerControlsAvailable
    ? membership.find((m) => m && m.party === customSpace)
    : undefined;
  const treasury = selectedMembership?.treasury ?? 0;
  const tier = INTENSITY_TIERS[intensity] ?? INTENSITY_TIERS.Petite; // GARDE
  const canAfford = treasury >= tier.cost;

  const handleLaunch = () => {
  if (!playerControlsAvailable || !canAfford || !customSpace) return;
  trigger("cityCouncil", "launchCampaign", customSpace, target, intensity, autoRenew ? "true" : "false");
};

  const costLine = `${t("CityCouncil.Propaganda.COST_LABEL", "Coût : ")}${tier.cost.toLocaleString()}`;
  const bonusLine = `${t("CityCouncil.Propaganda.BONUS_LABEL", "Bonus : ")}+${Math.round(tier.bonus * 100)}%`;
  const treasuryLine = `${t("CityCouncil.Propaganda.TREASURY_LABEL", "Réserves : ")}${treasury.toLocaleString()}`;
  const insufficientLine = t("CityCouncil.Propaganda.INSUFFICIENT_FUNDS", "Réserves insuffisantes.");
  const noPlayerPartyLine = t("CityCouncil.Propaganda.NO_PLAYER_PARTY", "Créez votre propre parti (onglet \"Votre Parti\") pour lancer vos propres campagnes de propagande.");

  const activeList = campaigns.filter((c) => c && c.active); // GARDE (c non-null)

  return (
    <div style={{ padding: "10rem", width: "100%", boxSizing: "border-box" }}>
      {playerControlsAvailable ? (
  playerActiveCampaign ? (
    <div style={{ padding: "10rem", background: "rgba(255,255,255,0.06)", borderRadius: "6rem", marginBottom: "12rem" }}>
      <div style={{ color: "rgba(255,180,120,0.9)", fontSize: "12rem", lineHeight: "16rem", marginBottom: "10rem" }}>
        {alreadyActiveLine}
      </div>
      <ActionButton label={cancelLabel} enabled={true} onClick={handleCancel} />
    </div>
  ) : (
    <>
      <div style={{ marginBottom: "12rem" }}>
        <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "11rem", marginBottom: "4rem", textTransform: "uppercase" }}>
          {t("CityCouncil.Propaganda.TARGET_LABEL", "Cible")}
        </div>
        <Dropdown value={target} options={TARGETS} labels={targetLabels} onChange={setTarget} />
      </div>

      <div style={{ marginBottom: "12rem" }}>
        <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "11rem", marginBottom: "4rem", textTransform: "uppercase" }}>
          {t("CityCouncil.Propaganda.INTENSITY_LABEL", "Intensité")}
        </div>
        <Dropdown value={intensity} options={INTENSITIES} labels={intensityLabels} onChange={setIntensity} />
      </div>

      <div style={{ padding: "10rem", background: "rgba(255,255,255,0.06)", borderRadius: "6rem", marginBottom: "12rem" }}>
        <div style={{ color: "white", fontSize: "13rem", whiteSpace: "nowrap", marginBottom: "4rem" }}>{costLine}</div>
        <div style={{ color: "rgba(150,190,255,0.9)", fontSize: "13rem", whiteSpace: "nowrap", marginBottom: "4rem" }}>{bonusLine}</div>
        <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", whiteSpace: "nowrap" }}>{treasuryLine}</div>
        {!canAfford && (
          <div style={{ color: "rgba(255,120,120,0.9)", fontSize: "12rem", marginTop: "6rem", whiteSpace: "nowrap" }}>
            {insufficientLine}
          </div>
        )}
      </div>

      <div
        onClick={() => setAutoRenew((v) => !v)}
        style={{ display: "flex", alignItems: "center", cursor: "pointer", marginBottom: "10rem" }}
      >
        <div
          style={{
            width: "16rem",
            height: "16rem",
            borderRadius: "3rem",
            border: autoRenew ? "1rem solid rgba(120,170,255,0.9)" : "1rem solid rgba(255,255,255,0.35)",
            background: autoRenew ? "rgba(70,130,220,0.85)" : "rgba(255,255,255,0.06)",
            marginRight: "8rem",
            flexShrink: 0,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
          }}
        >
          {autoRenew && <div style={{ color: "white", fontSize: "11rem", fontWeight: "bold" }}>✓</div>}
        </div>
        <div style={{ color: "rgba(255,255,255,0.9)", fontSize: "13rem", whiteSpace: "nowrap" }}>
          {t("CityCouncil.Propaganda.AUTO_RENEW_LABEL", "Reconduire automatiquement")}
        </div>
      </div>

      <ActionButton
        label={t("CityCouncil.Propaganda.LAUNCH_BUTTON", "Lancer la campagne")}
        enabled={canAfford}
        onClick={handleLaunch}
      />
    </>
  )
) : (
  <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "12rem", lineHeight: "17rem", marginBottom: "12rem" }}>
    {noPlayerPartyLine}
  </div>
)}

      <div style={{ marginTop: "16rem" }}>
        <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", marginBottom: "8rem", textTransform: "uppercase", whiteSpace: "nowrap" }}>
          {t("CityCouncil.Propaganda.ACTIVE_CAMPAIGNS_HEADER", "Campagnes en cours")}
        </div>
        {activeList.length === 0 && (
          <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "12rem" }}>
            {t("CityCouncil.Propaganda.NO_ACTIVE_CAMPAIGNS", "Aucune campagne active.")}
          </div>
        )}
        {activeList.map((c, i) => {
        const bonusPct = typeof c.bonusPercent === "number" ? c.bonusPercent : 0;
        const targetLabel = targetLabels[c.target] ?? c.target ?? "";
        const renewSuffix = c.autoRenew ? " 🔁" : "";
        const line = `${partyLabel(c.party)} — ${targetLabel} (+${Math.round(bonusPct * 100)}%)${renewSuffix}`;
        return (
        <div key={c.party ?? i} style={{ color: "rgba(255,255,255,0.85)", fontSize: "12rem", marginBottom: "4rem", whiteSpace: "nowrap" }}>
        {line}
        </div>
  );
})}
      </div>

      <div style={{ marginTop: "20rem", paddingTop: "16rem", borderTop: "1rem solid rgba(255,255,255,0.15)" }}>
  <div style={{ color: "rgba(255,255,255,0.7)", fontSize: "12rem", marginBottom: "8rem", textTransform: "uppercase" }}>
    {t("CityCouncil.DistrictCampaign.HEADER", "Campagnes de district")}
  </div>

  <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "11rem", marginBottom: "10rem" }}>
    {`${slotsUsed} / 3 campagnes de district actives`}
  </div>

  {playerControlsAvailable && !slotsFull && (
    <>
      {/* Sélection du district */}
      <div style={{ marginBottom: "10rem" }}>
        <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "11rem", marginBottom: "4rem" }}>
          {t("CityCouncil.DistrictCampaign.SELECT_DISTRICT", "District ciblé")}
        </div>
        <Dropdown
          value={selectedDistrictId !== null ? String(selectedDistrictId) : ""}
          options={districtList.map((d) => String(d.id))}
          labels={Object.fromEntries(districtList.map((d) => [String(d.id), d.name]))}
          onChange={(v) => setSelectedDistrictId(Number(v))}
        />
      </div>

      {/* Mode : classique ou attaque */}
      <div style={{ display: "flex", gap: "8rem", marginBottom: "10rem" }}>
        <ActionButton label={t("CityCouncil.DistrictCampaign.TYPE_BOOST", "Campagne classique")}
          enabled={true} onClick={() => setCampaignMode("boost")} />
        <ActionButton label={t("CityCouncil.DistrictCampaign.TYPE_ATTACK", "Campagne ciblée")}
          enabled={true} onClick={() => setCampaignMode("attack")} />
      </div>

      {campaignMode === "boost" && (
        <Dropdown value={boostTier} options={INTENSITIES} labels={intensityLabels} onChange={setBoostTier} />
      )}

      {campaignMode === "attack" && (
        <>
          <Dropdown
            value={attackTarget}
            options={["Ecologiste", "Democrate", "Populiste", "Republicain", "GaucheRadicale"].filter((p) => p !== customSpace)}
            labels={{}}
            onChange={setAttackTarget}
          />
          <div
            onClick={() => setAttackDirty((v) => !v)}
            style={{ marginTop: "8rem", padding: "8rem", background: "rgba(255,255,255,0.06)", borderRadius: "4rem", cursor: "pointer" }}
          >
            <div style={{ color: "white", fontSize: "12rem", fontWeight: 600 }}>
              {attackDirty
                ? t("CityCouncil.DistrictCampaign.ATTACK_DIRTY_LABEL", "Campagne sale")
                : t("CityCouncil.DistrictCampaign.ATTACK_CLEAN_LABEL", "Campagne propre")}
            </div>
            <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "11rem", marginTop: "4rem", lineHeight: "15rem" }}>
              {attackDirty
                ? t("CityCouncil.DistrictCampaign.ATTACK_DIRTY_DESC", "-4% pour le parti visé, mais un malus aléatoire (0 à -5%) frappe aussi votre propre parti dans ce district.")
                : t("CityCouncil.DistrictCampaign.ATTACK_CLEAN_DESC", "-2% pour le parti visé dans ce district. Aucun risque pour vous.")}
            </div>
          </div>
        </>
      )}

      <ActionButton
        label={t("CityCouncil.DistrictCampaign.LAUNCH_BUTTON", "Lancer la campagne")}
        enabled={selectedDistrictId !== null && (campaignMode === "boost" || !!attackTarget)}
        onClick={handleLaunchDistrict}
      />
    </>
  )}

  {slotsFull && (
    <div style={{ color: "rgba(255,180,120,0.9)", fontSize: "12rem" }}>
      {t("CityCouncil.DistrictCampaign.MAX_REACHED", "Nombre maximum de campagnes de district atteint (3).")}
    </div>
  )}

{playerControlsAvailable && blackFundActive && (
  <div style={{ marginTop: "20rem", paddingTop: "16rem", borderTop: "1rem solid rgba(220,80,80,0.3)" }}>
    <div style={{ color: "rgba(255,140,140,0.9)", fontSize: "12rem", marginBottom: "6rem", textTransform: "uppercase" }}>
      {t("CityCouncil.Illegal.SECTION_HEADER", "Campagne illégale")}
    </div>

    <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "11rem", lineHeight: "16rem", marginBottom: "10rem" }}>
      {t("CityCouncil.Illegal.DESCRIPTION", "Cible un parti dans ce district avec un malus aléatoire de 0 à 6%, financé par la caisse noire. En cas de détection par la Commission Électorale, votre parti encourt une sanction à l'échelle de la ville, une amende, et la perte de la caisse noire.")}
    </div>

    <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "11rem", marginBottom: "8rem" }}>
      {`${illegalCampaigns.length} / 3`}
    </div>

    {!illegalSlotsFull ? (
      <>
        <div style={{ marginBottom: "10rem" }}>
          <Dropdown
            value={illegalDistrictId !== null ? String(illegalDistrictId) : ""}
            options={districtList.map((d) => String(d.id))}
            labels={Object.fromEntries(districtList.map((d) => [String(d.id), d.name]))}
            onChange={(v) => setIllegalDistrictId(Number(v))}
          />
        </div>
        <div style={{ marginBottom: "10rem" }}>
          <Dropdown
            value={illegalTarget}
            options={["Ecologiste", "Democrate", "Populiste", "Republicain", "GaucheRadicale"].filter((p) => p !== customSpace)}
            labels={{}}
            onChange={setIllegalTarget}
          />
        </div>
        <div style={{ color: "rgba(255,255,255,0.5)", fontSize: "11rem", marginBottom: "10rem" }}>
          {`${t("CityCouncil.Illegal.COST_LABEL", "Coût : ")}10 000`}
        </div>
        <ActionButton
          label={t("CityCouncil.Illegal.LAUNCH_BUTTON", "Lancer la campagne illégale")}
          enabled={illegalDistrictId !== null && !!illegalTarget}
          onClick={handleLaunchIllegal}
        />
      </>
    ) : (
      <div style={{ color: "rgba(255,180,120,0.9)", fontSize: "12rem" }}>
        {t("CityCouncil.Illegal.MAX_REACHED", "Nombre maximum de campagnes illégales atteint (3).")}
      </div>
    )}

    <div style={{ marginTop: "12rem" }}>
      {illegalCampaigns.map((c) => (
        <div key={c.districtId} style={{ color: "rgba(255,255,255,0.8)", fontSize: "12rem", marginBottom: "4rem" }}>
          {`${c.districtName} — ${translatePartyName(c.targetParty, translate)} -${Math.round(c.malusPercent * 100)}%`}
        </div>
      ))}
    </div>
  </div>
)}

  {/* Liste des campagnes actives */}
  <div style={{ marginTop: "14rem" }}>
    {districtCampaigns.map((c) => {
      const typeLabel = c.type === "Boost"
        ? `+${Math.round(c.bonusPercent * 100)}%`
        : `${translatePartyName(c.targetParty, translate)} -${Math.round(c.bonusPercent * 100)}%` +
          (c.type === "AttackDirty" ? ` (risque -${Math.round(c.selfMalusPercent * 100)}% pour vous)` : "");
      return (
        <div key={`${c.districtId}-${c.type}`} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "6rem" }}>
          <span style={{ color: "white", fontSize: "12rem" }}>{`${c.districtName} — ${typeLabel}`}</span>
          <ActionButton label={t("CityCouncil.DistrictCampaign.CANCEL_BUTTON", "Annuler")}
            enabled={true} onClick={() => handleCancelDistrict(c.districtId)} />
        </div>
      );
    })}
  </div>
</div>

    </div>
  );
}