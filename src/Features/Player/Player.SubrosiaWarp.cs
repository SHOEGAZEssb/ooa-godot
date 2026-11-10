namespace Oracles;

public partial class Player
{
    private bool _subrosiaWarpActive;
    private NativeScriptInputControl? _subrosiaWarpControl;
    internal bool SubrosiaWarpActive => _subrosiaWarpActive;
    internal bool SubrosiaWarpPending => _subrosiaWarpActive && _inventory.RuntimeState.ReadField("wLinkForceState") == 0x0a;
    internal void RequestSubrosiaWarp()
    {
        _subrosiaWarpActive = true;
        _subrosiaWarpControl = new(_inventory.RuntimeState);
        _inventory.RuntimeState.SetField("wLinkForceState", 0x0a);
    }
    internal void CancelSubrosiaWarp()
    {
        if (!_subrosiaWarpActive) return;
        _subrosiaWarpActive = false;
        _subrosiaWarpControl?.Cancel(); _subrosiaWarpControl = null;
        if (_inventory.RuntimeState.ReadField("wLinkForceState") == 0x0a) _inventory.RuntimeState.SetField("wLinkForceState", 0);
        SetCutsceneDrawZFixed(0);
        SetScriptedLinkAnimationMode(null);
    }
    internal void SetSubrosiaWarpObjectMask(byte value) => _subrosiaWarpControl!.SetDisabledObjects(value);
    internal void ReleaseSubrosiaWarpInput()
    { _subrosiaWarpControl!.SetDisabledObjects(0); _subrosiaWarpControl.SetMenuDisabled(0); }
    // link.s:warpTransition8 retains Link's object through CUTSCENE_15's room load.
    private bool AdvanceSubrosiaWarp()
    {
        if (!_subrosiaWarpActive) return false;
        var memory = _inventory.RuntimeState;
        if (memory.ReadField("wLinkForceState") == 0x0a)
        {
            if (IsDying) return false;
            if (_world.NativePaletteChanging || (memory.ReadField("wScrollMode") & 0x0e) != 0) return true;
            memory.SetField("wLinkForceState", 0);
            memory.SetWramByte(0xd004, 0x0a); NativeLinkSubstate = 0;
        }
        void Spin() { byte direction = (byte)((memory.ReadWramByte(0xd008) + 1) & 3); memory.SetWramByte(0xd008, direction); Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8)); }
        void OddSpin() { if ((memory.FrameCounter & 1) != 0) Spin(); }
        void UpdateZ()
        {
            int z = unchecked((short)(memory.ReadWramByte(0xd00e) | memory.ReadWramByte(0xd00f) << 8));
            int speed = unchecked((short)(memory.ReadWramByte(0xd014) | memory.ReadWramByte(0xd015) << 8));
            OracleObjectMath.UpdateSpeedZ(ref z, ref speed, 2);
            memory.SetWramByte(0xd00e, (byte)z); memory.SetWramByte(0xd00f, (byte)(z >> 8));
            memory.SetWramByte(0xd014, (byte)speed); memory.SetWramByte(0xd015, (byte)(speed >> 8));
            SetCutsceneDrawZFixed(z);
        }
        switch (NativeLinkSubstate)
        {
            case 0:
                BeginRoomWarpTransition(); NativeLinkSubstate = 1;
                _subrosiaWarpControl!.SetDisabledObjects(0xff); _subrosiaWarpControl.SetMenuDisabled(0x80);
                memory.SetField("wCutsceneTrigger", 0x15);
                memory.SetWramByte(0xd014, 0x60); memory.SetWramByte(0xd015, 0xff); NativeLinkCounter = 0x30;
                DropHeldItemsForScript(); ClearNativeItemParents();
                _world.RestartNativeSound(); _world.PlaySound(SoundId.SndFadeOut);
                SetScriptedPosition(new(((int)_precisePosition.X & 0xf0) + 8, ((int)_precisePosition.Y & 0xf0) + 8));
                break;
            case 1:
                UpdateZ();
                if ((memory.FrameCounter & 3) == 0)
                { memory.SetField("wGenericCutscene.cbbc", (byte)(memory.ReadField("wGenericCutscene.cbbc") + 1)); Spin(); }
                if (--NativeLinkCounter == 0) NativeLinkSubstate++;
                break;
            case 2:
                UpdateZ(); OddSpin();
                if ((memory.ReadWramByte(0xd015) & 0x80) == 0)
                { NativeLinkCounter = 0x28; _world.BeginNativeWhiteFade(true, 2); NativeLinkSubstate++; }
                break;
            case 3:
                OddSpin();
                if (--NativeLinkCounter == 0)
                { memory.SetField("wGenericCutscene.cbb3", (byte)(memory.ReadField("wGenericCutscene.cbb3") + 1)); NativeLinkSubstate++; }
                break;
            case 4:
                OddSpin();
                if (memory.ReadField("wCutsceneState") == 2) { NativeLinkSubstate++; NativeLinkCounter = 0x28; }
                break;
            case 5:
                UpdateZ(); OddSpin();
                if (--NativeLinkCounter == 0) NativeLinkSubstate++;
                break;
            case 6:
                UpdateZ();
                if ((memory.FrameCounter & 3) != 0) break;
                Spin();
                byte amplitude = (byte)(memory.ReadField("wGenericCutscene.cbbc") - 1);
                memory.SetField("wGenericCutscene.cbbc", amplitude);
                if (amplitude == 0) { memory.SetField("wGenericCutscene.cbb3", (byte)(memory.ReadField("wGenericCutscene.cbb3") + 1)); NativeLinkSubstate++; }
                break;
            case 7:
                if ((memory.ReadField("wDisabledObjects") & 0x81) != 0)
                { if ((memory.FrameCounter & 7) == 0) Spin(); break; }
                if (memory.ReadWramByte(0xd008) != 2) { Spin(); break; }
                _subrosiaWarpActive = false;
                _subrosiaWarpControl!.Relinquish(); _subrosiaWarpControl = null;
                ResetLinkInvincibility(); memory.SetWramByte(0xd004, 1); NativeLinkSubstate = 0; memory.SetWramByte(0xd035, 0);
                SetScriptedLinkAnimationMode(null); ResetLinkWalkAnimation();
                break;
            default: throw new NotSupportedException($"link.s:warpTransition8 substate ${NativeLinkSubstate:x2} is unsupported.");
        }
        QueueRedraw();
        return true;
    }
}
