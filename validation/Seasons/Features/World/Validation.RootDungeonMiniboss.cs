namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonMiniboss()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);var entities=s.Entities;
                s.Inventory.GiveTreasure(TreasureId.Sword,1);s.Inventory.EquipA(TreasureId.Sword);s.Inventory.EquipB(0);
                s.DebugTravel(4,0x0b);s.Link.WarpTo(new(120,146));
                int dungeonMusic=s.Sound.NativeRoomMusic;
                FailIf(dungeonMusic is 0 or 0x2d,"Room $4:0b must retain its dungeon song independently of MUS_MINIBOSS $2d.");
                s.Memory.SetWramByte(0xcfc1,0xa5);s.Memory.SetWramByte(0xcfc2,0x5a);
                app.Step(1,Vector2.Zero,batched:batched);
                FailIf(s.Memory.ReadWramByte(0xcfc1)!=0||s.Memory.ReadWramByte(0xcfc2)!=0,
                    "INTERAC$20 state0 must clear both shared dungeon-script scratch bytes before its reward script.");
                for(int tick=0;tick<100&&!s.Dialogue.IsOpen;tick++)app.Step(1,Vector2.Up,batched:batched);
                FailIf(!s.Dialogue.IsOpen||s.Dialogue.SourceTextId!=0x2f00||s.Link.Position.Y>=120||
                    s.Collision.Collides(s.Link.Position)||entities.Entities<BrotherGoriyaCharacter>().Count!=2,
                    "The actual southern floor approach must trigger Brother Goriyas' $2f00 introduction.");
                s.Dialogue.Close();app.Step(3,Vector2.Zero,batched:batched);
                FailIf(s.Sound.ActiveMusic!=0x2d||s.Sound.NativeRoomMusic!=dungeonMusic,
                    "The introduction must play MUS_MINIBOSS $2d while preserving the sound owner's room song.");
                var leader=entities.Entities<BrotherGoriyaCharacter>().Single(actor=>actor.SubId==0);
                FailIf(leader.Health!=16||entities.RoomEnemyCount!=2,"The original pair must begin with $10 HP each and two live counts.");
                void Swing()
                {
                    // AI movement has an independent ROM comparison. Isolate
                    // the actual Sword collision on unchanged room floor.
                    s.Link.WarpTo(new(120,104));s.Link.Face(Vector2I.Up);leader.Position=new(120,88);
                    entities.Entities<BrotherGoriyaCharacter>().Single(actor=>actor.SubId==1).Position=new(168,72);
                    s.Memory.SetField("wDisabledObjects",4);
                    app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                    app.Step(11,Vector2.Zero,batched:batched);
                    s.Memory.SetField("wDisabledObjects",0);
                }
                Swing();
                FailIf(leader.Health!=14,"sword.s:@swordLevelData's L1 damage -2 must pass through native collision mode$41.");
                app.Step(40,Vector2.Zero,batched:batched);
                leader.Health=1;Swing();
                FailIf(leader.Health!=0||entities.RoomEnemyCount!=2,
                    "The final Sword contact must retain both counts until their boss death PARTs complete.");
                app.Step(12,Vector2.Zero,batched:batched);
                FailIf(entities.RoomEnemyCount!=2||s.Save.HasRoomFlag(4,0x0b,0x80),
                    "A health-zero miniboss cannot publish completion during recoil/death.");
                for(int tick=0;tick<230&&entities.RoomEnemyCount!=0;tick++)app.Step(1,Vector2.Zero,batched:batched);
                s.Memory.SetField("wDisabledObjects",2);app.Step(3,Vector2.Zero,batched:batched);
                FailIf(s.Save.HasRoomFlag(4,0x0b,0x80)||entities.EntityAdapters<MinibossPortalRoomEntity>().Any(),
                    "An initialized INTERAC$20 must pause its completion script under the native interaction mask.");
                s.Memory.SetField("wDisabledObjects",0);app.Step(40,Vector2.Zero,batched:batched);
                FailIf(entities.RoomEnemyCount!=0||entities.Entities<BrotherGoriyaCharacter>().Count!=0||
                    !s.Save.HasRoomFlag(4,0x0b,0x80)||entities.LinkCollisionsAndMenuDisabled||
                    s.Room.Layout[0x50]!=0xa0||s.Room.Layout[0xa7]!=0xa0||
                    entities.EntityAdapters<MinibossPortalRoomEntity>().Count()!=1||
                    s.Sound.ActiveMusic!=dungeonMusic||s.Sound.NativeActiveMusic!=dungeonMusic,
                    "The paired Sword defeat must complete both counted explosions, reopen both shutters and create one portal.");
                CheckSeasonsMinibossPortalGraphics(s);
                s.Link.WarpTo(new(120,104));
                for(int tick=0;tick<32&&s.Memory.ReadField("wDisabledObjects")!=1;tick++)app.Step(1,Vector2.Up,batched:batched);
                FailIf(s.Memory.ReadField("wDisabledObjects")!=1,"The completed room's portal must accept a real floor approach.");
                s.DebugTravel(4,0x10);
                FailIf(s.Memory.ReadField("wDisabledObjects")!=0||s.Link.CutsceneControlled,
                    "Room replacement must cancel portal control and its native object mask.");
                s.DebugTravel(4,0x0b);s.Link.WarpTo(new(120,104));app.Step(30,Vector2.Zero,batched:batched);
                FailIf(entities.Entities<BrotherGoriyaCharacter>().Count!=0||entities.RoomEnemyCount!=0||
                    entities.EntityAdapters<MinibossPortalRoomEntity>().Count()!=1,
                    "Completed-room re-entry must skip the pair and retain exactly one placed portal.");
                for(int tick=0;tick<240&&s.RoomId==0x0b;tick++)app.Step(1,Vector2.Up,batched:batched);
                FailIf(s.RoomId!=0x15,"The native miniboss portal must return to its original $4:15 entrance pair.");
                for(int tick=0;tick<240&&s.Transitions.IsTransitioning;tick++)app.Step(1,Vector2.Zero,batched:batched);
                app.Step(3,Vector2.Zero,batched:batched);
                FailIf(entities.EntityAdapters<MinibossPortalRoomEntity>().Count()!=1||s.Memory.ReadField("wDisabledObjects")!=0||
                    s.Link.CutsceneControlled,"Portal arrival must restore control and expose the original entrance-side portal.");
            }
            finally {root.Free();}
        }
    }

    private void CheckSeasonsMinibossPortalGraphics(SeasonsOpeningSession session)
    {
        var portal=session.Entities.Entities<MinibossPortal>().Single();
        var animation=SomariaPrivate<EnemyAnimationPlayer>(portal,"_animation");
        var frames=SomariaPrivate<List<EnemyAnimationPlayerAnimationFrame>[]>(animation,"_animations")[0];
        FailIf(frames.Count!=2||frames.Any(frame=>frame.Duration!=9)||
            SomariaPrivate<int[]>(animation,"_loopStarts")[0]!=0,
            "INTERAC$7e must loop the original two nine-update poses.");
        var native=new BraceletRom(session.Room,session.Link.Position,0,session.Inventory.ActiveRing,game:OracleGame.Seasons);
        native.LoadSave(session.Save);native.LoadDungeon(1);
        native.SetNativeByte(session.Memory.FieldAddress("wActiveGroup"),4);
        native.SetNativeByte(session.Memory.FieldAddress("wActiveRoom"),0x0b);
        native.SetNativeByte(session.Memory.FieldAddress("wScrollMode"),1);
        const int slot=0xd240;
        native[slot]=1;native[slot+1]=0x7e;
        native.AdvanceInteractions(0);
        var graphics=new LinkCollisionRom(game:OracleGame.Seasons);
        graphics.Call(0x0602,bank:0,accumulator:0x83); // GFXH_COMMON_SPRITES -> bank1:$8000.
        graphics.Call(0x04e7,bank:0,accumulator:0x0f); // PALH_0f supplies OBJ0-5.
        for(int pose=0;pose<2;pose++)
        {
            CompareSeasonsInteractionPixels(native,graphics,frames[pose].Texture,frames[pose].Offset,slot,$"INTERAC$7e pose{pose}");
            for(int update=0;update<9;update++)native.AdvanceInteractions(update+1);
        }
    }
}
