using Godot;

namespace oracleofages;
internal readonly record struct SplashPart(int Y, int X, int PaletteXor = 0, bool FlipX = false);
