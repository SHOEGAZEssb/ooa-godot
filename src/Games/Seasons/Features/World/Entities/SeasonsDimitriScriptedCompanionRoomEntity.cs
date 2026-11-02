
namespace Oracles;

// Seasons dimitriState0/stateA. Common movement and mounting retain the shared D1 owner.
internal sealed class SeasonsDimitriScriptedCompanionRoomEntity(NpcCharacter npc,OracleRuntimeState memory,
    OracleSaveData save,Func<OracleRoomData> currentRoom,DimitriVisualDatabase visual,
    Action<ScriptedCompanionRoomEntity,Player,bool> transfer)
    :ScriptedCompanionRoomEntity(npc,SpecialObjectId.Dimitri,memory,currentRoom)
{
    protected override void UpdateNativeFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        Npc.Position=OracleObjectMath.ToPixelPosition(NativeSpecialObjectPosition);
        if(RoomInitialization is {Pending:true} initialization)
        {
            Vector2 position=NativeSpecialObjectPosition;
            if(!initialization.Advance(ref position))return;
            NativeSpecialObjectPosition=position;Finished=initialization.Rejected;
            if(Finished)return;
            Memory.SetWramByte(0xd108,2);Memory.SetWramByte(0xd109,2);Npc.SetActive(true);
            if((save.ReadField("wDimitriState")&0x80)!=0)
            {
                SetAnimation(0x1e+Memory.ReadWramByte(0xd138));SetVisible(0x81);
                transfer(this,frame.Player,false);return;
            }
            Memory.SetWramByte(0xd104,0x0a);Npc.SetScriptButtonSensitive(true);
            Memory.SetWramByte(0xd13f,0x24);SetAnimation(0x24);
            Memory.SetWramByte(0xd126,4);Memory.SetWramByte(0xd127,8);SetVisible(0x81);return;
        }
        if(Memory.ReadWramByte(0xd104)!=0x0a)
            throw new NotSupportedException($"dimitri.s: Seasons scripted SPECIALOBJECT $0c state${Memory.ReadWramByte(0xd104):x2} is not state$0a.");
        int animation=Memory.ReadWramByte(0xd13f);
        if(Memory.ReadWramByte(0xd130)!=animation)SetAnimation(animation);
        LinkObjectBlocking.PreventPassing(frame.Player,Npc.Position,4,8);
        Npc.AdvanceAnimationUpdates(1);SetVisible(0xc7);
        if((save.ReadField("wDimitriState")&0x80)!=0)
        {
            SetVisible(0xc1);Memory.SetField("wStatusBarNeedsRefresh",0xff);
            SetAnimation(0x1c+Memory.ReadWramByte(0xd108)+Memory.ReadWramByte(0xd138));
            if(TryForceCompanionMount(frame.Player,value=>Memory.SetField("wMenuDisabled",value)))
            {PublishAnimation();transfer(this,frame.Player,true);return;}
        }
        PublishAnimation();
    }
    private void SetAnimation(int animation)
    {
        if((uint)animation>=visual.Animations.Length)throw new NotSupportedException($"dimitri.s: Seasons animation${animation:x2} has no imported stream.");
        Npc.SetScriptAnimation(visual.Animations[animation],visual.SourceOffsets[animation]);
        Memory.SetWramByte(0xd130,(byte)animation);PublishAnimation();
    }
    private void SetVisible(byte visible)
    {Memory.SetWramByte(0xd11a,visible);Npc.SetFixedDrawPriority(ObjectDrawPriority.FromVisible(visible),withTerrainEffects:(visible&0x40)!=0);}
    private void PublishAnimation()
    {CompanionNativeAnimationDatabase.For(visual.AssetRoot).PublishAnimation(Memory,SpecialObjectId.Dimitri,
        Memory.ReadWramByte(0xd130),Npc.CurrentAnimationFrame,Npc.CurrentAnimationCounter,Npc.CurrentAnimationParameter);}
}
