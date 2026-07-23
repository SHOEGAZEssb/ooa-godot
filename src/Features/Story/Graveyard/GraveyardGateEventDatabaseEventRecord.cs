using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace oracleofages;
internal readonly record struct GraveyardGateEventDatabaseEventRecord(int Group, int Room, int InteractionId, int SubId, byte RoomFlag, byte ClearTile, int ShakeFrames, IReadOnlyList<int> Phase1Ordinary, IReadOnlyList<InterleavedTile> Phase1Interleaved, IReadOnlyList<Vector2> Phase1Puffs, IReadOnlyList<int> Phase2Ordinary, IReadOnlyList<Vector2> Phase2Puffs, string Source);
