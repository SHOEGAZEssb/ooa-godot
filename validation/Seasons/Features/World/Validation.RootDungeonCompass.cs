namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonCompassPath()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
                s.DebugTravel(4,0x0e);s.Memory.SetField("wDisabledObjects",4);
                s.Inventory.EquipA(0);s.Inventory.EquipB(0);
                // The cart handoff is covered separately. Begin on its real
                // landing platform and isolate the raised-floor chest route.
                s.Link.WarpTo(new(152,134));
                void Walk(Vector2 input,Func<bool> arrived,int limit)
                {
                    for(int tick=0;tick<limit&&!arrived();tick++)app.Step(1,input,batched:batched);
                    FailIf(!arrived(),$"The original compass path must remain reachable: room${s.RoomId:x2}, Link{s.Link.Position}, input{input}.");
                }
                Walk(Vector2.Up,()=>s.Link.Position.Y<=104,48);
                Walk(Vector2.Right,()=>s.Link.Position.X>=200,64);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=24,140);
                FailIf(s.Room.Layout[0x5c]!=0x52 || s.Collision.Collides(s.Link.Position),
                    "$4:0e's original $5c stairs must connect the cart floor to the raised eastern passage.");
                Walk(Vector2.Right,()=>s.Scrolling,64);
                FailIf(s.RoomId!=0x0f,"The raised eastern $4:0e passage must scroll into the compass floor of $4:0f.");
                for(int tick=0;tick<120&&s.Scrolling;tick++)app.Step(1,Vector2.Zero,batched:batched);
                s.Memory.SetField("wDisabledObjects",4);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=24,40);
                Walk(Vector2.Right,()=>s.Link.Position.X>=168,300);
                Walk(Vector2.Down,()=>s.Link.Position.Y>=64,64);
                Walk(Vector2.Right,()=>s.Link.Position.X>=184,32);
                s.Link.Face(Vector2I.Up);
                // This narrow shelf admits the native horizontal wall probes
                // at y/y+5; a full bounding box overlaps the chest above it.
                FailIf(new[]{new Vector2(-5,0),new Vector2(4,0),new Vector2(-5,5),new Vector2(4,5)}
                    .Any(offset=>s.Room.HasSimpleCollision(s.Link.Position+offset)) || s.Room.Layout[0x3b]!=0xf1,
                    $"The source $3b compass chest must be reachable from its southern shelf: Link{s.Link.Position}, tile${s.Room.Layout[0x3b]:x2}.");
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                app.Step(40,Vector2.Zero,batched:batched);
                FailIf(!s.Inventory.HasDungeonCompass(1) || s.Inventory.HasDungeonCompass(0) || s.Inventory.HasDungeonCompass(2) ||
                    !s.Save.HasRoomFlag(4,0x0f,0x20) || s.Room.Layout[0x3b]!=0xf0,
                    "The original compass chest must grant only dungeon$01's compass and persist ROOMFLAG_ITEM.");
                s.Dialogue.Close();app.Step(80,Vector2.Zero,batched:batched);
                s.DebugTravel(4,0x0e);s.DebugTravel(4,0x0f);app.Step(3,Vector2.Zero,batched:batched);
                FailIf(s.Room.Layout[0x3b]!=0xf0 || !s.Inventory.HasDungeonCompass(1),
                    "Re-entering the compass room must retain its opened source chest.");
            }
            finally {root.Free();}
        }
    }
}
