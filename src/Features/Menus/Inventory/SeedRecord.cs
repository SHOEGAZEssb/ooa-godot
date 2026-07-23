using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct SeedRecord(int ParentItem, int SeedItem, int TreasureId, string Sprite, int TileBase, int Palette, int Collision, int CollisionRadiusY, int CollisionRadiusX, int Damage, int InitialZ, int SpeedZ, int Gravity, int SpeedRaw, Vector2I UpOffset, Vector2I RightOffset, Vector2I DownOffset, Vector2I LeftOffset, int LinkFrames, string FlameSprite, int FlameTileBase, int FlameOamFlags, int FlameCounter, int LandingSound, int FlameSound, string Animation, string Source)
{
    public int FlamePalette => FlameOamFlags & 0x07;

    public Vector2I Offset(Vector2I direction) => direction == Vector2I.Up ? UpOffset : direction == Vector2I.Right ? RightOffset : direction == Vector2I.Down ? DownOffset : direction == Vector2I.Left ? LeftOffset : throw new ArgumentOutOfRangeException(nameof(direction));
}
