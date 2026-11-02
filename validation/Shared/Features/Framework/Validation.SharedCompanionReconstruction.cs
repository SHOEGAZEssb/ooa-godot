using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedCompanionReconstruction(FrontendValidationRoot root,bool batched)
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        var game=root._gameplaySession!.Rooms.SaveData.Game;
        var app=new ApplicationValidationFixture(root);
        GameplaySession engine=null!;RoomSession rooms=null!;RoomEntityManager entities=null!;
        Player player=null!;InventoryState inventory=null!;OracleRuntimeState memory=null!;
        void Bind()
        {
            engine=root._gameplaySession!;rooms=engine.Rooms;entities=root._seasonsOpening?.Entities??root._entities;
            player=root._scene.Player;inventory=player.Inventory;memory=rooms.RuntimeState;
        }
        Bind();int oldGroup=rooms.ActiveGroup,oldRoom=rooms.CurrentRoom.Id;
        void Travel(int group,int room)
        {
            if(game==OracleGame.Ages)root.LoadDebugRoom(group,room);else root._seasonsOpening!.DebugTravel(group,room);
            Bind();entities.Clear();
        }
        IRoomEntity Body()=>entities.EntityAdapters<IRoomEntity>().SingleOrDefault(actor=>actor is INativeSpecialObjectRoomEntity)
            ??throw new InvalidOperationException($"{game}: reconstruction fixture lost companion ${memory.ReadWramByte(0xd101):x2} "+
                $"state${memory.ReadWramByte(0xd104):x2} at ({memory.ReadWramByte(0xd10d)},{memory.ReadWramByte(0xd10b)}) room${rooms.ActiveGroup:x2}:${rooms.CurrentRoom.Id:x2}.");
        EnemyAnimationPlayer Animation()=> (EnemyAnimationPlayer)Body().GetType().GetField("_animation",hidden)!.GetValue(Body())!;
        byte[] NativeBytes()=>memory.CaptureState().Wram.AsSpan(0x1000,0x140).ToArray();
        string Differences(byte[] wanted)
        {
            byte[] actual=NativeBytes();
            return string.Join(',',Enumerable.Range(0,wanted.Length).Where(i=>wanted[i]!=actual[i])
                .Select(i=>$"${0xd000+i:x4}:{wanted[i]:x2}->{actual[i]:x2}"));
        }
        ulong Pixels()=>OracleGraphicsCache.PixelHash(Animation().CurrentTexture.GetImage());
        bool RandomMatches(OracleRandomState expected)
        {
            var actual=root._random.CaptureState();
            return (actual with {PlacementBuffer=expected.PlacementBuffer})==expected &&
                actual.PlacementBuffer.AsSpan().SequenceEqual(expected.PlacementBuffer);
        }
        void Clear()
        {
            entities.Clear();player.ClearNativeItemParents();CompanionRuntimeState.ForgetRemembered(memory);
            for(int at=0xd100;at<0xd140;at++)memory.SetWramByte(at,0);
            memory.SetField("wLinkObjectIndex",0xd0);memory.SetField("wDisabledObjects",0);memory.SetField("wMenuDisabled",0);
            memory.SetField("wDisableWarpTiles",0);memory.SetField("wWarpsDisabled",0);memory.SetField("wDisableScreenTransitions",0);
        }
        try
        {
            // troupeScript_startDanceScene sets this before Din's capture and GLOBALFLAG_INTRO_DONE.
            if(game==OracleGame.Seasons)rooms.SaveData.SetRoomFlag(0,0x98,0x40);
            foreach(int id in new[]{0x0b,0x0c,0x0d})
            foreach(bool riding in new[]{false,true})
            {
                Clear();Travel(0,game==OracleGame.Ages?0x45:0x97);Clear();
                var save=rooms.SaveData;var room=rooms.CurrentRoom;
                string field=id==0x0b?"wRickyState":id==0x0c?"wDimitriState":"wMooshState";
                save.WriteField(field,0x80);inventory.AssignAnimalCompanion(id);inventory.EquipA(0);inventory.EquipB(0);
                inventory.SetScriptedHealth(inventory.MaxHealthQuarters);engine.StatusBar.SynchronizeHealth();engine.StatusBar.SynchronizeRupees();
                Vector2 point=(from y in Enumerable.Range(1,room.HeightInTiles-3)
                    from x in Enumerable.Range(1,room.WidthInTiles-2)
                    let p=new Vector2(x*16+8,y*16+8)
                    where p.X+48<room.Width && Enumerable.Range(-6,39).All(dy=>Enumerable.Range(-6,45).All(dx=>
                        !room.IsSolid(p+new Vector2(dx,dy)) && room.GetTerrainInfo(p+new Vector2(dx,dy)).Hazard==HazardType.None))
                    orderby (p-new Vector2(room.Width/2,room.Height/2-8)).LengthSquared()
                    select p).First();
                player.WarpTo(point+new Vector2(0,24));player.Face(Vector2I.Up);player.ResetEnemyInvincibility();
                entities.Spawn(id switch
                {
                    0x0b=>new RickyCompanionSpawn(point,2,0,room.Id),
                    0x0c=>new DimitriCompanionSpawn(point,2,0,room.Id),
                    _=>(RoomEntitySpawn)new MooshCompanionSpawn(point,2,0,room.Id)
                });
                app.Step(4,Vector2.Zero,batched:batched);
                if(riding)
                {
                    for(int approach=0;!player.CompanionJumpActive && !player.CompanionRideActive && approach<40;approach++)
                        app.Step(1,Vector2.Up,batched:batched);
                    for(int landing=0;(!player.CompanionRideActive || player.CompanionJumpActive) && landing<80;landing++)
                        app.Step(1,Vector2.Zero,batched:batched);
                    FailIf(!player.CompanionRideActive || player.CompanionJumpActive,
                        $"{game}: companion${id:x2} reconstruction fixture must mount through the room's real geometry.");
                    app.Step(8,Vector2.Right,batched:batched);
                }
                else
                    for(int wait=0;(memory.ReadWramByte(0xd10e)!=0 || memory.ReadWramByte(0xd10f)!=0 || memory.ReadWramByte(0xd120)>3) && wait<128;wait++)
                        app.Step(1,Vector2.Zero,batched:batched);
                string context=$"{game}/companion${id:x2}/riding{riding}/batch{batched}";
                FailIf(!engine.CanCaptureDebugState,context+": native stable companion state must be reconstructible.");
                var captured=root.CaptureDebugSavestate();
                FailIf(!DebugSavestateData.TryDeserialize(captured.Serialize(),game,out var decoded),context+": valid capture did not roundtrip.");
                byte[] capturedNative=NativeBytes();ulong capturedPixels=Pixels();int frame=Animation().FrameIndex,counter=Animation().RemainingUpdates;
                Vector2 position=Body().Node.Position;var seed=root._random.CaptureState();int writes=root._saveWriteRequests;

                // Semantic failures must be rejected before replacing the scene or mutating live owners.
                foreach(int bad in new[]{0,1,2,3,4,5,6,7,8})
                {
                    var invalid=new OracleRuntimeState(game);invalid.RestoreState(memory.CaptureState());
                    if(bad==0){invalid.SetWramByte(0xd122,0);invalid.SetWramByte(0xd123,0);}
                    if(bad==1)invalid.SetWramByte(0xd105,1);
                    if(bad==2)invalid.SetWramByte(0xd10e,1);
                    if(bad==3)invalid.SetWramByte(0xd132,(byte)(invalid.ReadWramByte(0xd132)^1));
                    if(bad==4)invalid.SetWramByte(0xd11a,0);
                    if(bad==5)invalid.SetField("wLinkObjectIndex",0xd2);
                    if(bad==6)invalid.SetWramByte(0xd10d,0xff);
                    if(bad==7)invalid.SetWramByte(0xd11e,(byte)(invalid.ReadWramByte(0xd11e)^1));
                    if(bad==8)
                    {
                        if(riding)invalid.SetWramByte(0xd121,0x32);
                        else invalid.SetWramByte(0xd108,4);
                    }
                    var unsupported=(DebugSavestateData)typeof(DebugSavestateData).GetConstructors(hidden).Single().Invoke(
                        [captured.Group,captured.Room,captured.PlayerPosition,captured.PlayerFacing,captured.AnimationTicks,
                         captured.CreateSaveData().Serialize(),invalid.CaptureState(),seed,entities.CaptureDebugState()]);
                    byte[] before=memory.CaptureState().Wram,saved=save.Serialize();bool rejected=false;
                    try{root.RestoreDebugSavestate(unsupported);}catch(ArgumentException){rejected=true;}
                    FailIf(!rejected || !ReferenceEquals(root._gameplaySession,engine) || !engine.Active ||
                        !memory.CaptureState().Wram.AsSpan().SequenceEqual(before) || !save.Serialize().AsSpan().SequenceEqual(saved) ||
                        !RandomMatches(seed) || root._saveWriteRequests!=writes,
                        context+$": invalid native reconstruction variant{bad} changed an owner before rejection.");
                    var original=memory.CaptureState();
                    try
                    {
                        memory.RestoreState(invalid.CaptureState());rejected=false;
                        try{root.CaptureDebugSavestate();}catch(NotSupportedException){rejected=true;}
                        FailIf(engine.CanCaptureDebugState || !rejected,
                            context+$": invalid native reconstruction variant{bad} must reject capture as well as restore.");
                    }
                    finally{memory.RestoreState(original);}
                }

                var expected=new List<(byte[] Bytes,Vector2 Link,ulong Pixels,int Frame,int Counter,OracleRandomState Random,int[] Cues)>();
                var cues=root._sound.AttachPlayRequestAudit();
                void Run(Action after)=>app.Step(24,riding?Vector2.Right:Vector2.Zero,batched:batched,afterUpdate:after);
                Run(()=>expected.Add((NativeBytes(),player.PrecisePosition,Pixels(),Animation().FrameIndex,Animation().RemainingUpdates,
                    root._random.CaptureState(),cues.Requests.ToArray())));
                var retired=engine;root.RestoreDebugSavestate(decoded!);Bind();
                FailIf(retired.Active || ReferenceEquals(retired,engine) || !capturedNative.AsSpan().SequenceEqual(NativeBytes()) ||
                    Pixels()!=capturedPixels || Animation().FrameIndex!=frame || Animation().RemainingUpdates!=counter ||
                    Body().Node.Position!=position || !RandomMatches(seed) || root._saveWriteRequests!=writes,
                    context+$": restoration changed native bytes, pose, animation phase, RNG or explicit-save count; "+
                    "native differences "+Differences(capturedNative)+".");
                cues=root._sound.AttachPlayRequestAudit();int update=0;
                Run(()=>
                {
                    var wanted=expected[update++];
                    FailIf(!NativeBytes().AsSpan().SequenceEqual(wanted.Bytes) || player.PrecisePosition!=wanted.Link ||
                        Pixels()!=wanted.Pixels || Animation().FrameIndex!=wanted.Frame || Animation().RemainingUpdates!=wanted.Counter ||
                        !RandomMatches(wanted.Random) || !cues.Requests.SequenceEqual(wanted.Cues),
                        context+$": restored gameplay diverged at update{update}; native {Differences(wanted.Bytes)}; "+
                        $"Link {wanted.Link}->{player.PrecisePosition}, pixels {wanted.Pixels:x}->{Pixels():x}, "+
                        $"animation {wanted.Frame}/{wanted.Counter}->{Animation().FrameIndex}/{Animation().RemainingUpdates}, "+
                        $"RNG matches {RandomMatches(wanted.Random)}, cues {string.Join(',',wanted.Cues)}->{string.Join(',',cues.Requests)}.");
                });
            }
        }
        finally
        {
            Bind();Clear();Travel(oldGroup,oldRoom);entities.Clear();
        }
    }
}
