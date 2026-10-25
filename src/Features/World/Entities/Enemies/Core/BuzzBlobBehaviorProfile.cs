namespace Oracles;

internal readonly record struct BuzzBlobBehaviorProfile(
    int Speed, int AngleMask, int DurationMask, int DurationBase,
    int ShockFrames, int TextBase, int TextMask);
