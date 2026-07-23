using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed class NayruPossessionState(Vector2 nayruStart, Vector2 ralphStart)
{
    public Vector2 NayruStart { get; } = nayruStart;
    public Vector2 RalphStart { get; } = ralphStart;
    public int Elapsed { get; set; }
    public int SwayX { get; set; }
    public int MinimumSwayX { get; set; }
    public int MaximumSwayX { get; set; }
    public int PaletteCounter { get; set; } = 15;
    public int NormalPaletteFrames { get; set; } = 15;
    public int PossessedPaletteFrames { get; set; } = 1;
    public bool PossessedPalette { get; set; }
    public bool PaletteComplete { get; set; }
    public int NayruMoveStart { get; set; } = -1;
    public int RalphMoveStart { get; set; } = -1;
}
