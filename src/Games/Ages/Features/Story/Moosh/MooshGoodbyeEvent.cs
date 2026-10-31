using Godot;
using System;
using System.Linq;

namespace Oracles;

// Owns Ages farewell content and control resources; D1 owns all native updates.
internal sealed class MooshGoodbyeEvent(RoomEventContext context) : RoomCutsceneCommandHost, IRoomEvent
{
    private readonly MooshGoodbyeEventDatabase _database = new();
    private readonly MooshDatabase _moosh = new(AgesGameContent.AssetRoot);
    private AgesMooshScriptedCompanionRoomEntity? _body;
    public override RoomEventContext Context => context;
    public bool HasState => _body is { Finished: false } && context.Entities.IsSpecialObjectOwner(_body);
    public bool BlocksGameplay => InputControlHeld;
    public bool MenusDisabled => HasState && context.Entities.RuntimeState.ReadField("wMenuDisabled") != 0;
    public bool RequiresRoomEventUpdate => false;
    internal AgesMooshScriptedCompanionRoomEntity? Body => _body;

    internal void SpawnPreset()
    {
        if (!context.Entities.SpecialObjectSlotAvailable ||
            !_database.ShouldSpawn(context.Rooms.ActiveGroup, context.Rooms.CurrentRoom.Id, context.Rooms.SaveData, context.Inventory)) return;
        MooshGoodbyeEventRecord record = _database.Record;
        CompanionRuntimeState.InstallPreset(context.Entities.RuntimeState, context.Rooms.SaveData, new(record.MooshX, record.MooshY));
        _body = CreateBody(new(record.MooshX, record.MooshY));
        context.Entities.AddEntity(_body);
    }

    internal AgesMooshScriptedCompanionRoomEntity CreateBody(Vector2 position)
    {
        NpcRecord npc = _moosh.Visual.CreateNativePresenter(context.Rooms.ActiveGroup,context.Rooms.CurrentRoom.Id,position,0);
        NpcCharacter actor = NpcCharacter.CreateFromRecord(npc, context.Entities.AssetRoot);
        actor.SetActive(false);
        return new AgesMooshScriptedCompanionRoomEntity(actor,
            context.Entities.RuntimeState, context.Rooms.SaveData, () => context.Rooms.CurrentRoom, _moosh.Visual, _database.Record,
            Transfer, value => SetDisabledObjects(value), SetNativeMenuDisabled, context.Entities.OnRoomEntityDialogueRequested);
    }

    internal void OnRoomLoaded(int group, OracleRoomData room)
    {
        // The remembered producer precedes placed objects and room-entry
        // cancellation. Adopt its body only after that cancellation completes.
        Cancel();
        if (_database.Matches(group, room.Id))
            _body = context.Entities.EntityAdapters<AgesMooshScriptedCompanionRoomEntity>()
                .SingleOrDefault(body => context.Entities.IsSpecialObjectOwner(body));
    }

    private void Transfer(ScriptedCompanionRoomEntity body, Player player, bool mounting)
    {
        body.CompleteNativeUpdate();
        int frame = body.Npc.CurrentAnimationFrame, counter = body.Npc.CurrentAnimationCounter;
        body.Npc.SetScriptButtonSensitive(false); body.Npc.SetActive(false);
        var next = (MooshCompanionRoomEntity)context.Entities.ReplaceSpecialObjectPresentation(body,
            new MooshCompanionSpawn(body.NativeSpecialObjectPosition, ObjectDirection.Down,
                context.Rooms.ActiveGroup, context.Rooms.CurrentRoom.Id, ForceMount: mounting));
        next.ContinueNativePresentation(player, frame, counter, mounting);
        _body = null;
    }

    public void UpdateFrame() => throw new InvalidOperationException("Ages Moosh farewell advances only from its native D1 slot.");
    public void Cancel()
    {
        ReleaseInputControl();
        if (_body is not null) context.Entities.RetireEntity(_body);
        _body = null;
    }
}
