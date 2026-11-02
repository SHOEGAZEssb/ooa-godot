using System.IO;

namespace Oracles;

// Executes clean-US checkUseItems, updateItems and updateItemsPost in that
// order. Weapon graphics are already resident; no instruction is replaced.
// Link movement and target updates belong to the caller, collision to Scan.
internal sealed class SwordRom : RomFixture
{
    internal int RandomCalls { get; private set; }
    internal List<int> Sounds { get; } = new();
    internal int MovementKeys { get; set; }

    internal SwordRom(int direction, int level, int ring = 0xff, int seed = 0x1234) : base("Sword", 6)
    {
        this[0xcc2c] = 0xd0;
        this[0xcc2b] = 0xff; // Neutral input retains the supplied facing.
        this[0xc6cb] = (byte)ring;
        this[0xc6b2] = (byte)level;
        this[0xc6aa] = this[0xc6ab] = 12;
        this[0xcc1b] = 0x1a; // bank3f.loadWeaponGfx: resident sword graphics.
        this[0xffb5] = 0xa0;
        this[0xff94] = (byte)seed;
        this[0xff95] = (byte)(seed >> 8);
        this[0xd008] = (byte)direction;
        this[0xd009] = (byte)(direction * 8);
        this[0xd00b] = 64;
        this[0xd00d] = 80;
        // Floor beneath all weapon probes; no breakable tile or clink.
        Array.Fill(_memory, (byte)0xa0, 0xcf00, 0x100);
    }

    internal byte this[int address] { get => _memory[address]; set => _memory[address] = value; }
    internal void Update(bool held, bool pressed = false, bool primary = true)
    {
        this[primary ? 0xc689 : 0xc688] = 5;
        this[primary ? 0xc688 : 0xc689] = 0;
        this[0xcc29] = (byte)(MovementKeys | (held ? primary ? 1 : 2 : 0));
        this[0xcc2a] = pressed ? (byte)(primary ? 1 : 2) : (byte)0;
        // The Link caller does not dispatch parent items while text is active.
        if (this[0xcba0] == 0) Call(6, 0x48b3);
        Call(7, 0x4872);
        Call(7, 0x491a);
    }

    internal void Call(int bank, int address)
    {
        _bank = bank;
        this[0xff97] = (byte)bank;
        _cpu.RunCall(address);
    }


    protected override void Write(int address, int value)
    {
        if (address == 0xff94) RandomCalls++;
        if (address is >= 0xc0a0 and <= 0xc0af) Sounds.Add(value);
        base.Write(address, value);
    }

    protected override bool Allowed(int address) => address is
        >= 0xc000 and < 0xe000 or >= 0xff80 and < 0xffc0;
}
