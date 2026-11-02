
namespace Oracles;

// include/structs.s: one live INTERACTION allocation, including script/native aliases.
internal sealed class NativeInteractionState(OracleRuntimeState memory, int slot)
{
    private readonly int _page = slot is >= 0 and < 16 ? (0xd0 + slot) << 8 :
        throw new ArgumentOutOfRangeException(nameof(slot));
    internal int this[int offset]
    {
        get => memory.ReadWramByte(Address(offset));
        set => memory.SetWramByte(Address(offset), unchecked((byte)value));
    }
    private int Address(int offset) => offset is >= 0x40 and < 0x80 ? _page | offset :
        throw new ArgumentOutOfRangeException(nameof(offset), $"INTERACTION slot ${slot:x2}: invalid byte ${offset:x2}.");
    internal int Word(int offset) => this[offset] | this[offset + 1] << 8;
    internal void SetWord(int offset, int value)
    { _ = Address(offset + 1); this[offset] = value; this[offset + 1] = value >> 8; }
    internal Vector2 Position
    {
        get => new(Word(0x4c) / 256f, Word(0x4a) / 256f);
        set { var position = OracleObjectPosition.FromPixels(value); SetWord(0x4c, position.XFixed); SetWord(0x4a, position.YFixed); }
    }
    internal void Clear()
    { for (int offset = 0x40; offset < 0x80; offset++) this[offset] = 0; }
}
