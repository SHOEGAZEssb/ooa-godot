using Godot;
using System;

namespace oracleofages;

/// <summary>
/// INTERAC_RALPH $37:$10 and ralphSubid10Script after leaving Cheval's house.
/// </summary>
internal sealed class RalphAfterChevalEvent :
    InteractiveInfiniteScriptHost<RalphAfterChevalCharacter>,
    IRoomEntryEvent,
    ICutsceneCommandHost
{
    private const string ActorName = "Ralph";

    private readonly RalphAfterChevalEventDatabase _database = new();
    private readonly RalphAfterChevalEventRecord _record;
    private Vector2 _precisePosition;
    private bool _facingBit;
    private int _substate;
    private bool _menusDisabled;
    private readonly PuzzlePuffEffect?[] _dustSlots =
        new PuzzlePuffEffect?[0x10];

    internal RalphAfterChevalEvent(RoomEventContext context)
        : base(context, ActorName)
    {
        _record = _database.Record;
    }

    internal RalphAfterChevalEventDatabase Database => _database;
    internal RalphAfterChevalCharacter? Actor => ScriptActor;
    internal int Substate => _substate;
    internal bool FacingBit => _facingBit;
    internal bool MenusDisabled => _menusDisabled;

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
            !save.HasGlobalFlag(_record.TalkedGlobalFlag) ||
            Context.Transitions.ActiveWarpDestinationPosition !=
                _record.WarpDestination)
        {
            ralph.SetActive(false);
            return;
        }

        _precisePosition = ralph.Position;
        _facingBit = false;
        _substate = 0;
        _menusDisabled = _record.MenuDisabled != 0;
        StartInfiniteScript(ralph, _database.Commands);
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

        if (_facingBit)
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
            (CutsceneShowTextCommand)_database.Commands[12];
        if (textId != 0x2a20 ||
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
            0x00 => 0,
            0x10 => 2,
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

    public override void MoveActorAtSpeed(string actor, int speed, int angle)
    {
        bool supported =
            angle == 0x00 &&
                speed is var upSpeed &&
                (upSpeed == _record.Speed200 ||
                 upSpeed == _record.Speed100 ||
                 upSpeed == _record.Speed080) ||
            angle == 0x10 && speed == _record.Speed200;
        if (!supported)
        {
            throw new InvalidOperationException(
                $"Unsupported Ralph movement ${speed:x2}/${angle:x2}.");
        }
        RequireScriptActor(actor).SetStatePosition(
            OracleObjectMovement.Shared.ApplySpeed(
                ref _precisePosition, speed, angle));
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
                _facingBit = !_facingBit;
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
            InputLeaseHeld || _menusDisabled)
        {
            throw new InvalidOperationException(
                "Ralph-after-Cheval script ended before restoring input/menu state.");
        }
        RequireScriptActor(ActorName).SetActive(false);
    }

    protected override void ResetEventState()
    {
        _precisePosition = Vector2.Zero;
        _facingBit = false;
        _substate = 0;
        _menusDisabled = false;
        Array.Clear(_dustSlots);
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
        int slot = 1;
        while (slot < _dustSlots.Length &&
               _dustSlots[slot] is { Finished: false } candidate &&
               GodotObject.IsInstanceValid(candidate))
        {
            slot++;
        }
        if (slot == _dustSlots.Length)
            return;

        PuzzlePuffEffect puff = Context.Entities.Spawn<PuzzlePuffEffect>(
            new PuzzlePuffSpawn(
                position,
                Sound: 0,
                Flickers: true,
                FlickerVisibleOnEvenUpdates: (slot & 1) != 0));
        _dustSlots[slot] = puff;
        // Ralph is the first placed interaction in this room, so the next free
        // $05 slot receives state 0 later in the same original interaction pass.
        puff.UpdateFrame();
    }
}
