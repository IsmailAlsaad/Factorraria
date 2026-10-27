using System;

namespace Factorraria.Common.Cosmic
{
    // Marks a public field of CosmicRadiationSystem as a balancing parameter. /fcosmic params lists every marked field with its
    // current value, unit and description, so there is always one place that says where each variable is and what it does.
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CosmicParamAttribute : Attribute
    {
        public string Description;
        public string Unit;

        public CosmicParamAttribute(string description, string unit = "")
        {
            Description = description;
            Unit = unit;
        }
    }
}