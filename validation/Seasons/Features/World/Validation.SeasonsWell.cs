
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsHerosCaveWell()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var session=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
                session.Inventory.GiveTreasure(TreasureId.Sword,1);session.Inventory.EquipA(TreasureId.Sword);
                void EnterWell()
                {
                    session.DebugTravel(0,0xd4);session.Link.WarpTo(new(120,24));session.Link.Face(Vector2I.Down);
                    FailIf(session.Collision.Collides(session.Link.Position),"Hero's Cave well must be approached from its actual northern floor.");
                    app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                    app.Step(20,Vector2.Zero,batched:batched);
                    FailIf(session.Room.GetPackedStorageMetatile(0x27)==0xc4,"The actual sword caller must cut the bush above the well's ledge.");
                    for(int update=0;update<80 && !session.Transitions.IsTransitioning;update++)app.Step(1,Vector2.Down,batched:batched);
                    FailIf(session.Transitions.PhaseName!="HoleFall" || session.RoomId!=0xd4,
                        $"The actual $0:d4 well at$57 must select source$09: phase={session.Transitions.PhaseName}, room=${session.RoomId:x2}, position={session.Link.PrecisePosition}.");
                }
                for(int repeat=0;repeat<2;repeat++)
                {
                    var sounds=root._sound.AttachPlayRequestAudit();int health=session.Link.HealthQuarters;
                    EnterWell();app.Step(180,Vector2.Zero,batched:batched);
                    FailIf(session.Rooms.ActiveGroup!=4 || session.RoomId!=5 || session.Transitions.IsTransitioning ||
                        session.Link.IsRoomWarpFalling || session.Link.HealthQuarters!=health ||
                        sounds.RequestsFor(SoundId.SndLinkFall)!=1 || sounds.Requests.Contains(SoundId.SndEnterCave),
                        "Hero's Cave well must finish source$09 and destination$05 in $4:05 without damage, repeated cues or retained control.");
                    var before=session.Link.PrecisePosition;app.Step(3,Vector2.Up,batched:batched);
                    FailIf(before==session.Link.PrecisePosition,"The well's falling arrival must return actual movement input.");
                }
                EnterWell();app.Step(1,Vector2.Zero,batched:batched);
                FailIf(session.Link.ScriptedLinkAnimationMode!=0x0d,"The retirement fixture requires an active native hole animation.");
                var engine=session.Engine!;
                root._scene.Free();
                FailIf(engine.Active || session.Link.ScriptedLinkAnimationMode is not null,
                    "Scene retirement must cancel the source animation and its transition owner.");
            }
            finally {root.Free();}
        }
    }
}
