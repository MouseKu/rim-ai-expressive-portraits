using Verse;

namespace RimAIPortrait
{
    public sealed class XenotypePromptEntry : IExposable
    {
        public string DefName;
        public string Value;
        public string DefaultValue;
        public bool Enabled;

        public void ExposeData()
        {
            Scribe_Values.Look(ref DefName, "defName");
            Scribe_Values.Look(ref Value, "value");
            Scribe_Values.Look(ref DefaultValue, "defaultValue");
            Scribe_Values.Look(ref Enabled, "enabled", false);
        }
    }
}
