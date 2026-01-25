using Vintagestory.API.Common;

namespace VsQuest
{
    public class ActionItemSoundConfig
    {
        public AssetLocation CastLoopSound { get; } = new AssetLocation("albase:sounds/dark-magic-charge-up");
        public AssetLocation CastCompleteSound { get; } = new AssetLocation("albase:sounds/atmospheric-metallic-swipe");
        public float CastSoundVolume { get; } = 0.35f;
        public float CastSoundRange { get; } = 12f;
        public float CastCompleteSoundRange { get; } = 16f;
        public float CastCompleteSoundVolume { get; } = 0.75f;
    }
}
