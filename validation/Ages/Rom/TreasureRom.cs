using System.IO;

namespace Oracles;

// Original clean-US giveTreasure/removeRupeeValue, including bank-$3f
// collection modes, extra grants, inventory placement and sound requests.
internal sealed class TreasureRom : RomFixture
{
    internal TreasureRom() : base("Treasure")
    {
    }
    internal byte this[int address] => _memory[address];
    internal void Seed(OracleSaveData save, int dungeon = 0, int upgrades = 0)
    {
        Array.Clear(_memory);
        save.ReadWramBytes(0xc5b0, _memory.AsSpan(0xc5b0, OracleSaveData.FileSize));
        _memory[0xcc39] = (byte)dungeon; // wDungeonIndex
        _memory[0xcca8] = (byte)upgrades;
        _memory[0xffb5] = 0xa0; // sound request queue write pointer
    }
    internal void Give(int treasure, int parameter) => Call(0x171c, treasure, parameter);
    internal void RemoveRupees(int value) => Call(0x1778, value, 0);
    internal void RoomLoadMaturity() => Call(0x1821, 5, 0);
    private void Call(int entry, int argument, int parameter)
    {
        _bank = 1;
        _memory[0xff97] = 1;
        _caller = [0x3e, (byte)argument, 0x0e, (byte)parameter,
            0xcd, (byte)entry, (byte)(entry >> 8), 0xc9];
        _cpu.RunCall(0xc100);
        if (_bank != 1 || _memory[0xff97] != 1)
            throw new InvalidDataException("Treasure ROM lost bank restoration.");
    }
    // Ring overflow counts duplicates in bank-4 w4TmpRingBuffer $d3a0;
    // this bounded helper uses no other bank's storage at that address.
    protected override bool RomBankAllowed(int bank) => bank == 0x3f;

    protected override bool Allowed(int address) => address == 0xff70 ||
        address is >= 0xc000 and < 0xe000 or >= 0xff80 and < 0xffc0;
}
