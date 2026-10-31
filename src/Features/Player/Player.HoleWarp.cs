using Godot;

namespace Oracles;

public partial class Player
{
    private bool _holeWarpActive;

    internal void RequestHoleWarp()
    {
        _holeWarpActive=true;
        _inventory.RuntimeState.SetField("wLinkForceState",0x0a);
    }

    internal void CancelHoleWarp()
    {
        if(!_holeWarpActive)return;
        _holeWarpActive=false;
        if(_inventory.RuntimeState.ReadField("wLinkForceState")==0x0a)
            _inventory.RuntimeState.SetField("wLinkForceState",0);
        SetScriptedLinkAnimationMode(null);
    }

    private bool AdvanceHoleWarp()
    {
        if(!_holeWarpActive)return false;
        var memory=_inventory.RuntimeState;
        if(memory.ReadField("wLinkForceState")==0x0a)
        {
            if(_world.NativePaletteChanging || memory.ReadField("wPaletteThread_mode")!=0 ||
                (memory.ReadField("wScrollMode")&0x0e)!=0)return true;
            memory.SetField("wLinkForceState",0);
            NativeLinkSubstate=0;
        }
        // checkLinkForceState dispatches state$0a immediately on consumption.
        if(NativeLinkSubstate==0)
        {
            BeginRoomWarpTransition();NativeLinkSubstate=1;
            int y=unchecked((byte)((int)_precisePosition.Y+8));
            SetScriptedPosition(new(((int)_precisePosition.X&0xf0)+8,(y&0xf0)+8));
            ClearNativeItemParents();SetScriptedLinkAnimationMode(0x0d);
            _world.PlaySound(SoundId.SndLinkFall);
        }
        else if(memory.ReadWramByte(0xd021)==0xff)
            memory.SetField("wWarpTransition2",3);
        else
            AdvanceNativeLinkAnimation();
        return true;
    }
}
