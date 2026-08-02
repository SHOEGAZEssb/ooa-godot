using Godot;
using System;

namespace oracleofages;

/// <summary>
/// INTERAC_RAFTON $69:$00/$01, rafton_subid00Script, and
/// rafton_subid01Script in the two rooms of Rafton's house.
/// </summary>
internal sealed class RaftonEvent :
    InteractiveInfiniteScriptHost<RaftonCharacter>,
    IRoomEntryEvent,
    IUpdatesDuringDialogueRoomEvent,
    ICutsceneCommandHost
{
    private const string ActorName = "Rafton";

    private readonly RaftonEventDatabase _database = new();
    private readonly RaftonEventRecord _record;
    private NpcCharacter? _exclamation;
    private Vector2 _precisePosition;
    private bool _rightRoom;
    private bool _d3EssenceObtained;
    private int _behaviour;
    private int _loadedTextId;
    private int _exclamationCounter;
    private int _effectSerial;
    private bool _exclamationFresh;

    internal RaftonEvent(RoomEventContext context) : base(context, ActorName)
    {
        _record = _database.Record;
    }

    internal RaftonEventDatabase Database => _database;
    internal RaftonCharacter? Actor => ScriptActor;
    internal int Behaviour => _behaviour;
    internal int LoadedTextId => _loadedTextId;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group &&
        room.Id is var roomId &&
        (roomId == _record.LeftRoom || roomId == _record.RightRoom);

    public void Start(OracleRoomData room)
    {
        Cancel();
        if (Context.Rooms.ActiveGroup != _record.Group ||
            room.Id != _record.LeftRoom && room.Id != _record.RightRoom)
        {
            throw new InvalidOperationException(
                $"INTERAC_RAFTON cannot initialize in " +
                $"{Context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        }

        OracleSaveData save = Context.Rooms.SaveData;
        _rightRoom = room.Id == _record.RightRoom;
        bool changedRooms = save.HasGlobalFlag(_record.ChangedRoomsFlag);
        if (save.HasRoomFlag(_record.Group, room.Id, OracleSaveData.RoomFlag80) ||
            _rightRoom != changedRooms)
        {
            return;
        }

        int subId = _rightRoom ? _record.RightSubId : _record.LeftSubId;
        NpcCharacter npc = Context.RequireNpc(
            _record.Group,
            room.Id,
            _record.InteractionId,
            subId,
            "INTERAC_RAFTON");
        RaftonCharacter rafton = npc as RaftonCharacter ??
            throw new InvalidOperationException(
                $"Room 2:{room.Id:x2} instantiated INTERAC_RAFTON " +
                "without its native actor.");

        _behaviour = _rightRoom ? -1 : ResolveLeftBehaviour(save);
        _precisePosition = rafton.Position;
        StartInfiniteScript(
            rafton,
            _rightRoom ? _database.RightCommands : _database.LeftCommands,
            _record.InitialScriptUpdates);
    }

    public override void UpdateFrame()
    {
        RaftonCharacter? rafton = ScriptActor;
        if (rafton is null)
            return;

        // $69:$01 calls interactionAnimateAsNpc before interactionRunScript;
        // $69:$00 runs its script first and selects animation by behaviour.
        if (_rightRoom)
            rafton.AdvanceAsNpc(Context.Player);

        AdvanceInfiniteScript();

        if (!_rightRoom && rafton.Active)
            AdvanceLeftNativeAnimation(rafton);
        UpdateExclamation();
    }

    public void UpdateDuringDialogueFrame()
    {
        RaftonCharacter? rafton = ScriptActor;
        if (rafton is not null && rafton.Active)
        {
            if (_rightRoom)
                rafton.AdvanceAsNpc(Context.Player);
            else
                AdvanceLeftNativeAnimation(rafton);
        }
        UpdateExclamation();
    }

    public override int ReadMemory(string binding)
    {
        if (binding != "RaftonBehaviour")
        {
            throw new InvalidOperationException(
                $"rafton_subid00Script cannot read '{binding}'.");
        }
        return _behaviour;
    }

    public override bool MemoryEquals(string binding, int value)
    {
        if (binding != "D3EssenceObtained" || value != 1)
        {
            throw new InvalidOperationException(
                $"rafton_subid01Script cannot compare '{binding}' with ${value:x2}.");
        }
        return _d3EssenceObtained;
    }

    public override bool RoomFlagSet(int flag)
    {
        if (!_rightRoom || flag != _record.RoomFlag)
        {
            throw new InvalidOperationException(
                $"rafton_subid01Script cannot read room flag ${flag:x2}.");
        }
        return Context.Rooms.SaveData.HasRoomFlag(
            _record.Group, _record.RightRoom, (byte)flag);
    }

    public override bool TradeItemEquals(int value)
    {
        if (!_rightRoom || value != _record.RequiredTradeItem)
        {
            throw new InvalidOperationException(
                $"rafton_subid01Script cannot compare trade item ${value:x2}.");
        }
        return Context.Inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) &&
            Context.Inventory.TradeItem == value;
    }

    public override bool TextOptionEquals(int value)
    {
        if (value != 0 || !Context.TryTakeDialogueChoice(out int choice))
        {
            throw new InvalidOperationException(
                "Rafton text-option branch has no supported completed choice.");
        }
        return choice == value;
    }

    public override void ShowText(int textId, string message)
    {
        if (message != _database.Text(textId))
        {
            throw new InvalidOperationException(
                $"Rafton TX_{textId:x4} payload diverges from imported text.");
        }
        if (textId is 0x2703 or 0x2711)
            Context.ShowChoiceDialogue(message);
        else
            Context.ShowDialogue(message);
    }

    public override void ShowLoadedText()
    {
        if (_loadedTextId == 0)
        {
            throw new InvalidOperationException(
                "Rafton attempted showloadedtext before settextid.");
        }
        Context.ShowDialogue(_database.DialogueText(_loadedTextId));
    }

    public override void WriteMemory(string binding, int value)
    {
        if (binding != "LoadedText" || value is not (
                0x2700 or 0x2701 or 0x2706 or 0x2708 or 0x270a))
        {
            throw new InvalidOperationException(
                $"Rafton cannot write '{binding}'=${value:x4}.");
        }
        _ = _database.Text(value);
        _loadedTextId = value;
    }

    public override void SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation)
    {
        if (encodedAnimation != _record.Animation(animation))
        {
            throw new InvalidOperationException(
                $"Rafton animation ${animation:x2} payload diverged from metadata.");
        }
        RequireScriptActor(actor).SetDirection(animation);
    }

    public override void SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation)
    {
        if (angle != _record.RightAngle ||
            encodedAnimation != _record.Animation(1))
        {
            throw new InvalidOperationException(
                $"Rafton movement animation for angle ${angle:x2} changed.");
        }
        RequireScriptActor(actor).SetDirection(1);
    }

    public override void SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX)
    {
        if (radiusY != _record.CollisionRadiusY ||
            radiusX != _record.CollisionRadiusX)
        {
            throw new InvalidOperationException(
                $"Rafton initialized unexpected collision radii " +
                $"${radiusY:x2}/${radiusX:x2}.");
        }
        RequireScriptActor(actor).SetCollisionRadii(radiusY, radiusX);
    }

    public override void MoveActorAtSpeed(string actor, int speed, int angle)
    {
        if (speed != _record.Speed || angle != _record.RightAngle)
        {
            throw new InvalidOperationException(
                $"Unsupported Rafton movement ${speed:x2}/${angle:x2}.");
        }
        RequireScriptActor(actor).SetStatePosition(
            OracleObjectMovement.Shared.ApplySpeed(
                ref _precisePosition, speed, angle));
    }

    public override void WriteObjectByte(
        string actor,
        int address,
        int value)
    {
        if (address != _record.AnimationCounterAddress ||
            value != _record.FreezeCounter)
        {
            throw new InvalidOperationException(
                $"Rafton cannot write Interaction.${address:x2}=${value:x2}.");
        }
        RequireScriptActor(actor).SetAnimationCounter(value);
    }

    public override void GiveItem(int treasureId, int parameter)
    {
        if (!_rightRoom ||
            treasureId != _record.RewardTreasure ||
            parameter != _record.RewardParameter)
        {
            throw new InvalidOperationException(
                $"rafton_subid01Script requested unexpected reward " +
                $"${treasureId:x2}:${parameter:x2}.");
        }
        Context.GrantScriptTreasure(
            _record.Group,
            _record.RightRoom,
            treasureId,
            parameter,
            _record.RewardObject,
            "scriptHelper.s:rafton_subid01Script giveitem TREASURE_TRADEITEM,$0a");
    }

    public override void SetGlobalFlag(int flag)
    {
        if (flag != _record.GaveRopeFlag &&
            flag != _record.ChangedRoomsFlag)
        {
            throw new InvalidOperationException(
                $"rafton_subid00Script cannot set global flag ${flag:x2}.");
        }
        base.SetGlobalFlag(flag);
    }

    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "TurnToFaceLink":
                RequireScriptActor(ActorName).FaceLink(Context.Player);
                return;

            case "CreateExclamationMark":
                CreateExclamation();
                return;

            case "LoseChevalRope":
                if (!Context.Inventory.LoseTreasure(_record.ChevalRopeTreasure))
                {
                    throw new InvalidOperationException(
                        "rafton_subid00Script tried to lose an unowned Cheval Rope.");
                }
                return;

            case "CheckD3Essence":
                _d3EssenceObtained =
                    (Context.Inventory.Essences & _record.D3EssenceMask) != 0;
                return;

            default:
                throw new InvalidOperationException(
                    $"Unknown INTERAC_RAFTON native handler '{handler}'.");
        }
    }

    public override void ScriptEnded()
    {
        if (_rightRoom || _behaviour != 4 ||
            !Context.Rooms.SaveData.HasGlobalFlag(_record.ChangedRoomsFlag))
        {
            throw new InvalidOperationException(
                "INTERAC_RAFTON ended outside its completed room-move path.");
        }
        RaftonCharacter rafton = RequireScriptActor(ActorName);
        rafton.SetScriptButtonSensitive(false);
        rafton.SetActive(false);
        ClearPendingActorButton();
    }

    protected override void ResetEventState()
    {
        RetireExclamation();
        _precisePosition = Vector2.Zero;
        _rightRoom = false;
        _d3EssenceObtained = false;
        _behaviour = 0;
        _loadedTextId = 0;
    }

    private int ResolveLeftBehaviour(OracleSaveData save)
    {
        if (Context.Inventory.HasTreasure(_record.IslandChartTreasure))
            return 4;
        if (save.HasGlobalFlag(_record.GaveRopeFlag))
            return 3;
        if (Context.Inventory.HasTreasure(_record.ChevalRopeTreasure))
            return 2;
        return (Context.Inventory.Essences & _record.D2EssenceMask) != 0 ? 1 : 0;
    }

    private void AdvanceLeftNativeAnimation(RaftonCharacter rafton)
    {
        if (_behaviour != 4)
        {
            rafton.AdvanceAsNpc(Context.Player);
            return;
        }

        int animationUpdates = 1;
        if (Counter != 0 && ScriptActorSpeed >= _record.Speed)
            animationUpdates++;
        rafton.AdvanceDeparture(Context.Player, animationUpdates);
    }

    private void CreateExclamation()
    {
        RaftonCharacter rafton = RequireScriptActor(ActorName);
        if (_exclamation is not null)
        {
            throw new InvalidOperationException(
                "rafton_subid00Script created a second exclamation mark.");
        }

        int y = OracleObjectPosition.HighByte(rafton.Position.Y) + _record.EffectY;
        int x = OracleObjectPosition.HighByte(rafton.Position.X) + _record.EffectX;
        _exclamation = Context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(
                _database.CreateExclamationRecord(y, x),
                $"RaftonExclamation{_effectSerial++}"));
        _exclamation.SetAnimationRate(0.0f);
        _exclamationCounter = _record.EffectFrames;
        _exclamationFresh = true;
        Context.Sound.PlaySound(_record.ClinkSound);
    }

    private void UpdateExclamation()
    {
        if (_exclamation is null)
            return;
        if (_exclamationFresh)
        {
            _exclamationFresh = false;
            return;
        }
        if (_exclamationCounter <= 1)
        {
            RetireExclamation();
            return;
        }
        _exclamationCounter--;
        _exclamation.AdvanceAnimationUpdates(1);
    }

    private void RetireExclamation()
    {
        if (_exclamation is not null &&
            GodotObject.IsInstanceValid(_exclamation))
        {
            _exclamation.SetActive(false);
        }
        _exclamation = null;
        _exclamationCounter = 0;
        _exclamationFresh = false;
    }
}
