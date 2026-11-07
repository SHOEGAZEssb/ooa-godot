
namespace Oracles;

// Native content factories delegate common requests here. The manager retains
// allocation, slot order, updates and retirement for both games.
internal sealed class CommonRoomEntityFactory(RoomEntityManager owner, Func<SwordBeamDatabase>? swordBeamSource = null,
    RoomSession? rooms = null, Action<RickyCompanionRoomEntity,RoomEntityFrame>? rickyScriptedState = null,
    Action<DimitriCompanionRoomEntity,RoomEntityFrame,System.Collections.Generic.ICollection<RoomEntitySpawn>>? dimitriScriptedState = null)
{
    private string AssetRoot => owner.AssetRoot;
    private SwordBeamDatabase? _swordBeam;
    private BreakableTileDatabase? _breakables;
    private ZoraFireDatabase? _zoraFire;
    private AnimatedPartDatabase? _beamosBeam;
    private TreasureDatabase? _treasures;
    private DimitriDatabase? _dimitri;
    private MooshDatabase? _moosh;
    private ExclamationMarkDatabase? _exclamation;
    private RickyDatabase? _ricky;
    private LedgeJumpDatabase? _ledgeJumps;
    private DungeonSpinnerVisualDatabase? _spinner;
    private DungeonPuzzleDatabase? _puzzles;
    private BrotherGoriyaDatabase? _brotherGoriyas;
    private DungeonEntranceInteractionDatabase? _dungeonEntrances;
    private DungeonEntranceInteractionDatabase DungeonEntrances => _dungeonEntrances ??= new(AssetRoot);
    private DungeonPuzzleDatabase Puzzles => _puzzles ??= new(AssetRoot);

    internal DungeonSpinnerRoomEntity CreateSpinner(DungeonObjectRecord record) =>
        new(record, owner.RuntimeState, (_spinner ??= new(AssetRoot)).Visual, owner.OnSoundRequested, owner.BeginScreenShake);

    internal DungeonSpinnerRoomEntity CreateSpinner(DungeonSpinnerPlacement placement) =>
        new(placement, owner.RuntimeState, (_spinner ??= new(AssetRoot)).Visual, owner.OnSoundRequested, owner.BeginScreenShake);

    internal EnemyClearChestRoomEntity CreateEnemyClearChest(DungeonMechanicDatabaseRecord record)=>
        new(record,Puzzles,owner,rooms??throw new InvalidOperationException("INTERAC$12:$02 requires its active room session."));

    internal DungeonTriggerChestScriptRoomEntity CreateTriggerChest(NativeObjectInstruction source,OracleRoomData room)=>
        new(new(source.X,source.Y),owner.RuntimeState,1,Puzzles.ChestPuffWait,()=>owner.ActiveTriggers,
            ()=>owner.SaveData?.HasRoomFlag(room.Group,room.Id,OracleSaveData.RoomFlagItem)==true,
            owner.IsScriptTextActive,owner.OnSoundRequested,()=>SetPuzzleTile(room,(byte)room.GetPackedPosition(new(source.X,source.Y)),ChestDatabase.ClosedTile));

    internal DungeonRewardRoomEntity CreateMinibossReward(NativeObjectInstruction source,OracleRoomData room)=>
        new(new(room.Group,room.Id,source.Address,DungeonObjectKind.MinibossReward,source.Id,source.SubId,source.Y,source.X,
            DungeonObjectCondition.Always,source.Source),DungeonRewardDatabase.For(AssetRoot),owner.RuntimeState,owner.SaveData,()=>owner.RoomEnemyCount,
            null,owner.EnableLinkCollisionsAndMenu,owner.TryCreateMinibossPortal,scriptTextActive:owner.IsScriptTextActive);

    internal DungeonRewardRoomEntity CreateBossReward(NativeObjectInstruction source,OracleRoomData room)
    {
        var data=DungeonRewardDatabase.For(AssetRoot);
        var record=new DungeonObjectRecord(room.Group,room.Id,source.Address,DungeonObjectKind.BossReward,
            source.Id,source.SubId,88,120,DungeonObjectCondition.Always,source.Source);
        var treasure=new GroundTreasureGrantRequest(room.Group,room.Id,source.Address,88,120,"TREASURE_OBJECT_HEART_CONTAINER_00",source.Source)
        {SpawnMode=TreasureSpawnMode.Puff,GrabMode=TreasureGrabMode.TwoHands,SpawnDelayFrames=data.TreasurePuffWait,ExpectedTreasureId=0x2a,ExpectedSubId=0};
        return new(record,data,owner.RuntimeState,owner.SaveData,()=>owner.RoomEnemyCount,treasure,
            owner.EnableLinkCollisionsAndMenu,trySpawnTreasure:owner.TryCreateGroundTreasure,scriptTextActive:owner.IsScriptTextActive);
    }

    internal IRoomEntity? TryCreateStreamInteraction(NativeObjectInstruction source, OracleRoomData room, EnemyPlacementContext placement,LightableTorchState torches) =>
        source switch
        {
            {Id:InteractionId.SwitchTileToggler}=>CreateSwitchTileToggler(new(room.Group,room.Id,source.Address,
                DungeonObjectKind.SwitchTileToggler,source.Id,source.SubId,source.Y,source.X,DungeonObjectCondition.Always,source.Source),room),
            {Id:0x7e,SubId:0}=>CreateMinibossPortal(new(room.Group,room.Id,source.Address,
                DungeonEntranceInteractionDatabaseObjectKind.MinibossPortal,source.Id,source.SubId,source.Y,source.X,
                RequireActiveRoom(room).CurrentDungeonIndex,source.Source)),
            {Id:InteractionId.DungeonStuff,SubId:1}=>CreateEnemySmallKey(source,room),
            {Id:InteractionId.DungeonStuff,SubId:2}=>CreateEnemyClearChest(
                StreamMechanic(source,room,room.GetPackedPosition(new(source.X,source.Y)))),
            { Id:InteractionId.StatueEyeball, SubId:1 } =>
                new StatueEyeballSpawnerRoomEntity(room,DungeonEntrances,owner.TryCreateStatueEyeball),
            { Id:0x7d, SubId:0 or 1 } => CreateSpinner(new DungeonObjectRecord(room.Group, room.Id, 0, DungeonObjectKind.Spinner, source.Id, source.SubId,
                source.Y, source.X, DungeonObjectCondition.Always, source.Source, source.Var03)),
            { Id:0x9f, SubId:0 } => CreateExclamation(new(new(source.X,source.Y),Duration:0,PlaySound:false),room),
            { Id:0x05, SubId:0 } => CreatePuff(new(new(source.X,source.Y),SoundId.SndPoof)),
            { Id:0x08, SubId:0 } => CreateKillPuff(new(new(source.X,source.Y))),
            { Id:InteractionId.PushBlockTrigger, SubId:1 } => new PushBlockTriggerRoomEntity(
                StreamMechanic(source,room,room.GetPackedPosition(new(source.X,source.Y))),room,Puzzles,
                ()=>owner.RoomEnemyCount,owner.AnimationTick,owner.ClearRoomEnemyCount,owner.IsOutgoingEntity),
            {Id:InteractionId.CreateObjectAtEachTileIndex,SubId:8}=>new LightableTorchScannerRoomEntity(
                StreamMechanic(source,room,source.Y),room,torches,LightableTorchDatabase.For(AssetRoot),owner.TryCreateLightableTorch),
            { Id:InteractionId.DoorController, SubId:>=4 and <=0x0b or >=0x14 and <=0x17 } => new DungeonDoorRoomEntity(
                StreamMechanic(source,room,source.Y),room,Puzzles,()=>owner.RoomEnemyCount,owner.TriggerIsActive,
                owner.ToScreen,owner.AnimationTick,owner.OnSoundRequested,placement,true,
                (position,tile)=>SetPuzzleTile(room,position,tile),owner.UpdateBossShutterSignal,
                owner.IsScriptTextActive,owner.IsOutgoingEntity,()=>owner.DoorPaletteFadeActive,()=>torches.LitCount),
            { Id:InteractionId.DungeonStuff, SubId:4 } => new EnemyClearStairsRoomEntity(
                StreamMechanic(source,room,0),room,Puzzles,owner.SaveData ?? throw new InvalidOperationException("Dungeon stairs require their save owner."),()=>owner.RoomEnemyCount,
                owner.AnimationTick,owner.OnSoundRequested,owner.TryCreatePuzzlePuff),
            _ => null
        };

    internal MinibossPortalRoomEntity CreateMinibossPortal(PlacementRecord placement)
    {
        var portal=new MinibossPortal();portal.Initialize(DungeonEntrances);
        return new(portal,placement,DungeonEntrances,owner.SaveData,owner.OnRoomWarpRequested,
            owner.OnSoundRequested,owner.IsOutgoingEntity,owner.RuntimeState);
    }

    internal IEnumerable<IRoomEntity> CreateStaticRoomEntities(OracleRoomData room)
    {
        if(!owner.ScreenTransitionActive && owner.SpecialObjectPresentationAvailable &&
            MinecartRuntimeState.TryGetRide(owner.RuntimeState,room.Id,out _))yield return CreateMinecartBody(room);
    }

    internal IRoomEntity CreateStaticObject(StaticObjectRecord source,OracleRoomData room)=>
        source.Type==3 && (source.Id&0x7f)==0x16
            ?CreateMinecartInteraction(new(new(source.X,source.Y),source.Slot,source.SubId,(source.Id&0x80)!=0?3:1),room)
            :throw new NotSupportedException($"{source.Source}: {AssetRoot} static type${source.Type:x2}, ID${source.Id:x2}:${source.SubId:x2} has no content binding.");

    private MinecartInteractionRoomEntity CreateMinecartInteraction(MinecartInteractionSpawn spawn,OracleRoomData room)=>
        new(spawn,room,owner.RuntimeState,MinecartData.For(AssetRoot).Visual,owner);

    private MinecartRoomEntity CreateMinecartBody(OracleRoomData room)
    {
        var active=CompanionRuntimeState.Read(owner.RuntimeState);
        return new(new(-1,room.Id,active.Y,active.X,active.Direction,true),room,owner.RuntimeState,
            MinecartData.For(AssetRoot).Visual,owner.OnSoundRequested,owner);
    }

    internal IEnumerable<IRoomEntity> CreateMinecartShutterControllers(OracleRoomData room,EnemyPlacementContext placement)
    {
        // tileSubstitutions.s gates the caller by group; commonTileSubstitutions.s
        // additionally checks TILESETFLAG_DUNGEON only in Ages.
        if(room.Group<4 || owner.RuntimeState.Game==OracleGame.Ages &&
            (room.Group>5 || (room.TilesetFlags&(int)TilesetFlags.Dungeon)==0))yield break;
        for(int y=room.HeightInTiles-1;y>=0;y--)
        for(int x=room.WidthInTiles-1;x>=0;x--)
        {
            int packed=(y<<4)|x;
            if(packed==0)continue;
            int tile=room.GetOriginalMetatile(new(x*16+8,y*16+8));
            if(tile is <DungeonShutterEntry.FirstMinecartShutterTile or >DungeonShutterEntry.LastMinecartShutterTile)continue;
            if(placement.Kind==EnemyPlacementEntryKind.Scrolling &&
                !DungeonShutterEntry.FacesScrollingEntry(placement,tile-DungeonShutterEntry.FirstMinecartShutterTile))continue;
            if(owner.InteractionSlotAvailable)yield return CreateMinecartShutter(packed,tile,false,room);
        }
    }

    private MinecartShutterRoomEntity CreateMinecartShutter(int packed,int tile,bool opener,OracleRoomData room)=>
        new(packed,tile,opener,room,Puzzles,owner.ToScreen,owner.AnimationTick,owner.OnSoundRequested,
            (position,replacement)=>SetPuzzleTile(room,position,replacement),owner.UpdateBossShutterSignal,
            ()=>owner.DoorPaletteFadeActive,owner.IsScriptTextActive);

    private DungeonRewardRoomEntity CreateEnemySmallKey(NativeObjectInstruction source,OracleRoomData room)
    {
        var record=new DungeonObjectRecord(room.Group,room.Id,source.Address,DungeonObjectKind.EnemySmallKey,
            source.Id,source.SubId,source.Y,source.X,DungeonObjectCondition.ItemClear,source.Source);
        return new(record,DungeonRewardDatabase.For(AssetRoot),owner.RuntimeState,owner.SaveData,()=>owner.RoomEnemyCount,
            FallingSmallKey(record),owner.EnableLinkCollisionsAndMenu,scriptTextActive:owner.IsScriptTextActive);
    }

    internal static GroundTreasureGrantRequest FallingSmallKey(DungeonObjectRecord record)=>
        new(record.Group,record.Room,record.Order,record.Y,record.X,"TREASURE_OBJECT_SMALL_KEY_01",record.Source)
        {
            SpawnMode=TreasureSpawnMode.FromScreenTop,GrabMode=TreasureGrabMode.NoChange,SpawnDelayFrames=40,
            BounceCount=2,Gravity=0x10,BounceSpeed=-0xaa,SpawnSound=SoundId.SndSolvePuzzle,
            LandingSound=SoundId.SndDropEssence,InitialZAboveScreen=true,
            ExpectedTreasureId=0x30,ExpectedSubId=1
        };

    private static DungeonMechanicDatabaseRecord StreamMechanic(NativeObjectInstruction source,OracleRoomData room,int position) =>
        new(room.Group,room.Id,source.Address,source.Id,source.SubId,position,source.X,TriggerPredicate.None,true);

    internal DungeonSwitchRoomEntity CreateDungeonSwitch(DungeonMechanicDatabaseRecord record,OracleRoomData room)=>
        new(record,room,DungeonSwitchDatabase.For(AssetRoot),owner.RuntimeState,owner.AnimationTick,
            owner.OnRoomTileChanged,owner.OnSoundRequested,(position,tile)=>SetPuzzleTile(room,position,tile),owner.SaveData);

    internal SwitchTileTogglerRoomEntity CreateSwitchTileToggler(DungeonObjectRecord record,OracleRoomData room)=>
        new(record,DungeonSwitchDatabase.For(AssetRoot),owner.RuntimeState,owner.OnRoomTileChanged,
            (position,tile)=>SetPuzzleTile(room,position,tile));

    private RoomSession RequireActiveRoom(OracleRoomData room) => rooms is not null && ReferenceEquals(rooms.CurrentRoom,room)
        ?rooms:throw new InvalidOperationException($"{AssetRoot}: native tile mutation requires its active room session.");

    private bool SetPuzzleTile(OracleRoomData room,byte position,byte tile)
    {
        bool changed=RequireActiveRoom(room).TrySetTile(position,tile);
        if(changed)owner.OnRoomTileChanged();
        return changed;
    }

    internal IRoomEntity? TryCreatePlacedPart(RoomObjectRecord source,OracleRoomData room) => source.Id==0x05
        ?CreateDungeonSwitch(new(source.Group,source.Room,source.Order,source.Id,source.SubId,source.PackedPosition,
            0,TriggerPredicate.None,true),room):source.Id==PartId.Button
        ?new GroundButtonRoomEntity(new(source.Group,source.Room,source.Order,source.Id,source.SubId,source.PackedPosition,
            0,TriggerPredicate.None,true),room,Puzzles,owner.SetTrigger,
            (position,tile)=>SetPuzzleTile(room,position,tile),owner.OnSoundRequested)
        :null;

    internal bool CanReconstructCompanion(OracleRuntimeState memory)
    {
        // shortcut: offscreen projection and rider $32 retain unsaved presentation history; admit them when that history is captured.
        int id=memory.ReadWramByte(0xd101),state=memory.ReadWramByte(0xd104),link=memory.ReadField("wLinkObjectIndex");
        return id is >= SpecialObjectId.Ricky and <= SpecialObjectId.Moosh && state is 1 or 5 &&
            memory.ReadWramByte(0xd105)==0 && memory.ReadWramByte(0xd10e)==0 && memory.ReadWramByte(0xd10f)==0 &&
            memory.ReadWramByte(0xd10b)<248 && memory.ReadWramByte(0xd10d)<248 &&
            memory.ReadWramByte(0xd108)<4 && link is 0xd0 or 0xd1 && (state==5)==(link==0xd1) &&
            (state!=5 || (memory.ReadWramByte(0xd121)&0x3f)<0x32) &&
            CompanionNativeAnimationDatabase.For(AssetRoot).CanRestorePresentation(memory);
    }

    internal IRoomEntity? TryCreate(RoomEntitySpawn spawn, OracleRoomData room) => spawn switch
    {
        BossDeathExplosionSpawn explosion => CreateBossDeathExplosion(explosion),
        BossShadowSpawn shadow => CreateBossShadow(shadow),
        AquamentusSpawn child=>CreateAquamentus(new(AssetRoot,SeasonsEnemyId.Aquamentus,child.SubId,0xc8,2,0,
            "object_code/seasons/enemies/aquamentus.s:aquamentus_state_spawner"),room,child.Parent.Position.Floor(),child.Parent),
        AquamentusProjectileSpawn projectile=>new AquamentusProjectileRoomEntity(projectile,AquamentusDatabase.For(AssetRoot).Projectile,owner),
        MinibossPortalSpawn => CreateMinibossPortal(new(room.Group,room.Id,0,
            DungeonEntranceInteractionDatabaseObjectKind.MinibossPortal,0x7e,0,0,0,RequireActiveRoom(room).CurrentDungeonIndex,
            "dungeonScript_minibossDeath:spawninteraction INTERAC_MINIBOSS_PORTAL,$00,$00,$00")),
        BrotherGoriyaSpawn brother => CreateBrotherGoriya(
            new(AssetRoot,SeasonsEnemyId.BrotherGoriyas,1,ObjectCollisionDatabase.For(AssetRoot).EnemyMode(SeasonsEnemyId.BrotherGoriyas),1,0,
                "brotherGoriyas.s:@state0 ecom_spawnEnemyWithSubid01"),room,brother.Parent.Position.Floor(),brother.Parent),
        BrotherGoriyaBoomerangSpawn boomerang => new BrotherGoriyaBoomerangRoomEntity(boomerang,
            (_brotherGoriyas??=new(AssetRoot)).Boomerang,owner),
        MoblinBoomerangSpawn boomerang=>new MoblinBoomerangRoomEntity(new MoblinBoomerangProjectile(
            boomerang.Owner,room,boomerang.Position,boomerang.Angle,BoomerangMoblinDatabase.For(AssetRoot).Visual,owner.RuntimeState)
            {Name="MoblinBoomerang",ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex}),
        MinecartBodySpawn=>CreateMinecartBody(room),
        DungeonSwitchSpawn button=>CreateDungeonSwitch(button.Record,room),
        MinecartInteractionSpawn cart=>CreateMinecartInteraction(cart,room),
        MinecartShutterOpenSpawn shutter=>CreateMinecartShutter(shutter.PackedPosition,shutter.ClosedTile,true,room),
        LightableTorchSpawn torch=>new LightableTorchRoomEntity(torch.State,torch.PackedPosition,
            LightableTorchDatabase.For(AssetRoot),owner.OnSoundRequested,(position,tile)=>SetPuzzleTile(room,position,tile)),
        StatueEyeballSpawn { Puzzle:false } eye => CreateStatueEyeball(eye.Position),
        EraOrSeasonInfoSpawn => CreateRoomInfo(),
        DungeonKeyUseSpawn key => CreateDungeonKeyUse(key),
        OverworldKeyUseSpawn key => CreateOverworldKeyUse(key),
        GhiniSpawn ghini => CreateNativeEnemy(new(AssetRoot,EnemyId.Ghini,ghini.SubId,
            ObjectCollisionDatabase.For(AssetRoot).EnemyMode(EnemyId.Ghini),0,0,ghini.Source),room,ghini.Position,ghini.Name),
        RickyCompanionSpawn ricky => CreateRicky(ricky,room),
        RickyPunchAttackSpawn punch => new RickyPunchAttackRoomEntity(punch,(_ricky??=new(AssetRoot)).Behavior,
            CreateCompanionTileBreaker(punch.Group,punch.Room,room)),
        RickyTornadoSpawn tornado => new RickyTornadoRoomEntity(tornado,(_ricky??=new(AssetRoot)).Behavior,room,
            CreateCompanionTileBreaker(tornado.Group,tornado.Room,room),owner.RuntimeState),
        RickyTileBreakSpawn tileBreak => new RickyTileBreakRoomEntity(tileBreak,CreateCompanionTileBreaker(tileBreak.Group,tileBreak.Room,room)),
        DimitriCompanionSpawn dimitri => CreateDimitri(dimitri,room),
        DimitriMouthSpawn mouth => new DimitriMouthRoomEntity(CreateCompanionTileBreaker(mouth.Group,room.Id,room),_dimitri??=new(AssetRoot),owner.RuntimeState),
        MooshCompanionSpawn moosh => CreateMoosh(moosh,room),
        MooshStompAttackSpawn stomp => CreateMooshStomp(stomp,room),
        ExclamationMarkSpawn mark => CreateExclamation(mark,room),
        MooshHoverExclamationSpawn mark => CreateMooshHoverExclamation(mark,room),
        GroundTreasureRecordSpawn treasure => CreateGroundTreasure(treasure,room),
        GroundTreasureGrantSpawn treasure => CreateGroundTreasure(new(treasure.Request.Resolve(_treasures ??=new(AssetRoot)),0),room,hideUntilFirstUpdate:true),
        GelSpawn gel => CreateNativeEnemy(new(AssetRoot,EnemyId.Gel,0,
            ObjectCollisionDatabase.For(AssetRoot).EnemyMode(EnemyId.Gel),0,gel.KillableEnemyIndex,"zol.s:split"),room,gel.Position,gel.Name),
        GrassDebrisSpawn grass => CreateGrass(grass),
        RockDebrisSpawn rock => CreateRock(rock),
        EnemySplashSpawn splash => CreateSplash(splash),
        SwordBeamSpawn beam => CreateBeam(beam,room),
        SwordBeamClinkSpawn clink => CreateClink(clink.Position,true,0,"SwordBeamClink",false),
        SwordWallClinkSpawn clink => CreateClink(clink.Position,clink.Flickers,clink.ZHigh,"SwordWallClink",clink.Flickers),
        EnemyClinkSpawn clink => CreateEnemyClink(clink),
        ItemDropSpawn drop => CreateDrop(drop, room),
        FallingDownHoleSpawn hole => CreateHole(hole),
        EnemyDeathPuffSpawn puff => CreateDeathPuff(puff),
        BombSpawn bomb => CreateBomb(bomb,room),
        BombchuSpawn bombchu => CreateBombchu(bombchu,room),
        BoomerangSpawn boomerang => new BoomerangRoomEntity(new BoomerangItem(room, boomerang.Position,
            boomerang.Angle, boomerang.ZHigh, () => RingEffects.BoomerangDamage(BoomerangDatabase.For(AssetRoot, boomerang.SubId).Damage,
                owner.Inventory ?? throw new InvalidOperationException("ITEM$06 requires the live inventory ring owner.")),
            owner.OnSoundRequested, owner.TryCreateItemCollisionClink, boomerang.SubId, boomerang.ParentSlot, boomerang.BreakTile)
            { Name = "Boomerang", ZIndex = ObjectDrawPriority.BehindLinkZIndex }),
        EmberSeedSpawn seed => CreateSeed(seed,room),
        BurningEnemySpawn burn => CreateBurningEnemy(burn),
        BeamosBeamSpawn beam => new BeamosBeamRoomEntity(new BeamosBeamPart(beam,
            _beamosBeam ??= AnimatedPartDatabase.Beam(AssetRoot), room, owner.RuntimeState)),
        OctorokRockSpawn rock => CreateRock(rock, room),
        EnemyArrowSpawn arrow => CreateArrow(arrow, room),
        PuzzlePuffSpawn puff => CreatePuff(puff),
        KillEnemyPuffSpawn puff => CreateKillPuff(puff),
        SnowDebrisSpawn snow => CreateSnow(snow),
        ShovelDebrisSpawn shovel => CreateShovelDebris(shovel),
        SideScrollBubbleSpawn bubble => new SideScrollBubbleRoomEntity(bubble.Position,room,owner.Random,owner.RuntimeState),
        DiggingEnemySpawn dug => CreateNativeEnemy(owner.DiggingEnemies.Enemy(dug.Roll,dug.SubId,room.TilesetFlags),
            room,dug.Position,$"DugEnemy_{dug.SubId:x2}"),
        ZoraFireSpawn fire => new ZoraFireRoomEntity(new ZoraFireProjectile(fire,_zoraFire ??= new(AssetRoot),
            owner.ToScreen,owner.RuntimeState) { Name = "ZoraFire", ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex }),
        _ => null
    };

    private IRoomEntity CreateStatueEyeball(Vector2 position)
    {
        var eye = new StatueEyeball();
        eye.Initialize(position,DungeonEntrances);
        return new StatueEyeballRoomEntity(eye);
    }

    private static IRoomEntity CreateOverworldKeyUse(OverworldKeyUseSpawn spawn)
    {
        var effect = new OverworldKeyUseEffect
        {
            Name = "OverworldKeyUse",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex // overworldKeySprite.s: visible80.
        };
        effect.Initialize(spawn.Position, spawn.Visual, spawn.Constants);
        return new DialogueFixedEffectRoomEntityAdapter<OverworldKeyUseEffect>(effect);
    }

    private IRoomEntity CreateDimitri(DimitriCompanionSpawn spawn,OracleRoomData room)
    {
        if(owner.RuntimeState.ReadWramByte(0xd104)==0)
            throw new InvalidOperationException($"dimitri.s: {owner.RuntimeState.Game} state$00 requires its game-native initialization owner.");
        return new DimitriCompanionRoomEntity(spawn,room,_dimitri??=new(AssetRoot),
            owner.SaveData??throw new InvalidOperationException("SPECIALOBJECT $0c requires the live save owner."),
            owner.RuntimeState,owner.OnSoundRequested,destination=>CreateCompanionTileBreaker(spawn.Group,destination.Id,destination),owner.ReinitializeCompanion,dimitriScriptedState);
    }

    internal RickyCompanionRoomEntity CreateRicky(RickyCompanionSpawn spawn,OracleRoomData room) => new(
        spawn,room,_ricky??=new(AssetRoot),owner.SaveData,owner.RuntimeState,owner.Random,
        _ledgeJumps??=new(AssetRoot),owner.OnSoundRequested,owner.ReinitializeCompanion,rickyScriptedState);

    private IRoomEntity CreateExclamation(ExclamationMarkSpawn spawn,OracleRoomData room)
    {
        var record=spawn.Record is { } custom?custom with {X=(int)spawn.Position.X,Y=(int)spawn.Position.Y}:
            (_exclamation??=new(AssetRoot)).CreateRecord(room,spawn.Position);
        var actor=NpcCharacter.CreateFromRecord(record,AssetRoot);
        actor.ZIndex=ObjectDrawPriority.FixedHighPriorityZIndex;
        if(spawn.PlaySound)owner.OnSoundRequested(SoundId.SndClink);
        return new ExclamationMarkRoomEntity(actor,spawn.Duration);
    }

    private IRoomEntity CreateMoosh(MooshCompanionSpawn spawn, OracleRoomData room)
    {
        if (owner.RuntimeState.ReadWramByte(0xd104) is 0 or 0x0a)
            throw new NotSupportedException($"moosh.s: {owner.RuntimeState.Game} state${owner.RuntimeState.ReadWramByte(0xd104):x2} requires its native initialization/story binding.");
        return new MooshCompanionRoomEntity(spawn, room, _moosh ??= new(AssetRoot), owner.SaveData,
            owner.RuntimeState, owner.OnSoundRequested, owner.SetVerticalScreenShake,
            destination => CreateCompanionTileBreaker(spawn.Group,destination.Id,destination), owner.ReinitializeCompanion);
    }

    private IRoomEntity CreateMooshStomp(MooshStompAttackSpawn spawn, OracleRoomData room)
    {
        int? Neighbor(Vector2I direction) => rooms is not null &&
            rooms.TryGetNeighbor(spawn.Group, spawn.Room, direction, out int neighbor) ? neighbor : null;
        return new MooshStompAttackRoomEntity(spawn, room, _breakables ??= new(AssetRoot), owner.SaveData,
            Neighbor, owner.OnRoomTileChanged, owner.AnimationTick, owner.OnSoundRequested,
            drop => owner.ItemDrops.DecideBreakableDrop(drop, owner.Random, owner.Inventory, owner.SaveData));
    }

    private IRoomEntity CreateMooshHoverExclamation(MooshHoverExclamationSpawn spawn,OracleRoomData room)
    {
        var data=_moosh??=new(AssetRoot);
        var position=spawn.Position+new Vector2(0,(spawn.ZFixed>>8)+data.WaterExclamationZOffset);
        var actor=NpcCharacter.CreateFromRecord((_exclamation??=new(AssetRoot)).CreateRecord(room,position),AssetRoot);
        actor.Name="MooshHoverExclamation";actor.ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex;
        // mooshState8Substate1 allocates directly, without objectCreateExclamationMark's cue.
        return new ExclamationMarkRoomEntity(actor,data.WaterHoverFrames);
    }

    private CompanionAttackTileBreaker CreateCompanionTileBreaker(int group,int roomId,OracleRoomData room)
    {
        int? Neighbor(Vector2I direction)=>rooms is not null && rooms.TryGetNeighbor(group,roomId,direction,out int neighbor)?neighbor:null;
        return new(group,room,_breakables??=new(AssetRoot),owner.SaveData,Neighbor,owner.OnRoomTileChanged,
            owner.AnimationTick,owner.OnSoundRequested,
            drop=>owner.ItemDrops.DecideBreakableDrop(drop,owner.Random,owner.Inventory,owner.SaveData));
    }

    private IRoomEntity CreateGroundTreasure(GroundTreasureRecordSpawn spawn,OracleRoomData room,bool hideUntilFirstUpdate=false)
    {
        var treasure=new GroundTreasurePickup {Name=$"GroundTreasure_{spawn.Record.Order}",ZIndex=ObjectDrawPriority.FixedHighPriorityZIndex};
        treasure.Initialize(AssetRoot,spawn.Record,owner.OnSoundRequested,owner.ToScreen,room,owner.TryCreatePuzzlePuff);
        if(hideUntilFirstUpdate)treasure.Visible=false;
        int id=(_treasures ??=new(AssetRoot)).GetObject(spawn.Record.TreasureObject).TreasureId;
        return new GroundTreasureRoomEntity(treasure,owner.RuntimeState,id,spawn.ZHigh,owner.CanCollectGroundTreasure,owner.OnGroundTreasureCollected);
    }

    private IRoomEntity CreateDungeonKeyUse(DungeonKeyUseSpawn spawn)
    {
        var effect = new DungeonKeyUseEffect
        {
            Name = "DungeonKeyUse",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex // dungeonKeySprite.s: visible80.
        };
        effect.Initialize(spawn.Position, spawn.Visual,
            () => owner.OnSoundRequested(SoundId.SndGetSeed));
        return new DungeonKeyUseRoomEntity(effect);
    }

    private IRoomEntity CreateBossDeathExplosion(BossDeathExplosionSpawn spawn)
    {
        var effect=new BossDeathExplosionEffect{Name="BossDeathExplosion",ZIndex=ObjectDrawPriority.FixedHighPriorityZIndex};
        effect.Initialize(AssetRoot,spawn.Position,spawn.BossId,owner.OnSoundRequested,spawn.ZHigh,spawn.NativeSubid);
        return new BossDeathExplosionRoomEntity(effect,owner.ItemDrops,owner.Random,owner.Inventory,owner.SaveData,()=>owner.RoomEnemyCount);
    }

    private IRoomEntity CreateBossShadow(BossShadowSpawn spawn)
    {
        var shadow=new BossShadowEffect{Name="BossShadow",ZIndex=ObjectDrawPriority.FixedLowPriorityZIndex};
        shadow.Initialize(AssetRoot,spawn.ParentPosition,spawn.ParentZ,spawn.ParentExists,spawn.Size,spawn.YOffset);
        return new BossShadowRoomEntity(shadow);
    }

    private AquamentusRoomEntity CreateAquamentus(EnemyCombatSourceDescriptor source,OracleRoomData room,Vector2 position,AquamentusCharacter? parent=null)
    {
        if(owner.RuntimeState.Game!=OracleGame.Seasons)throw new NotSupportedException($"{source.Source}: Aquamentus is a Seasons ENEMY$78 binding.");
        if(source.SubId!=0&&parent is null)throw new NotSupportedException($"{source.Source}: Aquamentus $78:${source.SubId:x2} requires its linked physical spawner.");
        var actor=new AquamentusCharacter(parent){Name=$"Aquamentus_{source.SubId:x2}"};
        actor.Initialize(EnemyDefinitionDatabase.For(AssetRoot).Get(source.Id,source.SubId),position,owner,source.CountsAsEnemy);
        return new(actor,source,owner);
    }

    private BrotherGoriyaRoomEntity CreateBrotherGoriya(EnemyCombatSourceDescriptor source,OracleRoomData room,Vector2 position,
        BrotherGoriyaCharacter? parent=null)
    {
        if(owner.RuntimeState.Game!=OracleGame.Seasons)throw new NotSupportedException($"{source.Source}: Brother Goriya is a Seasons ENEMY$70 binding.");
        var actor=new BrotherGoriyaCharacter(parent){Name=$"BrotherGoriya_{source.SubId:x2}",ZIndex=ObjectDrawPriority.BehindLinkZIndex};
        actor.Initialize(EnemyDefinitionDatabase.For(AssetRoot).Get(source.Id,source.SubId),room,position,owner,_brotherGoriyas??=new(AssetRoot));
        return new(actor,source,owner.OnSoundRequested);
    }

    internal IRoomEntity? TryCreateEnemy(EnemyHandlerDescriptor handler, RoomObjectRecord source,
        OracleRoomData room, Vector2 position, int instance, int killableEnemyIndex)
    {
        if (!handler.SupportsOrderedConstruction) return null;
        if(handler.Handler==EnemyHandlerKind.Aquamentus)
            return CreateAquamentus(handler.CombatSource(source,killableEnemyIndex),room,position);
        if(handler.Handler==EnemyHandlerKind.Floormaster)
        {
            FloormasterRoomEntity? adapter=null;
            var floor=new FloormasterCharacter{Name=$"Floormaster_{source.Order}_{instance}"};
            floor.Initialize(EnemyDefinitionDatabase.For(AssetRoot).Get(source.Id,source.SubId),room,position,owner.Random,
                spawn:()=>owner.TryAllocateEnemy(_=>CreateFloormasterChild(adapter!,room)) is not null);
            adapter=new(floor,handler.CombatSource(source,killableEnemyIndex),owner.OnSoundRequested,
                ()=>owner.Random.Next().Value,()=>owner.PartSlotAvailable);
            return adapter;
        }
        if(handler.Handler==EnemyHandlerKind.BrotherGoriyas)
            return CreateBrotherGoriya(handler.CombatSource(source,killableEnemyIndex),room,position);
        if(handler.Handler==EnemyHandlerKind.BoomerangMoblin)
        {
            var moblin=new BoomerangMoblinCharacter{Name=$"BoomerangMoblin_{source.Order}_{instance}",ZIndex=ObjectDrawPriority.BehindLinkZIndex};
            moblin.Initialize(EnemyDefinitionDatabase.For(AssetRoot).Get(source.Id,source.SubId),room,position,owner.Random);
            return new BoomerangMoblinRoomEntity(moblin,handler.CombatSource(source,killableEnemyIndex),owner.OnSoundRequested);
        }
        if(handler.Handler==EnemyHandlerKind.BladeTrap)
        {
            var trap=new BladeTrapCharacter{Name=$"BladeTrap_{source.Order}_{instance}",ZIndex=ObjectDrawPriority.BehindLinkZIndex};
            trap.Initialize(EnemyDefinitionDatabase.For(AssetRoot).Get(source.Id,source.SubId),room,position,owner.Random,owner.OnSoundRequested);
            return new BladeTrapRoomEntity(trap,handler.CombatSource(source,killableEnemyIndex),owner.OnSoundRequested);
        }
        if (handler is {Handler:EnemyHandlerKind.Stalfos,SubId:0})
        {
            var stalfos=new StalfosCharacter{Name=$"Stalfos_{source.SubId:x2}_{source.Order}_{instance}",ZIndex=ObjectDrawPriority.BehindLinkZIndex};
            stalfos.Initialize(StalfosDatabase.For(AssetRoot).Get(0),room,position,owner.Random,owner.OnSoundRequested);
            return new StalfosRoomEntity(stalfos,handler.CombatSource(source,killableEnemyIndex),owner.OnSoundRequested,()=>owner.PartSlotAvailable);
        }
        if (handler.Handler == EnemyHandlerKind.ArrowMoblin)
        {
            var moblin = new ArrowMoblinCharacter { Name = $"Archer_{source.Id:x2}_{source.SubId:x2}_{source.Order}_{instance}", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
            moblin.Initialize(EnemyDefinitionDatabase.For(AssetRoot).Get(source.Id,source.SubId), room, position, owner.Random);
            return new ArrowMoblinRoomEntity(moblin,handler.CombatSource(source,killableEnemyIndex),owner.OnSoundRequested);
        }
        if (handler.Handler == EnemyHandlerKind.Octorok)
        {
            if (!OctorokDatabase.For(AssetRoot).TryGetDefinition(source, out var octorokRecord))
                throw new InvalidOperationException($"{source.Source}: {handler.Source} requires a native Octorok definition.");
            var octorok = new OctorokCharacter { Name = $"Octorok_{source.SubId:x2}_{source.Order}_{instance}", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
            octorok.Initialize(octorokRecord, room, position, owner.Random);
            return new OctorokRoomEntity(octorok, handler.CombatSource(source,killableEnemyIndex), owner.OnSoundRequested);
        }
        if (handler.Handler == EnemyHandlerKind.Beamos)
        {
            var beamos = new BeamosCharacter { Name = $"Beamos_16_{source.Order}_{instance}", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
            beamos.Initialize(EnemyDefinitionDatabase.For(AssetRoot).Get(EnemyId.Beamos), room, position, owner.Random,
                owner.OnSoundRequested, () => owner.PartSlotAvailable, owner.AnimationTick);
            return new BeamosRoomEntity(beamos, (source.Flags & 2) == 0);
        }
        if (handler.Handler == EnemyHandlerKind.Ghini)
            return CreateNativeEnemy(handler.CombatSource(source,killableEnemyIndex),room,position,
                source.SubId==2?$"Ghini_{source.SubId:x2}_{source.Order}_{instance}":$"Ghini_{source.Order}_{instance}");
        if (handler.Handler is EnemyHandlerKind.Rope or EnemyHandlerKind.Beetle or EnemyHandlerKind.PodobooTower or EnemyHandlerKind.Gel or EnemyHandlerKind.Zol or EnemyHandlerKind.RiverZora)
            return CreateNativeEnemy(handler.CombatSource(source,killableEnemyIndex),room,position,
                $"Enemy_{source.Id:x2}_{source.SubId:x2}_{source.Order}_{instance}");
        if (handler.Handler != EnemyHandlerKind.Keese) return null;
        if (!KeeseDatabase.For(AssetRoot).TryGetDefinition(source,out var record))
            throw new InvalidOperationException($"{source.Source}: {handler.Source} requires a native Keese definition.");
        var character = new KeeseCharacter
        {
            Name = $"Keese_{source.SubId:x2}_{source.Order}_{instance}",
            ZIndex = ObjectDrawPriority.BehindLinkZIndex
        };
        character.Initialize(record,room,position,owner.Random);
        return new KeeseRoomEntity(character,handler.CombatSource(source,killableEnemyIndex),owner.OnSoundRequested);
    }

    private IRoomEntity CreateRock(OctorokRockSpawn spawn, OracleRoomData room)
    {
        var rock = new OctorokRockProjectile { Name = "OctorokRock", ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex };
        rock.Initialize(OctorokDatabase.For(AssetRoot).Projectile, room, spawn.Position, spawn.Angle);
        return new PostObjectHostileProjectileRoomEntity<OctorokRockProjectile>(rock);
    }

    private IRoomEntity CreateArrow(EnemyArrowSpawn spawn, OracleRoomData room)
    {
        var arrow = new EnemyArrowProjectile { Name = "EnemyArrow", ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex };
        arrow.Initialize(EnemyArrowDatabase.For(AssetRoot).Record,room,spawn.Position,spawn.Angle,spawn.SubId);
        return new EnemyArrowRoomEntity(arrow);
    }

    private IRoomEntity CreateNativeEnemy(EnemyCombatSourceDescriptor source, OracleRoomData room, Vector2 position, string name)
    {
        var record = EnemyDefinitionDatabase.For(AssetRoot).Get(source.Id,
            source.Id == EnemyId.PodobooTower ? 0 : source.SubId) with { SubId = source.SubId };
        switch (source.Id)
        {
            case EnemyId.RiverZora:
                var zora=new RiverZoraCharacter {Name=name,ZIndex=ObjectDrawPriority.FixedLowPriorityZIndex};
                zora.Initialize(record,room,position,owner.Random);
                return new RiverZoraRoomEntity(zora,source,owner.OnSoundRequested,()=>-owner.ToScreen(Vector2.Zero));
            case EnemyId.Gel:
                if(record.Sprites.Length!=1 || record.Animations.Length!=3 || !record.SourceGrayscaleInverted)
                    throw new InvalidOperationException($"{source.Source}: ENEMY_GEL requires one graphic and three native animations.");
                var gel=new GelCharacter {Name=name,ZIndex=ObjectDrawPriority.BehindLinkZIndex};
                gel.Initialize(record,room,position,owner.Random);
                return new GelRoomEntity(gel,source,owner.OnSoundRequested);
            case EnemyId.Zol:
                if(record.Sprites.Length!=1 || record.Animations.Length!=6 || !record.SourceGrayscaleInverted)
                    throw new InvalidOperationException($"{source.Source}: ENEMY_ZOL requires one graphic and six native animations.");
                var zol=new ZolCharacter {Name=name,ZIndex=ObjectDrawPriority.BehindLinkZIndex};
                zol.Initialize(record,room,position,owner.Random,owner.OnSoundRequested);
                return new ZolRoomEntity(zol,source,owner.OnSoundRequested);
            case EnemyId.Ghini:
                if(source.SubId==2)
                {
                    var target=new TargetGhiniCharacter {Name=name};
                    target.Initialize(record,room,position,owner.Random,owner.RuntimeState,KillSpawnedGhinis);
                    return new TargetGhiniRoomEntity(target,source,owner.OnSoundRequested,
                        ()=>owner.Random.Next().Value,()=>owner.PartSlotAvailable);
                }
                var ghini=new GhiniCharacter {Name=name,ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex};
                ghini.Initialize(record,room,position,owner.Random,KillSpawnedGhinis);
                return source.SubId==1
                    ? new SpawnGhiniRoomEntity(ghini,source,owner.OnSoundRequested,()=>owner.Random.Next().Value,()=>owner.PartSlotAvailable)
                    : new GhiniRoomEntity(ghini,source,owner.OnSoundRequested);
            case EnemyId.Rope:
                var rope = new RopeCharacter { Name = name, ZIndex = ObjectDrawPriority.BehindLinkZIndex };
                rope.Initialize(record,room,position,owner.Random,owner.OnSoundRequested,
                    () => -(int)owner.ToScreen(Vector2.Zero).Y);
                return new RopeRoomEntity(rope,source,owner.OnSoundRequested);
            case EnemyId.Beetle:
                var beetle = new BeetleCharacter { Name = name, ZIndex = ObjectDrawPriority.BehindLinkZIndex };
                beetle.Initialize(record,room,position,owner.Random,owner.DiggingEnemies.BeetleCounters,owner.OnSoundRequested);
                return new BeetleRoomEntity(beetle,source,owner.OnSoundRequested);
            case EnemyId.PodobooTower:
                var tower = new PodobooTowerCharacter { Name = name, ZIndex = ObjectDrawPriority.BehindLinkZIndex };
                tower.Initialize(record,position,owner.Random);
                return new PodobooTowerRoomEntity(tower,source,owner.OnSoundRequested);
            default: throw new NotSupportedException($"{source.Source}: native enemy ${source.Id:x2}:${source.SubId:x2} has no shared constructor.");
        }
    }

    private void KillSpawnedGhinis()
    {
        foreach(var ghini in owner.Entities<GhiniCharacter>())
            if(ghini.Record.SubId==1)ghini.KillFromParentDeath();
    }

    internal IRoomEntity? TryCreateStandaloneEnemy(EnemyHandlerDescriptor handler, int group,
        OracleRoomData room, Vector2 position, string origin)
    {
        // Native allocation bypasses placement RNG and is uncounted (flags $02).
        var source = new RoomObjectRecord(group,room.Id,0,RoomObjectKind.FixedEnemy,handler.Id,handler.SubId,
            2,1,(int)position.Y,(int)position.X,0,0xff) { SourceOverride = $"{origin} via {handler.Source}" };
        return TryCreateEnemy(handler,source,room,position,0,0);
    }

    private IRoomEntity CreateFloormasterChild(FloormasterRoomEntity parent,OracleRoomData room)
    {
        var spawner=(FloormasterCharacter)parent.Node;
        var child=new FloormasterCharacter{Name="FloormasterHand"};
        child.Initialize(EnemyDefinitionDatabase.For(AssetRoot).Get(EnemyId.Floormaster,spawner.ChildSubId),room,Vector2.Zero,owner.Random,
            parent:()=>parent.ResolveSlot(parent.Slot)?.Node as FloormasterCharacter ??
                throw new InvalidOperationException($"floorMaster.s: relatedObj1 slot ${parent.Slot:x2} lost its spawner."));
        var source=new EnemyCombatSourceDescriptor(AssetRoot,EnemyId.Floormaster,spawner.ChildSubId,
            ObjectCollisionDatabase.For(AssetRoot).EnemyMode(EnemyId.Floormaster),2,0,
            "object_code/common/enemies/floorMaster.s:ecom_spawnUncountedEnemyWithSubid01");
        return new FloormasterRoomEntity(child,source,owner.OnSoundRequested,()=>owner.Random.Next().Value,()=>owner.PartSlotAvailable);
    }

    private IRoomEntity CreateRoomInfo()
    {
        var display = new EraOrSeasonInfoDisplay();
        display.Initialize(EraOrSeasonInfoDatabase.For(AssetRoot),() => owner.ActiveRoom,owner.RuntimeState);
        return new EraOrSeasonInfoRoomEntity(display);
    }

    private IRoomEntity CreateDeathPuff(EnemyDeathPuffSpawn spawn)
    {
        var puff = new EnemyDeathPuffEffect { Name = "EnemyDeathPuff", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
        puff.Initialize(AssetRoot,spawn.Position,spawn.HighKnockback,spawn.EnemyId,spawn.ZHigh);
        owner.OnSoundRequested(SoundId.SndKillEnemy);
        return new DeathPuffRoomEntity(puff,owner.ItemDrops,owner.Random,owner.Inventory,owner.SaveData,
            spawn.DecrementsRoomCount,spawn.DropsItem);
    }

    private IRoomEntity CreateGrass(GrassDebrisSpawn spawn)
    {
        var effect = new GrassDebrisEffect
        {
            Name = spawn.InteractionId == InteractionId.RedGrassDebris ? "RedGrassDebris" : "GrassDebris",
            ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex
        };
        effect.Initialize(AssetRoot,spawn.Position,spawn.InteractionId,spawn.Flickers,spawn.Underwater,
            owner.OnSoundRequested,() => owner.RuntimeState.ReadField("wGrassAnimationModifier"));
        return new GrassDebrisRoomEntity(effect);
    }

    private IRoomEntity CreateRock(RockDebrisSpawn spawn)
    {
        var effect = new RockDebrisEffect
        {
            Name = spawn.InteractionId == InteractionId.RockDebris2 ? "RockDebris2" : "RockDebris",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
        };
        effect.Initialize(AssetRoot,spawn.Position,spawn.InteractionId,owner.OnSoundRequested);
        return new DialogueFixedEffectRoomEntityAdapter<RockDebrisEffect>(effect);
    }

    private IRoomEntity CreateSplash(EnemySplashSpawn spawn)
    {
        if (spawn.Hazard is not (HazardType.Water or HazardType.Lava))
            throw new InvalidOperationException($"Native splash cannot represent {spawn.Hazard}.");
        var effect = new SplashEffect
        {
            Name = spawn.Hazard == HazardType.Lava ? "EnemyLavaSplash" : "EnemyWaterSplash",
            ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex
        };
        effect.InitializeNative(AssetRoot,spawn.Position,spawn.Hazard,owner.OnSoundRequested);
        effect.SetPhysicsProcess(false);
        return new NativeSplashRoomEntity(effect);
    }

    private IRoomEntity CreateShovelDebris(ShovelDebrisSpawn spawn)
    {
        var debris = new ShovelDebrisEffect { Name = "ShovelDebris", ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex };
        debris.Initialize(AssetRoot, spawn.Position, spawn.Direction);
        return new ShovelDebrisRoomEntity(debris);
    }

    private IRoomEntity CreateSnow(SnowDebrisSpawn spawn)
    {
        var snow = new StationaryDebrisEffect { Name = "SnowDebris", ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex };
        snow.Initialize(AssetRoot, spawn.Position, SoundId.MusNone, spawn.Flickers, owner.OnSoundRequested,
            interactionId: InteractionId.SnowDebris);
        return new StationaryDebrisRoomEntity(snow);
    }

    private IRoomEntity CreateKillPuff(KillEnemyPuffSpawn spawn)
    {
        var puff=new KillEnemyPuffEffect {Name="KillEnemyPuff",ZIndex=ObjectDrawPriority.FixedHighPriorityZIndex};
        puff.Initialize(AssetRoot,spawn.Position,()=>owner.OnSoundRequested(SoundId.SndKillEnemy),spawn.ZHigh);
        return new KillPuffRoomEntity(puff);
    }

    private IRoomEntity CreatePuff(PuzzlePuffSpawn spawn)
    {
        var puff = new StationaryDebrisEffect { Name = "PuzzlePuff", ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex };
        puff.Initialize(AssetRoot, spawn.Position, spawn.Sound, spawn.Flickers, owner.OnSoundRequested,
            spawn.ZHigh, spawn.AlwaysUpdates);
        return new StationaryDebrisRoomEntity(puff);
    }

    private IRoomEntity CreateBomb(BombSpawn spawn, OracleRoomData room)
    {
        var inventory = owner.Inventory ?? throw new InvalidOperationException("ITEM_BOMB allocation requires live inventory.");
        if (spawn.Record.AssetRoot != AssetRoot || room.AssetRoot != AssetRoot || !ReferenceEquals(inventory,spawn.Player.Inventory))
            throw new InvalidOperationException("ITEM_BOMB allocation requires the selected pack and authoritative inventory.");
        var bomb = new BombEffect { Name = "Bomb", ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex };
        bomb.Initialize(spawn.Record,room,_breakables ??= new(AssetRoot),spawn.Player,spawn.Group,spawn.HeldExplosion,
            owner.OnSoundRequested,(position,hazard,kind) =>
            {
                if (owner.ActiveRoom.Group == spawn.Record.HazardGroup && owner.ActiveRoom.Id == spawn.Record.HazardRoom)
                    owner.RuntimeState.SetWramByte(spawn.Record.HazardSignal,1);
                if (hazard is HazardType.Water or HazardType.Lava) owner.OnItemDropEnteredHazard(position,hazard);
                else if (hazard == HazardType.Hole) owner.OnObjectFellInHole(kind);
            },owner.OnRoomTileChanged,owner.AnimationTick,
            drop => owner.ItemDrops.DecideBreakableDrop(drop,owner.Random,inventory,owner.SaveData),owner.SaveData,
            rooms is null ? null : direction => rooms.TryGetNeighbor(owner.ActiveRoom.Group,owner.ActiveRoom.Id,direction,out int neighbor) ? neighbor : null);
        return new BombRoomEntity(bomb);
    }

    private IRoomEntity CreateBombchu(BombchuSpawn spawn, OracleRoomData room)
    {
        var inventory = owner.Inventory ?? throw new InvalidOperationException("ITEM$0d allocation requires live inventory.");
        if (!ReferenceEquals(inventory,spawn.Player.Inventory) || room.AssetRoot != AssetRoot)
            throw new InvalidOperationException("ITEM$0d allocation requires the selected pack and authoritative inventory.");
        var item = new BombchuItem(AssetRoot) { Name = "Bombchu", ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex };
        item.Initialize(item.ExplosionRecord,room,_breakables ??= new(AssetRoot),spawn.Player,spawn.Group,
            _ => throw new InvalidOperationException("ITEM$0d cannot enter a held-Bomb explosion path."),
            owner.OnSoundRequested,(_,_,_) => throw new InvalidOperationException("ITEM$0d uses its common item-hazard caller."),
            owner.OnRoomTileChanged,owner.AnimationTick,
            drop => owner.ItemDrops.DecideBreakableDrop(drop,owner.Random,inventory,owner.SaveData),owner.SaveData,
            rooms is null ? null : direction => rooms.TryGetNeighbor(spawn.Group,room.Id,direction,out int neighbor) ? neighbor : null);
        item.Configure(room,spawn.Player,owner,spawn.Group,spawn.Position,spawn.ZFixed);
        return new BombchuRoomEntity(item);
    }

    private IRoomEntity CreateSeed(EmberSeedSpawn spawn, OracleRoomData room)
    {
        var inventory = owner.Inventory ?? throw new InvalidOperationException("Native seed allocation requires live inventory.");
        if (spawn.Record.AssetRoot != AssetRoot || room.AssetRoot != AssetRoot)
            throw new InvalidOperationException($"ITEM ${spawn.Record.SeedItem:x2} requires its selected native room/seed pack.");
        var seed = new EmberSeedEffect
        {
            Name = spawn.Record.SeedItem switch
            {
                ItemId.EmberSeed => "EmberSeed",
                ItemId.ScentSeed => "ScentSeed",
                ItemId.MysterySeed => "MysterySeed",
                _ => $"Seed_{spawn.Record.SeedItem:x2}"
            },
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
        };
        seed.Initialize(spawn.Record,room,_breakables ??= new(AssetRoot),spawn.LinkPosition,spawn.Direction,
            owner.OnSoundRequested,owner.OnItemDropEnteredHazard,owner.OnRoomTileChanged,owner.AnimationTick,
            drop => owner.ItemDrops.DecideBreakableDrop(drop,owner.Random,inventory,owner.SaveData),owner.SaveData,
            spawn.Group,rooms is null ? null : direction => rooms.TryGetNeighbor(spawn.Group,room.Id,direction,out int neighbor) ? neighbor : null,
            objectFellInHole:owner.OnObjectFellInHole,launchKind:spawn.LaunchKind,angle:spawn.Angle,linkZFixed:spawn.LinkZFixed,
            mysteryEffectSource:() => owner.Random.Next().Value);
        return new EmberSeedRoomEntity(seed);
    }

    private IRoomEntity CreateBurningEnemy(BurningEnemySpawn spawn)
    {
        var part = new BurningEnemyPart { Name = "BurningEnemy", ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex };
        part.Initialize(AssetRoot,spawn.Target);
        return new BurningEnemyRoomEntity(part);
    }

    private IRoomEntity CreateBeam(SwordBeamSpawn spawn, OracleRoomData room)
    {
        var effect = new SwordBeamEffect { Name = "SwordBeam", ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex };
        effect.Initialize(_swordBeam ??= swordBeamSource?.Invoke() ?? new SwordBeamDatabase(AssetRoot + "metadata/sword_beam.tsv"),
            room,spawn.LinkPosition,spawn.Direction,owner.ToScreen,owner.OnSoundRequested);
        return new SwordBeamRoomEntity(effect);
    }

    private IRoomEntity CreateClink(Vector2 position, bool flickers, int z, string name, bool playOnUpdate)
    {
        var effect = new ClinkEffect { Name = name, ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex };
        effect.Initialize(AssetRoot,position,flickers,z);
        effect.SetPhysicsProcess(false);
        return new SwordBeamClinkRoomEntity(effect,playOnUpdate ? () => owner.OnSoundRequested(SoundId.SndClink) : null);
    }

    private IRoomEntity CreateEnemyClink(EnemyClinkSpawn spawn)
    {
        var entity = CreateClink(spawn.Position,false,spawn.ZHigh,"EnemyClink",spawn.InitializeOnUpdate);
        if (!spawn.InitializeOnUpdate) owner.OnSoundRequested(SoundId.SndClink);
        else entity.Node.Visible = false;
        return entity;
    }

    private IRoomEntity CreateDrop(ItemDropSpawn spawn, OracleRoomData room)
    {
        var data = owner.ItemDrops;
        var rule = data.Rule(spawn.SubId);
        var drop = new ItemDropEffect { Name = $"ItemDrop_{spawn.SubId:x2}", ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex };
        int sound = rule.Treasure == 0 ? SoundId.MusNone : owner.Treasures.GetBehaviour(rule.Treasure).Sound;
        drop.Initialize(spawn.SubId,spawn.Position,room,data.GetVisual(spawn.SubId),spawn.Angle,spawn.DugUp,
            owner.OnSoundRequested,sound,data,owner.Random,owner.MaplePresent,owner.SpawnDiggingEnemy,spawn.ZHigh);
        drop.InvincibilityCounter = spawn.InvincibilityCounter;
        drop.OreFiftyCollected = () => (owner.SaveData ?? throw new InvalidOperationException("PART_ITEM_DROP:$0e requires its save owner."))
            .SetRoomFlag(room.Group,room.Id,0x20);
        return new ItemDropRoomEntity(drop,owner.OnItemDropEnteredHazard);
    }

    private IRoomEntity CreateHole(FallingDownHoleSpawn spawn)
    {
        var effect = new FallingDownHoleEffect { Name = "FallingDownHole", ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex };
        effect.Initialize(AssetRoot,spawn.Position,owner.OnSoundRequested,spawn.Silent);
        return new FallingDownHoleRoomEntity(effect);
    }
}

internal sealed record SwordBeamClinkSpawn(Vector2 Position) : RoomEntitySpawn;
internal sealed record SwordWallClinkSpawn(Vector2 Position, bool Flickers, int ZHigh = 0) : RoomEntitySpawn;
internal sealed record EnemyClinkSpawn(Vector2 Position, bool InitializeOnUpdate = false, int ZHigh = 0) : RoomEntitySpawn;
internal sealed record GrassDebrisSpawn(Vector2 Position, int InteractionId = InteractionId.GrassDebris,
    bool Flickers = false, bool Underwater = false) : RoomEntitySpawn(UpdateThisFrame: true);
internal sealed record RockDebrisSpawn(Vector2 Position, int InteractionId = InteractionId.RockDebris)
    : RoomEntitySpawn(UpdateThisFrame: true);
internal sealed record DiggingEnemySpawn(int Roll, int SubId, Vector2 Position) : RoomEntitySpawn;
