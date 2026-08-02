using Godot;
using System;

namespace oracleofages;

/// <summary>
/// INTERAC_BOY $3c:$07 and boySubid07Script in past room $2:$f3.
/// </summary>
internal sealed class DepressedBoyEvent :
    InteractiveInfiniteScriptHost<DepressedBoyCharacter>,
    IRoomEntryEvent, ICutsceneCommandHost
{
    private const string ActorName = "DepressedBoy";
    private readonly DepressedBoyEventDatabase _database = new();
    private readonly DepressedBoyEventRecord _record;
    private OracleRoomData? _room;
    private bool _paletteFadeActive;
    private int _paletteOffset;
    private bool _linkApproachActive;
    private int _danceCounter;
    private int _danceIndex;
    private bool _danceComplete;

    public DepressedBoyEvent(RoomEventContext context) :
        base(context, ActorName)
    {
        _record = _database.Record;
    }

    internal DepressedBoyEventDatabase Database => _database;
    internal bool PaletteFadeActive => _paletteFadeActive;
    internal int PaletteOffset => _paletteOffset;
    internal bool LinkApproachActive => _linkApproachActive;
    internal int DanceCounter => _danceCounter;
    internal int DanceIndex => _danceIndex;
    internal bool DanceComplete => _danceComplete;
    internal bool InputDisabled => InputLeaseHeld;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start(OracleRoomData room)
    {
        _room = room;
        ResetRuntimeState(clearLinkPose: true);

        NpcCharacter actor = Context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_BOY");
        DepressedBoyCharacter boy = actor as DepressedBoyCharacter ??
            throw new InvalidOperationException(
                "Room 2:f3 instantiated INTERAC_BOY $3c:$07 without " +
                "its native actor.");
        boy.SetCutscenePose(false);
        room.SetTemporaryBackgroundPaletteOffset(0);
        StartInfiniteScript(
            boy,
            _database.Commands,
            _record.InitialScriptUpdates);

        // State 0 calls @initSubid07, falls through to boyState1, runs the
        // script once, then runs the native facing/animation tail.
        boy.UpdateDepressedBoy(Context.Player);
    }

    public override void UpdateFrame()
    {
        AdvanceInfiniteScript();
        AdvancePaletteFade();
        AdvanceLinkApproach();
        ScriptActor?.UpdateDepressedBoy(Context.Player);
    }

    protected override void ResetEventState()
    {
        ResetRuntimeState(clearLinkPose: true);
        _room = null;
    }

    bool ICutsceneCommandHost.GateOpen(string gate)
    {
        if (gate != "PaletteFade")
            throw new InvalidOperationException(
                $"boySubid07Script cannot read gate '{gate}'.");
        return !_paletteFadeActive;
    }

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        ReadScriptMemory(binding) == value;

    int ICutsceneCommandHost.ReadMemory(string binding) =>
        ReadScriptMemory(binding);

    bool ICutsceneCommandHost.RoomFlagSet(int flag)
    {
        if (flag != _record.RoomFlag)
        {
            throw new InvalidOperationException(
                $"boySubid07Script cannot read room flag ${flag:x2}.");
        }
        return Context.Rooms.SaveData.HasRoomFlag(
            _record.Group, _record.Room, (byte)flag);
    }

    bool ICutsceneCommandHost.TradeItemEquals(int value)
    {
        if (value != _record.RequiredTradeItem)
        {
            throw new InvalidOperationException(
                $"boySubid07Script cannot compare trade item ${value:x2}.");
        }
        return Context.Inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) &&
            Context.Inventory.TradeItem == value;
    }

    bool ICutsceneCommandHost.TextOptionEquals(int value)
    {
        if (!Context.TryTakeDialogueChoice(out int choice))
        {
            throw new InvalidOperationException(
                "boySubid07Script text-option branch has no completed choice result.");
        }
        return choice == value;
    }

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if (textId is < 0x2515 or > 0x2518)
        {
            throw new InvalidOperationException(
                $"boySubid07Script requested unknown TX_{textId:x4}.");
        }
        if (textId == 0x2515)
            Context.ShowChoiceDialogue(message);
        else
            Context.ShowDialogue(message);
    }

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX)
    {
        if (radiusY != _record.CollisionRadiusY ||
            radiusX != _record.CollisionRadiusX)
        {
            throw new InvalidOperationException(
                $"boySubid07Script initialized unexpected collision radii " +
                $"${radiusY:x2}/${radiusX:x2}.");
        }
        RequireScriptActor(actor).SetCollisionRadii(radiusY, radiusX);
    }

    void ICutsceneCommandHost.WriteObjectByte(
        string actor,
        int address,
        int value)
    {
        if (address != 0x3d || value is not (0 or 1))
        {
            throw new InvalidOperationException(
                $"boySubid07Script wrote unexpected object byte " +
                $"${address:x2}=${value:x2}.");
        }
        RequireScriptActor(actor).SetCutscenePose(value != 0);
    }

    void ICutsceneCommandHost.GiveItem(int treasureId, int parameter)
    {
        if (treasureId != _record.RewardTreasure ||
            parameter != _record.RewardParameter)
        {
            throw new InvalidOperationException(
                $"boySubid07Script requested unexpected reward " +
                $"${treasureId:x2}:${parameter:x2}.");
        }
        Context.GrantScriptTreasure(
            _record.Group,
            _record.Room,
            treasureId,
            parameter,
            _record.RewardObject,
            "scriptHelper.s:boySubid07Script giveitem TREASURE_TRADEITEM,$08");
    }

    void ICutsceneCommandHost.PlaySound(int sound)
    {
        if (sound != _record.RewardSound)
        {
            throw new InvalidOperationException(
                $"boySubid07Script requested unexpected sound ${sound:x2}.");
        }
        Context.Sound.PlaySound(sound);
    }

    void ICutsceneCommandHost.SetMusic(int music)
    {
        if (music != _record.DanceMusic)
        {
            throw new InvalidOperationException(
                $"boySubid07Script requested unexpected music ${music:x2}.");
        }
        Context.Sound.PlayMusicIfChanged(music);
    }

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "DarkenRoomLightly":
                BeginDarkening();
                break;
            case "MoveLinkToFunnyJokePosition":
                _linkApproachActive = true;
                break;
            case "SetLinkNormalDown":
                SetLinkNormal(Vector2I.Down);
                break;
            case "AdvanceFunnyJokeDance":
                AdvanceFunnyJokeDance();
                break;
            case "RestartSound":
                Context.Sound.RestartSound();
                break;
            case "SetLinkGetItemTwoHand":
                Context.Player.SetScriptedLinkAnimationMode(0x0f);
                break;
            case "SetLinkNormalUp":
                SetLinkNormal(Vector2I.Up);
                break;
            case "ResetMusic":
                Context.Sound.PlayRoomMusic(_record.Group, _record.Room);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown depressed-boy native handler '{handler}'.");
        }
    }

    private int ReadScriptMemory(string binding) => binding switch
    {
        "MenuDisabled" => Context.Transitions.IsTransitioning ? 1 : 0,
        "LinkObjectId" => _linkApproachActive ? 5 : 0,
        "DanceComplete" => _danceComplete ? 1 : 0,
        _ => throw new InvalidOperationException(
            $"boySubid07Script cannot read '{binding}'.")
    };

    private void BeginDarkening()
    {
        OracleRoomData room = _room ?? throw new InvalidOperationException(
            "boySubid07Script began room darkening without an active room.");
        _paletteOffset = 0;
        room.SetTemporaryBackgroundPaletteOffset(_paletteOffset);
        _paletteFadeActive = true;
    }

    private void AdvancePaletteFade()
    {
        if (!_paletteFadeActive)
            return;
        OracleRoomData room = _room ?? throw new InvalidOperationException(
            "The depressed-boy palette fade lost its room binding.");

        int candidate = _paletteOffset - 1;
        if (candidate < _record.DarkenTarget)
        {
            _paletteFadeActive = false;
            return;
        }
        _paletteOffset = candidate;
        room.SetTemporaryBackgroundPaletteOffset(_paletteOffset);
    }

    private void AdvanceLinkApproach()
    {
        if (!_linkApproachActive)
            return;

        int currentY = Mathf.FloorToInt(Context.Player.PrecisePosition.Y);
        if (currentY == _record.ApproachY)
        {
            _linkApproachActive = false;
            return;
        }
        Vector2I direction = currentY < _record.ApproachY
            ? Vector2I.Down
            : Vector2I.Up;
        Context.Player.AdvanceCutsceneMovement((Vector2)direction, direction);
    }

    private void AdvanceFunnyJokeDance()
    {
        if (_danceComplete)
            return;

        _danceCounter--;
        if (_danceCounter != 0)
            return;
        if (_danceIndex == _record.DanceCount)
        {
            _danceComplete = true;
            return;
        }

        DepressedBoyDanceFrame frame = _record.DanceAnimations[_danceIndex++];
        Context.Player.SetScriptedLinkAnimationMode(frame.Mode);
        _danceCounter = frame.Duration;
    }

    private void SetLinkNormal(Vector2I direction)
    {
        _linkApproachActive = false;
        Context.Player.SetScriptedLinkAnimationMode(null);
        Context.Player.EndGetItemOneHandPose();
        Context.Player.EndGetItemTwoHandPose();
        Context.Player.Face(direction);
    }

    private void ResetRuntimeState(bool clearLinkPose)
    {
        _paletteFadeActive = false;
        _paletteOffset = 0;
        _linkApproachActive = false;
        _danceCounter = 1;
        _danceIndex = 0;
        _danceComplete = false;
        if (clearLinkPose)
        {
            Context.Player.SetScriptedLinkAnimationMode(null);
            Context.Player.EndGetItemOneHandPose();
            Context.Player.EndGetItemTwoHandPose();
        }
    }
}
