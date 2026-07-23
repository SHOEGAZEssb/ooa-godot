using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct GelRecord(int Group, int Room, int Flags, int Count, bool FixedPosition, int Y, int X);
