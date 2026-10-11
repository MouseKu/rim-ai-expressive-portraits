using Verse;

namespace RimAIPortrait
{
    public sealed class ProviderStatePrompts : IExposable
    {
        public string Normal;
        public string High;
        public string Low;
        public string Sleep;
        public string Pain;
        public string Down;
        public string Hot;
        public string Cold;
        public string Angry;
        public string Distressed;
        public string Love;

        public string Get(PortraitState state)
        {
            switch (state)
            {
                case PortraitState.Normal: return Normal;
                case PortraitState.High: return High;
                case PortraitState.Low: return Low;
                case PortraitState.Sleep: return Sleep;
                case PortraitState.Pain: return Pain;
                case PortraitState.Down: return Down;
                case PortraitState.Hot: return Hot;
                case PortraitState.Cold: return Cold;
                case PortraitState.Angry: return Angry;
                case PortraitState.Love: return Love;
                default: return Distressed;
            }
        }

        public void Set(PortraitState state, string value)
        {
            switch (state)
            {
                case PortraitState.Normal: Normal = value; break;
                case PortraitState.High: High = value; break;
                case PortraitState.Low: Low = value; break;
                case PortraitState.Sleep: Sleep = value; break;
                case PortraitState.Pain: Pain = value; break;
                case PortraitState.Down: Down = value; break;
                case PortraitState.Hot: Hot = value; break;
                case PortraitState.Cold: Cold = value; break;
                case PortraitState.Angry: Angry = value; break;
                case PortraitState.Love: Love = value; break;
                default: Distressed = value; break;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Normal, "normal");
            Scribe_Values.Look(ref High, "high");
            Scribe_Values.Look(ref Low, "low");
            Scribe_Values.Look(ref Sleep, "sleep");
            Scribe_Values.Look(ref Pain, "pain");
            Scribe_Values.Look(ref Down, "down");
            Scribe_Values.Look(ref Hot, "hot");
            Scribe_Values.Look(ref Cold, "cold");
            Scribe_Values.Look(ref Angry, "angry");
            Scribe_Values.Look(ref Distressed, "distressed");
            Scribe_Values.Look(ref Love, "love");
        }
    }
}
