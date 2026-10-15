using Godot;

namespace oracleofages;

// CUTSCENE_NAYRU_WARP_TO_MAKU_TREE $0c replaces cutscene01. Actor scripts
// still run at their own slots, after this handler, including during fades.
internal sealed class NayruRescueWarpEvent(RoomEventContext context) : IRoomEvent
{
    private readonly NayruRescueWarpDatabase _data=new();
    private readonly RoomEventResources _resources=new(context,new object());
    internal int State {get;private set;}
    public bool HasState {get;private set;}
    public bool BlocksGameplay => HasState;
    public bool OwnsGameLogic => HasState;
    public bool MenusDisabled => HasState;
    public bool AllScreenTransitionsDisabled => HasState;
    private byte Read(int address) => context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address,int value) => context.Entities.RuntimeState.SetWramByte(address,(byte)value);
    private bool Decrement(int address) {Write(address,Read(address)-1);return Read(address)==0;}
    internal void Begin() {HasState=true;State=0;_resources.CaptureFullScreenFade();}
    private void Actor(int id,int subid,int y,int x) => context.Entities.AddEntity(new NayruRescueActorRoomEntity(context,_data,id,subid,new(x,y)));
    private void Load(int group,int id)
    {
        var room=context.Rooms.LoadCutsceneRoom(group,id);
        context.Entities.LoadCutsceneRoom(group,room,includeTimePortals:false);
        context.RoomView.SetRoom(room.Texture);context.Transitions.ResetCamera();
        context.Player.WarpTo(Vector2.Zero,recordSafe:false);context.Player.Visible=false;
        if(id==0x38) {Actor(0x87,3,0x40,0x50);Actor(0x37,4,0x56,0x38);Actor(0x36,2,0x48,0x50);}
        else if(group==2 && id==0x0e) {Actor(0x3c,5,0x48,0x38);Actor(0x3d,3,0x48,0x48);}
        else if(group==0 && id==0x5a) Actor(0x39,4,0,0);
        else throw new System.NotSupportedException($"CUTSCENE$0c room {group:x}:{id:x2}.");
    }
    private void RevealLink()
    {context.Player.WarpTo(new(0x50,0x68),recordSafe:false);context.Player.Face(Vector2I.Up);context.Player.Visible=true;}
    internal void AdvanceBeforeObjects()
    {
        if(!HasState)return;
        context.Transitions.AdvanceNativeWhiteFade();
        bool faded=context.Transitions.NativePaletteFadeActive;
        switch(State)
        {
            case 0:
                if(faded)return;
                Load(0,0x38);Write(0xcbb3,60);
                for(int address=0xcfc0;address<0xcfe0;address++)Write(address,0);
                Write(WramAddress.wDisabledObjects,1);Write(WramAddress.wMenuDisabled,1);
                context.Sound.PlaySound(SoundId.SndCtrlStopMusic);context.Sound.PlaySound(SoundId.MusMakuTree);
                context.Rooms.SaveData.SetMakuTreeState(System.Math.Min(255,context.Rooms.SaveData.MakuTreeState+1));
                context.Transitions.BeginNativeWhiteFade(false);State=1;return;
            case 1:
                if(faded||!Decrement(0xcbb3))return;
                Write(0xcbb3,60);context.Entities.CreateEnergySwirl(new(0x50,0x50),0x68);State=2;return;
            case 2:
                if(!Decrement(0xcbb3))return;
                Write(0xcbba,0xff);State=3;return;
            case 3:
                Write(0xcbb3,Read(0xcbb3)+1);
                var flashes=_data.Table("arrival-flashes");int index=0;
                while(index<flashes.Length && flashes[index]<Read(0xcbb3))index++;
                if(index<flashes.Length && flashes[index]!=0xff)
                {Write(0xcbba,index&1);context.RoomView.SetBackgroundFade(Colors.White,(index&1)==0?1:0);return;}
                context.RoomView.SetBackgroundFade(Colors.White,0);Write(0xcbb3,60);Write(0xcfd0,1);
                RevealLink();context.Transitions.BeginNativeWhiteFade(false,3);State=4;return;
            case 4:if(!faded&&Decrement(0xcbb3))State=5;return;
            case 5:
                if(Read(0xcfd0)!=2)return;
                Write(0xcfde,5);Write(0xcfdf,0);context.Transitions.BeginNativeWhiteFade(true);State=6;return;
            case 6:
                if(faded)return;
                if(Read(0xcfde)==5)Load(2,0x0e);else Load(0,0x5a);
                context.Transitions.BeginNativeWhiteFade(false);State=7;return;
            case 7:
                if(faded||Read(0xcfdf)!=0xff)return;
                Write(0xcfdf,0);Write(0xcfde,Read(0xcfde)+1);
                State=Read(0xcfde)==7?8:6;if(State==8)Write(0xcfd0,3);
                context.Transitions.BeginNativeWhiteFade(true);return;
            case 8:
                if(faded)return;
                Load(0,0x38);RevealLink();Write(WramAddress.wDisabledObjects,1);Write(WramAddress.wMenuDisabled,1);
                if(context.Rooms.SaveData.IsLinkedGame)Actor(0x93,0,0,0);
                context.Sound.PlaySound(SoundId.MusMakuTree);context.Transitions.BeginNativeWhiteFade(false);State=9;return;
            case 9:
                if(Read(0xcfd0)!=7)return;
                int text=context.Rooms.SaveData.IsLinkedGame?0x2802:0x2800;
                context.ShowDialogue(_data.Text(text),sourceTextId:text);State=10;return;
            case 10:
                if(context.DialogueOpen)return;
                Write(0xcbb3,180);Write(0xcbb7,1);context.Player.Face(Vector2I.Right);RandomDelay();State=11;return;
            case 11:
                if(Decrement(0xcbb3))
                {
                    if(context.Rooms.SaveData.IsLinkedGame)Write(0xcfd0,8);
                    else {if(context.Entities.PartSlotAvailable)context.Entities.AddEntity(new LightningRoomEntity(context,new(0x88,0x40)));Actor(0x8d,0,0,0);}
                    State=12;return;
                }
                if(Decrement(0xcbb6)) {Write(0xcbb7,Read(0xcbb7)^2);context.Player.RequestNativeScriptFacing(this,Read(0xcbb7));RandomDelay();}
                return;
            case 12:
                if(Read(0xcfd0)!=0x63)
                {
                    if(context.Rooms.SaveData.IsLinkedGame&&Read(0xcfd0)<9&&(context.Entities.FrameCounter&7)==0)
                    {
                        int direction=((OracleObjectMovement.Shared.RelativeAngle(context.Player.Position,new(Read(0xcfd6),Read(0xcfd5)))+4)&0x18)>>3;
                        context.Player.RequestNativeScriptFacing(this,direction);
                    }
                    return;
                }
                Write(WramAddress.wDisabledObjects,0);Write(WramAddress.wMenuDisabled,0);context.Rooms.SaveData.SetGlobalFlag(0x11);
                Write(0xcc31,Read(0xcc45));
                HasState=false;_resources.ReleaseFullScreenFade();return;
        }
    }
    private void RandomDelay() => Write(0xcbb6,((context.Entities.Random.Next().Value&3)<<2)+16);
    public void UpdateFrame() { }
    public void Cancel()
    {
        if(!HasState)return;
        HasState=false;State=0;context.Player.Visible=true;context.Player.EndHarpPose();context.Player.EndCutsceneControl();
        context.Transitions.CancelNativeWhiteFade();_resources.ReleaseFullScreenFade();
        Write(WramAddress.wDisabledObjects,0);Write(WramAddress.wMenuDisabled,0);
    }
}
