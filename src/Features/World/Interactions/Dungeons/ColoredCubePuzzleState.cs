using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface IColoredCubePuzzleStateSource
{
    ColoredCubePuzzleState ColoredCubePuzzleState { get; }
}

internal sealed class ColoredCubePuzzleState
{
    private readonly int _redPushableBlock;

    internal ColoredCubePuzzleState(int redPushableBlock)
    {
        _redPushableBlock = redPushableBlock;
    }

    internal int CubePosition { get; set; }
    internal int CubeColor { get; set; }

    internal bool PermitsPushBlock(byte tile)
    {
        if (CubePosition == 0)
            return true;
        return (CubeColor & 0x80) != 0 &&
            tile - _redPushableBlock == (CubeColor & 0x7f);
    }
}
