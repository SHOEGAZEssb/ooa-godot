using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct SwordBeamDatabaseRecord(int Direction, int OffsetY, int OffsetX, string Sprite, int TileBase, int Palette, int RadiusY, int RadiusX, int Damage, int SpeedRaw, int Sound, string Oam, string Source);
