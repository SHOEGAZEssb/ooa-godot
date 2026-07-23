using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct OctorokProjectileRecord(string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int SpeedRaw, string NormalAnimation, string BounceAnimation);
