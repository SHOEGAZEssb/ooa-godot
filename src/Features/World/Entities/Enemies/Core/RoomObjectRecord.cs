using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct RoomObjectRecord(int Group, int Room, int Order, RoomObjectKind Kind, int Id, int SubId, int Flags, int Count, int Y, int X, int PackedPosition, int ConditionMask);
