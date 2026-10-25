using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTwinrovaFlameCutsceneLifecycle()
    {
        foreach (int elapsed in new[] { 0, 64, 150, 280, 470, 570 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(5,0xf1,0x80,false);
            LoadValidationRoom(5,0xf5);
            foreach(var twin in _entities.EntityAdapters<TwinrovaRoomEntity>().ToArray()) _entities.RetireEntity(twin);
            _player.WarpTo(new(0x78,0x78));
            var owner = _roomEvents.Get<RoomOfRitesBattleEvent>();
            owner.Begin(0x18); StepGameplayUpdates(elapsed,Vector2.Zero,batched:true);
            // Direct cancellation must clean its own actors and effects before
            // the ordinary room loader clears the rest of the live room.
            owner.Cancel(); owner.Cancel();
            FailIf(owner.HasState || _transitions.NativePaletteFadeActive || !_player.Visible ||
                _entities.EntityAdapters<TwinrovaFlameRoomEntity>().Any() ||
                _entities.EntityAdapters<RoomOfRitesZeldaActor>().Any() ||
                _entities.EntityAdapters<GrassDebrisRoomEntity>().Any() || _roomEvents.DisablesLink,
                $"CUTSCENE $18 cancellation after {elapsed} updates retained actors, palette or Link control.");
            LoadValidationRoom(5,0xf0);
            LoadValidationRoom(5,0xf5);
            FailIf(_entities.EntityAdapters<TwinrovaRoomEntity>().Count()!=1,
                "Cancelled CUTSCENE $18 did not permit a fresh ordered ENEMY $03 on re-entry.");
        }
        GD.Print("Validated Twinrova flame cutscene cancellation during every fade/actor phase and fresh room re-entry.");
    }
}
