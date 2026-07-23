using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct EnemyRecord(int Id, int SubId, string[] Sprites, int TileBase, int Palette, bool SourceGrayscaleInverted, int RadiusY, int RadiusX, int DamageQuarters, int Health, string[] Animations);
