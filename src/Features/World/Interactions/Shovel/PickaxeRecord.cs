using System;

namespace oracleofages;
internal readonly record struct PickaxeRecord(string SpriteName, int WorkerTileBase, int WorkerPalette, string WorkAnimation, string TalkAnimation, string DebrisSpriteName, int DebrisTileBase, string DebrisAnimation, int TextId, string Message, int Sound, int DebrisCount, int OffsetY, int OffsetX, int Speed, int InitialSpeedZ, int Gravity, int Angle0, int Angle1);
