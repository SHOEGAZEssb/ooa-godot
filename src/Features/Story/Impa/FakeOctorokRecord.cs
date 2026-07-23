using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct FakeOctorokRecord(int Index, int Id, int SubId, int Y, int X, int Var03, string SpriteName, int TileBase, int Palette, string InitialAnimation, string FleeAnimation, int SignalWaitFrames, int FleeCounter, int Angle, int Speed)
{
    public NpcRecord ToNpcRecord(int group, int room) => new(group, room, Id, SubId, Y, X, Var03, 0, SpriteName, TileBase, Palette, 0, false, InitialAnimation, InitialAnimation, InitialAnimation, InitialAnimation, string.Empty);
}
