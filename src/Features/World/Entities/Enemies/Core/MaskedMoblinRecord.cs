using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct MaskedMoblinRecord(int Id, int SubId, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, int SpeedRaw, int MoveCounterBase, int MoveCounterMask, int TurnWait, string UpAnimation, string RightAnimation, string DownAnimation, string LeftAnimation);
