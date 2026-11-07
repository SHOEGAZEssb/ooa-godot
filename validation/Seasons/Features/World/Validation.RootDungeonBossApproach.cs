namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonBossApproach()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var entities=s.Entities;var app=new ApplicationValidationFixture(root);
                s.Inventory.GiveTreasure(TreasureId.SeedSatchel,1);s.Inventory.GiveTreasure(TreasureId.EmberSeeds,8);
                s.Inventory.GiveTreasure(TreasureId.Sword,1);s.Inventory.SelectSatchelSeeds(0);
                s.Inventory.EquipA(TreasureId.SeedSatchel);s.Inventory.EquipB(0);
                s.Save.SetRoomFlag(4,0x1b,0x20);s.DebugTravel(4,0x1b);
                s.Memory.SetField("wDisabledObjects",4);entities.DisableLinkCollisionsAndMenu();
                s.Link.WarpTo(new(120,24));app.Step(3,Vector2.Zero,batched:batched);
                var path=new List<string>();
                void Walk(Vector2 input,Func<bool> arrived,int limit=300)
                {
                    for(int tick=0;tick<limit&&!arrived();tick++)app.Step(1,input,batched:batched);
                    path.Add($"{input} -> ${s.RoomId:x2}/{s.Link.Position}");
                    FailIf(!arrived(),$"The source boss approach must remain reachable: {string.Join("; ",path)}; mask${s.DisabledObjects:x2}, grab{s.Link.WallmasterGrabActive}/{s.Link.EnemyGrabActive}, collision{s.Collision.Collides(s.Link.Position)}, hand{s.Entities.Entities<FloormasterCharacter>().Count}.");
                }
                void Arrive()
                {
                    for(int tick=0;tick<240&&s.Transitions.IsTransitioning;tick++)app.Step(1,Vector2.Zero,batched:batched);
                    FailIf(s.Transitions.IsTransitioning,"The boss approach's original room scroll must complete.");
                }
                foreach(var direction in new[]{Vector2I.Left,Vector2I.Right})
                {
                    s.Link.Face(direction);app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                    app.Step(24,Vector2.Zero,batched:batched);
                }
                app.Step(90,Vector2.Zero,batched:batched);
                FailIf(s.Room.Layout[7]!=0xa0,"The acquired Satchel must release the original $4:1b north gate.");
                Walk(Vector2.Up,()=>s.Scrolling);Arrive();
                FailIf(s.RoomId!=0x17||entities.RoomEnemyCount!=1||entities.Entities<FloormasterCharacter>().Count!=1,
                    "The torch gate must enter $4:17's counted four-kill Floormaster controller.");
                Walk(Vector2.Up,()=>s.Link.Position.Y<=136);
                Walk(Vector2.Left,()=>s.Link.Position.X<=56);
                Walk(Vector2.Left,()=>s.Link.Position.X<=24);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=56);
                Walk(Vector2.Right,()=>s.Link.Position.X>=56);
                app.Step(8,Vector2.Up,batched:batched);app.Step(2,Vector2.Zero,batched:batched);
                s.Link.Face(Vector2I.Up);int rings=s.Inventory.UnappraisedRingCount;
                void FreezeEnemies()=>s.Memory.SetField("wDisabledObjects",(byte)(s.DisabledObjects|4));
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched,afterUpdate:FreezeEnemies);
                app.Step(40,Vector2.Zero,batched:batched,afterUpdate:FreezeEnemies);
                FailIf(s.Room.Layout[0x23]!=0xf0||!s.Save.HasRoomFlag(4,0x17,0x20)||
                    s.Inventory.UnappraisedRingCount!=rings+1||s.Inventory.UnappraisedRingAt(rings)!=0x68,
                    $"The original $4:17 west corridor must admit its $23 Discovery Ring chest and grant parameter$28 once: Link{s.Link.Position}, tile${s.Room.Layout[0x23]:x2}, rings{s.Inventory.UnappraisedRingCount}/{rings}, ring${s.Inventory.UnappraisedRingAt(rings):x2}, text{s.Dialogue.SourceTextId:x4}.");
                s.Dialogue.Close();app.Step(80,Vector2.Zero,batched:batched,afterUpdate:FreezeEnemies);
                s.Memory.SetField("wDisabledObjects",4);
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched,afterUpdate:FreezeEnemies);
                app.Step(40,Vector2.Zero,batched:batched,afterUpdate:FreezeEnemies);
                FailIf(s.Inventory.UnappraisedRingCount!=rings+1||s.Dialogue.IsOpen,"The opened ring chest must reject a repeated action.");
                // Frozen hands still participate in the native collision pass.
                entities.DisableLinkCollisionsAndMenu();
                Walk(Vector2.Down,()=>s.Link.Position.Y>=56);
                Walk(Vector2.Left,()=>s.Link.Position.X<=24);Walk(Vector2.Down,()=>s.Link.Position.Y>=136);
                Walk(Vector2.Right,()=>s.Link.Position.X>=56);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=104);
                Walk(Vector2.Right,()=>s.Link.Position.X>=88);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=56);
                Walk(Vector2.Right,()=>s.Link.Position.X>=120);
                Walk(Vector2.Down,()=>s.Link.Position.Y>=72);
                Walk(Vector2.Right,()=>s.Link.Position.X>=152);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=24);
                Walk(Vector2.Right,()=>s.Scrolling);Arrive();
                FailIf(s.RoomId!=0x18||entities.RoomEnemyCount!=3||entities.Entities<ItemDropProducer>().Count!=2||
                    s.Room.Layout[7]!=0x74||s.Room.Layout[0x99]!=0x21||s.Room.Layout[0x9b]!=0x21,
                    "The original northeast route must enter $4:18's controller, two counted fairy producers and locked boss door.");
                Walk(Vector2.Right,()=>s.Link.Position.X>=88);
                Walk(Vector2.Down,()=>s.Link.Position.Y>=56);
                Walk(Vector2.Right,()=>s.Link.Position.X>=120);
                Walk(Vector2.Down,()=>s.Link.Position.Y>=88);
                Walk(Vector2.Right,()=>s.Link.Position.X>=152);
                Walk(Vector2.Down,()=>s.Link.Position.Y>=104);
                Walk(Vector2.Right,()=>s.Link.Position.X>=200);
                Walk(Vector2.Down,()=>s.Link.Position.Y>=136);
                s.Inventory.EquipA(TreasureId.Sword);
                foreach(int x in new[]{184,152})
                {
                    Walk(Vector2.Left,()=>s.Link.Position.X<=x);s.Link.Face(Vector2I.Down);
                    app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                    app.Step(32,Vector2.Zero,batched:batched);
                }
                FailIf(s.Room.Layout[0x99]==0x21||s.Room.Layout[0x9b]==0x21,
                    "The two fairy bushes must be reachable by actual Sword swings from the original southern floor.");
                s.Memory.SetField("wDisabledObjects",0);app.Step(3,Vector2.Zero,batched:batched);
                FailIf(entities.Entities<ItemDropProducer>().Count!=0||entities.RoomEnemyCount!=3||entities.ActiveRoomDefeatBitset!=0x0c,
                    "Both counted $59 fairy producers must mark their source indices and retain their counts through plain enemyDelete.");
                s.Memory.SetField("wDisabledObjects",4);
                // The native $f7 pit diagonal blocks a straight north walk
                // at X=$c8; go around its eastern tip through column$0d.
                Walk(Vector2.Right,()=>s.Link.Position.X>=216);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=56);
                Walk(Vector2.Left,()=>s.Link.Position.X<=200);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=24);
                Walk(Vector2.Left,()=>s.Link.Position.X<=120);
                app.Step(40,Vector2.Up,batched:batched);
                FailIf(s.RoomId!=0x18||s.Room.Layout[7]!=0x74||s.Link.Position.X!=120,
                    $"The source boss door must reject pushing without dungeon$01's boss key: Link{s.Link.Position}; {string.Join("; ",path)}.");
                s.Dialogue.Close();app.Step(2,Vector2.Zero,batched:batched);
                s.Inventory.GrantDungeonBossKey(1);
                Walk(Vector2.Up,()=>s.Room.Layout[7]!=0x74);
                FailIf(s.RoomId!=0x18||s.Room.Layout[7]!=0xa0||!s.Inventory.HasDungeonBossKey(1),
                    "Pushing the native boss door with its acquired key must open it without consuming the key.");
                s.DebugTravel(4,0x17);
                FailIf(s.Room.Layout[0x23]!=0xf0||s.Inventory.UnappraisedRingCount!=rings+1,"Ring-room re-entry must retain its opened chest and collected ring.");
                s.DebugTravel(4,0x18);app.Step(3,Vector2.Zero,batched:batched);
                FailIf(entities.Entities<ItemDropProducer>().Count!=0||entities.RoomEnemyCount!=1,
                    "Immediate re-entry must suppress consumed fairy producers and reset their leaked counts.");
            }
            finally {root.Free();}
        }
    }
}
