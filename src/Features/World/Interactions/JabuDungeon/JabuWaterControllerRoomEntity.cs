using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// miscPuzzles_subid0a. The high nibble remembers the fill buttons; bit 7
// drains on either edge. Room tiles change on subsequent native room loads.
internal sealed partial class JabuWaterControllerRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly OracleSaveData _save;
    private readonly OracleRuntimeState _runtime;
    private readonly Func<byte> _triggers;
    private readonly Action<int> _sound;
    private readonly Action<int> _shake;
    private readonly Action<int> _text;
    private readonly Func<int> _music;
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal byte LastTriggers { get; private set; }
    internal int TextId { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;

    internal JabuWaterControllerRoomEntity(OracleSaveData save,OracleRuntimeState runtime,
        Func<byte> triggers,Action<int> sound,Action<int> shake,Action<int> text,Func<int> music)
    {
        _save=save; _runtime=runtime; _triggers=triggers; _sound=sound;
        _shake=shake; _text=text; _music=music; Visible=false; Name="JabuWaterController";
    }
    private void Initialize()
    {
        LastTriggers=_triggers();
        _runtime.SetWramByte(OracleRuntimeState.SwitchStateAddress,(byte)(_save.ReadWramByte(WramAddress.wJabuWaterLevel)&0xf0));
        State=1;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        switch (State)
        {
            case 0: Initialize(); return;
            case 1:
                byte triggers=_triggers(), changed=(byte)(triggers ^ LastTriggers);
                LastTriggers=triggers;
                int water=_save.ReadWramByte(WramAddress.wJabuWaterLevel);
                byte switches=_runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress);
                if ((changed&0x80) != 0)
                {
                    if ((water&7) == 0) return;
                    _save.WriteWramByte(WramAddress.wJabuWaterLevel,0);
                    _runtime.SetWramByte(OracleRuntimeState.SwitchStateAddress,0);
                    TextId=0x1208;
                }
                else
                {
                    if ((triggers&changed) == 0 || (switches&changed) != 0) return;
                    switches |= changed;
                    _runtime.SetWramByte(OracleRuntimeState.SwitchStateAddress,switches);
                    _save.WriteWramByte(WramAddress.wJabuWaterLevel,(byte)(((water&3)+1)|(switches&0xf0)));
                    TextId=0x1209;
                }
                _runtime.SetWramByte(WramAddress.wDisabledObjects,0x81);
                _runtime.SetWramByte(WramAddress.wMenuDisabled,0x81);
                Counter=60; _sound(SoundId.SndCtrlStopMusic); State=2; return;
            case 2:
                if (--Counter != 0) return;
                Counter=0xf0; _shake(Counter); _sound(SoundId.SndFloodgates); State=3; return;
            case 3:
                if (--Counter != 0) return;
                State=1;
                _runtime.SetWramByte(WramAddress.wDisabledObjects,0);
                _runtime.SetWramByte(WramAddress.wMenuDisabled,0);
                _text(TextId); _sound(SoundId.SndCtrlStopSfx); _sound(_music()); return;
            default: throw new InvalidOperationException($"INTERAC$90:$0a: invalid state${State:x2}.");
        }
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { if (State == 0) Initialize(); return ScreenTransitionPresentation.Hidden; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
