namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonHandGrab()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool cancel in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var entities=s.Entities;var app=new ApplicationValidationFixture(root);
                s.DebugTravel(4,0x17);s.Link.WarpTo(new(120,88));s.Inventory.EquipA(0);s.Inventory.EquipB(0);
                FailIf(s.Collision.Collides(s.Link.Position)||s.Memory.ReadField("wDungeonWallmasterDestRoom")!=0x1c,
                    "The source $4:17 grab fixture must begin on original floor with dungeon$01's imported return room$1c.");
                for(int tick=0;tick<600&&!s.Link.WallmasterGrabPending;tick++)app.Step(1,Vector2.Zero,batched:batched);
                FailIf(!s.Link.WallmasterGrabPending||s.Link.WallmasterGrabActive||!s.Link.PatchCollisionsEnabled,
                    "An actual native hand chase/contact must publish Link state$0c for the following update.");
                app.Step(1,Vector2.Zero,batched:batched);
                FailIf(!s.Link.WallmasterGrabActive||s.Link.WallmasterGrabSubstate!=1||s.Link.PatchCollisionsEnabled||
                    s.Memory.ReadField("wWarpsDisabled")!=1||s.Memory.ReadField("wScrollMode")!=0,
                    "Link's next update must consume the grab, disable collision/warps and clear wScrollMode.");
                var hand=entities.Entities<FloormasterCharacter>().Single(actor=>actor.State==12);
                int remaining=hand.Animation.RemainingUpdates;
                s.Dialogue.ShowGameplayMessage("Grab pause.",100);app.Step(4,Vector2.Zero,batched:batched);
                FailIf(!s.Link.WallmasterGrabActive||hand.Animation.RemainingUpdates!=remaining,
                    "The captured Link owner must remain active while text pauses the hand's initialized animation.");
                s.Dialogue.Close();
                if(cancel)
                {
                    s.DebugTravel(4,0x1b);
                    FailIf(s.Link.WallmasterGrabActive||s.Link.WallmasterGrabPending||!s.Link.Visible||
                        !s.Link.PatchCollisionsEnabled||s.Memory.ReadField("wWarpsDisabled")!=0||entities.Entities<FloormasterCharacter>().Count!=0,
                        "Room replacement must cancel a captured Link and retire both the controller and its children.");
                }
                else
                {
                    for(int tick=0;tick<240&&s.RoomId!=0x1c;tick++)app.Step(1,Vector2.Zero,batched:batched);
                    FailIf(s.Rooms.ActiveGroup!=4||s.RoomId!=0x1c,"The terminal grab must request the original $4:1c falling return warp.");
                    for(int tick=0;tick<300&&s.Transitions.IsTransitioning;tick++)app.Step(1,Vector2.Zero,batched:batched);
                    FailIf(s.Transitions.IsTransitioning||s.Link.WallmasterGrabActive||s.Link.WallmasterGrabPending||
                        s.Memory.ReadField("wWarpsDisabled")!=0||!s.Link.Visible||entities.Entities<FloormasterCharacter>().Count!=0,
                        "The source falling arrival must release the hand owner and restore visible Link/warp admission.");
                    s.Dialogue.Close();app.Step(4,Vector2.Zero,batched:batched);
                }
                // The same real contact remains usable after either teardown.
                s.DebugTravel(4,0x17);s.Link.WarpTo(new(120,88));
                for(int tick=0;tick<600&&!s.Link.WallmasterGrabPending;tick++)app.Step(1,Vector2.Zero,batched:batched);
                FailIf(!s.Link.WallmasterGrabPending,"A repeated room visit must construct a fresh four-hand controller and admit another grab.");
            }
            finally {root.Free();}
        }
    }
}
