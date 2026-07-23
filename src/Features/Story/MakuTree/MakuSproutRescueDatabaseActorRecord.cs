using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct MakuSproutRescueDatabaseActorRecord(string Actor, int Id, int SubId, int Y, int X, string SpriteName, int TileBase, int Palette, string UpAnimation, string RightAnimation, string DownAnimation, string LeftAnimation)
{
    public NpcRecord ToNpcRecord(int group, int room) => new(group, room, Id, SubId, Y, X, 0, 0, SpriteName, TileBase, Palette, 0, false, UpAnimation, RightAnimation, DownAnimation, LeftAnimation, string.Empty);
}
