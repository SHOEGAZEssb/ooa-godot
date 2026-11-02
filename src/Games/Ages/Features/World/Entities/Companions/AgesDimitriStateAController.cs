
namespace Oracles;

// dimitri.s: Ages stateA var03$02-$04. D1 and physical movement stay with the shared body.
internal sealed class AgesDimitriStateAController(OracleRuntimeState memory,Action<int> playSound)
{
    internal void Update(DimitriCompanionRoomEntity body,RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        switch(memory.ReadWramByte(0xd103))
        {
            case 2:
                if(memory.ReadField("wLinkObjectIndex")!=0xd1)return;
                body.Dismount(frame.Player);
                CompanionRuntimeState.ForgetRemembered(memory);
                return;
            case 3:
                memory.SetWramByte(0xd108,1);memory.SetWramByte(0xd109,8);
                body.SetAnimation(0);memory.SetWramByte(0xd103,4);playSound(SoundId.SndDimitri);
                return;
            case 4:
                body.AdvanceNativeMovement(spawns);
                memory.SetWramByte(0xd104,0x0a);
                if(unchecked((byte)(OracleObjectPosition.HighByte(body.PrecisePosition.X)+7))<0xaf &&
                    unchecked((byte)(OracleObjectPosition.HighByte(body.PrecisePosition.Y)+7))<0x8f)return;
                memory.SetField("wDisabledObjects",0);memory.SetField("wMenuDisabled",0);memory.SetField("wUseSimulatedInput",0);
                CompanionRuntimeState.Clear(memory,SpecialObjectId.Dimitri);
                return;
            default:
                throw new NotSupportedException($"dimitri.s: Ages state$0a var03${memory.ReadWramByte(0xd103):x2} requires its scripted presenter or native handler ($02-$04).");
        }
    }
}
