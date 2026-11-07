namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonBoss()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var entities=s.Entities;var app=new ApplicationValidationFixture(root);
                s.Inventory.GiveTreasure(TreasureId.Sword,1);s.Inventory.EquipA(TreasureId.Sword);s.Inventory.EquipB(0);
                s.DebugTravel(4,0x18);s.Inventory.GrantDungeonBossKey(1);s.Link.WarpTo(new(120,24));
                s.Memory.SetField("wDisabledObjects",4);entities.DisableLinkCollisionsAndMenu();
                for(int tick=0;tick<120&&!s.Scrolling;tick++)app.Step(1,Vector2.Up,batched:batched);
                FailIf(!s.Scrolling||s.RoomId!=0x12||entities.Entities<AquamentusCharacter>().Count!=1,
                    "The opened $4:18 boss door must physically scroll into $4:12's counted Aquamentus spawner.");
                for(int tick=0;tick<240&&s.Scrolling;tick++)
                {
                    app.Step(1,Vector2.Zero,batched:batched);
                    FailIf(s.Scrolling&&entities.Entities<AquamentusCharacter>().Any(actor=>actor.SubId!=0),
                        "The destination boss must retain only its initialized spawner while the room is scrolling.");
                }
                s.Memory.SetField("wDisabledObjects",0);
                for(int tick=0;tick<400&&!entities.Entities<AquamentusCharacter>().Any(actor=>actor.SubId==1&&actor.State==11);tick++)
                    app.Step(1,Vector2.Zero,batched:batched);
                var body=entities.Entities<AquamentusCharacter>().Single(actor=>actor.SubId==1);
                FailIf(body.State!=11||entities.Entities<AquamentusCharacter>().Count!=3||entities.RoomEnemyCount!=1||
                    s.Memory.ReadField("wcc93")!=0||s.Room.Layout[0xa7]!=0x7a||s.Room.Layout[0x5e]!=0x79,
                    "The actual entry must close both boss shutters, admit three uncounted linked children and land the one counted boss.");
                entities.DisableLinkCollisionsAndMenu();
                bool sawDeath=false,sawExplosion=false;
                void Observe()
                {
                    sawDeath|=entities.Entities<AquamentusCharacter>().Any(actor=>actor.SubId==2&&actor.Health==0);
                    sawExplosion|=entities.Entities<BossDeathExplosionEffect>().Count!=0;
                    FailIf(entities.RoomEnemyCount!=0&&(s.Save.HasRoomFlag(4,0x12,0x80)||entities.Entities<GroundTreasurePickup>().Count!=0),
                        "The boss flag and heart must wait for the renderer's counted death explosion.");
                }
                var horn=entities.Entities<AquamentusCharacter>().Single(actor=>actor.SubId==3);
                for(int swing=0;swing<10;swing++)
                {
                    bool SafeFloor()=>body.ZFixed>>8==0&&body.Position.X>=56&&body.Position.X<=200&&
                        !s.Collision.Collides(horn.Position.Floor()+new Vector2(0,16));
                    for(int tick=0;tick<900&&!SafeFloor();tick++)app.Step(1,Vector2.Zero,batched:batched,afterUpdate:Observe);
                    FailIf(!SafeFloor(),$"The Sword fixture must wait for a grounded horn with clear original floor: state{body.State}, body{body.Position}, horn{horn.Position}, Z{body.ZFixed}.");
                    s.Link.WarpTo(horn.Position.Floor()+new Vector2(0,16));s.Link.Face(Vector2I.Up);
                    FailIf(s.Collision.Collides(s.Link.Position),"The boss Sword handoff must begin on unchanged room floor below the horn.");
                    s.Memory.SetField("wDisabledObjects",4);
                    app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched,afterUpdate:Observe);
                    app.Step(11,Vector2.Zero,batched:batched,afterUpdate:Observe);
                    FailIf(horn.Health!=18-swing*2,$"The actual L1 Sword must damage only the twenty-HP horn by2: swing{swing}, HP{horn.Health}.");
                    s.Memory.SetField("wDisabledObjects",0);app.Step(35,Vector2.Zero,batched:batched,afterUpdate:Observe);
                }
                FailIf(!sawDeath||entities.RoomEnemyCount!=1,"The lethal horn hit must kill its linked body/renderer and retain the boss count during the native death wait.");
                s.Memory.SetField("wDisabledObjects",2);
                for(int tick=0;tick<300&&entities.RoomEnemyCount!=0;tick++)app.Step(1,Vector2.Zero,batched:batched,afterUpdate:Observe);
                FailIf(!sawExplosion||entities.RoomEnemyCount!=0||s.Save.HasRoomFlag(4,0x12,0x80),
                    "PART$04 must release the final count while the initialized reward script is paused by the native interaction mask.");
                s.Memory.SetField("wDisabledObjects",0);app.Step(45,Vector2.Zero,batched:batched,afterUpdate:Observe);
                FailIf(entities.Entities<AquamentusCharacter>().Count!=0||!s.Save.HasRoomFlag(4,0x12,0x80)||
                    entities.LinkCollisionsAndMenuDisabled||s.Room.Layout[0xa7]!=0xa0||s.Room.Layout[0x5e]!=0xa0,
                    "The completed boss script must record defeat, release Link and reopen the original south/east shutters.");
                var heart=entities.Entities<GroundTreasurePickup>().Single();
                FailIf(heart.Position!=new Vector2(120,88)||heart.Record.TreasureObject!="TREASURE_OBJECT_HEART_CONTAINER_00"||
                    heart.Record.SpawnMode!=TreasureSpawnMode.Puff||heart.Record.SpawnDelayFrames!=30||s.Save.HasRoomFlag(4,0x12,0x20),
                    "The native boss reward must puff one uncollected heart container at ($78,$58) with wait30.");
                int maximum=s.Inventory.MaxHealthQuarters;
                s.Link.WarpTo(new(88,104));
                for(int tick=0;tick<40&&s.Link.Position.X<120;tick++)app.Step(1,Vector2.Right,batched:batched);
                for(int tick=0;tick<40&&!s.Dialogue.IsOpen;tick++)app.Step(1,Vector2.Up,batched:batched);
                app.Step(3,Vector2.Zero,batched:batched);
                FailIf(s.Inventory.MaxHealthQuarters!=maximum+4||!s.Save.HasRoomFlag(4,0x12,0x20)||!s.Link.IsHoldingItemTwoHands,
                    $"Walking to the original heart reward must add four health quarters, set ROOMFLAG_ITEM and use the shared held-item flow: Link{s.Link.Position}, HP{s.Inventory.MaxHealthQuarters}/{maximum}, flag{s.Save.HasRoomFlag(4,0x12,0x20)}, hold{s.Link.IsHoldingItemTwoHands}, text{s.Dialogue.IsOpen}, reward{heart.State}.");
                s.Dialogue.Close();app.Step(80,Vector2.Zero,batched:batched);
                s.DebugTravel(4,0x18);s.DebugTravel(4,0x12);app.Step(4,Vector2.Zero,batched:batched);
                FailIf(entities.Entities<AquamentusCharacter>().Count!=0||entities.RoomEnemyCount!=0||entities.Entities<GroundTreasurePickup>().Count!=0,
                    "Completed-room re-entry must suppress the source before-event boss and the collected heart.");
            }
            finally {root.Free();}
        }
    }
}
