using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// object_code/ages/interactions/miscPuzzles.s, $90:$14-$18/$20.
internal sealed partial class HerosCavePuzzleRoomEntity : Node2D, IRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly DungeonObjectRecord _record;
    private readonly HerosCaveDatabase _data;
    private readonly RoomEntityManager _owner;
    private readonly OracleSaveData _save;
    private readonly OracleRuntimeState _runtime;
    private readonly ColoredCubePuzzleState? _cube;
    private readonly Action<byte, byte> _tile;
    private readonly GroundTreasureGrantRequest _treasure;
    internal int State { get; private set; }
    public bool Finished { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;

    internal HerosCavePuzzleRoomEntity(DungeonObjectRecord record, HerosCaveDatabase data,
        RoomEntityManager owner, OracleSaveData save, OracleRuntimeState runtime,
        ColoredCubePuzzleState? cube, Action<byte, byte> tile, GroundTreasureGrantRequest treasure)
    {
        _record = record; _data = data; _owner = owner; _save = save;
        _runtime = runtime; _cube = cube; _tile = tile; _treasure = treasure;
        Position = record.Position; Visible = false; Name = $"HerosCavePuzzle_{record.SubId:x2}";
    }

    private bool ItemFlag => _owner.ActiveRoomHasFlag(_save, OracleSaveData.RoomFlagItem);
    private void Advance()
    {
        if (Finished) return;
        var room = _owner.ActiveRoom;
        if (_record.SubId == 0x17)
        {
            if (_save.HasRoomFlag(room.Group, 0xc9, OracleSaveData.RoomFlagItem))
                _save.SetRoomFlag(room.Group, room.Id, OracleSaveData.RoomFlagItem);
            Finished = true;
            return;
        }
        if (_record.SubId == 0x20)
        {
            if (!ItemFlag) _owner.TryCreateGroundTreasure(_treasure);
            // This producer deletes even when createTreasure has no free slot.
            Finished = true;
            return;
        }
        if (State == 0)
        {
            Finished = _record.SubId is 0x14 or 0x15
                ? _owner.ActiveRoomHasFlag(_save, OracleSaveData.RoomFlag80) : ItemFlag;
            if (!Finished) State = 1;
            return;
        }
        switch (_record.SubId)
        {
            case 0x14:
                byte position = (byte)room.GetPackedPosition(Position);
                int color = room.GetPackedStorageMetatile(position) - _data.Constant("cube-first-floor");
                if (color is < 0 or >= 3 || _cube!.CubePosition != position || (_cube.CubeColor & 3) != color) return;
                _owner.SetTrigger(color, true);
                _tile(position, (byte)_data.Constant("cube-filled-floor"));
                room.SetPackedTileCollision(position, 0x0f);
                _owner.OnSoundRequested(SoundId.SndClink);
                return;
            case 0x15:
                if (_owner.ActiveTriggers != 7) return;
                _owner.OnSoundRequested(SoundId.SndSolvePuzzle);
                _tile(0x15, (byte)_data.Constant("cube-stairs"));
                _save.SetRoomFlag(room.Group, room.Id, OracleSaveData.RoomFlag80);
                Finished = true;
                return;
            case 0x16:
                if (State == 1 && !ItemFlag) return;
                State = 2;
                _runtime.SetWramByte(WramAddress.wDisabledObjects, 0x81);
                _runtime.SetWramByte(WramAddress.wDisableLinkCollisionsAndMenu, 0x81);
                if (_owner.IsTextActive()) return;
                _owner.OnRoomWarpRequested(_data.ExitWarp with { SourceGroup = room.Group, SourceRoom = room.Id });
                Finished = true;
                return;
            case 0x18:
                int statue = _data.Constant("pushable-statue");
                if (room.GetPackedStorageMetatile(0x95) != statue || room.GetPackedStorageMetatile(0x5d) != statue) return;
                if (_owner.TryCreateGroundTreasure(_treasure)) Finished = true;
                return;
            default:
                throw new InvalidOperationException($"{_record.Source}: unsupported INTERAC $90:${_record.SubId:x2}.");
        }
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { if (State == 0) Advance(); return ScreenTransitionPresentation.Hidden; }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) { if (State == 0) Advance(); }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
