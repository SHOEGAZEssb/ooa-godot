using System;

namespace oracleofages;

/// <summary>
/// Advances the source Wild Tokay var38/var3b pattern state once per
/// 60-update controller boundary.
/// </summary>
internal sealed class WildTokaySpawnSchedule
{
    private const int PatternSlots = 4;

    private readonly WildTokayGameDatabase _database;
    private readonly Func<int> _nextRandomValue;
    private int _level;
    private int _cyclesRemaining;
    private int _slot;
    private int _randomIndex;

    internal int CyclesRemaining => _cyclesRemaining;
    internal int Slot => _slot;
    internal int RandomIndex => _randomIndex;

    internal WildTokaySpawnSchedule(
        WildTokayGameDatabase database,
        Func<int> nextRandomValue)
    {
        _database = database;
        _nextRandomValue = nextRandomValue;
    }

    internal void Begin(int level)
    {
        _level = level;
        _cyclesRemaining = _database.WildCycleCount(level);
        _slot = 0;
        SelectPattern();
    }

    internal WildTokaySpawnInstruction Advance()
    {
        if (_cyclesRemaining == 0)
            return default;

        // var38 values 0-3 are spawn slots. Value 4 consumes a full
        // 60-update interval to reset the slot, decrement var3b, and select
        // another pattern even when the final cycle has just completed.
        if (_slot == PatternSlots)
        {
            _slot = 0;
            _cyclesRemaining--;
            SelectPattern();
            return new WildTokaySpawnInstruction(0, false, true);
        }

        WildTokayPatternRecord pattern =
            _database.Pattern(_level, _randomIndex);
        int code = CodeAt(pattern, _slot);
        bool final = _cyclesRemaining == 1 &&
            IsLastOccupiedSlot(pattern, _slot);
        _slot++;
        return new WildTokaySpawnInstruction(code, final, false);
    }

    internal void Clear()
    {
        _cyclesRemaining = 0;
        _slot = 0;
        _randomIndex = 0;
    }

    private void SelectPattern() =>
        _randomIndex = _nextRandomValue() & 0x0f;

    private static int CodeAt(WildTokayPatternRecord pattern, int slot) =>
        slot switch
        {
            0 => pattern.LeftBlue,
            1 => pattern.LeftRed,
            2 => pattern.RightBlue,
            3 => pattern.RightRed,
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };

    private static bool IsLastOccupiedSlot(
        WildTokayPatternRecord pattern,
        int slot)
    {
        for (int next = slot + 1; next < PatternSlots; next++)
        {
            if (CodeAt(pattern, next) != 0)
                return false;
        }
        return CodeAt(pattern, slot) != 0;
    }
}

internal readonly record struct WildTokaySpawnInstruction(
    int Code,
    bool Final,
    bool Reset);
