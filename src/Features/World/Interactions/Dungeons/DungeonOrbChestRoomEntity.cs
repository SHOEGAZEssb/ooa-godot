using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class DungeonOrbChestRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly OracleRoomData _room;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _sound;
    private readonly Action _changed;
    private readonly Func<long> _tick;
    private readonly Func<bool> _textActive;
    private readonly int _mask, _wait, _tile;
    private bool _initialized;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal int Counter { get; private set; } = -1;
    internal DungeonOrbChestRoomEntity(DungeonObjectRecord record, OracleRoomData room,
        OracleRuntimeState runtime, DungeonInteractionDatabase data, Action<int> sound, Action changed, Func<long> tick, Func<bool> textActive)
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/orb_chest_scripts.tsv",
            new GeneratedTableSchema("orb chest scripts", GeneratedTableKeySemantics.Unique,
                ["subid", "mask", "wait", "source"], ["subid"], headerRequired: true));
        if (table.Rows.Count != 2 || record.SubId is not (2 or 3)) throw new InvalidOperationException($"Unknown orb script at {record.Source}.");
        var row = table.Rows[record.SubId - 2];
        if (row.HexByte(0) != record.SubId) throw row.Invalid(0, "ordered dungeon04 script subids02/03");
        _mask = row.HexByte(1); _wait = row.UnsignedDecimal(2); _tile = data.Constant("chest");
        _room = room; _runtime = runtime; _sound = sound; _changed = changed; _tick = tick; _textActive = textActive; Position = record.Position;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (!_initialized)
        {
            _initialized = true;
            _runtime.SetWramByte(0xcfc1, 0); _runtime.SetWramByte(0xcfc2, 0);
        }
        if (frame.Player.IsDying || _textActive()) return;
        if (Counter < 0)
        {
            if ((_runtime.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) & _mask) == 0) return;
            _sound(OracleSoundEngine.SndSolvePuzzle); spawns.Add(new PuzzlePuffSpawn(Position, OracleSoundEngine.SndPoof));
            Counter = _wait; return;
        }
        if (--Counter != 0) return;
        _room.SetPositionTileAndCollision(Position, (byte)_tile, null, _tick()); _changed(); Finished = true;
    }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
