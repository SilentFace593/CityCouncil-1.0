using System.Collections.Generic;
using Colossal.UI.Binding;

namespace CityCouncil.Systems
{
    /// <summary>
    /// Extension PARTIELLE de CouncilUISystem : binding "recordsJson" (encart Records de l'onglet
    /// Score). Même pattern que CouncilLawUIBindings.cs / CouncilDistrictsOverviewUIBindings.cs.
    ///
    /// À ajouter à la main dans CouncilUISystem.cs :
    ///   - fin de OnCreate() : SetupRecordsBindings();
    ///   - fin de OnUpdate() : UpdateRecordsBindingIfChanged();
    /// </summary>
    public partial class CouncilUISystem
    {
        private CityCouncil.CouncilRecordSystem m_RecordSystem;
        private ValueBinding<string> m_RecordsJsonBinding;
        private string m_LastPushedRecordsJson;
        private bool m_HasLastPushedRecords;

        private void SetupRecordsBindings()
        {
            m_RecordSystem = World.GetOrCreateSystemManaged<CityCouncil.CouncilRecordSystem>();
            m_RecordsJsonBinding = new ValueBinding<string>(kGroup, "recordsJson", "[]");
            AddBinding(m_RecordsJsonBinding);
        }

        private void UpdateRecordsBindingIfChanged(bool force = false)
        {
            var custom = m_CustomPartySystem.GetData();
            var dtos = new List<RecordDto>();

            foreach (CityCouncil.RecordCategory cat in System.Enum.GetValues(typeof(CityCouncil.RecordCategory)))
            {
                if (!m_RecordSystem.TryGetHolder(cat, out var holder, out float value)) continue;

                var dto = new RecordDto
                {
                    category = cat.ToString(),
                    value = value,
                    party = holder.ToString(),
                    displayName = "",
                    displayColor = "",
                };
                if (custom.m_Exists && custom.m_SubstitutionActive && custom.m_ActiveSpace.ToString() == dto.party)
                {
                    dto.displayName = custom.m_Name.ToString();
                    dto.displayColor = custom.m_Color.ToString();
                }
                dtos.Add(dto);
            }

            string json = RecordDto.ToJsonArray(dtos);
            if (!force && m_HasLastPushedRecords && json == m_LastPushedRecordsJson) return;

            m_RecordsJsonBinding.Update(json);
            m_LastPushedRecordsJson = json;
            m_HasLastPushedRecords = true;
        }
    }

    public struct RecordDto
    {
        public string category;
        public float value;
        public string party;
        public string displayName;
        public string displayColor;

        public static string ToJsonArray(IEnumerable<RecordDto> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var r in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{');
                sb.Append("\"category\":\"").Append(r.category).Append("\",");
                sb.Append("\"value\":").Append(r.value.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                sb.Append("\"party\":\"").Append(r.party).Append("\",");
                sb.Append("\"displayName\":\"").Append((r.displayName ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\",");
                sb.Append("\"displayColor\":\"").Append(r.displayColor ?? "").Append("\"");
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}