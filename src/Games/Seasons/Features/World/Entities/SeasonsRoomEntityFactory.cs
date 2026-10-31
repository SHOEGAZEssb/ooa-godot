using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal sealed class SeasonsRoomEntityFactory(SeasonsOpeningSession session, RoomEntityManager owner) : IRoomEntityFactory
{
    public string AssetRoot => SeasonsGameContent.AssetRoot;
    public bool CanReconstructDebugSpecialObject(OracleRuntimeState memory,int group,int room)=>_common.CanReconstructCompanion(memory);
    private readonly CommonRoomEntityFactory _common = new(owner,rooms:session.Rooms);
    private EnemyHandlerRegistry? _enemyCatalog;
    private DimitriVisualDatabase? _dimitriVisual;
    private CompanionVisualDatabase? _mooshVisual;
    private RickyDatabase? _ricky;
    private SeasonsHerosCaveDatabase? _herosCave;
    private SeasonsHerosCaveDatabase HerosCave=>_herosCave??=new();
    public IRoomEntity? CreateStandaloneEnemy(int id, int subId, OracleRoomData room, Vector2 position,
        string source, out string error)
    {
        _enemyCatalog ??= new(AssetRoot);
        EnemyHandlerDescriptor? record = _enemyCatalog.Handlers.FirstOrDefault(entry => entry.Id == id && entry.SubId == subId);
        if (record is { SupportsStandaloneConstruction: true } && _common.TryCreateStandaloneEnemy(record,session.Rooms.ActiveGroup,room,position,source) is { } entity)
        {
            error = string.Empty;
            return entity;
        }
        error = $"{source}: Seasons enemy ${id:x2}:${subId:x2} has no imported standalone handler. " +
            (record?.Source ?? "No imported native ID/subid classification.");
        return null;
    }
    public int OpenShutterTile => 0xa0; // constants/common/tileIndices.s:TILEINDEX_STANDARD_FLOOR
    private readonly SeedTreeRefillData _refills = new(SeasonsGameContent.AssetRoot, 8);
    private readonly GeneratedTable _objectRooms = GeneratedTable.Load(SeasonsGameContent.AssetRoot + "opening/supported_object_rooms.tsv",
        new("Seasons room object bindings", GeneratedTableKeySemantics.Unique,
            ["group", "room", "source"], ["group","room"], headerRequired: true));

    public IEnumerable<bool> PrepareResources() => [];
    public void BindEntity(IRoomEntity entity) { }
    public void RefreshState(IEnumerable<IRoomEntity> entities)
    {
        foreach (var entity in entities)
            if (entity is IRoomSaveStateEntity state) state.RefreshSaveState();
    }
    public IRoomEntity Create(RoomEntitySpawn spawn, OracleRoomData room) => spawn is CompanionInitializationSpawn initialization
        ?CreateCompanionInitializer(initialization.Id,initialization.Position,room):
        spawn is RickyCompanionSpawn ricky
        ?CreateRicky(ricky,room):
        spawn is DimitriCompanionSpawn dimitri
        ?CreateDimitri(dimitri,room):spawn is MooshCompanionSpawn moosh
        ?CreateMoosh(moosh,room):_common.TryCreate(spawn,room) ?? (spawn switch
    {
        _ => throw new NotSupportedException($"Seasons room ${room.Group:x1}:{room.Id:x2}: unbound spawn {spawn.GetType().Name}.")
    });

    private ScriptedCompanionRoomEntity CreateCompanionInitializer(int id,Vector2 position,OracleRoomData room) => id switch
    {
        SpecialObjectId.Ricky => CreateRickyInitializer(position,room),
        SpecialObjectId.Dimitri => CreateDimitriInitializer(position,room),
        SpecialObjectId.Moosh => CreateMooshInitializer(position,room),
        _ => throw new NotSupportedException($"companionCheckCanSpawn: Seasons SPECIALOBJECT ${id:x2} initialization has no native content binding.")
    };

    private IRoomEntity CreateRicky(RickyCompanionSpawn spawn,OracleRoomData room)
    {
        if(session.Memory.ReadWramByte(0xd104) is 0 or 0x0a)
            return CreateRickyInitializer(spawn.Position,room);
        return _common.CreateRicky(spawn,room);
    }

    private ScriptedCompanionRoomEntity CreateRickyInitializer(Vector2 position,OracleRoomData room)
    {
        var data=_ricky??=new(AssetRoot);
        var npc=NpcCharacter.CreateFromRecord(data.Visual.CreateNativePresenter(session.Rooms.ActiveGroup,room.Id,position,0),AssetRoot);
        npc.SetActive(false);
        return new RickyScriptedCompanionRoomEntity(npc,session.Memory,session.Rooms.SaveData,
            ()=>session.Rooms.CurrentRoom,data.Visual,data.IdleHopSpeedZ,data.IdleGravity,TransferRicky,
            value=>session.Memory.SetField("wMenuDisabled",value));
    }

    private void TransferRicky(ScriptedCompanionRoomEntity previous,Player player,bool mounting)
    {
        previous.CompleteNativeUpdate();
        int frame=previous.Npc.CurrentAnimationFrame,counter=previous.Npc.CurrentAnimationCounter;
        previous.Npc.SetScriptButtonSensitive(false);previous.Npc.SetActive(false);
        var next=(RickyCompanionRoomEntity)owner.ReplaceSpecialObjectPresentation(previous,
            new RickyCompanionSpawn(previous.NativeSpecialObjectPosition,2,session.Rooms.ActiveGroup,session.Rooms.CurrentRoom.Id,ForceMount:mounting));
        next.ContinueNativePresentation(player,frame,counter,mounting);
    }

    private IRoomEntity CreateMoosh(MooshCompanionSpawn spawn,OracleRoomData room)
    {
        if(session.Memory.ReadWramByte(0xd104) is 0 or 0x0a)
            return CreateMooshInitializer(spawn.Position,room);
        return _common.TryCreate(spawn,room)??throw new InvalidOperationException("Common SPECIALOBJECT $0d binding is missing.");
    }

    private ScriptedCompanionRoomEntity CreateMooshInitializer(Vector2 position,OracleRoomData room)
    {
        var visual=_mooshVisual??=new(AssetRoot,SpecialObjectId.Moosh);
        var npc=NpcCharacter.CreateFromRecord(visual.CreateNativePresenter(session.Rooms.ActiveGroup,room.Id,position,0),AssetRoot);
        npc.SetActive(false);
        return new SeasonsMooshScriptedCompanionRoomEntity(npc,session.Memory,session.Rooms.SaveData,
            ()=>session.Rooms.CurrentRoom,visual,TransferMoosh);
    }

    private void TransferMoosh(ScriptedCompanionRoomEntity previous,Player player,bool mounting)
    {
        previous.CompleteNativeUpdate();
        int frame=previous.Npc.CurrentAnimationFrame,counter=previous.Npc.CurrentAnimationCounter;
        previous.Npc.SetScriptButtonSensitive(false);previous.Npc.SetActive(false);
        var next=(MooshCompanionRoomEntity)owner.ReplaceSpecialObjectPresentation(previous,
            new MooshCompanionSpawn(previous.NativeSpecialObjectPosition,2,session.Rooms.ActiveGroup,session.Rooms.CurrentRoom.Id));
        next.ContinueNativePresentation(player,frame,counter,mounting);
    }

    private IRoomEntity CreateDimitri(DimitriCompanionSpawn spawn,OracleRoomData room)
    {
        if(session.Memory.ReadWramByte(0xd104)==0 || session.Memory.ReadWramByte(0xd104)==0x0a)
            return CreateDimitriInitializer(spawn.Position,room);
        return _common.TryCreate(spawn,room)??throw new InvalidOperationException("Common SPECIALOBJECT $0c binding is missing.");
    }

    private ScriptedCompanionRoomEntity CreateDimitriInitializer(Vector2 position,OracleRoomData room)
    {
        var visual=_dimitriVisual??=new(AssetRoot);
        var npc=NpcCharacter.CreateFromRecord(visual.CreateNativePresenter(session.Rooms.ActiveGroup,room.Id,position),AssetRoot);
        npc.SetActive(false);
        return new SeasonsDimitriScriptedCompanionRoomEntity(npc,session.Memory,session.Rooms.SaveData,
            ()=>session.Rooms.CurrentRoom,visual,TransferDimitri);
    }

    private void TransferDimitri(ScriptedCompanionRoomEntity previous,Player player,bool mounting)
    {
        previous.CompleteNativeUpdate();
        int frame=previous.Npc.CurrentAnimationFrame,counter=previous.Npc.CurrentAnimationCounter;
        previous.Npc.SetScriptButtonSensitive(false);previous.Npc.SetActive(false);
        var next=(DimitriCompanionRoomEntity)owner.ReplaceSpecialObjectPresentation(previous,
            new DimitriCompanionSpawn(previous.NativeSpecialObjectPosition,2,session.Rooms.ActiveGroup,session.Rooms.CurrentRoom.Id));
        next.ContinueNativePresentation(player,frame,counter,mounting);
    }

    public IRoomEntity CreateRememberedCompanion(RememberedCompanionAdmission source,OracleRoomData room)
    {
        if(source.IsInteraction || source.SpecialObjectId is not (SpecialObjectId.Ricky or SpecialObjectId.Dimitri or SpecialObjectId.Moosh))
            throw new NotSupportedException($"{source.Source}: Seasons remembered companion content is unbound.");
        if(session.Memory.ReadWramByte(0xd104)!=0)
            throw new NotSupportedException($"{source.Source}: retained state ${session.Memory.ReadWramByte(0xd104):x2} has no fresh remembered-body binding.");
        return CreateCompanionInitializer(source.SpecialObjectId,source.Position,room);
    }

    public IEnumerable<IRoomEntity> CreateRoomEntities(int group, OracleRoomData room, EnemyPlacementContext placement)
    {
        if(!owner.ScreenTransitionActive && owner.SpecialObjectPresentationAvailable &&
            CompanionRuntimeState.IsActive(session.Memory,SpecialObjectId.Ricky))
        {
            var active=CompanionRuntimeState.Read(session.Memory);
            yield return CreateRicky(new(active.Position,active.Direction,group,room.Id,
                Riding:session.Memory.ReadField("wLinkObjectIndex")==0xd1),room);
        }
        else if(!owner.ScreenTransitionActive && owner.SpecialObjectPresentationAvailable &&
            CompanionRuntimeState.IsActive(session.Memory,SpecialObjectId.Dimitri))
        {
            var active=CompanionRuntimeState.Read(session.Memory);
            yield return CreateDimitri(new(active.Position,active.Direction,group,room.Id,
                Riding:session.Memory.ReadField("wLinkObjectIndex")==0xd1),room);
        }
        else if(!owner.ScreenTransitionActive && owner.SpecialObjectPresentationAvailable &&
            CompanionRuntimeState.IsActive(session.Memory,SpecialObjectId.Moosh))
        {
            var active=CompanionRuntimeState.Read(session.Memory);
            yield return CreateMoosh(new(active.Position,active.Direction,group,room.Id,
                Riding:session.Memory.ReadField("wLinkObjectIndex")==0xd1),room);
        }
        foreach (var row in _objectRooms.Rows)
        {
            int declaredGroup=row.Decimal(0,0,7), declaredRoom=row.HexByte(1); _=row.RequiredString(2);
            if (group!=declaredGroup || room.Id!=declaredRoom) continue;
            foreach (var entity in owner.CreateNativeObjectStream(group,room,
                NativeObjectStreamDatabase.For(AssetRoot).Root(group,room.Id),placement)) yield return entity;
            yield break;
        }
        GD.Print($"objects/seasons/mainData.s: group{group:x1}Map{room.Id:x2}ObjectData: layout navigation only; NPCs, enemies and events are unsupported.");
    }

    public IRoomEntity CreateStreamInteraction(NativeObjectInstruction source, OracleRoomData room, EnemyPlacementContext placement) =>
        _common.TryCreateStreamInteraction(source,room,placement) ??
        (source switch
        {
            {Id:0xc6,SubId:0}=>new SeasonsHerosCaveSwordRoomEntity(session,HerosCave,
                HerosCave.CreateSword(new(source.X,source.Y),session.Data.World.SpritePalettes)),
            {Id:0xdc,SubId:1 or 2}=>new SeasonsHerosCaveLinkedEntranceRoomEntity(session.Save,source),
            {Id:InteractionId.DungeonStuff,SubId:0} when session.Rooms.CurrentDungeonIndex==0=>
                new DungeonEntranceRoomEntity(new(source.X,source.Y),new(0,0x0200,HerosCave.Text(0x0200),0),
                    HerosCave.Constant("entry-min-y"),HerosCave.Constant("entry-radius"),session.Memory,
                    placement.Kind==EnemyPlacementEntryKind.ScreenWarp,
                    dungeon=>StaticObjectState.LoadDungeon(session.Memory,dungeon),
                    (id,message)=>{session.ShowText(id,message);session.Checkpoints.RecordCurrentPoint();}),
            {Id:InteractionId.DungeonScript,SubId:1} when session.Rooms.CurrentDungeonIndex==0=>
                new DungeonTriggerChestScriptRoomEntity(new(source.X,source.Y),session.Memory,1,HerosCave.Constant("chest-wait"),
                    ()=>owner.ActiveTriggers,()=>session.Save.HasRoomFlag(room.Group,room.Id,OracleSaveData.RoomFlagItem),
                    owner.IsScriptTextActive,owner.OnSoundRequested,()=>
                    {if(session.Rooms.TrySetTile((byte)room.GetPackedPosition(new(source.X,source.Y)),ChestDatabase.ClosedTile))owner.OnRoomTileChanged();}),
            _=>new SeasonsOpeningActor(session,source.Id,source.SubId,new(source.X,source.Y),source.Var03)
        });

    public IRoomEntity CreatePlacedPart(RoomObjectRecord source,OracleRoomData room)=>_common.TryCreatePlacedPart(source,room)??
        throw new NotSupportedException($"{source.Source}: Seasons PART${source.Id:x2}:${source.SubId:x2} has no native construction binding.");

    public void ValidateDebugNativeInteractions(OracleRuntimeState memory)
    {
        // shortcut: the first-sword sequence has no event snapshot; add one before admitting active captures.
        for(int slot=2;slot<16;slot++)
        {
            int address=((0xd0+slot)<<8)|0x40;
            if(memory.ReadWramByte(address)!=0 && memory.ReadWramByte(address+1)==0xc6 && memory.ReadWramByte(address+4)>=2)
                throw new NotSupportedException($"herosCaveSwordChest.s: INTERAC$c6 slot${slot:x2} sequence has no transient debug reconstruction.");
        }
    }

    public void UpdateSeedTreeRefillState(int group, int room)
    {
        if (group == 0) _refills.UpdateRefillState(session.Memory, group, room);
    }
    public IRoomEntity? CreatePlacedEnemy(EnemyHandlerDescriptor handler, RoomObjectRecord source,
        OracleRoomData room, Vector2 position, int instance, int killableIndex, EnemyPlacementContext placement) =>
        _common.TryCreateEnemy(handler,source,room,position,instance,killableIndex) ??
        throw new NotSupportedException($"{source.Source}: {handler.Source}: Seasons enemy ${source.Id:x2}:${source.SubId:x2} has no imported construction binding.");
}
