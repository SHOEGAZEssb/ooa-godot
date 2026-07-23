using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct OctorokRecord(int Group, int Room, int Id, int SubId, int Flags, int Count, bool FixedPosition, int Y, int X, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, int SpeedRaw, int CounterMask, string UpAnimation, string RightAnimation, string DownAnimation, string LeftAnimation);
