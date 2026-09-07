using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MISCELLANEOUS_1 $6b:$0f and its native simple script.</summary>
internal sealed partial class NuunBridgeRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IPlayerRestriction, IRoomEntityUpdateFreeze
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData _save;
    private readonly OracleRuntimeState _runtime;
    private readonly IReadOnlyList<NuunBridgeCommand> _commands;
    private readonly Func<long> _tick;
    private readonly Action _tileChanged;
    private readonly Action<int> _sound;
    private int _state;
    private int _counter;
    private int _pc;
    public bool Finished { get; private set; }
    public bool DisablesSword => _state == 2 && !Finished;
    public bool DisablesMovement => DisablesSword;
    public bool DisablesItems => DisablesSword;
    public bool DisablesMenus => DisablesSword;
    public bool DisablesScreenTransitions => DisablesSword;
    public bool FreezesRoomEntities => DisablesSword;

    internal NuunBridgeRoomEntity(DungeonMechanicDatabaseRecord record, OracleRoomData room,
        OracleSaveData save, OracleRuntimeState runtime, Func<long> tick,
        Action tileChanged, Action<int> sound) : base(record, "NuunBridgeController")
    {
        _record = record;
        _room = room;
        _save = save;
        _runtime = runtime;
        _tick = tick;
        _tileChanged = tileChanged;
        _sound = sound;
        _commands = new NuunBridgeDatabase().Commands;
        // tileReplacement_group0Map54 clears this before object initialization.
        runtime.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (_state == 0)
        {
            _state = 1;
            if (_save.HasRoomFlag(_record.Group, _record.Room, 0x40))
                Finished = true;
            else
                spawns.Add(new DungeonSwitchSpawn(_record with { Id = 0x05, SubId = _record.Parameter }));
            return;
        }
        if (_state == 1)
        {
            if (_runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0)
            {
                _save.SetRoomFlag(_record.Group, _record.Room, 0x40);
                _state = 2;
            }
            return;
        }
        // The native counter branch returns even on the update reaching zero.
        if (_counter != 0)
        {
            _counter--;
            return;
        }
        while (_pc < _commands.Count)
        {
            NuunBridgeCommand command = _commands[_pc++];
            Vector2 position = new((command.A & 15) * 16 + 8, (command.A >> 4) * 16 + 8);
            switch (command.Opcode)
            {
                case 0:
                    Finished = true;
                    return;
                case 1:
                    _counter = command.A;
                    return;
                case 2:
                    // playSound clears carry, so sound commands yield too.
                    _sound(command.A);
                    return;
                case 3:
                    _room.SetPositionTileAndCollision(position, (byte)command.B, null, _tick());
                    _tileChanged();
                    break;
                case 4:
                    _room.SetInterleavedMetatile(position, (byte)command.B,
                        (byte)command.C, command.D, _tick());
                    _tileChanged();
                    break;
                default:
                    throw new InvalidOperationException($"Nuun simple script command {_pc - 1}: unsupported opcode ${command.Opcode:x2}.");
            }
        }
        throw new InvalidOperationException("Nuun simple script ran past ss_end.");
    }
}
