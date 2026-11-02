
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void ResetMinecartsForValidation(OracleRuntimeState memory, IReadOnlyList<MinecartStaticRecord> records)
    {
        StaticObjectState.Clear(memory);
        MinecartRuntimeState.ClearActive(memory);
        foreach (var record in records)
            StaticObjectState.Write(memory, new(record.Slot, 3, record.Room, 0x16, 0, record.Y, record.X, record.Source));
    }
}
