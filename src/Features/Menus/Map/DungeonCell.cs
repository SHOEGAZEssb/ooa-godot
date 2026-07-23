using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct DungeonCell(int Floor, int X, int Y, int Room, byte Properties);
