using System.Collections.Generic;
using System.Linq;
using Colossal.UI.Binding;
using Unity.Collections;
using Unity.Entities;
using static CityCouncil.CouncilDistrictData;

namespace CityCouncil.Systems
{
    /// <summary>
    /// Extension PARTIELLE de CouncilUISystem : expose la liste des districts (nom, votants,
    /// sièges, % du conseil, parti dirigeant, état Bastion) pour l'entrée "Districts" de l'onglet
    /// Forces Politiques. Même pattern que CouncilLawUIBindings.cs (fichier séparé, `partial`).
    ///
    /// À ajouter à la main dans CouncilUISystem.cs :
    ///   - fin de OnCreate() : SetupDistrictsOverviewBindings();
    ///   - fin de OnUpdate() : UpdateDistrictsOverviewBindingIfChanged();
    /// </summary>
    public partial class CouncilUISystem
    {
        private ValueBinding<string> m_DistrictsOverviewJsonBinding;
        private string m_LastPushedDistrictsOverviewJson;
        private bool m_HasLastPushedDistrictsOverview;

        private void SetupDistrictsOverviewBindings()
        {
            m_DistrictsOverviewJsonBinding = new ValueBinding<string>(kGroup, "districtsOverviewJson", "[]");
            AddBinding(m_DistrictsOverviewJsonBinding);
        }

        private void UpdateDistrictsOverviewBindingIfChanged(bool force = false)
        {
            var dtos = new List<DistrictOverviewDto>();
            int totalSeatsAllDistricts = 0;

            var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var d in districts)
                {
                    if (!m_EntityManager.HasComponent<CouncilDistrictData>(d)) continue;
                    var data = m_EntityManager.GetComponentData<CouncilDistrictData>(d);
                    if (data.m_Phase != ElectionPhase.Completed) continue;
                    totalSeatsAllDistricts += data.m_TotalSeats;
                }

                var custom = m_CustomPartySystem.GetData();

                foreach (var d in districts)
                {
                    if (!m_EntityManager.HasComponent<CouncilDistrictData>(d)) continue;
                    var data = m_EntityManager.GetComponentData<CouncilDistrictData>(d);
                    if (data.m_Phase != ElectionPhase.Completed) continue;

                    int voters = data.m_VotersRound1 + data.m_AbstentionRound1;
                    if (voters <= 0) continue;

                    float pct = totalSeatsAllDistricts > 0
                        ? (data.m_TotalSeats * 100f / totalSeatsAllDistricts)
                        : 0f;

                    var dto = new DistrictOverviewDto
                    {
                        districtId = d.Index,
                        districtName = GetDistrictDisplayName(d),
                        voters = voters,
                        seats = data.m_TotalSeats,
                        percentOfCouncil = pct,
                        leadingParty = data.m_LeadingParty.ToString(),
                        displayName = "",
                        displayColor = "",
                        isBastion = data.m_IsBastion,
                        bastionParty = data.m_IsBastion ? data.m_BastionParty.ToString() : "",
                        bastionDisplayName = "",
                        bastionDisplayColor = "",
                        streakCount = data.m_StreakCount,
                        isReinforcedBastion = data.m_IsBastion
        && m_ReinforcedBastionSystem.IsReinforcedBastion(data.m_BastionParty, d),
                    };

                    if (custom.m_Exists && custom.m_SubstitutionActive)
                    {
                        if (custom.m_ActiveSpace.ToString() == dto.leadingParty)
                        {
                            dto.displayName = custom.m_Name.ToString();
                            dto.displayColor = custom.m_Color.ToString();
                        }
                        if (data.m_IsBastion && custom.m_ActiveSpace.ToString() == dto.bastionParty)
                        {
                            dto.bastionDisplayName = custom.m_Name.ToString();
                            dto.bastionDisplayColor = custom.m_Color.ToString();
                        }
                    }

                    dtos.Add(dto);
                }
            }
            finally { districts.Dispose(); }

            dtos.Sort((a, b) => b.voters.CompareTo(a.voters));

            string json = DistrictOverviewDto.ToJsonArray(dtos);
            if (!force && m_HasLastPushedDistrictsOverview && json == m_LastPushedDistrictsOverviewJson) return;

            m_DistrictsOverviewJsonBinding.Update(json);
            m_LastPushedDistrictsOverviewJson = json;
            m_HasLastPushedDistrictsOverview = true;
        }
    }

    public struct DistrictOverviewDto
    {
        public int districtId;
        public string districtName;
        public int voters;
        public int seats;
        public float percentOfCouncil;
        public string leadingParty;
        public string displayName;
        public string displayColor;
        public bool isBastion;
        public string bastionParty;
        public string bastionDisplayName;
        public string bastionDisplayColor;
        public int streakCount;
        public bool isReinforcedBastion; // AJOUT

        public static string ToJsonArray(System.Collections.Generic.IEnumerable<DistrictOverviewDto> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var d in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{');
                sb.Append("\"districtId\":").Append(d.districtId).Append(',');
                sb.Append("\"districtName\":\"").Append(Escape(d.districtName)).Append("\",");
                sb.Append("\"voters\":").Append(d.voters).Append(',');
                sb.Append("\"seats\":").Append(d.seats).Append(',');
                sb.Append("\"percentOfCouncil\":").Append(d.percentOfCouncil.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                sb.Append("\"leadingParty\":\"").Append(d.leadingParty).Append("\",");
                sb.Append("\"displayName\":\"").Append(Escape(d.displayName ?? "")).Append("\",");
                sb.Append("\"displayColor\":\"").Append(d.displayColor ?? "").Append("\",");
                sb.Append("\"isBastion\":").Append(d.isBastion ? "true" : "false").Append(',');
                sb.Append("\"bastionParty\":\"").Append(d.bastionParty ?? "").Append("\",");
                sb.Append("\"bastionDisplayName\":\"").Append(Escape(d.bastionDisplayName ?? "")).Append("\",");
                sb.Append("\"bastionDisplayColor\":\"").Append(d.bastionDisplayColor ?? "").Append("\",");
                sb.Append("\"streakCount\":").Append(d.streakCount).Append(',');
                sb.Append("\"isReinforcedBastion\":").Append(d.isReinforcedBastion ? "true" : "false"); // AJOUT
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}