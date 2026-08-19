using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;

namespace CityCouncil
{
    [FileLocation(nameof(CityCouncil))]
    [SettingsUIGroupOrder(kGeneralGroup)]
    [SettingsUIShowGroupName(kGeneralGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kGeneralGroup = "General";

        public Setting(IMod mod) : base(mod)
        {
        }

        /// <summary>
        /// Affiche ou masque l'onglet [DEBUG] dans le panneau hémicycle. Lu par CouncilUISystem
        /// à chaque frame UI (peu coûteux : simple lecture de propriété) et poussé vers React
        /// via le binding "showDebugTab".
        /// </summary>
        [SettingsUISection(kSection, kGeneralGroup)]
        public bool ShowDebugTab { get; set; } = false;

        public override void SetDefaults()
        {
            ShowDebugTab = false;
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "CityCouncil" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kGeneralGroup), "General" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShowDebugTab)), "Show debug tab" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ShowDebugTab)), "Adds a [DEBUG] tab to the City Council panel, grouping all debug/test buttons." },
            };
        }
        public void Unload() { }
    }

    public class LocaleFR : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleFR(Setting setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "CityCouncil" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Général" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kGeneralGroup), "Général" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShowDebugTab)), "Afficher l'onglet debug" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ShowDebugTab)), "Ajoute un onglet [DEBUG] au panneau Conseil Municipal, regroupant tous les boutons de debug/test." },
            };
        }
        public void Unload() { }
    }
}