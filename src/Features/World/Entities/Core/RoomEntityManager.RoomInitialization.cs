namespace Oracles;

public sealed partial class RoomEntityManager
{
    private void InitializeRememberedCompanion(int group, OracleRoomData room)
    {
        if (ReconstructingDebugObjects) return;
        if (_saveData is null || !NativeObjectStreamDatabase.For(AssetRoot).RememberedCompanionAllowed(_runtimeState)) return;
        if (!RememberedCompanionInitializationData.For(AssetRoot).TryInitialize(group, room.Id, room.TilesetFlags, _saveData, _runtimeState, out var admission)) return;
        if (admission.IsInteraction && !InteractionSlotAvailable) return;
        AddEntity(_factory.CreateRememberedCompanion(admission, room));
    }

    private void InitializeMaple(int group, OracleRoomData room)
    {
        if (ReconstructingDebugObjects) return;
        if (!NativeObjectStreamDatabase.For(AssetRoot).MapleAllowed(_runtimeState)) return;
        if (_saveData is null)
        {
            _runtimeState.SetField("wIsMaplePresent", 0);
            return;
        }
        if (!MapleAdmissionData.For(AssetRoot).TryAdmit(group, room.Id, _saveData, _runtimeState)) return;
        // Native past-world birth can overwrite another ID without clearing its bytes.
        ReconcileSpecialObjectOwner();
        if (_specialObjectOwner is MapleEncounterRoomEntity existing)
        {
            if (_outgoingEntities.Remove(existing)) _activeEntities.Add(existing);
            existing.Maple.RebindRoom(room);
            ((INativeSpecialObjectRoomEntity)existing).NativeSpecialObjectPosition = CompanionRuntimeState.Read(_runtimeState).Position;
            return;
        }
        AddEntity(_factory.CreateMaple(room));
    }
}
