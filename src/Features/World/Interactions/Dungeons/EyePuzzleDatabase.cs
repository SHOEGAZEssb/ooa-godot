using Godot;
using System;
using System.Linq;

namespace oracleofages;

// bank1.s:screenTransitionEyePuzzle and statueEyeball.s:$03/$04 share
// these live bytes. Preview queries never advance the transition count.
internal sealed class EyePuzzleDatabase
{
    internal static EyePuzzleDatabase Shared { get; } = new();
    private readonly int _group, _room, _counter, _secondCounter, _direction;
    private readonly int _required, _excluded;
    internal int SpinUpdates { get; }
    private EyePuzzleDatabase()
    {
        var row = GeneratedTable.Load("res://assets/oracle/objects/eye_puzzle.tsv",
            new GeneratedTableSchema("eye puzzle", GeneratedTableKeySemantics.Unique,
                ["group", "room", "counter-address", "second-counter-address", "correct-direction-address",
                    "required-transitions", "spin-updates", "excluded-direction", "source"],
                ["group", "room"], headerRequired: true)).Rows.Single();
        _group = row.UnsignedDecimal(0); _room = row.HexByte(1);
        _counter = row.HexWord(2); _secondCounter = row.HexWord(3); _direction = row.HexWord(4);
        _required = row.UnsignedDecimal(5); SpinUpdates = row.UnsignedDecimal(6);
        _excluded = row.UnsignedDecimal(7); _ = row.RequiredString(8);
    }
    internal bool PrepareEyes(RoomEntityManager owner)
    {
        var state = owner.RuntimeState;
        if ((state.ReadWramByte(WramAddress.wScrollMode) & 1) == 0) return false;
        int direction = 0;
        if (state.ReadWramByte(_counter) < _required)
            do { direction = owner.NextRandomValue() & 3; } while (direction == _excluded);
        state.SetWramByte(_direction, (byte)direction);
        return true;
    }
    internal int CorrectDirection(OracleRuntimeState state) => state.ReadWramByte(_direction);
    private bool Matches(int group, int room) => group == _group && room == _room;
    private int NextCount(OracleRuntimeState state, int direction) => direction == CorrectDirection(state)
        ? (state.ReadWramByte(_counter) + 1) & 0xff : 0;
    private bool Loops(int direction, int count) => direction != 2 && (direction != 0 || count < _required);
    internal bool TryResolve(OracleRuntimeState state, int group, int room, Vector2I direction, out int target)
    {
        target = room;
        int native = Direction(direction);
        return Matches(group, room) && Loops(native, NextCount(state, native));
    }
    internal void CommitTransition(OracleRuntimeState state, int group, int room, Vector2I direction)
    {
        int native = Direction(direction);
        if (Matches(group, room))
        {
            int count = NextCount(state, native);
            if (Loops(native, count))
            {
                if (native != CorrectDirection(state)) state.SetWramByte(_secondCounter, 0);
                state.SetWramByte(_counter, (byte)count);
                return;
            }
        }
        state.SetWramByte(_counter, 0);
        state.SetWramByte(_secondCounter, 0);
    }
    private static int Direction(Vector2I direction) => direction == Vector2I.Up ? 0 :
        direction == Vector2I.Right ? 1 : direction == Vector2I.Down ? 2 : direction == Vector2I.Left ? 3 :
        throw new ArgumentOutOfRangeException(nameof(direction));
}
