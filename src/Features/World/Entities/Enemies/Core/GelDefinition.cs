using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct GelDefinition(int Id, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, string NormalAnimation, string AttachedAnimation, string ShakeAnimation);
