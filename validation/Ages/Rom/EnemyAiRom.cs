using System.IO;

namespace Oracles;

// Executes bank0.updateEnemies, including its gates, slot walk, common
// initialization, native handlers and animation streams. Graphics are resident:
// this fixture does not emulate VRAM uploads, interrupts or hardware timing.
internal sealed class EnemyAiRom : RomFixture
{
    internal int RandomCalls { get; private set; }

    internal EnemyAiRom() : base("Enemy AI", 0x0d)
    {
        _memory[0xff97] = 0x0d;
        // bank3f.enemyData, loaded via enemyGetObjectGfxIndex $3f:$4337.
        _memory[0xcc08] = _rom.Span[0xfdd4b + 0x08 * 4];
        _memory[0xcc0a] = _rom.Span[0xfdd4b + 0x18 * 4];
        // Small-room loadRoomCollisions.@blankDataAroundCollisions: bottom
        // and wrapped top rows, right and wrapped left columns are $ff.
        for (int x = 0; x < 16; x++)
            _memory[0xce80 + x] = _memory[0xcef0 + x] = 0xff;
        for (int y = 0; y < 11; y++)
            _memory[0xce0a + y * 16] = _memory[0xce0f + y * 16] = 0xff;
    }

    internal byte this[int address] { get => _memory[address]; set => _memory[address] = value; }
    internal int Word(int address) => _memory[address] | (_memory[address + 1] << 8);
    internal void Word(int address, int value)
    {
        _memory[address] = unchecked((byte)value);
        _memory[address + 1] = unchecked((byte)(value >> 8));
    }
    internal void Update() => _cpu.RunCall(0x2ea5);


    protected override void Write(int address, int value)
    {
        if (address == 0xff94) RandomCalls++;
        base.Write(address, value);
    }

    protected override bool RomBankAllowed(int bank) => bank is 3 or 0x0d or 0x11 or 0x3f;

    protected override bool Allowed(int address) =>
        address is 0xc4ab or 0xcba0 or >= 0xcc00 and < 0xe000 or >= 0xff80 and < 0xffc0;
}
