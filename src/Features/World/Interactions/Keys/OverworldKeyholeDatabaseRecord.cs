using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct OverworldKeyholeDatabaseRecord(int Group, int Room, int Treasure, int SubId, string Sprite, int TileBase, int Palette, string Animation, string Source);
