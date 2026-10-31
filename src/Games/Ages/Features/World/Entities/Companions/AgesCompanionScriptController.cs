using Godot;
using System;
using System.Linq;

namespace Oracles;

// Content services for native Ages animal states; D1 owns their dispatch.
internal sealed class AgesCompanionScriptController(RoomEventContext context) : RoomCutsceneCommandHost, IRoomEvent
{
    private RickyDatabase? _rickyCache;
    private RickyDatabase _ricky => _rickyCache ??= new(AgesGameContent.AssetRoot);
    private MooshDatabase? _mooshCache;
    private MooshDatabase _moosh => _mooshCache ??= new(AgesGameContent.AssetRoot);
    private MooshGoodbyeEventDatabase? _goodbyeCache;
    private MooshGoodbyeEventDatabase _goodbye => _goodbyeCache ??= new();
    private DimitriVisualDatabase? _dimitriCache;
    private DimitriVisualDatabase _dimitri => _dimitriCache ??= new(AgesGameContent.AssetRoot);
    private ScriptedCompanionRoomEntity? _body;
    public override RoomEventContext Context => context;
    public bool HasState => _body is { Finished: false } && context.Entities.IsSpecialObjectOwner(_body);
    public bool BlocksGameplay => InputControlHeld;
    public bool RequiresRoomEventUpdate => false;

    internal ScriptedCompanionRoomEntity CreateBody(int id, Vector2 position)
    {
        NpcRecord record = id switch
        {
            SpecialObjectId.Ricky => _ricky.Visual.CreateNativePresenter(context.Rooms.ActiveGroup,context.Rooms.CurrentRoom.Id,position,0),
            SpecialObjectId.Moosh => _moosh.Visual.CreateNativePresenter(context.Rooms.ActiveGroup,context.Rooms.CurrentRoom.Id,position,0),
            SpecialObjectId.Dimitri => _dimitri.CreateNativePresenter(context.Rooms.ActiveGroup,context.Rooms.CurrentRoom.Id,position),
            _ => throw new NotSupportedException($"Ages native scripted SPECIALOBJECT ${id:x2} is not an animal.")
        };
        record = record with { Group = context.Rooms.ActiveGroup, Room = context.Rooms.CurrentRoom.Id, Y = (int)position.Y, X = (int)position.X };
        NpcCharacter npc = NpcCharacter.CreateFromRecord(record, context.Entities.AssetRoot);
        npc.SetActive(false);
        return id switch
        {
            SpecialObjectId.Ricky => new RickyScriptedCompanionRoomEntity(npc, context.Entities.RuntimeState, context.Rooms.SaveData,
                () => context.Rooms.CurrentRoom, _ricky.Visual, _ricky.IdleHopSpeedZ, _ricky.IdleGravity, Transfer, SetNativeMenuDisabled),
            SpecialObjectId.Dimitri => new AgesDimitriScriptedCompanionRoomEntity(npc, context.Entities.RuntimeState, context.Rooms.SaveData,
                () => context.Rooms.CurrentRoom, _dimitri, Transfer, value => SetDisabledObjects(value), SetNativeMenuDisabled),
            SpecialObjectId.Moosh => new AgesMooshScriptedCompanionRoomEntity(npc, context.Entities.RuntimeState, context.Rooms.SaveData,
                () => context.Rooms.CurrentRoom, _moosh.Visual, _goodbye.Record, Transfer,
                value => SetDisabledObjects(value), SetNativeMenuDisabled, context.Entities.OnRoomEntityDialogueRequested),
            _ => throw new InvalidOperationException()
        };
    }

    internal void SpawnPreset(int id, Vector2 position)
    {
        if (!context.Entities.SpecialObjectSlotAvailable) return;
        CompanionRuntimeState.InstallPreset(context.Entities.RuntimeState, context.Rooms.SaveData, position);
        _body = CreateBody(id, position);
        context.Entities.AddEntity(_body);
    }

    internal void OnRoomLoaded(int group, OracleRoomData room)
    {
        Cancel();
        _body = context.Entities.EntityAdapters<ScriptedCompanionRoomEntity>()
            .SingleOrDefault(body => context.Entities.IsSpecialObjectOwner(body));
    }

    private void Transfer(ScriptedCompanionRoomEntity body, Player player, bool mounting)
    {
        body.CompleteNativeUpdate();
        int frame = body.Npc.CurrentAnimationFrame, counter = body.Npc.CurrentAnimationCounter;
        body.Npc.SetScriptButtonSensitive(false); body.Npc.SetActive(false);
        int group = context.Rooms.ActiveGroup, room = context.Rooms.CurrentRoom.Id;
        RoomEntitySpawn spawn = body.NativeSpecialObjectId switch
        {
            SpecialObjectId.Ricky => new RickyCompanionSpawn(body.NativeSpecialObjectPosition, ObjectDirection.Down, group, room, ForceMount: mounting),
            SpecialObjectId.Dimitri => new DimitriCompanionSpawn(body.NativeSpecialObjectPosition, ObjectDirection.Down, group, room),
            SpecialObjectId.Moosh => new MooshCompanionSpawn(body.NativeSpecialObjectPosition, ObjectDirection.Down, group, room, ForceMount: mounting),
            _ => throw new InvalidOperationException("Only native animal bodies transfer to normal gameplay.")
        };
        IRoomEntity next = context.Entities.ReplaceSpecialObjectPresentation(body, spawn);
        switch (next)
        {
            case RickyCompanionRoomEntity r: r.ContinueNativePresentation(player, frame, counter, mounting); break;
            case DimitriCompanionRoomEntity d: d.ContinueNativePresentation(player, frame, counter, mounting); break;
            case MooshCompanionRoomEntity m: m.ContinueNativePresentation(player, frame, counter, mounting); break;
        }
        _body = null;
    }

    public void UpdateFrame() => throw new InvalidOperationException("Ages animal states advance only from native D1.");
    public void Cancel()
    {
        ReleaseInputControl();
        if (_body is not null) context.Entities.RetireEntity(_body);
        _body = null;
    }
}
