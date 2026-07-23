using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct FrameRecord(int Ring, int SpecialObject, int Direction, int Frame, string Sprite, int TileBase, string Oam, int InitialDuration, int LoopDuration);
