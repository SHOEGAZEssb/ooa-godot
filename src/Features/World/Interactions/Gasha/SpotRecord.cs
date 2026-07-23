using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct SpotRecord(int Group, int Room, int SubId, int Y, int X, int Rank, string Ground, int ReplacementTile)
{
    internal Vector2 Position => new(X, Y);
    internal Vector2 TreeTopLeft => new(X, Y - 16);
    internal int PackedPosition => (Y & 0xf0) | (X >> 4);
}
