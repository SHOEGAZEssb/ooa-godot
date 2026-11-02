
namespace Oracles;

// parentItemCheckButtonPressed and updateParentItemButtonAssignment_body.
internal static class ParentItemButtons
{
    internal static int Read(OracleRuntimeState memory, int slot) => memory.ReadWramByte(Address(slot));
    internal static void Set(OracleRuntimeState memory, int slot, int buttons) => memory.SetWramByte(Address(slot), checked((byte)buttons));
    internal static void SetAction(OracleRuntimeState memory, int slot, string? action) => Set(memory, slot, action switch
    {
        null => 0, "attack" => 1, "item" => 2,
        _ => throw new NotSupportedException($"Parent${slot:x2}: unsupported item button '{action}'.")
    });
    internal static bool IsHeld(OracleRuntimeState memory, int slot, bool primary, bool secondary) =>
        (Read(memory, slot) & ((primary ? 1 : 0) | (secondary ? 2 : 0))) != 0;
    internal static void Reassign(OracleRuntimeState memory, ReadOnlySpan<ParentItemSlotState> parents, int primary, int secondary)
    {
        if (parents.Length != 4) throw new ArgumentException("Supply native ParentItem2 through ParentItem5.", nameof(parents));
        for (int index = 0; index < parents.Length; index++)
        {
            var parent = parents[index];
            Set(memory, index + 2, parent.Enabled == 0 ? 0 : parent.Id == secondary ? 2 : parent.Id == primary ? 1 : 0);
        }
    }
    private static int Address(int slot) => slot is >= 2 and <= 5 ? ((0xd0 + slot) << 8) | 3 :
        throw new ArgumentOutOfRangeException(nameof(slot), "Native parent slot must be $02-$05.");
}
