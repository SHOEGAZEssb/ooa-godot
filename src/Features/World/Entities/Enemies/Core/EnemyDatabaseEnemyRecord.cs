using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct EnemyDatabaseEnemyRecord(int Group, int Room, int Id, int SubId, int Flags, int Count, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, string IdleAnimation, string FlyAnimation);
