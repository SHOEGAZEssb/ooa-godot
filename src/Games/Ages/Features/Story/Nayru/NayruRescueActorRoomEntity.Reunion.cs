namespace Oracles;

internal sealed partial class NayruRescueActorRoomEntity
{
    private bool ReunionActor=>Id==0x36&&SubId==8||Id==0x87&&SubId==5;
    private void InitializeReunion()
    {
        if(Id==0x36)
        {
            SetAnimation(3);Start("nayru-reunion");
            _actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);return;
        }
        Write(AgesWramAddress.wLoadedTreeGfxIndex,0x87);Write(AgesWramAddress.wInteractionIDToLoadExtraGfx,0x87);
        SetAnimation(0);Start("maku-reunion");_actor.SetFixedDrawPriority(ObjectDrawPriority.FixedLowPriorityZIndex);
        CreateFlower();UpdateReunion(); // makuTree.s state zero falls through.
    }
    private void UpdateReunion()
    {
        if(Id==0x36){AnimateMotion();AdvanceScript();}
        else{AdvanceScript();_actor.AdvanceAnimationUpdates(1);}
    }
}
