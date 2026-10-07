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
    private readonly OracleRuntimeState _runtimeState;

    internal ColoredCubePuzzleState(OracleRuntimeState runtimeState, int redPushableBlock)
    {
        _runtimeState = runtimeState;
        _redPushableBlock = redPushableBlock;
    }

    internal int CubePosition
    {
        get => _runtimeState.ReadWramByte(WramAddress.wRotatingCubePos);
        set => _runtimeState.SetWramByte(WramAddress.wRotatingCubePos, unchecked((byte)value));
    }
    internal int CubeColor
    {
        get => _runtimeState.ReadWramByte(WramAddress.wRotatingCubeColor);
        set => _runtimeState.SetWramByte(WramAddress.wRotatingCubeColor, unchecked((byte)value));
    }

    internal bool PermitsPushBlock(byte tile)
    {
        if (CubePosition == 0)
            return true;
        return (CubeColor & 0x80) != 0 &&
            tile - _redPushableBlock == (CubeColor & 0x7f);
    }
}
