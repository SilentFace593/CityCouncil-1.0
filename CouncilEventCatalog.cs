using System.Collections.Generic;

namespace CityCouncil
{
    public enum EventCategory
    {
        FaitDivers,
        Scandale,
        Evenement,
    }

    public enum EventAgeScope
    {
        All,
        AdultsOnly,
        SeniorsOnly,
    }

    public enum EventEffectTarget
    {
        SpecificParty,
        LeadingPartyCityWide,
        Abstention,
    }

    public struct EventEffect
    {
        public EventEffectTarget Target;
        public PoliticalParty Party;
        public float Percent;
        public EventAgeScope AgeScope;

        public static EventEffect PartyBonus(PoliticalParty party, float percent, EventAgeScope scope = EventAgeScope.All)
            => new EventEffect { Target = EventEffectTarget.SpecificParty, Party = party, Percent = percent, AgeScope = scope };

        public static EventEffect LeadingPartyBonus(float percent, EventAgeScope scope = EventAgeScope.All)
            => new EventEffect { Target = EventEffectTarget.LeadingPartyCityWide, Percent = percent, AgeScope = scope };

        public static EventEffect AbstentionDelta(float percent, EventAgeScope scope = EventAgeScope.All)
            => new EventEffect { Target = EventEffectTarget.Abstention, Percent = percent, AgeScope = scope };
    }

    public class CouncilEventDefinition
    {
        public string Id;
        public string Headline; // Contient la clé de localisation (LocaleKeys)
        public EventCategory Category;
        public EventEffect[] Effects;
    }

    public static class CouncilEventCatalog
    {
        public static readonly List<CouncilEventDefinition> Events = new()
        {
            new CouncilEventDefinition
            {
                Id = "scandale_politique_desabuse",
                Headline = LocaleKeys.Event_ScandalePolitique,
                Category = EventCategory.Scandale,
                Effects = new[]
                {
                    EventEffect.AbstentionDelta(0.10f, EventAgeScope.All),
                },
            },

            new CouncilEventDefinition
            {
                Id = "incendies_forets",
                Headline = LocaleKeys.Event_IncendiesForets,
                Category = EventCategory.FaitDivers,
                Effects = new[]
                {
                    EventEffect.PartyBonus(PoliticalParty.Ecologiste, 0.10f, EventAgeScope.All),
                },
            },

            new CouncilEventDefinition
            {
                Id = "reforme_pensions_retraite",
                Headline = LocaleKeys.Event_ReformePensions,
                Category = EventCategory.Evenement,
                Effects = new[]
                {
                    EventEffect.LeadingPartyBonus(-0.50f, EventAgeScope.SeniorsOnly),
                },
            },
        };
    }
}