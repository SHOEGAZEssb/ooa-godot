
namespace Oracles;

// ricky.s: Ages rickyStateASubstate2-$07. The shared body retains D1 and its presenter.
internal sealed class AgesRickyStateAController(OracleRuntimeState memory,RoomSession rooms,RickyDatabase data,
    AgesRickyDepartureDatabase departure,Func<bool> textActive,Action<int,string,Vector2> showText,Action<int> playSound)
{
    private TingleDatabase? _text;
    private int Direction {get=>memory.ReadWramByte(0xd108);set=>memory.SetWramByte(0xd108,(byte)value);}
    private int Angle {get=>memory.ReadWramByte(0xd109);set=>memory.SetWramByte(0xd109,(byte)value);}
    private int Counter {get=>memory.ReadWramByte(0xd106);set=>memory.SetWramByte(0xd106,(byte)value);}
    private int Stage {get=>memory.ReadWramByte(0xd103);set=>memory.SetWramByte(0xd103,(byte)value);}
    private int Speed {get=>memory.ReadWramByte(0xd110);set=>memory.SetWramByte(0xd110,(byte)value);}
    private int Word(int at)=>unchecked((short)(memory.ReadWramByte(at)|memory.ReadWramByte(at+1)<<8));
    private void Word(int at,int value)
    {memory.SetWramByte(at,unchecked((byte)value));memory.SetWramByte(at+1,unchecked((byte)(value>>8)));}
    private bool Gravity(int gravity)
    {
        int z=Word(0xd10e),speedZ=Word(0xd114);
        bool landed=OracleObjectMath.UpdateSpeedZ(ref z,ref speedZ,gravity);
        Word(0xd10e,z);Word(0xd114,speedZ);return landed;
    }
    private void SetAnimation(RickyCompanionRoomEntity body,int mode)
    {body.NativeAnimation.SetAnimation(mode);memory.SetWramByte(0xd130,(byte)mode);}
    private void Hop()
    {
        Word(0xd114,data.Behavior.HopSpeedZ);Speed=data.Behavior.HopSpeed;
        memory.SetWramByte(0xd105,1);Counter=departure.Parameters.HopDelay;
    }
    internal void Update(RickyCompanionRoomEntity body,RoomEntityFrame frame)
    {
        var p=departure.Parameters;var behavior=data.Behavior;
        switch(Stage)
        {
            case 2:
                if(!Gravity(data.IdleGravity))return;
                showText(0x2006,(_text??=new()).Text(0x2006),body.PrecisePosition);
                Direction=OracleObjectPosition.HighByte(body.PrecisePosition.Y)<OracleObjectPosition.HighByte(frame.Player.PrecisePosition.Y)?2:0;
                memory.SetWramByte(0xd13f,(byte)p.AirAnimation);
                SetAnimation(body,p.AirAnimation);Stage=3;Hop();return;
            case 3:
                if(textActive())return;
                Direction=2;Angle=p.DiagonalAngle;
                SetAnimation(body,p.GroundAnimationBase+Direction);Stage=4;return;
            case 4:
            case 7:
                int air=memory.ReadWramByte(0xd13f);
                if(body.NativeAnimation.AnimationIndex!=air)SetAnimation(body,air);
                if(!Gravity(behavior.HopGravity))
                {body.ApplyCompanionMovement(Speed);return;}
                SetAnimation(body,p.GroundAnimationBase+Direction);
                if(Counter!=0)Counter--;
                if(Counter!=0)return;
                Angle=p.WallProbeAngle;
                if(CompanionMovement.FacingWallMask(Angle,memory.ReadWramByte(0xd133))!=0)
                {
                    Angle=p.ExitAngle;
                    if(CompanionMovement.FacingWallMask(Angle,memory.ReadWramByte(0xd133))!=0)
                    {
                        Word(0xd114,behavior.LongJumpSpeedZ);Speed=behavior.LongJumpSpeed;Counter=behavior.LongJumpDelay;
                        SetAnimation(body,behavior.LongJumpAnimation+Direction);playSound(behavior.JumpSound);Stage++;return;
                    }
                }
                int x=OracleObjectPosition.HighByte(body.PrecisePosition.X),y=OracleObjectPosition.HighByte(body.PrecisePosition.Y);
                if(unchecked((byte)(y+7))>=0x8f || unchecked((byte)(x+7))>=0xaf)
                {Finish(frame.Player);return;}
                Angle=Stage==7?p.ExitAngle:p.DiagonalAngle;Hop();return;
            case 5:
                body.NativeAnimation.Advance();body.ApplyRawMovement(Speed);
                if(!Gravity(behavior.HopGravity))return;
                SetAnimation(body,p.PunchAnimation);Stage=6;return;
            case 6:
                body.NativeAnimation.Advance();int parameter=body.NativeAnimation.CurrentParameter;
                if(parameter==0){playSound(SoundId.SndRicky);return;}
                if((parameter&0x80)==0)return;
                Hop();Angle=p.ExitAngle;SetAnimation(body,p.GroundAnimationBase+Direction);Stage=7;return;
            default:
                throw new NotSupportedException($"ricky.s: Ages state$0a var03${Stage:x2} has no bound departure handler ($02-$07).");
        }
    }
    private void Finish(Player player)
    {
        memory.SetField("wDisabledObjects",0);memory.SetField("wMenuDisabled",0);
        CompanionRuntimeState.ForgetRemembered(memory);
        CompanionRuntimeState.Clear(memory,SpecialObjectId.Ricky);
        rooms.SaveData.WriteField("wRickyState",(byte)(rooms.SaveData.ReadField("wRickyState")|departure.Parameters.LeftMask));
        // saveLinkLocalRespawnAndCompanionPosition reads the slot after itemDelete cleared it.
        CompanionRuntimeState.Remember(memory,0,rooms.ActiveGroup,rooms.CurrentRoom.Id,Vector2.Zero);
        memory.SetField("wLinkLocalRespawnY",0);memory.SetField("wLinkLocalRespawnX",0);memory.SetField("wLinkLocalRespawnDir",0);
        player.SetLocalRespawnPosition(Vector2.Zero,Vector2I.Up);
    }
}
