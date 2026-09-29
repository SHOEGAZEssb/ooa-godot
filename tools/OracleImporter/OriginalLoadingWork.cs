using System.Text;
using oracleofages;

namespace OracleOfAges.Importer;

/// <summary>
/// Counts original CPU work, without interrupts, by executing only the clean-US
/// blocking loaders. This is an import operation, not a runtime ROM dependency.
/// </summary>
internal sealed class OriginalLoadingWork
{
    private readonly byte[] _rom;
    private readonly byte[] _memory = new byte[0x10000];
    private readonly byte[] _wram = new byte[0x8000];
    private readonly byte[] _vram = new byte[0x4000];
    private readonly OracleCpu _cpu;
    private int _bank = 2, _wramBank = 1, _vramBank;

    private OriginalLoadingWork(byte[] rom)
    {
        _rom = rom;
        _memory[0xff97] = 2;
        _memory[0xff70] = 1;
        _cpu = new OracleCpu(Read, Write, _ => { },
            (address, detail) => new InvalidDataException(
                $"code/bank0.s:loadGfxHeader ${_bank:x2}:${address:x4}: {detail}."));
    }

    internal static string Compile(byte[] rom)
    {
        if (rom.Length != 0x100000 ||
            !rom.AsSpan(0x0626, 8).SequenceEqual(new byte[] { 0x5f, 0xf0, 0x70, 0x4f, 0xf0, 0x97, 0x47, 0xc5 }))
            throw new InvalidDataException("code/bank0.s:loadGfxHeader clean-US entry $00:$0626 changed.");
        var rows = new StringBuilder("# header\tcpu-cycles\tsource\n");
        // data/ages/gfxHeaders.s:NUM_GFX_HEADERS, checked by the stage.
        for (int header = 0; header < 0xbb; header++)
        {
            var work = new OriginalLoadingWork(rom);
            // The original thread stack is in fixed WRAM. The decoder changes
            // SVBK when decompressing into banked WRAM, so $dxxx is unsafe.
            work._cpu.BeginCall(0x0626, header, stack: 0xc1f0);
            for (int instructions = 0; work._cpu.ProgramCounter != 0; instructions++)
            {
                if (instructions == 2_000_000)
                    throw new InvalidDataException($"GFX header ${header:x2} did not terminate.");
                int pc = work._cpu.ProgramCounter;
                // The decoder and rst_addDoubleIndex are fixed-bank routines.
                // Executing graphics payload as code is never a valid import.
                if (pc is not (>= 0x18 and <= 0x20) && pc is not (>= 0x0626 and < 0x07b0))
                    throw new InvalidDataException($"GFX header ${header:x2} escaped loader code at ${pc:x4}.");
                work._cpu.Step();
            }
            if (work._cpu.StackPointer != 0xc1f2)
                throw new InvalidDataException($"GFX header ${header:x2} returned with an unbalanced source stack.");
            rows.Append($"{header:x2}\t{work._cpu.Cycles}\tcode/bank0.s:loadGfxHeader\n");
        }
        return rows.ToString();
    }

    internal static string CompileRandomBuffer(byte[] rom)
    {
        // bank0.generateRandomBuffer -> roomInitialization.functionCaller.
        // The only data-dependent work is multiplyAByC's carry branch. Import
        // its extra work for each byte, plus the complete zero-seed call.
        if (!rom.AsSpan(0x3215, 4).SequenceEqual(new byte[] { 0x26, 0x04, 0x18, 0x06 }) ||
            !rom.AsSpan(0x019d, 15).SequenceEqual(new byte[] {
                0x1e, 0x08, 0x06, 0x00, 0x68, 0x60, 0x29, 0x87, 0x30, 0x01, 0x09, 0x1d, 0x20, 0xf8, 0xc9 }))
            throw new InvalidDataException("code/bank0.s:generateRandomBuffer/multiplyAByC clean-US entries changed.");
        var deltas = new long[256];
        long multiplyBase = 0;
        for (int value = 0; value < 256; value++)
        {
            var multiply = new OriginalLoadingWork(rom);
            multiply._cpu.SetBc(0xff);
            long cycles = multiply.RunRandomRoutine(0x019d, value);
            if (value == 0) multiplyBase = cycles;
            deltas[value] = cycles - multiplyBase;
        }
        long baseline = new OriginalLoadingWork(rom).RunRandomRoutine(0x3215);
        // Exercise every low seed and differing high seeds. Verify that the
        // table accounts for the entire call, including bank dispatch/return.
        for (int seed = 0; seed < 256; seed++)
        {
            var work = new OriginalLoadingWork(rom);
            int first = seed, second = (seed * 73 + 13) & 255;
            work._memory[0xff94] = (byte)first;
            work._memory[0xff95] = (byte)second;
            long expected = baseline;
            for (int call = 0; call < 256; call++)
            {
                second = (((second << 8 | first) * 3) >> 8) & 255;
                first = (first + second) & 255;
                if (call != 0) expected += deltas[first];
            }
            if (work.RunRandomRoutine(0x3215) != expected ||
                work._memory[0xff94] != first || work._memory[0xff95] != second)
                throw new InvalidDataException($"code/roomInitialization.s:generateRandomBuffer work changed for seed ${seed:x2}.");
        }
        var rows = new StringBuilder("# component\tcpu-cycles\tsource\n");
        rows.Append($"base\t{baseline}\tcode/bank0.s:generateRandomBuffer\n");
        for (int value = 0; value < 256; value++)
            rows.Append($"{value:x2}\t{deltas[value]}\tcode/bank0.s:multiplyAByC\n");
        return rows.ToString();
    }

    private long RunRandomRoutine(int entry, int value = 0)
    {
        _cpu.BeginCall(entry, value, stack: 0xc1f0);
        for (int instructions = 0; _cpu.ProgramCounter != 0 || _cpu.StackPointer != 0xc1f2; instructions++)
        {
            if (instructions == 100_000)
                throw new InvalidDataException("code/roomInitialization.s:generateRandomBuffer did not return.");
            _cpu.Step();
        }
        if (_cpu.StackPointer != 0xc1f2)
            throw new InvalidDataException("code/roomInitialization.s:generateRandomBuffer left an unbalanced stack.");
        return _cpu.Cycles;
    }

    private int Read(int address)
    {
        if (address < 0x4000) return _rom[address];
        if (address < 0x8000) return _rom[_bank * 0x4000 + address - 0x4000];
        if (address < 0xa000) return _vram[_vramBank * 0x2000 + address - 0x8000];
        if (address is >= 0xd000 and < 0xe000) return _wram[_wramBank * 0x1000 + address - 0xd000];
        if (address is >= 0xc000 and < 0xd000 or >= 0xff80 || address is 0xff70 or 0xff4f)
            return _memory[address];
        throw new InvalidDataException($"Graphics loader read unsupported memory ${address:x4}.");
    }

    private void Write(int address, int value)
    {
        if (address is >= 0x2000 and < 0x4000) { _bank = value & 0x3f; return; }
        if (address is >= 0x8000 and < 0xa000) { _vram[_vramBank * 0x2000 + address - 0x8000] = (byte)value; return; }
        if (address is >= 0xd000 and < 0xe000) { _wram[_wramBank * 0x1000 + address - 0xd000] = (byte)value; return; }
        if (address == 0xff70) _wramBank = Math.Max(1, value & 7);
        else if (address == 0xff4f) _vramBank = value & 1;
        else if (address is not (>= 0xc000 and < 0xd000 or >= 0xff80))
            throw new InvalidDataException($"Graphics loader wrote unsupported memory ${address:x4}.");
        _memory[address] = (byte)value;
    }
}
