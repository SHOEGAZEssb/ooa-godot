using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct EnemyArrowRecord(string SpriteName, int TileBase, int Palette, int DamageQuarters, int SpeedRaw, string UpAnimation, string RightAnimation, string DownAnimation, string LeftAnimation, string BounceAnimation);
