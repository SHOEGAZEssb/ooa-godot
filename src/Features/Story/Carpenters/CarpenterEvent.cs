using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>Room $0:$25's independently scheduled INTERAC_CARPENTER scripts.</summary>
internal sealed class CarpenterEvent : InteractiveCutsceneCommandHost, IRoomEntryEvent, ICutsceneCommandHost,
    IUpdatesDuringDialogueRoomEvent
{
    public RoomEventContext Context { get; }
    internal CarpenterDatabase Database { get; } = new();
    private readonly CutsceneCommandLaneScheduler _lanes;
    private readonly List<CarpenterScriptHost> _actors = new();
    private CarpenterRoomEntity? _blocker;
    protected override RoomEventContext InputContext => Context;
    private bool _exitController;
    private bool _exitChoice;
    private int _mountLock;
    public bool ScreenTransitionsDisabled => BlocksGameplay;
    public bool HasState { get; private set; }
    public bool BlocksGameplay => InputLeaseHeld;
    public bool MenusDisabled { get; private set; }

    public CarpenterEvent(RoomEventContext context)
    {
        Context = context;
        _lanes = new(this);
    }

    public bool Matches(int group, OracleRoomData room) => group == 0 &&
        (room.Id == Database.Constant("search-exit-room") || Context.Entities.EntityAdapters<CarpenterRoomEntity>().Any());

    public void Start(OracleRoomData room)
    {
        Cancel();
        _exitController = room.Id == Database.Constant("search-exit-room") &&
            !Context.Rooms.SaveData.HasGlobalFlag(Database.Constant("bridge-flag")) &&
            (!Context.Rooms.SaveData.IsLinkedGame || Context.Rooms.SaveData.HasGlobalFlag(Database.Constant("zelda-flag")));
        foreach (var entity in Context.Entities.EntityAdapters<CarpenterRoomEntity>())
        {
            if (!entity.Npc.Active) continue;
            entity.BindEvent(this);
            if (entity.Npc.Record.SubId == 1)
            {
                _blocker = entity;
                continue;
            }
            var actor = new CarpenterScriptHost(this, entity);
            bool firstWorker = entity.Npc.Record.SubId > 1 &&
                !_actors.Any(a => a.Npc.Record.SubId > 1);
            _actors.Add(actor);
            actor.Runner = _lanes.StartLane($"Carpenter{entity.Npc.Record.SubId:x2}",
                Database.Commands, actor, Database.Script(entity.ScriptSubid).Entry,
                () =>
                {
                    // Native slot $01 runs after the boss and before the first
                    // remaining worker, including partial found-bit masks.
                    if (firstWorker) UpdateBlocker();
                    actor.UpdateNative();
                });
        }
        HasState = _actors.Count != 0 || _blocker is not null || _exitController;
    }

    public void UpdateFrame()
    {
        if (!HasState) return;
        if (_exitController) { UpdateExit(); return; }
        foreach (var actor in _actors)
            if (actor.Departing && actor.Npc.Active) actor.UpdateDeparture();
        _lanes.AdvanceFrame();
        if (!_actors.Any(a => a.Npc.Record.SubId > 1 && a.Runner.Active)) UpdateBlocker();
        HasState = _lanes.Active || _blocker is not null || _actors.Any(a => a.Departing && a.Npc.Active);
    }

    // enabled bit 7 keeps native animation/gravity alive. interactionRunScript
    // itself freezes its pointer and both counters while a textbox is active.
    public void UpdateDuringDialogueFrame() => UpdateFrame();

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (!HasState || BlocksGameplay || Context.DialogueOpen) return false;
        var actor = _actors.Find(a => ReferenceEquals(a.Npc, npc));
        if (actor is null || !actor.ButtonSensitive || !npc.Active) return false;
        actor.ButtonPressed = true;
        return true;
    }

    public void Cancel()
    {
        SetInputEnabled(true);
        MenusDisabled = false;
        _lanes.Clear();
        // Outgoing actors retain their exact last picture during scrolling.
        _actors.Clear();
        _blocker = null;
        HasState = false;
        _exitController = false;
        _exitChoice = false;
        _mountLock = 0;
        CompanionRuntimeState.SetMountingLock(Context.Entities.RuntimeState, 0);
    }

    public override void SetInputEnabled(bool enabled)
    {
        if (InputLeaseHeld == !enabled) return;
        base.SetInputEnabled(enabled);
        if (!enabled) MenusDisabled = true;
    }
    public override void SetMenuEnabled(bool enabled) => MenusDisabled = !enabled;

    internal int SearchState
    {
        get => Context.Entities.RuntimeState.ReadWramByte(Database.Constant("state-address"));
        set => Context.Entities.RuntimeState.SetWramByte(Database.Constant("state-address"), checked((byte)value));
    }

    internal void BuildColumn(int position)
    {
        SetTile(position, Database.Constant("bridge-top"));
        SetTile(position + 0x10, Database.Constant("bridge-bottom"));
        SearchState = (SearchState + 1) & 0xff;
        Context.Sound.PlaySound(Database.Constant("bridge-sound"));
    }

    internal void ReturnWorker(int subid)
    {
        int address = Database.Constant("found-address");
        int found = Context.Entities.RuntimeState.ReadWramByte(address) | (1 << (subid & 15));
        Context.Entities.RuntimeState.SetWramByte(address, (byte)found);
        SetInputEnabled(true);
        SetMenuEnabled(true);
        if (found == Database.Constant("found-mask"))
            Context.Transitions.ApplyWarpWithFadeOut(Context.Player,
                new Warp(0, Context.Rooms.CurrentRoom.Id, -1, 0, 0, 0,
                    Database.Constant("return-room"), Database.Constant("return-position"), 0, 0));
    }

    private void UpdateExit()
    {
        if (SearchState == 0) return;
        if (_mountLock > 0)
            CompanionRuntimeState.SetMountingLock(Context.Entities.RuntimeState, --_mountLock);
        if (_exitChoice)
        {
            if (Context.DialogueOpen || !Context.TryTakeDialogueChoice(out int choice)) return;
            _exitChoice = false;
            if (choice != 1)
            {
                int address = Database.Constant("state-address");
                for (int i = 0; i < 0x10; i++) Context.Entities.RuntimeState.SetWramByte(address + i, 0);
                _exitController = HasState = false;
                return;
            }
            Context.Player.ApplyInteractionInvincibility(Database.Constant("exit-invincibility"));
            _mountLock = Database.Constant("mount-lock-frames");
            CompanionRuntimeState.SetMountingLock(Context.Entities.RuntimeState, _mountLock);
            var mounted = Context.Entities.PlayerScreenTransitionOwner;
            if (mounted is not null)
            {
                mounted.SetScreenTransitionBoundaryCoordinate(true, Database.Constant("exit-push-x"), Context.Player);
                if (mounted is RickyCompanionRoomEntity ricky) ricky.StopAtCarpenterSearchBoundary();
                if (mounted is DimitriCompanionRoomEntity dimitri) dimitri.StopAtCarpenterSearchBoundary();
            }
            else Context.Player.SetScrollingTransitionPosition(new Vector2(Database.Constant("exit-push-x"), Context.Player.Position.Y), Vector2.Zero);
            return;
        }
        Vector2 position = Context.Entities.PlayerScreenTransitionOwner?.ScreenTransitionPosition ?? Context.Player.Position;
        if (position.X > Database.Constant("exit-boundary") || Context.DialogueOpen) return;
        _exitChoice = true;
        Context.ShowChoiceDialogue(Database.LeaveMessage);
    }

    private void UpdateBlocker()
    {
        if (_blocker is null || SearchState != 0x0b) return;
        SetTile(Database.Constant("blocker-position"), Database.Constant("blocker-tile"));
        _blocker.Npc.SetActive(false);
        _blocker = null;
    }

    private void SetTile(int position, int tile) => Context.Rooms.CurrentRoom.SetPositionTileAndCollision(
        new Vector2((position & 15) * 16 + 8, (position >> 4) * 16 + 8),
        checked((byte)tile), null, Context.AnimationTick());
}
