using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// The room-local wNumTorchesLit count and palette-thread state shared by one
/// PART_DARK_ROOM_HANDLER and its generated PART_LIGHTABLE_TORCH children.
/// </summary>
internal sealed class DarkRoomState
{
    private readonly OracleRoomData _room;
    private readonly DarkRoomDatabase _data;
    private int _fadeOffset;
    private int _fadeDirection;
    private bool _torchTotalInitialized;

    internal int LitCount { get; private set; }
    internal int TotalTorches { get; private set; }
    internal int Parameter { get; private set; }
    internal int RenderedOffset => _fadeOffset;
    internal bool FadeActive { get; private set; }

    internal DarkRoomState(OracleRoomData room, DarkRoomDatabase data)
    {
        _room = room;
        _data = data;
        Parameter = data.FullDarkParameter;
        _fadeOffset = SignedParameter(Parameter);
        _room.SetTemporaryBackgroundPaletteOffset(_fadeOffset);
    }

    internal void SetTotalTorches(int count)
    {
        if (count < 0 || _torchTotalInitialized)
            throw new InvalidOperationException("The dark-room torch total can only be initialized once.");
        TotalTorches = count;
        _torchTotalInitialized = true;
    }

    internal void IncrementLitCount()
    {
        if (LitCount >= TotalTorches)
            throw new InvalidOperationException("The dark-room lit count exceeded its torch total.");
        LitCount++;
    }

    internal void BeginBrighten(int targetParameter) => BeginFade(targetParameter, 1);
    internal void BeginDarken(int targetParameter) => BeginFade(targetParameter, -1);

    internal void AdvanceFade()
    {
        if (!FadeActive)
            return;
        int target = SignedParameter(Parameter);
        int candidate = _fadeOffset + _fadeDirection * _data.FadeSpeed;
        bool finished = _fadeDirection > 0
            ? candidate >= target
            : candidate < target;
        if (finished)
        {
            FadeActive = false;
            return;
        }
        _fadeOffset = candidate;
        _room.SetTemporaryBackgroundPaletteOffset(_fadeOffset);
    }

    private void BeginFade(int targetParameter, int direction)
    {
        if (targetParameter is < 0 or > 0xff || direction is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(targetParameter));
        // _setDarkeningVariables starts from the previous parameter, not the
        // last rendered offset, then immediately stores the new target.
        _fadeOffset = SignedParameter(Parameter);
        Parameter = targetParameter;
        _fadeDirection = direction;
        FadeActive = true;
    }

    private static int SignedParameter(int parameter) => unchecked((sbyte)(byte)parameter);
}
