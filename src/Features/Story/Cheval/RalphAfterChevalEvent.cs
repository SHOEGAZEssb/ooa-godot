using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// INTERAC_RALPH $37:$10 and ralphSubid10Script after leaving Cheval's house.
/// </summary>
internal class RalphAfterChevalEvent :
    InteractiveInfiniteScriptHost<RalphAfterChevalCharacter>,
    IRoomEntryEvent,
    ICutsceneCommandHost
{
    private const string ActorName = "Ralph";

    private readonly RalphAfterChevalEventDatabase _database = new();
    private readonly RalphAfterChevalEventRecord _record;
    private Vector2 _precisePosition;
    private bool FacingSignal => (Context.Entities.RuntimeState.ReadWramByte(WramAddress.wTmpcfc0) & 1) != 0;
    private int _substate;
    private bool _menusDisabled;
    private readonly bool _beforeCurrents;
    private readonly IReadOnlyList<CutsceneCommand> _commands;

    internal RalphAfterChevalEvent(RoomEventContext context, bool beforeCurrents = false)
        : base(context, ActorName)
    {
        _beforeCurrents = beforeCurrents;
        // ralph.s:$0b and $10 share @setScriptAndDisableObjects and their
        // entire state-one native dispatcher, including dust and facing.
        _record = beforeCurrents ? _database.Record with { Room = 0x20, SubId = 0x0b, WarpDestination = 0x24 } : _database.Record;
        _commands = beforeCurrents ? CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/ralph_before_currents_commands.tsv") : _database.Commands;
    }

    internal RalphAfterChevalEventDatabase Database => _database;
    internal int Substate => _substate;
    internal bool FacingBit => FacingSignal;
    public bool MenusDisabled => _menusDisabled;
    public bool FreezesNonInteractionObjects => _menusDisabled;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start(OracleRoomData room)
    {
        Cancel();
        if (Context.Rooms.ActiveGroup != _record.Group ||
            room.Id != _record.Room)
        {
            throw new InvalidOperationException(
                $"INTERAC_RALPH $37:$10 cannot initialize in " +
                $"{Context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        }

        NpcCharacter npc = Context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_RALPH $37:$10");
        RalphAfterChevalCharacter ralph =
            npc as RalphAfterChevalCharacter ??
            throw new InvalidOperationException(
                "Room 1:79 instantiated Ralph $37:$10 without its native actor.");

        OracleSaveData save = Context.Rooms.SaveData;
        if (save.HasRoomFlag(
                _record.Group, _record.Room, (byte)_record.RoomFlag) ||
            (_beforeCurrents ? save.HasTreasure(TreasureId.TuneOfCurrents) || Context.Transitions.ScreenEntryDirection != Vector2I.Zero : !save.HasGlobalFlag(_record.TalkedGlobalFlag)) ||
            Context.Transitions.ActiveWarpDestinationPosition !=
                _record.WarpDestination)
        {
            ralph.SetActive(false);
            return;
        }

        _precisePosition = ralph.Position;
        _substate = 0;
        _menusDisabled = _record.MenuDisabled != 0;
        StartInfiniteScript(ralph, _commands);
        SetInputEnabled(enabled: false);

        // @setScriptAndDisableObjects jumps directly into ralphRunSubid from
        // source state 0, so the initial wait and both substate-0 animation
        // calls happen before the first ordinary state-1 dispatch.
        for (int update = 0; update < _record.InitialNativeUpdates; update++)
            UpdateFrame();
    }

    public override void UpdateFrame()
    {
        RalphAfterChevalCharacter? ralph = ScriptActor;
        if (ralph is null || !ralph.Active)
            return;

        if (FacingSignal)
            TurnLinkTowardRalph(ralph);

        int entrySubstate = _substate;
        if (entrySubstate == 0)
            ralph.AdvanceRalphAnimation(1);
        else if (entrySubstate == 1 &&
                 (Context.Entities.FrameCounter & 0x07) == 0)
            SpawnDust(ralph);

        AdvanceInfiniteScript();

        // var3f remains zero for subid $10, so every non-terminal script
        // update performs the shared trailing interactionAnimate call.
        if (ralph.Active)
            ralph.AdvanceRalphAnimation(1);
    }

    public override void SetInputEnabled(bool enabled)
    {
        base.SetInputEnabled(enabled);
        if (enabled)
            _menusDisabled = false;
    }

    public override void ShowText(int textId, string message)
    {
        CutsceneShowTextCommand imported =
            _commands.OfType<CutsceneShowTextCommand>().Single();
        if (textId != (_beforeCurrents ? 0x2a1a : 0x2a20) ||
            textId != imported.TextId ||
            message != imported.Message)
        {
            throw new InvalidOperationException(
                $"ralphSubid10Script requested invalid TX_{textId:x4} payload.");
        }
        Context.ShowDialogue(message);
    }

    public override void SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation)
    {
        int direction = angle switch
        {
            ObjectAngle.Up => 0,
            ObjectAngle.Down => 2,
            ObjectAngle.Right when _beforeCurrents => 1,
            ObjectAngle.Left when _beforeCurrents => 3,
            _ => throw new InvalidOperationException(
                $"ralphSubid10Script requested unsupported angle ${angle:x2}.")
        };
        if (encodedAnimation != _record.Animation(direction))
        {
            throw new InvalidOperationException(
                $"Ralph movement animation ${direction:x2} changed.");
        }
        RequireScriptActor(actor).SetDirection(direction);
    }
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation)
    {
        if (!_beforeCurrents || animation != 0 || encodedAnimation != _record.Animation(0))
            throw UnsupportedCommand($"Ralph animation ${animation:x2}");
        RequireScriptActor(actor).SetDirection(animation);
    }

    public override void MoveActorAtSpeed(string actor, int speed, int angle)
    {
        bool supported =
            angle == ObjectAngle.Up &&
                speed is var upSpeed &&
                (upSpeed == _record.Speed200 ||
                 upSpeed == _record.Speed100 ||
                 upSpeed == _record.Speed080) ||
            angle == ObjectAngle.Down && speed == _record.Speed200;
        if (_beforeCurrents)
            supported = angle == ObjectAngle.Left && (speed == _record.Speed200 || speed == _record.Speed100 || speed == _record.Speed080) ||
                angle == ObjectAngle.Right && (speed == _record.Speed100 || speed == _record.Speed200);
        if (!supported)
        {
            throw new InvalidOperationException(
                $"Unsupported Ralph movement ${speed:x2}/${angle:x2}.");
        }
        RequireScriptActor(actor).SetStatePosition(
            NativeObjectMovement.ApplySpeed(
                Context.Entities.RuntimeState, ref _precisePosition, speed, angle));
    }

    public override void SetMusic(int music)
    {
        if (music != _record.Music)
        {
            throw new InvalidOperationException(
                $"ralphSubid10Script requested unexpected music ${music:x2}.");
        }
        Context.Sound.PlayMusicIfChanged(music);
    }

    public override void OrRoomFlag(int flag)
    {
        if (flag != _record.RoomFlag)
        {
            throw new InvalidOperationException(
                $"ralphSubid10Script cannot OR room flag ${flag:x2}.");
        }
        Context.Rooms.SaveData.SetRoomFlag(
            _record.Group, _record.Room, (byte)flag);
    }

    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "ToggleFacingBit":
                var memory = Context.Entities.RuntimeState;
                memory.SetWramByte(WramAddress.wTmpcfc0, (byte)(memory.ReadWramByte(WramAddress.wTmpcfc0) ^ 1));
                return;

            case "IncrementSubstate":
                if (_substate >= 2)
                {
                    throw new InvalidOperationException(
                        "ralphSubid10Script incremented substate past 2.");
                }
                _substate++;
                return;

            case "SetSubstate0":
                _substate = 0;
                return;

            case "ResetMusic":
                Context.Sound.PlayRoomMusic(_record.Group, _record.Room);
                return;

            default:
                throw new InvalidOperationException(
                    $"Unknown Ralph-after-Cheval native handler '{handler}'.");
        }
    }

    public override void ScriptEnded()
    {
        if (!Context.Rooms.SaveData.HasRoomFlag(
                _record.Group, _record.Room, (byte)_record.RoomFlag) ||
            InputControlHeld || _menusDisabled)
        {
            throw new InvalidOperationException(
                "Ralph-after-Cheval script ended before restoring input/menu state.");
        }
        RequireScriptActor(ActorName).SetActive(false);
    }

    protected override void ResetEventState()
    {
        _precisePosition = Vector2.Zero;
        _substate = 0;
        _menusDisabled = false;
    }

    private void TurnLinkTowardRalph(RalphAfterChevalCharacter ralph)
    {
        int link = (OracleObjectPosition.HighByte(Context.Player.Position.X) +
            0x10) & 0xff;
        int actor = (OracleObjectPosition.HighByte(ralph.Position.X) +
            0x10) & 0xff;
        int difference = (actor - link) & 0xff;
        Vector2I direction;
        int magnitude;
        if (actor >= link)
        {
            direction = Vector2I.Right;
            magnitude = difference;
        }
        else
        {
            direction = Vector2I.Left;
            magnitude = (~difference) & 0xff;
        }
        if (magnitude < _record.FacingThreshold)
            direction = Vector2I.Down;
        Context.Player.Face(direction);
    }

    private void SpawnDust(RalphAfterChevalCharacter ralph)
    {
        if (_record.PuffId != 0x05 || _record.PuffSubId != 0x81)
        {
            throw new InvalidOperationException(
                "Ralph-after-Cheval imported an unsupported dust interaction.");
        }

        Vector2 position = ralph.Position +
            new Vector2(_record.PuffXOffset, _record.PuffYOffset);
        if (!Context.Entities.InteractionSlotAvailable)
            return;

        PuzzlePuffEffect puff = Context.Entities.Spawn<PuzzlePuffEffect>(
            new PuzzlePuffSpawn(
                position,
                Sound: SoundId.MusNone,
                Flickers: true));
        // Ralph is the first placed interaction in this room, so the next free
        // $05 slot receives state 0 later in the same original interaction pass.
        puff.UpdateFrame();
    }
}
