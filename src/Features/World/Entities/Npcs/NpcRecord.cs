using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct NpcRecord(int Group, int Room, int Id, int SubId, int Y, int X, int Var03, int TextId, string SpriteName, int TileBase, int Palette, int DefaultAnimation, bool CanFace, string UpAnimation, string RightAnimation, string DownAnimation, string LeftAnimation, string Message);
