using Godot;
using System;

namespace oracleofages;

/// <summary>
/// INTERAC_TOILET_HAND $5b:$00, toiletHandScript, and the native object-in-hole
/// buffer consumer in room 2:3e.
/// </summary>
internal sealed class ToiletHandEvent :
    InteractiveInfiniteScriptHost<ToiletHandCharacter>,
    IRoomEntryEvent, ICutsceneCommandHost,
    IUpdatesDuringDialogueRoomEvent
{
    private const string ActorName = "ToiletHand";
    private const string CloseBinding = "ToiletHandClose";
    private const string PressedBinding = "ToiletHandPressedA";
    private const string AnimationParameterBinding =
        "ToiletHandAnimParameter";
    private const string PriorityBinding = "ToiletHandPriority";
    private const string HoleReactionBinding = "ToiletHandHoleReaction";

    private readonly ToiletHandEventDatabase _database = new();
    private readonly ToiletHandEventRecord _record;
    private bool _linkClose;
    private bool _reactionActive;
    private bool _reactionFinished;
    private int _holeReaction = -1;
    private int _pendingHoleReaction = -1;

    public ToiletHandEvent(RoomEventContext context) :
        base(context, ActorName)
    {
        _record = _database.Record;
    }

    internal ToiletHandEventDatabase Database => _database;
    internal bool LinkClose => _linkClose;
    internal bool ReactionActive => _reactionActive;
    internal int PendingHoleReaction => _pendingHoleReaction;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start(OracleRoomData room)
    {
        _ = room;
        NpcCharacter actor = Context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_TOILET_HAND");
        ToiletHandCharacter toiletHand =
            actor as ToiletHandCharacter ??
            throw new InvalidOperationException(
                "Room 2:3e instantiated INTERAC_TOILET_HAND without " +
                "its native actor.");

        _linkClose = false;
        _reactionActive = false;
        _reactionFinished = false;
        _holeReaction = -1;
        _pendingHoleReaction = -1;
        StartInfiniteScript(
            toiletHand,
            _database.Commands,
            _record.InitialScriptUpdates);
    }

    public override void UpdateFrame()
    {
        ToiletHandCharacter? actor = ScriptActor;
        if (actor is null)
            return;

        if (!_reactionActive && _pendingHoleReaction >= 0)
            StartReaction(actor);

        AdvanceInfiniteScript();
        if (_reactionFinished)
        {
            _reactionFinished = false;
            _reactionActive = false;
            _holeReaction = -1;
            StartInfiniteScript(actor, _database.Commands);
            return;
        }

        actor.UpdateVisibleState(
            Context.Player,
            _reactionActive ? 2 : 1);
    }

    public void UpdateDuringDialogueFrame()
    {
        if (!_reactionActive)
            ScriptActor?.UpdateVisibleState(Context.Player, 1);
    }

    internal void OnObjectFellInHole(ObjectFellInHoleKind kind)
    {
        if (ScriptActor is null || _reactionActive)
            return;

        int reaction = kind switch
        {
            ObjectFellInHoleKind.Bomb => 0,
            ObjectFellInHoleKind.Bombchu => 1,
            ObjectFellInHoleKind.CaneOfSomariaBlock => 2,
            ObjectFellInHoleKind.EmberSeed => 3,
            ObjectFellInHoleKind.ScentSeed => 4,
            ObjectFellInHoleKind.GaleSeed => 5,
            ObjectFellInHoleKind.MysterySeed => 6,
            ObjectFellInHoleKind.BraceletObject or
                ObjectFellInHoleKind.PushBlock => 7,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        _pendingHoleReaction = reaction;
    }

    protected override void ResetEventState()
    {
        _linkClose = false;
        _reactionActive = false;
        _reactionFinished = false;
        _holeReaction = -1;
        _pendingHoleReaction = -1;
    }

    public override bool MemoryEquals(string binding, int value) =>
        ReadMemory(binding) == value;

    public override int ReadMemory(string binding) => binding switch
    {
        CloseBinding => _linkClose ? 1 : 0,
        PressedBinding => PendingActorButton ? 1 : 0,
        AnimationParameterBinding =>
            RequireScriptActor(ActorName).CurrentAnimationParameter,
        PriorityBinding =>
            RequireScriptActor(ActorName).HasVisiblePriority ? 1 : 0,
        HoleReactionBinding => _holeReaction,
        _ => throw new InvalidOperationException(
            $"toiletHandScript cannot read '{binding}'.")
    };

    bool ICutsceneCommandHost.RoomFlagSet(int flag)
    {
        if (flag != _record.RoomFlag)
        {
            throw new InvalidOperationException(
                $"toiletHandScript cannot read room flag ${flag:x2}.");
        }
        return Context.Rooms.SaveData.HasRoomFlag(
            _record.Group, _record.Room, (byte)flag);
    }

    bool ICutsceneCommandHost.TradeItemEquals(int value)
    {
        if (value != _record.RequiredTradeItem)
        {
            throw new InvalidOperationException(
                $"toiletHandScript cannot compare trade item ${value:x2}.");
        }
        return Context.Inventory.HasTreasure(
                TreasureDatabase.TreasureTradeItem) &&
            Context.Inventory.TradeItem == value;
    }

    bool ICutsceneCommandHost.TextOptionEquals(int value)
    {
        if (!Context.TryTakeDialogueChoice(out int choice))
        {
            throw new InvalidOperationException(
                "toiletHandScript text-option branch has no completed choice.");
        }
        return choice == value;
    }

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if (textId is < 0x0b07 or > 0x0b0c &&
            textId is < 0x0b25 or > 0x0b2b)
        {
            throw new InvalidOperationException(
                $"toiletHandScript requested unknown TX_{textId:x4}.");
        }
        if (textId == 0x0b08)
            Context.ShowChoiceDialogue(message);
        else
            Context.ShowDialogue(message);
    }

    void ICutsceneCommandHost.SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation) =>
        RequireScriptActor(actor).SetToiletAnimation(
            animation, encodedAnimation);

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX)
    {
        if (radiusY != 6 || radiusX != 6)
        {
            throw new InvalidOperationException(
                "toiletHandScript initcollisions lost its standard fallback.");
        }

        // Interaction data's two $10 bytes are its OAM tile base and
        // animation/palette byte, not collision radii. The interaction starts
        // with zero radii, so scriptCmd_initNpcHitbox installs $06/$06.
        RequireScriptActor(actor).SetCollisionRadii(
            _record.CollisionRadiusY,
            _record.CollisionRadiusX);
    }

    void ICutsceneCommandHost.GiveItem(int treasureId, int parameter)
    {
        if (treasureId != _record.RewardTreasure ||
            parameter != _record.RewardParameter)
        {
            throw new InvalidOperationException(
                $"toiletHandScript requested unexpected reward " +
                $"${treasureId:x2}:${parameter:x2}.");
        }

        Context.GrantScriptTreasure(
            _record.Group,
            _record.Room,
            treasureId,
            parameter,
            _record.RewardObject,
            "scripts.s:toiletHandScript giveitem TREASURE_TRADEITEM,$02");
    }

    public override void RunNativeHandler(string handler)
    {
        ToiletHandCharacter actor = RequireScriptActor(ActorName);
        switch (handler)
        {
            case "toiletHand_checkVisibility":
                // The source copies Interaction.visible to wcddb, then
                // accidentally tests priority bits 0-2 instead of bit 7.
                // Priority is retained by HasVisiblePriority after hiding.
                break;
            case "toiletHand_setInvisible":
                actor.SetScriptVisible(false);
                break;
            case "toiletHand_setVisible":
                actor.SetScriptVisible(true);
                break;
            case "toiletHand_clearPressedAButton":
                ClearPendingActorButton();
                break;
            case "toiletHand_checkLinkIsClose":
                _linkClose = IsLinkClose(Context.Player.Position);
                break;
            case "toiletHand_retreatIntoToiletIfNotAlready":
                if (actor.Direction != 2)
                {
                    actor.SetToiletAnimation(
                        2, _record.Animation(2));
                }
                break;
            case "toiletHand_setScreenShake60":
                Context.Entities.BeginScreenShake(60);
                break;
            case "toiletHand_playExplosion":
                Context.Sound.PlaySound(OracleSoundEngine.SndExplosion);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown Toilet Hand native handler '{handler}'.");
        }
    }

    public override void ScriptEnded()
    {
        if (!_reactionActive)
        {
            throw new InvalidOperationException(
                "The infinite toiletHandScript ended unexpectedly.");
        }
        _reactionFinished = true;
    }

    private void StartReaction(ToiletHandCharacter actor)
    {
        _holeReaction = _pendingHoleReaction;
        _pendingHoleReaction = -1;
        _reactionActive = true;
        _reactionFinished = false;
        StartInfiniteScript(actor, _database.ReactionCommands);
    }

    private bool IsLinkClose(Vector2 position)
    {
        int y = ((byte)(Mathf.FloorToInt(position.Y) + 4)) & 0xf0;
        int x = (((byte)(Mathf.FloorToInt(position.X) - 4)) & 0xf0) >> 4;
        int packed = y | x;
        foreach (int expected in _record.ClosePacked)
        {
            if (packed == expected)
                return true;
        }
        return false;
    }
}
