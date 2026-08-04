using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Owns INTERAC_COMPANION_SPAWNER $67:$02 and
/// INTERAC_COMPANION_SCRIPTS $71:$03 in room $0:$6a. The interaction actor
/// transfers to the live w1Companion slot when the glove script force-mounts
/// Link.
/// </summary>
internal sealed class RickyGlovesEvent :
    InteractiveInfiniteScriptHost<NpcCharacter>,
    IRoomEntryEvent,
    IUpdatesDuringDialogueRoomEvent,
    ICutsceneCommandHost
{
    private const string Ricky = "Ricky";

    private readonly RickyGlovesEventDatabase _database = new();
    private readonly RickyGlovesEventRecord _record;
    private NpcCharacter? _placedActor;
    private RickyCompanionRoomEntity? _companion;
    private int _initialSpecialObjectUpdates;
    private int _zFixed;
    private int _speedZ;
    private bool _menusDisabled;

    internal RickyGlovesEvent(RoomEventContext context)
        : base(context, Ricky)
    {
        _record = _database.Record;
    }

    RoomEventContext ICutsceneCommandHost.Context => Context;
    internal RickyGlovesEventDatabase Database => _database;
    internal NpcCharacter? RickyActor => _placedActor;
    internal RickyCompanionRoomEntity? Companion => _companion;
    internal bool MenusDisabled => _menusDisabled;

    public bool Matches(int group, OracleRoomData room) =>
        !CompanionRuntimeState.AnyActive(Context.Entities.RuntimeState) &&
        _database.ShouldSpawn(group, room.Id, Context.Rooms.SaveData);

    public void Start(OracleRoomData room)
    {
        Cancel();
        int group = Context.Rooms.ActiveGroup;
        if (!Matches(group, room))
        {
            throw new InvalidOperationException(
                $"The Ricky glove interaction cannot start in " +
                $"{group:x}:{room.Id:x2} for the current source state.");
        }

        if (CompanionRuntimeState.AnyActive(Context.Entities.RuntimeState))
        {
            ActiveCompanion active = CompanionRuntimeState.Read(
                Context.Entities.RuntimeState);
            throw new InvalidOperationException(
                $"Room 0:6a cannot install SPECIALOBJECT_RICKY $0b while " +
                $"w1Companion is owned by ${active.Id:x2}.");
        }

        // The source spawner clears only wRememberedCompanionId after
        // installing Ricky's fixed preset.
        CompanionRuntimeState.ForgetRemembered(Context.Entities.RuntimeState);
        _placedActor = Context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(
                _database.CreateActorRecord(),
                Ricky,
                Talkable: true,
                Solid: true));
        // Ricky is a special object, not a fixed 16x16 interaction. Preserve
        // the signed OAM cell positions above and beside his object origin.
        _placedActor.SetScriptAnimation(
            _database.Visual.Animations[_record.InitialAnimation],
            _database.Visual.AnimationSourceOffsets[
                _record.InitialAnimation]);
        _zFixed = 0;
        _speedZ = 0;
        _initialSpecialObjectUpdates = 0;
        _menusDisabled = false;
        _placedActor.SetActive(false);
        StartInfiniteScript(
            _placedActor,
            _database.Commands,
            _record.InitialScriptUpdates);
        if (Context.Entities.ScreenTransitionActive)
        {
            // State-0 interactions continue dispatching while wScrollMode is
            // active. Resolve both companionCheckCanSpawn initialization
            // returns during destination preload so Ricky's final visible
            // presentation scrolls in while all state-$0a/script work remains
            // frozen.
            CompleteInitialSpecialObjectPresentation();
        }
    }

    public override void UpdateFrame()
    {
        if (!HasState)
            return;
        if (_initialSpecialObjectUpdates <
            _record.InitialSpecialObjectUpdates)
        {
            _initialSpecialObjectUpdates++;
            if (_initialSpecialObjectUpdates ==
                _record.InitialSpecialObjectUpdates)
            {
                CompleteInitialSpecialObjectPresentation();
            }
            AdvanceInfiniteScript();
            return;
        }
        // EventOwnedNpcRoomEntity has already performed this update's
        // interaction animation before room-event dispatch.
        UpdatePlacedRicky(advanceAnimation: false);
        AdvanceInfiniteScript();
    }

    private void CompleteInitialSpecialObjectPresentation()
    {
        NpcCharacter actor = _placedActor ??
            throw UnsupportedCommand("initialize missing Ricky");
        _initialSpecialObjectUpdates =
            _record.InitialSpecialObjectUpdates;
        actor.SetActive(true);
        SetInitialActorButtonSensitive();
    }

    public void UpdateDuringDialogueFrame() =>
        // wTextIsActive freezes the room-entity adapter, while Ricky's
        // special object remains enabled. Reproduce that one animation update
        // here before applying his animParameter-driven vertical motion.
        UpdatePlacedRicky(advanceAnimation: true);

    private void UpdatePlacedRicky(bool advanceAnimation)
    {
        if (_placedActor is null ||
            !GodotObject.IsInstanceValid(_placedActor) ||
            !_placedActor.Active)
        {
            return;
        }

        // rickyStateASubstate0 reads animParameter after specialObjectAnimate.
        // A marked frame starts the $ff00 jump; every other frame applies
        // gravity $40 in the original signed 8.8 Z domain.
        if (advanceAnimation)
        {
            _placedActor.AnimateAndUpdateDrawPriorityOneUpdate(Context.Player);
        }
        if ((_placedActor.CurrentAnimationParameter & 0x80) != 0)
            _speedZ = _record.JumpSpeedZ;
        else
            OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, _record.JumpGravity);
        _placedActor.SetScriptDrawOffset(new Vector2(0, _zFixed >> 8));
    }

    public override bool MemoryEquals(string binding, int value) =>
        binding switch
        {
            "RickyTalked" =>
                ((Context.Rooms.SaveData.ReadWramByte(
                    _record.RickyStateAddress) & _record.TalkedMask) != 0
                        ? 1
                        : 0) == value,
            "AnimalCompanion" => Context.Inventory.AnimalCompanion == value,
            "HasRickyGloves" =>
                (Context.Inventory.HasTreasure(_record.GlovesTreasure)
                    ? 1
                    : 0) == value,
            "RickyMounted" => (_companion?.LinkRiding == true ? 1 : 0) == value,
            _ => throw UnsupportedCommand($"read '{binding}'=${value:x2}")
        };

    public override void WriteMemory(string binding, int value)
    {
        if (binding != "RickyStateOr")
            throw UnsupportedCommand($"write '{binding}'=${value:x2}");

        OracleSaveData save = Context.Rooms.SaveData;
        save.WriteWramByte(
            _record.RickyStateAddress,
            (byte)(save.ReadWramByte(_record.RickyStateAddress) | value));
    }

    public override void ShowText(int textId, string message)
    {
        if (textId is not (0x2000 or 0x2001 or 0x2003 or 0x2004 or 0x2005))
            throw UnsupportedCommand($"show TX_{textId:x4}");
        Context.ShowDialogue(message);
    }

    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "ResetRickyButton":
                ClearPendingActorButton();
                break;
            case "LoseRickyGloves":
                if (!Context.Inventory.LoseTreasure(_record.GlovesTreasure))
                {
                    throw UnsupportedCommand(
                        $"lose missing Ricky gloves ${_record.GlovesTreasure:x2}");
                }
                break;
            case "BeginRickyMount":
                BeginRickyMount();
                break;
            case "EnableObjectsForRickyMount":
                // The script clears wDisabledObjects while retaining
                // wMenuDisabled until TX_2005 has closed.
                SetInputEnabled(enabled: true);
                _menusDisabled = true;
                break;
            case "EnableRickyMenu":
                _menusDisabled = false;
                break;
            default:
                throw UnsupportedCommand($"run native handler '{handler}'");
        }
    }

    private void BeginRickyMount()
    {
        NpcCharacter actor = _placedActor ??
            throw UnsupportedCommand("force-mount missing Ricky");
        if (!GodotObject.IsInstanceValid(actor) || !actor.Active)
            throw UnsupportedCommand("force-mount inactive Ricky");

        actor.SetScriptButtonSensitive(false);
        Vector2 position = actor.Position;
        actor.SetActive(false);
        _companion = Context.Entities.Spawn<RickyCompanionRoomEntity>(
            new RickyCompanionSpawn(
                position,
                2,
                _record.Group,
                _record.Room,
                ForceMount: true));
    }

    public override void ScriptEnded()
    {
        _menusDisabled = false;
        _placedActor = null;
        _companion = null;
    }

    protected override void ResetEventState()
    {
        if (_placedActor is not null &&
            GodotObject.IsInstanceValid(_placedActor) &&
            _placedActor.Active)
        {
            _placedActor.SetScriptDrawOffset(Vector2.Zero);
            _placedActor.SetActive(false);
        }
        _placedActor = null;
        _companion = null;
        _zFixed = 0;
        _speedZ = 0;
        _initialSpecialObjectUpdates = 0;
        _menusDisabled = false;
    }
}
