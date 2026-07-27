using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Native state machine for interaction $dc:$02 and CUTSCENE_D2_COLLAPSE.
/// The controller arms on room entry, observes the shared Bracelet parent,
/// and restores roomGfxChanges.roomTileChangesAfterLoad00 on later loads.
/// </summary>
internal sealed class WingDungeonCollapseEvent : IRoomEntryEvent,
    IUpdatesDuringDialogueRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly WingDungeonCollapseDatabase _database = new();
    private readonly WingDungeonCollapseRecord _record;
    private readonly Action _startRemoteMakuWarning;
    private WingDungeonCollapseStage _stage;
    private int _counter;
    private int _phase;
    private int _dustCounter;
    private int _dustSpawnCounter;
    private int _effectSerial;
    private NpcCharacter? _exclamation;

    internal WingDungeonCollapseEvent(
        RoomEventContext context,
        Action startRemoteMakuWarning)
    {
        _context = context;
        _record = _database.Record;
        _startRemoteMakuWarning = startRemoteMakuWarning;
    }

    public bool HasState => _stage is not (
        WingDungeonCollapseStage.Inactive or
        WingDungeonCollapseStage.Completed);
    public bool BlocksGameplay => _stage is not (
        WingDungeonCollapseStage.Inactive or
        WingDungeonCollapseStage.AwaitingRockLift or
        WingDungeonCollapseStage.AwaitingPickup or
        WingDungeonCollapseStage.Completed);
    internal WingDungeonCollapseStage Stage => _stage;
    internal int Counter => _counter;
    internal int Phase => _phase;
    internal int DustCounter => _dustCounter;
    internal WingDungeonCollapseRecord Record => _record;
    internal IReadOnlyList<WingDungeonCollapseMapRecord> Maps => _database.Maps;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group &&
        room.Id == _record.Room &&
        !_context.Rooms.SaveData.HasRoomFlag(
            group, room.Id, _record.RoomFlag);

    public void Start(OracleRoomData room)
    {
        Cancel();
        if (room.Id != _record.Room ||
            _context.Rooms.ActiveGroup != _record.Group)
        {
            throw new InvalidOperationException(
                $"Wing Dungeon collapse cannot arm in " +
                $"{_context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        }
        _stage = WingDungeonCollapseStage.AwaitingRockLift;
    }

    internal void RestoreCollapsedEntrance(int group, OracleRoomData room)
    {
        if (group != _record.Group || room.Id != _record.Room ||
            !_context.Rooms.SaveData.HasRoomFlag(
                group, room.Id, _record.RoomFlag))
        {
            return;
        }
        ApplyFinalRoomState(room);
    }

    internal void OnTileLifted(BraceletTileLifted lifted)
    {
        if (_stage != WingDungeonCollapseStage.AwaitingRockLift ||
            !MatchesLift(lifted))
        {
            return;
        }
        if (lifted.ReplacementTile != _record.GroundTile ||
            _context.Rooms.CurrentRoom.GetMetatile(lifted.Position) !=
                _record.GroundTile)
        {
            throw new InvalidOperationException(
                "Room 0:83 interaction $dc:$02 did not observe rock $c3 " +
                "being replaced by standard ground $3a.");
        }

        _context.Rooms.CurrentRoom.SetPositionTileAndCollision(
            lifted.Position,
            (byte)_record.DugTile,
            null,
            _context.AnimationTick());
        _context.RoomView.QueueRedraw();
        _stage = WingDungeonCollapseStage.AwaitingPickup;
    }

    internal void OnTileLiftCompleted(BraceletTileLifted lifted)
    {
        if (_stage != WingDungeonCollapseStage.AwaitingPickup ||
            !MatchesLift(lifted))
        {
            return;
        }

        _context.Player.Face(Vector2I.Right);
        _context.Player.BeginCutsceneControl(interruptBracelet: false);
        _context.Player.ResetEnemyInvincibility();
        _context.Sound.PlaySound(OracleSoundEngine.SndCtrlStopMusic);
        _counter = _record.PickupWait;
        _stage = WingDungeonCollapseStage.PickupWait;
    }

    public void UpdateFrame()
    {
        if (_stage is >= WingDungeonCollapseStage.PreCollapseShake and
            < WingDungeonCollapseStage.Completed)
        {
            _context.AdvanceBracelet();
        }
        switch (_stage)
        {
            case WingDungeonCollapseStage.AwaitingRockLift:
            case WingDungeonCollapseStage.AwaitingPickup:
            case WingDungeonCollapseStage.Inactive:
            case WingDungeonCollapseStage.Completed:
                return;

            case WingDungeonCollapseStage.PickupWait:
                if (--_counter == 0)
                    BeginPreCollapseShake();
                return;

            case WingDungeonCollapseStage.PreCollapseShake:
                _context.Entities.BeginScreenShake(_record.PreCollapseShake);
                UpdateExclamation();
                if (--_counter == 0)
                    _stage = WingDungeonCollapseStage.CollapseStart;
                return;

            case WingDungeonCollapseStage.CollapseStart:
                BeginCollapse();
                return;

            case WingDungeonCollapseStage.InitialWait:
                if (--_counter == 0)
                {
                    BeginDust();
                    _stage = WingDungeonCollapseStage.FirstPhase;
                }
                return;

            case WingDungeonCollapseStage.FirstPhase:
                ApplyPhase(0);
                _context.Sound.PlaySound(OracleSoundEngine.SndDoorClose);
                _phase = 1;
                _counter = _record.PhaseWait;
                _stage = WingDungeonCollapseStage.PhaseWait;
                UpdateDust();
                return;

            case WingDungeonCollapseStage.PhaseWait:
                _context.Entities.BeginScreenShake(_record.CollapseShake);
                UpdateDust();
                if (--_counter != 0)
                    return;
                ApplyPhase(_phase);
                _context.Sound.PlaySound(OracleSoundEngine.SndDoorClose);
                if (_phase == _database.Maps.Count - 1)
                {
                    _counter = _record.FinalWait;
                    _stage = WingDungeonCollapseStage.FinalWait;
                    return;
                }
                _phase++;
                _counter = _record.PhaseWait;
                return;

            case WingDungeonCollapseStage.FinalWait:
                UpdateDust();
                if (--_counter == 0)
                    _stage = WingDungeonCollapseStage.Finish;
                return;

            case WingDungeonCollapseStage.Finish:
                Finish();
                return;

            default:
                throw new InvalidOperationException(
                    $"Unsupported Wing Dungeon collapse stage {_stage}.");
        }
    }

    public void UpdateDuringDialogueFrame() => UpdateFrame();

    public void Cancel()
    {
        if (BlocksGameplay)
            _context.Player.EndCutsceneControl();
        if (_exclamation is not null)
            _exclamation.SetActive(false);
        _exclamation = null;
        _stage = WingDungeonCollapseStage.Inactive;
        _counter = 0;
        _phase = 0;
        _dustCounter = 0;
        _dustSpawnCounter = 0;
    }

    private void BeginPreCollapseShake()
    {
        _exclamation = _context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(
                _database.CreateExclamationRecord(),
                $"WingDungeonExclamation{_effectSerial++}"));
        _context.Sound.PlaySound(OracleSoundEngine.SndClink);
        _context.InterruptBracelet(discard: false);
        _context.AdvanceBracelet();
        _counter = _record.ExclamationFrames;
        _stage = WingDungeonCollapseStage.PreCollapseShake;
    }

    private void BeginCollapse()
    {
        OracleSaveData save = _context.Rooms.SaveData;
        save.SetRoomFlag(_record.Group, _record.Room, _record.RoomFlag);
        save.SetRoomFlag(
            _record.Group, _record.LinkedRoom, _record.LinkedRoomFlag);
        _counter = _record.CollapseInitialWait;
        _stage = WingDungeonCollapseStage.InitialWait;
    }

    private void BeginDust()
    {
        _dustCounter = _record.DustFrames;
        _dustSpawnCounter = 1;
    }

    private void UpdateDust()
    {
        if (_dustCounter == 0)
            return;
        _dustCounter--;
        if (_dustCounter == 0)
            return;
        _dustSpawnCounter--;
        if (_dustSpawnCounter != 0)
            return;

        // INTERAC_97 consumes and discards one RNG value before resetting
        // counter2 to three, then consumes X and Y offsets in that order.
        _ = _context.Entities.NextRandomValue() & 0x03;
        _dustSpawnCounter = _record.DustInterval;
        int xOffset = (_context.Entities.NextRandomValue() & 0x1f) - 0x10;
        int yOffset = (_context.Entities.NextRandomValue() & 0x07) - 0x04;
        Vector2 position = new(
            _record.DustX + xOffset,
            _record.DustY + yOffset);
        PuzzlePuffEffect puff = _context.Entities.Spawn<PuzzlePuffEffect>(
            new PuzzlePuffSpawn(position, OracleSoundEngine.SndPoof));
        // The source emitter precedes its new puff slot in updateAllObjects.
        puff.UpdateFrame();
    }

    private void UpdateExclamation()
    {
        if (_exclamation is null)
            return;
        _exclamation.SetActive(_counter > 1);
        if (_counter <= 1)
            _exclamation = null;
    }

    private void ApplyPhase(int phase)
    {
        WingDungeonCollapseMapRecord map = _database.Maps[phase];
        OracleRoomData room = _context.Rooms.CurrentRoom;
        if (phase == _database.Maps.Count - 1)
            ApplyFinalRoomState(room);
        else
            room.SetBackgroundSubtileRectangle(
                FacadeTopLeft(),
                _record.FacadeWidth * 2,
                map.TileIds,
                _context.AnimationTick());
        _context.RoomView.QueueRedraw();
    }

    private void ApplyFinalRoomState(OracleRoomData room)
    {
        // drawCollapsedWingDungeon copies the six-by-six tile IDs first, then
        // rewrites room layout/collisions. UNCMP_GFXH_AGES_3c uploads only
        // w3VramTiles, so retain the original façade's attribute bytes.
        room.SetBackgroundSubtileRectangle(
            FacadeTopLeft(),
            _record.FacadeWidth * 2,
            _database.Maps[^1].TileIds,
            _context.AnimationTick());
        room.SetMetatileRectangle(
            FacadeTopLeft(),
            _record.FacadeWidth,
            _record.FinalTiles,
            _record.FinalCollisions,
            _context.AnimationTick(),
            preserveBackgroundMappings: true);
        _context.RoomView.QueueRedraw();
    }

    private void Finish()
    {
        _context.Player.EndCutsceneControl();
        _context.Sound.PlayRoomMusic(_record.Group, _record.Room);
        _stage = WingDungeonCollapseStage.Completed;
        _startRemoteMakuWarning();
    }

    private bool MatchesLift(BraceletTileLifted lifted) =>
        lifted.Group == _record.Group &&
        lifted.Room == _record.Room &&
        lifted.PackedPosition == _record.RockPosition &&
        lifted.SourceTile == _record.RockTile;

    private Vector2 FacadeTopLeft() => new(
        (_record.FacadePosition & 0x0f) * OracleRoomData.MetatileSize,
        (_record.FacadePosition >> 4) * OracleRoomData.MetatileSize);
}

internal enum WingDungeonCollapseStage
{
    Inactive,
    AwaitingRockLift,
    AwaitingPickup,
    PickupWait,
    PreCollapseShake,
    CollapseStart,
    InitialWait,
    FirstPhase,
    PhaseWait,
    FinalWait,
    Finish,
    Completed
}
