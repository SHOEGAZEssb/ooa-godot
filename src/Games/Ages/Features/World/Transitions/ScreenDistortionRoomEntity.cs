using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// Ages INTERAC_SCREEN_DISTORTION $7c. The LCD wave is presented by RoomView;
// bank2's immutable $ff-amplitude sine table is projected from generated data.
// It must not be written into the bank1 object pages at the same $d800 address.
internal sealed class ScreenDistortionRoomEntity(RoomEntityManager owner) : INativeInteractionRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private int _address;
    private bool _retired;
    public Node2D? PresentationNode => null;
    public bool Finished => _retired || _address != 0 && Read(0) == 0;
    public bool NativeInitializationPending => !Finished && (_address == 0 || Read(4) == 0);
    public bool UpdatesDuringDialogue => _address != 0 && (Read(0) & 0x80) != 0;
    public bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue;

    public void BindNativeInteractionSlot(int slot)
    {
        if (_address != 0) throw new InvalidOperationException("INTERAC $7c already owns its native slot.");
        _address = 0xd040 + slot * 0x100;
        Write(0, 1); Write(1, 0x7c); Write(2, 0);
    }

    public void ReleaseNativeInteractionSlot()
    {
        if (_address != 0 && !_retired && Read(1) == 0x7c)
            for (int offset = 0; offset < 0x40; offset++) Write(offset, 0);
        _retired = true;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (Read(1) != 0x7c)
            throw new NotSupportedException($"screenDistortion.s: slot${_address:x4} replaced by unsupported ID${Read(1):x2}.");
        // checkInteractionState only distinguishes zero from nonzero.
        if (Read(4) == 0)
        {
            Write(0, Read(0) | 0x80); Write(4, 1);
            owner.RuntimeState.SetField("wGfxRegs2.LYC", 0x10);
            owner.RequestSound(SoundId.SndWarpStart);
            return;
        }
        int phase = owner.RuntimeState.FrameCounter & 0x7f;
        int buffer = owner.RuntimeState.FieldAddress("wBigBuffer");
        for (int line = 0; line < 0x100; line++)
            owner.RuntimeState.SetWramByte(buffer + line,
                unchecked((byte)ScreenWaveData.Shared.Offset(0xff, phase + line)));
        owner.RequestScreenWave(0xff, phase);
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => UpdateFrame(frame, Array.Empty<RoomEntitySpawn>());
    private int Read(int offset) => owner.RuntimeState.ReadWramByte(_address + offset);
    private void Write(int offset, int value) => owner.RuntimeState.SetWramByte(_address + offset, unchecked((byte)value));
}
