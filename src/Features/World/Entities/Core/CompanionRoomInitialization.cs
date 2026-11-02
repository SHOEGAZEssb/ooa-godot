
namespace Oracles;

// companionCheckCanSpawn: only Ages yields for solid-object reservations.
// Both games copy fallback high bytes without changing coordinate fractions.
internal sealed class CompanionRoomInitialization(Func<OracleRoomData> currentRoom, OracleSaveData saveData, OracleRuntimeState memory,
    Func<Vector2, bool> occupied)
{
    private bool _completed;
    internal bool Pending => !_completed;
    internal bool Rejected { get; private set; }
    internal bool Advance(ref Vector2 position)
    {
        if (memory.ReadWramByte(0xd104) == 0)
        {
            if (saveData.Game == OracleGame.Ages)
            {
                if (memory.ReadWramByte(0xd105) == 0)
                { memory.SetWramByte(0xd105, 1); return false; }
                memory.SetWramByte(0xd105, 0);
                if (occupied(position)) return Reject();
            }
            OracleRoomData room = currentRoom();
            Vector2 feet = new(OracleObjectPosition.HighByte(position.X),
                unchecked((byte)(OracleObjectPosition.HighByte(position.Y) + 5)));
            int collision = room.GetTerrainInfo(feet).Collision;
            if (collision is 0x0f or 0x10)
            {
                position = CompanionRuntimeState.ReadLastAnimalMountPosition(saveData) +
                    position - OracleObjectMath.ToPixelPosition(position);
                memory.SetWramByte(0xd10b, OracleObjectPosition.HighByte(position.Y));
                memory.SetWramByte(0xd10d, OracleObjectPosition.HighByte(position.X));
                if (room.GetTerrainInfo(position).Collision != 0) return Reject();
            }
        }
        SpecialObjectOamDatabase.For(currentRoom().AssetRoot).InitializeCompanion(memory);
        memory.SetWramByte(0xd104, unchecked((byte)(memory.ReadWramByte(0xd103) + 1)));
        memory.SetWramByte(0xd124, 0x80);
        _completed = true;
        return true;
    }
    private bool Reject()
    {
        Rejected = _completed = true;
        CompanionRuntimeState.Clear(memory, memory.ReadWramByte(0xd101));
        return true;
    }
}
