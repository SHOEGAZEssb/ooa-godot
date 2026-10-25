using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

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
        get => _runtimeState.ReadWramByte(AgesWramAddress.wRotatingCubePos);
        set => _runtimeState.SetWramByte(AgesWramAddress.wRotatingCubePos, unchecked((byte)value));
    }
    internal int CubeColor
    {
        get => _runtimeState.ReadWramByte(AgesWramAddress.wRotatingCubeColor);
        set => _runtimeState.SetWramByte(AgesWramAddress.wRotatingCubeColor, unchecked((byte)value));
    }

    internal bool PermitsPushBlock(byte tile)
    {
        if (CubePosition == 0)
            return true;
        return (CubeColor & 0x80) != 0 &&
            tile - _redPushableBlock == (CubeColor & 0x7f);
    }
}
