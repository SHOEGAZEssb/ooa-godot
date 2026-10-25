using Godot;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoomOfRitesRitualLifecycle()
    {
        var waveActive = typeof(RoomView).GetField("_waveActive", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (int elapsed in new[] { 0, 30, 258, 380, 470 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(5, 0xf1, 0x80, false);
            LoadValidationRoom(5, 0xf1);
            _player.WarpTo(new(0x78, 0x88));
            var owner = _roomEvents.Get<RoomOfRitesZeldaEvent>();
            StepGameplayUpdates(3, Vector2.Zero);
            int updates = 0;
            while (!owner.WarpActive && updates++ < 100)
                StepGameplayUpdates(1, Vector2.Up, ["move_up"], ["move_up"]);
            FailIf(!owner.WarpActive, "Ritual cancellation fixture could not approach through the room aisle.");
            StepGameplayUpdates(elapsed, Vector2.Zero, batched: true);
            LoadValidationRoom(5, 0xf0);
            owner.Cancel(); owner.Cancel();
            FailIf(owner.HasState || owner.WarpActive || owner.SkipsObjectUpdate || _transitions.NativePaletteFadeActive ||
                (bool)waveActive.GetValue(_roomView)! || _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0 ||
                _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0,
                $"Ritual cancellation after {elapsed} updates retained cutscene, wave, palette or raw controls.");
            LoadValidationRoom(5, 0xf1); StepGameplayUpdates(3, Vector2.Zero);
            FailIf(_entities.EntityAdapters<TwinrovaFlameRoomEntity>().Count() != 3 ||
                _entities.EntityAdapters<ZeldaApproachRoomEntity>().Count() != 1,
                "Cancelled ritual did not recreate its original actor stream on re-entry.");
        }
        ReinitializeGameplayForValidation();
        _saveData.SetRoomFlag(5, 0xf1, 0x80);
        LoadValidationRoom(5, 0xf1); StepGameplayUpdates(3, Vector2.Zero);
        FailIf(_entities.EntityAdapters<TwinrovaFlameRoomEntity>().Any() ||
            _entities.EntityAdapters<ZeldaApproachRoomEntity>().Any() || _roomEvents.Get<RoomOfRitesZeldaEvent>().WarpActive,
            "Cleared room $5:$f1 retained the pre-boss ritual trigger or flames.");
        GD.Print("Validated ritual cancellation before wave setup, during ramp/flicker/fade, clean re-entry and flag-$80 suppression.");
    }
}
