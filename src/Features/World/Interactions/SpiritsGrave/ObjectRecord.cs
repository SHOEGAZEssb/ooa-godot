using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct ObjectRecord(int Group, int Room, int Order, ObjectKind Kind, int Id, int SubId, int Y, int X, SpiritsGraveDatabaseCondition Predicate, string Source)
{
    internal Vector2 Position => new(X, Y);
}
