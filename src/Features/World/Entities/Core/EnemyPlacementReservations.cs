using System;

namespace oracleofages;

internal sealed class EnemyPlacementReservations
{
    private const int CountAddress = 0xcec1;
    private const int PositionsAddress = 0xced0;
    private readonly OracleRuntimeState _runtime;

    internal EnemyPlacementReservations(OracleRuntimeState? runtime = null) =>
        _runtime = runtime ?? new OracleRuntimeState();

    internal int Count => _runtime.ReadWramByte(CountAddress);

    internal static void BeginRoomParse(OracleRuntimeState runtime)
    {
        // parseObjectData clears this block; parseGivenObjectData does not.
        for (int address = 0xcec0; address < 0xcee0; address++)
            runtime.SetWramByte(address, 0);
        runtime.SetWramByte(0xcfc0, 0);
    }

    internal bool Contains(int packedPosition)
    {
        for (int index = 0; index < Count; index++)
        {
            if (_runtime.ReadWramByte(PositionsAddress + index) == packedPosition)
                return true;
        }
        return false;
    }

    internal void Add(int packedPosition)
    {
        if (packedPosition is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(packedPosition));
        int count = Count;
        // addPositionToPlacedEnemyPositions adds the full old count to HL
        // before masking its increment. A stale count can therefore write
        // past the ordinary sixteen-byte reservation area.
        _runtime.SetWramByte(PositionsAddress + count, (byte)packedPosition);
        _runtime.SetWramByte(CountAddress, (byte)((count + 1) & 0x0f));
    }
}
