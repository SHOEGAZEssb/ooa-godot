using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateScrollModeSuppressionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (Vector2 position in new[] { new Vector2(4.25f, 56.5f), new(158.25f, 56.5f), new(72.25f, 4.5f), new(72.25f, 126.5f) })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            FailIf(_runtimeState.ReadWramByte(WramAddress.wScrollMode) != 1, "An ordinary initialized room must publish source wScrollMode=$01.");
            _player.WarpTo(position); _player.BeginCutsceneControl(owner: this);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 1);
            // Suppress transitions separately so the restored boundary pass
            // can be compared without loading a destination room. Source
            // state2 clamps before checking wDisableScreenTransitions.
            _runtimeState.SetWramByte(WramAddress.wDisableScreenTransitions, 1);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, (int)position.X, (int)position.Y)
                { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc8a] = 1; native[WramAddress.wDisableScreenTransitions] = 1;
            native.Word(0xd00a, (int)(position.Y * 256)); native.Word(0xd00c, (int)(position.X * 256));
            native[0xcd04] = 2; native[0xcd0c] = 154; native[0xcd0d] = 121;
            _runtimeState.SetWramByte(WramAddress.wScrollMode, 0); native[0xcd00] = 0;
            double animation = _animationTicks;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, afterInteractions: native.AdvanceScreenBoundary);
                FailIf(IsTransitioning || _player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f),
                    $"wScrollMode=${native[0xcd00]:x2} must govern the shared boundary clamp under actual gameplay: runtime={_player.PrecisePosition}, native={native.Word(0xd00c)/256f},{native.Word(0xd00a)/256f}.");
            });
            Step(3);
            FailIf(_player.PrecisePosition != position || _animationTicks != animation,
                "wScrollMode=$00 must retain boundary coordinates and animation counters, independently of input/menu locks.");
            _runtimeState.SetWramByte(WramAddress.wScrollMode, 1); native[0xcd00] = 1; Step(1);
            FailIf(_player.PrecisePosition == position || _animationTicks != animation + 1,
                "Restoring wScrollMode=$01 must resume source high-byte clamping and animation on the next update.");
            _runtimeState.SetWramByte(WramAddress.wScrollMode, 0);
            _rooms.LoadCutsceneRoom(0, 0x33);
            FailIf(_runtimeState.ReadWramByte(WramAddress.wScrollMode) != 1,
                "disableLcdAndLoadRoom_body must restore wScrollMode=$01 after the cutscene background replacement.");
            _player.EndCutsceneControl(this);
        }
    }
}
