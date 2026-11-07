namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonBombPassage()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);var entities=s.Entities;
                s.DebugTravel(4,0x10);s.Memory.SetField("wDisabledObjects",4);
                s.Link.WarpTo(new(120,136));s.Inventory.EquipA(0);s.Inventory.EquipB(0);
                void Walk(Vector2 input,Func<bool> arrived,int limit=160)
                {
                    for(int tick=0;tick<limit&&!arrived();tick++)app.Step(1,input,batched:batched);
                    FailIf(!arrived(),$"$4:10/$0c corridor must remain reachable: room${s.RoomId:x2}, Link{s.Link.Position}, input{input}.");
                }
                Walk(Vector2.Up,()=>s.Link.Position.Y<=120);app.Step(8,Vector2.Up,batched:batched);
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                app.Step(40,Vector2.Zero,batched:batched);
                FailIf(s.Inventory.Bombs!=0x10||!s.Save.HasRoomFlag(4,0x10,0x20),
                    "The original $67 chest must grant BOMBS_00's packed-BCD ten Bombs and ROOMFLAG_ITEM.");
                s.Dialogue.Close();app.Step(80,Vector2.Zero,batched:batched);
                // The eastern stairs/cart approach has separate coverage.
                // Stage the unchanged upper floor to isolate Bomb -> passage.
                s.Link.WarpTo(new(120,24));s.Link.Face(Vector2I.Up);s.Inventory.EquipA(TreasureId.Bombs);
                FailIf(s.Collision.Collides(s.Link.Position)||s.Room.Layout[7]!=0x30,
                    "The bombable north wall must be approached from original upper floor.");
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                app.Step(19,Vector2.Zero,batched:batched);
                FailIf(!s.Link.IsCarryingObject||s.Inventory.Bombs!=9,"The source chest Bomb must complete its native lift.");
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                app.Step(32,Vector2.Down,batched:batched);app.Step(160,Vector2.Zero,batched:batched);
                FailIf(s.Room.Layout[7]!=0x34||!s.Save.HasRoomFlag(4,0x10,1)||!s.Save.HasRoomFlag(4,0x0c,4)||
                    entities.ActiveBombCount!=0||s.Inventory.Bombs!=9,
                    "One real Bomb must open both native wall flags, replace $30 with $34 and retire its child.");
                Walk(Vector2.Up,()=>s.Scrolling);
                FailIf(s.RoomId!=0x0c||entities.Entities<BladeTrapCharacter>().Count!=6,
                    "The bomb passage must enter $4:0c's original six-trap maze.");
                for(int tick=0;tick<240&&s.Scrolling;tick++)app.Step(1,Vector2.Zero,batched:batched);
                s.Memory.SetField("wDisabledObjects",4);s.Inventory.EquipA(0);
                // Trap AI/contact is verified separately; keep a frozen trap
                // from repeatedly knocking Link back in the narrow corridor.
                entities.DisableLinkCollisionsAndMenu();
                // Original winding floor; no tile edits, cuts or block teleports.
                Walk(Vector2.Up,()=>s.Link.Position.Y<=146);
                Walk(Vector2.Right,()=>s.Link.Position.X>=136);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=136);
                Walk(Vector2.Right,()=>s.Link.Position.X>=200);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=104);
                Walk(Vector2.Left,()=>s.Link.Position.X<=88);
                Walk(Vector2.Down,()=>s.Link.Position.Y>=152);
                Walk(Vector2.Left,()=>s.Link.Position.X<=56);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=120);
                Walk(Vector2.Left,()=>s.Link.Position.X<=24);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=24);
                Walk(Vector2.Right,()=>s.Link.Position.X>=120);
                entities.EnableLinkCollisionsAndMenu();
                entities.Inventory!.GiveTreasure(entities.Treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
                Walk(Vector2.Up,()=>s.Scrolling);
                FailIf(s.RoomId!=0x0b||s.Inventory.GetDungeonSmallKeys(1)!=0||
                    !s.Save.HasRoomFlag(4,0x0c,1)||!s.Save.HasRoomFlag(4,0x0b,4),
                    "The reachable north key door must spend one dungeon$01 key and enter the original miniboss room.");
                for(int tick=0;tick<240&&s.Scrolling;tick++)app.Step(1,Vector2.Zero,batched:batched);
                FailIf(entities.Entities<BrotherGoriyaCharacter>().Count!=2,
                    "The original $4:0b before-event leader must construct its linked brother after scrolling.");
                s.DebugTravel(4,0x10);s.Memory.SetField("wDisabledObjects",4);
                FailIf(s.Room.Layout[7]!=0x34,"Re-entry must retain the bombed north passage.");
                s.DebugTravel(4,0x0c);s.Memory.SetField("wDisabledObjects",4);
                FailIf(s.Room.Layout[7]!=0xa0||s.Room.Layout[0xa7]!=0x36,
                    "Both unlocked maze exits must survive room reconstruction without another Bomb or key.");
            }
            finally {root.Free();}
        }
    }
}
