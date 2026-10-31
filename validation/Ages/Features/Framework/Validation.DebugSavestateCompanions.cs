using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateDebugSavestateCompanions()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool riding in new[] { false, true })
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x2a);
            _saveData.WriteWramByte(0xc646 + id - 0x0b, 0x80);
            Vector2 position = new(72, 64);
            FailIf(_currentRoom.GetTerrainInfo(position).Collision != 0 ||
                _currentRoom.GetTerrainInfo(position + new Vector2(0, 5)).Collision != 0,
                "Companion debug-state fixture must use original room $0:$2a floor.");
            if (riding)
            {
                CompanionRuntimeState.BeginAnimalRide(_runtimeState, _saveData, id, position, 2);
                // These retained fixtures have already passed native graphics/state-zero initialization.
                SpecialObjectOamDatabase.For(AgesGameContent.AssetRoot).InitializeCompanion(_runtimeState);
                _runtimeState.SetWramByte(0xd11a,0x81);
                if(id==0x0b)_runtimeState.SetWramByte(0xd139,0x10);
            }
            else CompanionRuntimeState.Remember(_runtimeState, id, 0, 0x2a, position);
            LoadValidationRoom(0, 0x2a);
            _player.WarpTo(new(8, 8));
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!_entities.CanCaptureDebugState, $"Companion ${id:x2} stable riding={riding} must admit capture.");
            var state = CaptureDebugSavestate();
            Vector2 expectedPosition = CompanionRuntimeState.Read(_runtimeState).Position;
            RestoreDebugSavestate(state);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            var bodies = _entities.EntityAdapters<IRoomEntity>().OfType<INativeSpecialObjectRoomEntity>().ToArray();
            FailIf(bodies.Length != 1 || bodies[0].NativeSpecialObjectId != id ||
                CompanionRuntimeState.Read(_runtimeState).Position != expectedPosition ||
                _player.CompanionRideActive != riding || !_entities.CanCaptureDebugState,
                $"Companion ${id:x2} debug restore lost its native body, motion or riding={riding} ownership.");
            RestoreDebugSavestate(state);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(_entities.EntityAdapters<IRoomEntity>().OfType<INativeSpecialObjectRoomEntity>().Count() != 1,
                $"Repeated companion ${id:x2} debug restore must retain exactly one physical owner.");
        }
    }
}
