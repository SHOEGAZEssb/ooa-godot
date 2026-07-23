using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct SavedEventRecord(int Group, int Room, int InteractionId, int SubId, string Animation0, string Animation1, string Animation2, string Animation3, string Animation4, string ExtraSprite, int TextboxPosition, int Music, int AdviceFlag, int MapTextLow, string FallingTreasureObject, string RespawnTreasureObject, int DropY, int RespawnY, int DefaultX, int LowerBound, int MiddleBound, int UpperBound, int LowerBandX, int UpperBandX, int InitialZPixels, int DropDelayFrames, int BounceCount, int Gravity, int BounceSpeed, int SpawnSound, int LandingSound)
{
    public string Animation(int index) => index switch
    {
        0 => Animation0,
        1 => Animation1,
        2 => Animation2,
        3 => Animation3,
        4 => Animation4,
        _ => throw new ArgumentOutOfRangeException(nameof(index))};
}
