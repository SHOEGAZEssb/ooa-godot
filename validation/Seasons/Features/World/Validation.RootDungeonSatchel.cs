namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonSatchel()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var entities=s.Entities;var app=new ApplicationValidationFixture(root);
                int receivedText=-1;
                entities.GroundTreasureDialogueRequested+=(_,record,_)=>receivedText=record.TextId;
                // The paired defeat has its own focused regression.
                s.Save.SetRoomFlag(4,0x0b,0x80);s.DebugTravel(4,0x0b);s.Link.WarpTo(new(96,88));
                s.Inventory.EquipA(0);s.Inventory.EquipB(0);
                void Walk(Vector2 input,Func<bool> arrived,int limit=240)
                {
                    for(int tick=0;tick<limit&&!arrived();tick++)app.Step(1,input,batched:batched);
                    FailIf(!arrived(),$"The native Satchel route must remain reachable: room${s.Rooms.ActiveGroup:x1}:${s.RoomId:x2}, Link{s.Link.Position}, input{input}, transitioning{s.Transitions.IsTransitioning}.");
                }
                void Arrive()
                {
                    for(int tick=0;tick<240&&s.Transitions.IsTransitioning;tick++)app.Step(1,Vector2.Zero,batched:batched);
                    FailIf(s.Transitions.IsTransitioning,"The Satchel route's native transition must complete.");
                }
                Walk(Vector2.Left,()=>s.Scrolling);Arrive();
                FailIf(s.RoomId!=0x0a||entities.Entities<BladeTrapCharacter>().Count!=4||entities.RoomEnemyCount!=0,
                    "The defeated miniboss's west shutter must enter $4:0a's four uncounted blue traps.");
                s.Memory.SetField("wDisabledObjects",4);entities.DisableLinkCollisionsAndMenu();
                Walk(Vector2.Left,()=>s.Link.Position.X<=200);
                Walk(Vector2.Down,()=>s.Link.Position.Y>=152);
                Walk(Vector2.Left,()=>s.Link.Position.X<=72);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=104);
                Walk(Vector2.Up,()=>s.Room.Layout[0x54]!=0x18);
                app.Step(32,Vector2.Zero,batched:batched);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=88);
                FailIf(s.Room.Layout[0x54]!=0xa0||s.Room.Layout[0x44]!=0x1d,
                    "The original $18 block must be pushed north once to open the diamond's entrance.");
                Walk(Vector2.Right,()=>s.Link.Position.X>=104);entities.EnableLinkCollisionsAndMenu();
                Walk(Vector2.Right,()=>s.Rooms.ActiveGroup==6);Arrive();
                FailIf(s.RoomId!=9||!s.Link.World.SideScrolling||s.Rooms.CurrentDungeonIndex!=1||
                    s.Room.Width!=240||s.Room.Height!=176||entities.Entities<GroundTreasurePickup>().Count!=1||
                    entities.Entities<GroundTreasurePickup>().Single().Position!=new Vector2(104,88)||
                    s.Inventory.HasTreasure(TreasureId.SeedSatchel),
                    "The original stairs must enter native $6:09 and construct one uncollected SEED_SATCHEL_00 at ($68,$58).");
                Walk(Vector2.Down,()=>s.Link.Position.Y>=136,360);
                Walk(Vector2.Right,()=>s.Link.Position.X>=200);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=90);
                Walk(Vector2.Left,()=>s.Inventory.HasTreasure(TreasureId.SeedSatchel));
                app.Step(3,Vector2.Zero,batched:batched);
                FailIf(s.Inventory.SeedSatchelLevel!=1||s.Inventory.EmberSeeds!=0x20||receivedText!=0x002d||
                    !s.Save.HasRoomFlag(6,9,0x20)||!s.Save.HasRoomFlag(4,9,0x20)||!s.Link.IsHoldingItemTwoHands,
                    $"The actual upper-corridor pickup must grant Satchel level1, BCD$20 Ember Seeds, text$002d and the canonical ROOMFLAG_ITEM: level{s.Inventory.SeedSatchelLevel}, seeds${s.Inventory.EmberSeeds:x2}, text{receivedText}, flag6{s.Save.HasRoomFlag(6,9,0x20)}, flag4{s.Save.HasRoomFlag(4,9,0x20)}, hold{s.Link.IsHoldingItemTwoHands}, Link{s.Link.Position}.");
                s.Dialogue.Close();app.Step(80,Vector2.Zero,batched:batched);
                FailIf(s.Link.IsHoldingItemTwoHands||s.Link.CutsceneControlled,
                    "Satchel dialogue completion must release the shared held-item control.");
                Walk(Vector2.Right,()=>s.Link.Position.X>=200);
                Walk(Vector2.Down,()=>s.Link.Position.Y>=136);
                Walk(Vector2.Left,()=>s.Link.Position.X<=40);
                Walk(Vector2.Up,()=>s.Rooms.ActiveGroup==4,360);Arrive();
                FailIf(s.RoomId!=0x0a,"The original ladder must return from $6:09 to $4:0a's stairs.");
                s.Memory.SetField("wDisabledObjects",4);entities.DisableLinkCollisionsAndMenu();
                Walk(Vector2.Left,()=>s.Link.Position.X<=88);
                Walk(Vector2.Down,()=>s.Room.Layout[0x65]!=0x1a);
                app.Step(32,Vector2.Zero,batched:batched);
                Walk(Vector2.Down,()=>s.Link.Position.Y>=104);
                FailIf(s.Room.Layout[0x65]!=0xa0||s.Room.Layout[0x75]!=0x1d,
                    "The diamond's original $1a block must push south to open the return route.");
                entities.EnableLinkCollisionsAndMenu();s.Link.Face(Vector2I.Down);
                s.Inventory.EquipA(TreasureId.SeedSatchel);
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                FailIf(!s.Link.IsUsingSeedSatchel||s.Inventory.EmberSeeds!=0x19||entities.Entities<EmberSeedEffect>().Count!=1,
                    "The collected dungeon item must use the shared Satchel input and consume one packed-BCD Ember Seed.");
                app.Step(80,Vector2.Zero,batched:batched);
                s.DebugTravel(6,9);app.Step(3,Vector2.Zero,batched:batched);
                FailIf(entities.Entities<GroundTreasurePickup>().Count!=0||s.Inventory.SeedSatchelLevel!=1||s.Inventory.EmberSeeds!=0x19,
                    "Collected-room re-entry must suppress the native producer without refilling or duplicating the Satchel.");
            }
            finally {root.Free();}
        }
    }
}
