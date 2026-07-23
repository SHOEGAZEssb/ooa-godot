using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct EffectRecord(string Name, string SpriteName, int TileBase, int Palette, int Duration, float Speed, int Angle, bool Sway, int VelocityXFixed, int VelocityYFixed, string Animation)
{
    public NpcRecord ToNpcRecord(int group, int room, int y, int x) => new(group, room, 0, 0, y, x, 0, 0, SpriteName, TileBase, Palette, 0, false, Animation, Animation, Animation, Animation, string.Empty);
}
