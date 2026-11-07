namespace Oracles;

// makuLeaf.s: the producer and its three children use the live interaction walk.
internal sealed partial class SeasonsMakuLeafRoomEntity : DungeonInteractionVisualEntity,
    INativeInteractionRoomEntity,IFixedRoomEntity,IRoomEntityLifetime
{
    private readonly RoomEntityManager _owner;
    private readonly SeasonsDungeonEssenceDatabase _data;
    private NativeInteractionState _state=null!;
    private readonly int _subId;
    public Node2D Node=>this;
    public bool Finished {get;private set;}
    public bool NativeInitializationPending=>!Finished&&_state[0x44]==0;
    internal SeasonsMakuLeafRoomEntity(RoomEntityManager owner,SeasonsDungeonEssenceDatabase data,int subId)
    {
        _owner=owner;_data=data;_subId=subId;InitializeVisual(data.Visual("maku-leaf"),subId==0?new(80,64):Vector2.Zero);
        Visible=false;ZIndex=ObjectDrawPriority.FixedHighPriorityZIndex;
    }
    public void BindNativeInteractionSlot(int slot)
    {_state=new(_owner.RuntimeState,slot);_state.Clear();_state[0x40]=1;_state[0x41]=0x48;_state[0x42]=_subId;_state.Position=Position;}
    public void ReleaseNativeInteractionSlot() { }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset)=>SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        switch(_state[0x44])
        {
            case 0:_state[0x44]=_subId==0?1:2;_state[0x7a]=1;return;
            case 1:
                if(--_state[0x7a]!=0)return;
                if(!_owner.InteractionSlotAvailable)return;
                int child=++_state[0x46];_owner.AddEntity(new SeasonsMakuLeafRoomEntity(_owner,_data,child));
                if(child==3){Finish();return;}
                _state[0x7a]=_data.Table("leaf-delays")[child];return;
            case 2:
                _state[0x44]=3;var positions=_data.Table("leaf-positions");
                _state.Position=new(positions[_subId*2+1],positions[_subId*2]);_state[0x7b]=11;
                ReadMotion(0);_state[0x7c]=0x70;_owner.OnSoundRequested(0x83);Visible=true;Present();return;
            case 3:
                _state.Position=NativeObjectMovement.ApplySpeed(_owner.RuntimeState,OracleObjectPosition.FromPixels(_state.Position),_state[0x50],_state[0x49]).PrecisePosition;
                Present();
                bool sound=_state[0x7c]!=0&&--_state[0x7c]==0;
                if(sound)_owner.OnSoundRequested(0x83);
                // playSound leaves H=$c0; the following probes alias audio
                // instead of this leaf until interactionDecCounter1 reloads H.
                int x=sound?_owner.NativeSoundAllocationAliasSource(0xc04d):_state[0x4d];
                if((x&0xf0)==0xf0){Finish();return;}
                int sparkleCounter;
                if(sound)
                {
                    sparkleCounter=(_owner.NativeSoundAllocationAliasSource(0xc07b)-1)&255;
                    _owner.WriteSoundAllocationAlias(0xc07b,(byte)sparkleCounter);
                }
                else sparkleCounter=--_state[0x7b];
                if(sparkleCounter==0)
                {
                    var v=_data.Visual("leaf-sparkle");
                    _owner.TryCreateInteraction(new SeasonsMakuLeafSparkleSpawn(_state.Position.Floor(),new(v.AssetRoot,v.Sprites.Single(),v.TileBase,v.Palette,v.Animations.Single())));
                    // table_701e's $18,$f2 executes JR back to func_7012.
                    _state[0x7b]=11;
                }
                if(--_state[0x46]==0)ReadMotion(_state[0x78]);
                return;
            default:throw new NotSupportedException($"makuLeaf.s INTERAC$48:${_subId:x2}: state${_state[0x44]:x2}.");
        }
    }
    private void ReadMotion(int offset)
    {
        var motion=_data.Table("leaf-motion");if(motion[offset]==0xff){Finish();return;}
        _state[0x49]=motion[offset];_state[0x46]=motion[offset+1];_state[0x50]=motion[offset+2];_state[0x78]=offset+3;
    }
    private void Present()=>Position=_state.Position.Floor();
    private void Finish(){Finished=true;Visible=false;}
    public override void _Draw()
    {if(Visible)DrawTexture(CurrentTexture,CurrentDrawOffset+SourceOamDrawOffset);}
}

internal sealed record SeasonsMakuLeafSparkleSpawn(Vector2 Position,InteractionSparkleVisual Visual):RoomEntitySpawn;
