using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct CrowRecord(int Group, int Room, int Id, int SubId, int Flags, int Count, bool FixedPosition, int Y, int X, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, int SpeedRaw, string PerchedRightAnimation, string PerchedLeftAnimation, string FlightRightAnimation, string FlightLeftAnimation);
