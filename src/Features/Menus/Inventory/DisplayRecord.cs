using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct DisplayRecord(int TreasureId, int LeftSprite, int LeftPalette, int RightSprite, int RightPalette, int ExtraMode, int TextLow)
{
    public static readonly DisplayRecord Empty = new(0, 0, 0, 0, 0, 0xff, 0x00);
    public bool HasIcon => LeftSprite != 0 || RightSprite != 0;
}
