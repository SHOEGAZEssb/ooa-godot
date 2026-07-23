using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct GroundTreasureDatabaseRecord(int Group, int Room, int Order, int Y, int X, string TreasureObject, string Sprite, int TileBase, int Palette, string Animation, int CompletionTextId, string CompletionMessage, string Source, int SpawnMode = 0, int GrabMode = 2, int SpawnDelayFrames = 0, int InitialZPixels = 0, int BounceCount = 0, int Gravity = 0, int BounceSpeed = 0, int SpawnSound = 0, int LandingSound = 0, bool InitialZAboveScreen = false, int AboveScreenMargin = 8, int AboveScreenFallback = -128);
