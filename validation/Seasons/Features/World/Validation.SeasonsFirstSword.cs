
namespace Oracles;

public sealed partial class ValidationRoot
{
    private FrontendValidationRoot StartSeasonsAfterImpa(bool batched)
    {
        var root=new FrontendValidationRoot();AddChild(root);
        var save=OracleSaveData.CreateStandardGame(OracleGame.Seasons);
        save.SetLinkName("LINK");save.SetGlobalFlag(0x21);save.SetGlobalFlag(0x0a);
        save.SetRoomFlag(0,0xb6,0x50);save.SetDeathRespawnPoint(0,0xb6,3,2,0x48,0x78);
        var selector=root.InitializeGameSelect();root.SeedFileSlot(0,save);
        selector.RequestPointerSelection(OracleGame.Seasons,true);root.Step(1,batched);root.CompleteBootLoading();
        root.Step(1,batched);root.Step(1,batched,"inventory");root.Step(32,batched);
        root.Step(1,batched,"attack");root.Step(1,batched);root.Step(1,batched,"attack");root.Step(32,batched);
        FailIf(root._seasonsOpening is not {Complete:true},"The first-sword fixture must enter the real post-Impa file through the frontend.");
        return root;
    }

    private void ValidateSeasonsFirstSwordBindings()
    {
        var root=StartSeasonsAfterImpa(false);
        try
        {
        var session=root._seasonsOpening!;
        var app=new ApplicationValidationFixture(root);
        foreach(int room in new[]{0xb5,0xb4,0xc4,0xd4})
        {
            session.DebugTravel(0,room);app.Step(2,Vector2.Zero);
            FailIf(session.Entities.EntityAdapters<SeasonsHerosCaveLinkedEntranceRoomEntity>().Any(),
                "Unlinked INTERAC$dc:$01/$02 must delete through their physical state-zero visits.");
        }
        foreach(int room in Enumerable.Range(2,7))
        {
            session.DebugTravel(4,room);app.Step(2,Vector2.Zero);
            FailIf(session.Room.Width!=240 || session.Room.Height!=176 || session.Rooms.CurrentDungeonIndex!=0,
                $"Hero's Cave $4:{room:x2} must use native dungeon$00 and large-room geometry.");
        }
        session.DebugTravel(4,3);app.Step(2,Vector2.Zero);
        FailIf(session.Entities.Entities<GelCharacter>().Count!=3 || session.Entities.Entities<KeeseCharacter>().Count!=2 ||
            session.Entities.Entities<GroundButtonRoomEntity>().Count!=1,
            "Hero's Cave $4:03 must bind its source-ordered Gel/Keese stream and PART$09 button.");
        session.DebugTravel(4,8);app.Step(2,Vector2.Zero);
        FailIf(session.Entities.Entities<GelCharacter>().Count!=4 || session.Entities.Entities<ZolCharacter>().Count!=2 ||
            session.Entities.Entities<GroundButtonRoomEntity>().Count!=1 || session.Entities.Entities<DungeonDoorRoomEntity>().Count!=1,
            "Hero's Cave $4:08 must bind four Gels, two Zols, the floor button and north shutter.");
        session.DebugTravel(4,4);session.Link.WarpTo(new(120,88));
        var caveData=new SeasonsHerosCaveDatabase();
        session.Transitions.SetCutsceneCameraOrigin(Vector2.Zero);
        session.ShowText(0x0200,caveData.Text(0x0200));float topTextY=session.Dialogue.Position.Y;
        session.Dialogue.Close();session.Transitions.SetCutsceneCameraOrigin(new(0,32));
        session.ShowText(0x0200,caveData.Text(0x0200));
        FailIf(session.Dialogue.Position.Y!=topTextY+72,
            "Large-room dialogue must subtract native cameraY before selecting top versus bottom screen placement.");
        session.Dialogue.Close();
        }
        finally {root.Free();}
    }

    private void ValidateSeasonsFirstSwordSequence()
    {
        foreach(bool batched in new[]{false,true})
        foreach(string mode in new[]{"normal","full","retire"})
        {
            bool full=mode=="full";
            var root=StartSeasonsAfterImpa(batched);
            try
            {
            var session=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
            byte[] diskBefore=root.FileSlotSnapshot(0,OracleGame.Seasons);
            session.DebugTravel(4,6);
            session.Link.WarpTo(new(120,88));
            FailIf(session.Collision.Collides(session.Link.Position),"Hero's Cave sword fixture must start on actual southern floor.");
            app.Step(2,Vector2.Zero,batched:batched);
            var actor=session.Entities.EntityAdapters<SeasonsHerosCaveSwordRoomEntity>().Single();
            FailIf(actor.State!=1 || session.Memory.ReadField("wcca1")!=1 || actor.Node.Visible || session.Inventory.SwordLevel!=0,
                "INTERAC$c6 state0 must arm scripted chest handling without visibility or an early grant.");
            WalkSeasons(app,session,point=>point.X==120 && point.Y==54);
            app.Step(8,Vector2.Up,batched:batched);
            var native=new BraceletRom(session.Room,session.Link.PrecisePosition,0,session.Inventory.ActiveRing,game:OracleGame.Seasons);
            native.LoadSave(session.Save);native[0xcc2d]=4;
            int at=0xd040+session.Entities.NativeInteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0xc6;native[at+0xb]=32;native[at+0xd]=120;
            native.AdvanceInteractions(session.FrameCounter);
            var sounds=root._sound.AttachPlayRequestAudit();
            app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
            FailIf(session.Room.GetPackedStorageMetatile(0x27)!=ChestDatabase.OpenedTile || session.Memory.ReadField("wcca2")!=0x27,
                $"Link must open the sword chest from collision-valid southern floor through the real A-button caller: XY{session.Link.Position}, face{session.Link.FacingVector}, wall{session.Link.FacesTileWallForInteraction}, tile${session.Room.GetPackedStorageMetatile(0x27):x2}, signal${session.Memory.ReadField("wcca2"):x2}.");
            // nextToChestTile owns this signal; the native object dispatcher consumes it next.
            native.SetNativeByte(session.Memory.FieldAddress("wcca2"),0x27);
            native.AdvanceInteractions(session.FrameCounter);
            int steps=0;
            void Compare()
            {
                FailIf(actor.State!=native[at+4] || actor.Counter!=native[at+6] ||
                    actor.Node.Position!=new Vector2(native[at+0xd],native[at+0xb]) ||
                    actor.Node.Visible!=((native[at+0x1a]&128)!=0),
                    $"Hero sword/batch{batched}/update{steps}: state{actor.State}/{native[at+4]}, counter{actor.Counter}/{native[at+6]}, XY{actor.Node.Position}/{native.Word(at+0xc)/256f},{native.Word(at+0xa)/256f}, visible{actor.Node.Visible}/{(native[at+0x1a]&128)!=0}.");
            }
            Compare();
            while(actor.State<3 && steps<40)
                app.Step(batched?4:1,Vector2.Right,batched:batched,afterUpdate:()=>
                {
                    native.AdvanceInteractions(session.Entities.FrameCounter);steps++;Compare();
                    FailIf(session.Save.HasRoomFlag(4,6,0x20) || session.Memory.ReadField("wDisabledObjects")!=0x81 ||
                        session.Memory.ReadField("wDisableLinkCollisionsAndMenu")!=0x81,
                        "Sword rise must retain native masks$81 without setting ROOMFLAG_ITEM.");
                });
            FailIf(actor.State!=3 || session.Inventory.SwordLevel!=1 || session.LastText!=0x001c || !session.Dialogue.IsOpen ||
                !sounds.Requests.Contains(SoundId.SndGetItem),"The 32-update rise must grant sword$01 and TX_001c exactly once.");
            app.Step(5,Vector2.Right,batched:batched);
            FailIf(actor.State!=3 || session.Save.HasRoomFlag(4,6,0x20),"The sword return must wait for the pickup text.");
            if(mode=="retire")
            {
                var engine=session.Engine!;var save=session.Save;
                root._scene.Free();
                FailIf(engine.Active || save.HasRoomFlag(4,6,0x20),
                    "Retiring the active sword scene must cancel its pending return without writing the completion flag.");
                continue;
            }
            if(full)
            {
                int allocated=0;
                while(session.Entities.TryCreateExclamation(new(24,120),0xff) is not null)allocated++;
                FailIf(allocated!=13,"The failed sword-spin allocation fixture must fill all remaining native interaction slots.");
                for(int slot=3;slot<16;slot++)native[(0xd0+slot)*256+0x40]=1;
            }
            session.Dialogue.Close();native[0xcba0]=0;
            app.Step(1,Vector2.Zero,batched:batched);
            native.AdvanceInteractions(session.Entities.FrameCounter);Compare();
            FailIf(actor.State!=4 || actor.Counter!=90 || sounds.Requests.Contains(SoundId.SndCtrlMediumFadeOut)==full,
                "Closing TX_001c must create SWORD_03's spin presentation and arm exactly90 return updates.");
            FailIf(native.Sounds.Contains(SoundId.SndCtrlMediumFadeOut)==full,
                "The native checked sword-spin allocation must emit the fade control cue only on success.");
            app.Step(89,Vector2.Zero,batched:batched,afterUpdate:()=>
            {native.AdvanceInteractions(session.Entities.FrameCounter);Compare();});
            FailIf(actor.Finished || session.Save.HasRoomFlag(4,6,0x20) || session.Rooms.ActiveGroup!=4,
                "Hero sword must remain in the cave through return update89.");
            app.Step(1,Vector2.Zero,batched:batched);
            native.AdvanceInteractions(session.Entities.FrameCounter);
            foreach(var field in new[]{("wWarpDestGroup",0x80),("wWarpDestRoom",0xd4),("wWarpTransition",0),
                ("wWarpDestPos",0x54),("wWarpTransition2",0x83)})
                FailIf(native.ReadNativeByte(session.Memory.FieldAddress(field.Item1))!=field.Item2,
                    $"Native Hero's Cave return must retain {field.Item1}=${field.Item2:x2}.");
            FailIf(!session.Save.HasRoomFlag(4,6,0x20) || !sounds.Requests.Contains(SoundId.SndFadeOut),
                "Return update90 must set ROOMFLAG_ITEM and emit the native fade cue.");
            app.Step(124,Vector2.Zero,batched:batched);
            FailIf(session.Rooms.ActiveGroup!=4 || !session.Transitions.IsTransitioning,
                "Native warp$83 must retain the divisor4 white fade before loading the overworld.");
            app.Step(80,Vector2.Zero,batched:batched);
            FailIf(session.Rooms.ActiveGroup!=0 || session.RoomId!=0xd4 || session.Scrolling || session.Inventory.SwordLevel!=1 ||
                session.Memory.ReadField("wDisabledObjects")!=0 || session.Memory.ReadField("wDisableLinkCollisionsAndMenu")!=0,
                "The native direct warp must return Link to overworld$0:d4 and release control with sword$01.");
            var before=session.Link.PrecisePosition;app.Step(3,Vector2.Down,batched:batched);
            FailIf(session.Link.PrecisePosition==before,"The sword warp must restore actual movement input.");
            FailIf(session.Inventory.EquippedA!=ItemId.Sword && session.Inventory.EquippedB!=ItemId.Sword,
                "The native sword grant must equip the newly usable sword.");
            string swordButton=session.Inventory.EquippedA==ItemId.Sword?"attack":"item";
            app.Step(1,Vector2.Zero,held:[swordButton],pressed:[swordButton],batched:batched);
            FailIf(!session.Link.IsAttacking || session.Link.IsUsingPunch,"The obtained sword must attack through the actual equipped-item caller.");
            FailIf(!OracleSaveData.TryDeserialize(session.Save.Serialize(),OracleGame.Seasons,out var restored) ||
                !restored!.HasRoomFlag(4,6,0x20) || new InventoryState(TreasureDatabase.For(SeasonsGameContent.AssetRoot),restored).SwordLevel!=1 ||
                !diskBefore.SequenceEqual(root.FileSlotSnapshot(0,OracleGame.Seasons)),
                "The completed sword and room flag must serialize together without implicitly writing the disk file.");
            session.DebugTravel(4,6);app.Step(2,Vector2.Zero,batched:batched);
            FailIf(session.Room.GetPackedStorageMetatile(0x27)!=ChestDatabase.OpenedTile || session.Inventory.SwordLevel!=1,
                "Re-entering the collected sword room must preserve its open chest and sword without replaying the event.");
            }
            finally {root.Free();}
        }
    }

    private void ValidateSeasonsFirstSwordPuzzles()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var session=root._seasonsOpening!;
                // Contact grace isolates puzzle reachability from independently validated enemy combat.
                var app=new ApplicationValidationFixture(root,session.Link.ApplyObjectInteractionGrace);
                session.DebugTravel(4,5);session.Link.WarpTo(new(88,104));
                app.Step(2,Vector2.Zero,batched:batched);
                WalkSeasons(app,session,point=>point.X==88 && point.Y==88);
                app.Step(80,Vector2.Up,batched:batched);app.Step(20,Vector2.Zero,batched:batched);
                FailIf(session.Entities.RoomEnemyCount!=0 || new[]{0x07,0xa7,0x50}.Any(position=>session.Room.GetPackedStorageMetatile((byte)position)!=0xa0),
                    "Pushing Hero's Cave $4:05's source block must clear its synthetic enemy count and open all three shutters.");
                session.DebugTravel(4,2);session.Link.WarpTo(new(120,152));
                WalkSeasons(app,session,point=>point.X==104 && point.Y==120);
                app.Step(64,Vector2.Down,batched:batched);
                FailIf(session.Room.GetPackedStorageMetatile(0x86)!=0xa0 || session.Room.GetPackedStorageMetatile(0x96)!=0x1d,
                    "Hero's Cave $4:02's southern block must move down once to open the safe western path around the pits.");
                WalkSeasons(app,session,point=>point.X==168 && point.Y==40);
                app.Step(64,Vector2.Down,batched:batched);
                WalkSeasons(app,session,point=>point.X==168 && point.Y==56);
                app.Step(64,Vector2.Right,batched:batched);
                FailIf(session.Room.GetPackedStorageMetatile(0x3a)!=0xa0 || session.Room.GetPackedStorageMetatile(0x4a)!=0x1d ||
                    session.Room.GetPackedStorageMetatile(0x3b)!=0xa0 || session.Room.GetPackedStorageMetatile(0x3c)!=0x1d,
                    "The two northern source blocks must open $4:02's safe route to the key room without crossing a pit.");
                app.Step(4,Vector2.Left,batched:batched);
                WalkSeasons(app,session,point=>point.X==234 && point.Y==88);
                session.DebugTravel(4,3);session.Link.WarpTo(new(216,104));
                FailIf(session.Collision.Collides(session.Link.Position),"The key-button fixture must start on its actual southern floor.");
                app.Step(2,Vector2.Zero,batched:batched);
                WalkSeasons(app,session,point=>point.X==216 && point.Y==88);
                app.Step(20,Vector2.Zero,batched:batched);
                FailIf(session.Entities.ActiveTriggers!=1 || session.Room.GetPackedStorageMetatile(0x5b)!=ChestDatabase.ClosedTile,
                    "PART$09 at$5d must publish trigger$01 and INTERAC$20:$01 must create the native key chest at$5b after its puff.");
                WalkSeasons(app,session,point=>point.X==184 && point.Y==102);
                app.Step(8,Vector2.Up,batched:batched);
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                app.Step(40,Vector2.Zero,batched:batched);
                FailIf(session.Inventory.GetDungeonSmallKeys(0)!=1 || !session.Save.HasRoomFlag(4,3,0x20),
                    "The generated Hero's Cave chest must grant one dungeon$00 key through the live chest/item handoff.");
                session.Dialogue.Close();app.Step(4,Vector2.Zero,batched:batched);
                session.DebugTravel(4,3);app.Step(2,Vector2.Zero,batched:batched);
                FailIf(session.Entities.Entities<DungeonTriggerChestScriptRoomEntity>().Count!=0 || session.Inventory.GetDungeonSmallKeys(0)!=1,
                    "The collected key script must retire on room reload without creating another reward.");
                session.DebugTravel(4,7);session.Link.WarpTo(new(200,88));
                FailIf(session.Collision.Collides(session.Link.Position),"The east key-door fixture must start on actual western floor.");
                app.Step(100,Vector2.Right,batched:batched);app.Step(80,Vector2.Zero,batched:batched);
                FailIf(session.RoomId!=8 || session.Inventory.GetDungeonSmallKeys(0)!=0 || !session.Save.HasRoomFlag(4,7,0x02) ||
                    !session.Save.HasRoomFlag(4,8,0x08),"The first small key must open $4:07's east door, persist both native door flags and scroll into $4:08.");
                session.DebugTravel(4,8);session.Link.WarpTo(new(72,40));
                FailIf(session.Collision.Collides(session.Link.Position),"The sword-door button fixture must begin on actual floor.");
                app.Step(2,Vector2.Zero,batched:batched);
                WalkSeasons(app,session,point=>point.X==72 && point.Y==24);
                app.Step(40,Vector2.Zero,batched:batched);
                FailIf(session.Entities.ActiveTriggers!=1 || session.Room.GetPackedStorageMetatile(0x07)!=0xa0,
                    "PART$09 at$14 must open the native north shutter into the sword room.");
            }
            finally {root.Free();}
        }
    }

    private void ValidateSeasonsFirstSwordApproach()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var session=root._seasonsOpening!;
                var app=new ApplicationValidationFixture(root,session.Link.ApplyObjectInteractionGrace);
                foreach(var exit in new[]{(Room:0xc6,Direction:Vector2.Down),(Room:0xc5,Direction:Vector2.Left),
                    (Room:0xc4,Direction:Vector2.Left),(Room:0xc3,Direction:Vector2.Left),
                    (Room:0xd3,Direction:Vector2.Down),(Room:0xd4,Direction:Vector2.Right)})
                {
                    WalkSeasons(app,session,point=>exit.Direction==Vector2.Left?point.X==6:
                        exit.Direction==Vector2.Right?point.X==154 && point.Y==104:point.Y==120);
                    app.Step(8,exit.Direction,batched:batched);app.Step(80,Vector2.Zero,batched:batched);
                    FailIf(session.RoomId!=exit.Room || session.Transitions.IsTransitioning,
                        $"Post-Impa approach must reach native overworld$0:{exit.Room:x2} through its actual boundary.");
                }
                WalkSeasons(app,session,point=>point.X==72 && point.Y==104);
                app.Step(20,Vector2.Up,batched:batched);app.Step(100,Vector2.Zero,batched:batched);
                FailIf(session.Rooms.ActiveGroup!=4 || session.RoomId!=4,
                    "The actual $0:d4 tile$54 entrance must load Hero's Cave $4:04.");
                app.Step(40,Vector2.Up,batched:batched);
                FailIf(session.LastText!=0x0200 || session.Save.RespawnGroup!=4 || session.Save.RespawnRoom!=4,
                    "The source dungeon entrance must show TX_0200 and record its native checkpoint through actual Link collision.");
                session.Dialogue.Close();app.Step(3,Vector2.Zero,batched:batched);
            }
            finally {root.Free();}
        }
    }
}
