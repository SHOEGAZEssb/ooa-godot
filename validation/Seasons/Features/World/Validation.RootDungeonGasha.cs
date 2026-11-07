namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonGasha()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
                s.DebugTravel(4,0x0d);s.Link.WarpTo(new(232,88));
                FailIf(s.Collision.Collides(s.Link.Position),"The optional Gasha puzzle must start on its original east entrance floor.");
                void Walk(int packed)
                {
                    Vector2 target=new((packed&15)*16+8,(packed>>4)*16+8);
                    for(int tick=0;tick<220&&s.Link.Position!=target;tick++)
                    {
                        Vector2 input=s.Link.Position.X==target.X?new(0,Math.Sign(target.Y-s.Link.Position.Y)):new(Math.Sign(target.X-s.Link.Position.X),0);
                        app.Step(1,input,batched:batched);
                    }
                    FailIf(s.Link.Position!=target||s.Collision.Collides(s.Link.Position),$"The original Gasha maze must admit floor waypoint${packed:x2}: Link{s.Link.Position}, target{target}.");
                }
                void Push(int packed,Vector2 direction)
                {
                    for(int tick=0;tick<100&&s.Room.Layout[packed]==0x1c;tick++)app.Step(1,direction,batched:batched);
                    app.Step(24,Vector2.Zero,batched:batched);
                    FailIf(s.Room.Layout[packed]==0x1c,$"The Gasha maze's original ${packed:x2} block must accept the source push.");
                    Walk(packed);
                }
                Walk(0x5d);Walk(0x7d);Walk(0x79);Push(0x69,Vector2.Up);
                Walk(0x68);Push(0x67,Vector2.Left);
                Walk(0x57);Walk(0x55);Walk(0x75);Push(0x74,Vector2.Left);Push(0x84,Vector2.Down);
                Walk(0x82);Walk(0x62);Walk(0x61);Walk(0x51);
                Walk(0x21);app.Step(8,Vector2.Up,batched:batched);app.Step(2,Vector2.Zero,batched:batched);
                s.Link.Face(Vector2I.Up);int seeds=s.Inventory.GashaSeeds;
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);app.Step(40,Vector2.Zero,batched:batched);
                FailIf(s.Room.Layout[0x11]!=0xf0||!s.Save.HasRoomFlag(4,0x0d,0x20)||s.Inventory.GashaSeeds!=seeds+1,
                    $"The four original pushes and north ladder must reach the $11 Gasha Seed chest, granting exactly one seed: Link{s.Link.Position}, tile${s.Room.Layout[0x11]:x2}, seeds{s.Inventory.GashaSeeds}/{seeds}, text{s.Dialogue.SourceTextId:x4}.");
                s.Dialogue.Close();app.Step(80,Vector2.Zero,batched:batched);
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);app.Step(40,Vector2.Zero,batched:batched);
                FailIf(s.Dialogue.IsOpen||s.Inventory.GashaSeeds!=seeds+1,"Repeating the opened Gasha chest action must not grant another seed.");
                s.DebugTravel(4,0x0e);s.DebugTravel(4,0x0d);app.Step(3,Vector2.Zero,batched:batched);
                FailIf(s.Room.Layout[0x11]!=0xf0||s.Inventory.GashaSeeds!=seeds+1,"Gasha-room re-entry must retain the collected chest and reset its ordinary push blocks.");
            }
            finally {root.Free();}
        }
    }
}
