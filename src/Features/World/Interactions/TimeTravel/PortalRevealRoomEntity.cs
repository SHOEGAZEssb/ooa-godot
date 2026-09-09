using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MISCELLANEOUS_2 $dc:$03/$04, before the placed $e1 portals.</summary>
internal sealed class PortalRevealRoomEntity
    : RoomEntityAdapter<Node2D>, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly PortalRevealRecord _record;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData _save;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private bool _initialized;

    public bool Finished { get; private set; }

    internal PortalRevealRoomEntity(
        PortalRevealRecord record, OracleRoomData room, OracleSaveData save,
        Action<int> playSound, Action roomTileChanged, Func<long> animationTick)
        : base(new Node2D
        {
            Name = $"PortalReveal_dc_{record.SubId:x2}",
            Position = new Vector2(record.X, record.Y)
        }, static _ => { })
    {
        _record = record;
        _room = room;
        _save = save;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        if (record.X >= room.WidthInTiles * 16 || record.Y >= room.HeightInTiles * 16)
            throw new InvalidOperationException($"{record.Source}: invalid position in room {record.Group:x1}:{record.Room:x2}.");
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            Finished = _save.HasRoomFlag(_record.Group, _record.Room, _record.RoomFlag);
            _initialized = true;
            return;
        }

        // interactiondc_subid3And4_state1 accepts only standard ground $3a,
        // not any change to the bush. setTile precedes flag/sound/deletion.
        if (!_room.ReplaceMetatile(Entity.Position, 0x3a, 0xd7, _animationTick()))
            return;
        _roomTileChanged();
        _save.SetRoomFlag(_record.Group, _record.Room, _record.RoomFlag);
        _playSound(OracleSoundEngine.SndSolvePuzzle);
        Finished = true;
    }
}

internal readonly record struct PortalRevealRecord(
    int Group, int Room, int Order, int SubId, int Y, int X, byte RoomFlag, string Source);
