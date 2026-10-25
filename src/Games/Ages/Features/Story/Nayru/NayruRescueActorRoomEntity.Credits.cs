namespace Oracles;

internal sealed partial class NayruRescueActorRoomEntity
{
    private bool CreditsSwordActor=>Id==0x37&&SubId==8;
    private void InitializeCreditsSword()
    {
        if(_context.Entities.InteractionSlotAvailable)
            _context.Entities.AddEntity(new LinkedSwordRoomEntity(_data.CreateActor(0x5e,0,_precise),
                ()=>Finished?null:_actor,index=>_data.Animation(0x5e,index)));
        Start("ralph-credits-sword");_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex,withTerrainEffects:true);
    }
    private bool LoadCreditsSwordPreset()
    {
        var table=_data.Table("ralph-credits-sword");int index=_presetIndex*2;
        _direction=table[index];int counter=table[index+1];
        if(counter==0)return false;
        _counter=counter;_presetIndex++;return true;
    }
    private void UpdateCreditsSword()
    {
        // ralph.s:ralphSubid08 animates once before each script/movement pass.
        _actor.AdvanceAnimationUpdates(1);
        switch(Substate)
        {
            case 0:
                AdvanceScript();if(_scriptEnded){Substate=1;LoadCreditsSwordPreset();}return;
            case 1:
                MoveRaw(0x1e,_direction);
                if(--_counter!=0||LoadCreditsSwordPreset())return;
                Substate=2;_counter=90;SetAnimation(8);return;
            case 2:
                _counter=(_counter-1)&255;if(_counter==0)Write(0xcfdf,255);return;
            default:throw UnsupportedCommand($"ralphSubid08 substate ${Substate:x2}");
        }
    }
    private bool CreditsPerformanceActor=>Id==0x36&&SubId==6||Id==0x39&&SubId==6;
    private void InitializeCreditsPerformance()
    {_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex,withTerrainEffects:true);SetAnimation(Id==0x36?7:5);}
    private void UpdateCreditsPerformance()
    {
        _actor.AdvanceAnimationUpdates(1);
        if(Id!=0x36)return;
        int parameter=_actor.ConsumeAnimationParameter();
        if(parameter!=0)NayruRescueMusicNoteRoomEntity.Spawn(_context,_precise.Floor()+new Godot.Vector2(parameter==1?-6:8,-4),parameter==1);
    }
    private bool CreditsMakuActor=>Id==0x36&&SubId==5||Id==0x37&&SubId==7||Id==0x87&&SubId==4;
    private void InitializeCreditsMaku()
    {
        if(Id==0x87)
        {
            Write(AgesWramAddress.wLoadedTreeGfxIndex,0x87);Write(AgesWramAddress.wInteractionIDToLoadExtraGfx,0x87);
            SetAnimation(0);Start("maku-credits");_actor.SetFixedDrawPriority(ObjectDrawPriority.FixedLowPriorityZIndex);
            CreateFlower();AdvanceScript();_actor.AdvanceAnimationUpdates(1);return;
        }
        Start(Id==0x36?"nayru-credits-maku":"ralph-credits-maku");
        if(Id==0x36)SetAnimation(5);
        _actor.SetFixedDrawPriority(Id==0x36?ObjectDrawPriority.BehindLinkZIndex:ObjectDrawPriority.FixedHighPriorityZIndex,withTerrainEffects:Id==0x36);_actor.Visible=false;
    }
    private void UpdateCreditsMaku()
    {
        if(Id==0x37)
        {
            PublishTarget();
            if(Substate==1)
            {
                Substate=2;_speedZ=-0x1c0;
                _actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex,withTerrainEffects:true);
            }
            if(Substate==2)
            {
                if(OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20))
                {
                    Substate=3;_endingJump=(_endingJump+1)&255;
                    _actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
                }
                return;
            }
        }
        AnimateMotion();AdvanceScript();
        if(Id==0x36&&Read(0xcfc0) is >=3 and <5&&(FrameCounter&15)==0)
        {
            int direction=((OracleObjectMovement.Shared.RelativeAngle(_precise.Floor(),new(Read(0xcfd6),Read(0xcfd5)))+4)&0x18)>>3;
            if(direction!=_direction){_direction=direction;SetAnimation(direction);}
        }
    }
}
