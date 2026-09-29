using System.Text;
using oracleofages;

namespace OracleOfAges.Importer;

/// <summary>Import-time execution of blocking frontend initializers.</summary>
internal sealed class OriginalFrontendWork
{
    private readonly byte[] _rom;
    private readonly byte[] _memory = new byte[65536];
    private readonly byte[] _wram = new byte[32768];
    private readonly byte[] _vram = new byte[16384];
    private readonly OracleCpu _cpu;
    private readonly List<(string Kind, long Value)> _steps = [];
    private int _bank = 3, _wramBank = 1, _vramBank;
    private long _work, _dma;

    private OriginalFrontendWork(byte[] rom)
    {
        _rom = rom;
        Array.Fill(_memory, (byte)0xff, 0xa000, 0x2000);
        // The supported CGB cold-start profile leaves bank 4 uninitialized.
        // File loading replaces the first 18 name bytes; the six remaining
        // characters copied by textInput_updateEntryCursor still contain $ff.
        Array.Fill(_wram, (byte)0xff, 0x4000, 0x1000);
        _memory[0xff97] = 3;
        _memory[0xff70] = 1;
        _memory[0xff4d] = 0x80;
        _cpu = new OracleCpu(Read, Write, clocks => _work += clocks,
            (pc, message) => new InvalidDataException($"Frontend loading ${_bank:x2}:${pc:x4}: {message}."));
    }

    internal static string Compile(byte[] rom)
    {
        var result = new StringBuilder("# plan\tstep\tkind\tvalue\tsource\n");
        foreach (var (name, entry) in new[] { ("capcom", 0x4d46), ("title", 0x4d03), ("files", 0x1a17) })
        {
            var work = new OriginalFrontendWork(rom);
            work.Run(entry);
            work.Emit(result, name);
        }
        // setFileSelectMode($05) changes the mode on selection. Its state-0
        // loader runs on the next file-thread dispatch, before any input.
        var options = new OriginalFrontendWork(rom);
        options._memory[0xcbb3] = 5;
        options.Run(0x1a1f);
        options.Emit(result, "new-file-options");
        var nameEntry = new OriginalFrontendWork(rom);
        nameEntry._memory[0xcbb3] = 2;
        Array.Clear(nameEntry._memory, 0xa000, 0x2000);
        Array.Clear(nameEntry._wram, 0x47a0, 18); // w4NameBuffer from the file display
        nameEntry.Run(0x1a1f);
        nameEntry.Emit(result, "name-entry");
        // getNameBufferLength's two loops depend only on zero-byte count and
        // normalized length; file initialization/checksumming is data-independent.
        for (int length = 0; length <= 5; length++)
        {
            var commit = new OriginalFrontendWork(rom);
            commit._memory[0xcbb3] = 2;
            commit._memory[0xcbb4] = 2;
            Array.Clear(commit._wram, 0x47a0, 0x20);
            Array.Fill(commit._wram, (byte)0x41, 0x47a0, length);
            commit.Run(0x1a1f);
            commit.Emit(result, $"name-commit-{length}");
        }
        var spaces = new OriginalFrontendWork(rom);
        spaces._memory[0xcbb3] = 2;
        spaces._memory[0xcbb4] = 2;
        Array.Fill(spaces._wram, (byte)0x20, 0x47a0, 5);
        spaces._wram[0x47a0] = 0x41;
        spaces.Run(0x1a1f);
        spaces.Emit(result, "name-commit-spaces");
        var returnFiles = new OriginalFrontendWork(rom);
        returnFiles._memory[0xcbb3] = 1;
        Array.Clear(returnFiles._memory, 0xa000, 0x2000);
        returnFiles.Run(0x1a1f);
        returnFiles.Emit(result, "files-return");
        var selected = new OriginalFrontendWork(rom);
        selected.SetFile(0, 12, [0x41]);
        selected.Run(0x1a17);
        selected._steps.Clear();
        selected._memory[0xc482] = 8; // wKeysJustPressed: Start
        selected.Run(0x1a1f);
        selected.Emit(result, "file-select");
        selected._steps.Clear();
        selected.Run(0x1a1f);
        selected.Emit(result, "file-start");
        foreach (var (name, health) in new[] { ("pregame-start", 12), ("pregame-start-empty", 0), ("pregame-start-negative", 0x80) })
        {
            var game = new OriginalFrontendWork(rom);
            game.SetFile(0, 12, [0x41]);
            game.Run(0x1a17);
            game._memory[0xc482] = 8;
            game.Run(0x1a1f); // load the chosen save before initialization
            game.Run(0x1a1f);
            game._steps.Clear();
            game._memory[0xff96] = 0xff; // hGameboyType: supported GBA CGB profile
            game._memory[0xc4ab] = 0; // state 3 waits for the palette thread
            game._memory[0xc6aa] = (byte)health;
            int gameEntry = FindUnique(rom, 0, [0xcd, 0xb2, 0x0c, 0xcd, 0x4d, 0x18, 0x21, 0x22, 0xc6]);
            game._cpu.BeginCall(gameEntry, stack: 0xc220);
            game.Execute(0x0900); // first resumeThreadInAFrames, including gfx yields
            game.Finish();
            game.Emit(result, name);
        }
        var cleared = new OriginalFrontendWork(rom);
        Array.Clear(cleared._memory, 0xa000, 0x2000);
        cleared.Run(0x1a17);
        cleared.Emit(result, "files-cleared");
        // File verification/copying has one data-independent path per valid
        // canonical slot. Names and the selected file's hearts are separate.
        for (int slot = 0; slot < 3; slot++)
        {
            var work = new OriginalFrontendWork(rom);
            work.SetFile(slot, 0, []);
            work.Run(0x1a17);
            work.Emit(result, $"file-{slot}");
        }
        var nameWork = new OriginalFrontendWork(rom);
        nameWork.SetFile(1, 0, [0x41]);
        nameWork.Run(0x1a17);
        nameWork.Emit(result, "file-name");

        int heartEntry = FindUnique(rom, 2, [0x3e, 0x01, 0xe0, 0x8b, 0x78, 0x18]);
        for (int health = 0; health < 256; health++)
        {
            var work = new OriginalFrontendWork(rom) { _bank = 2 };
            work._memory[0xff97] = 2;
            work._cpu.BeginCall(heartEntry, stack: 0xc220);
            work._cpu.SetBc(health * 0x101);
            work.Execute(0);
            work.Event("cpu", work._work);
            work._work = 0;
            work.Emit(result, $"hearts-{health:x2}");
        }
        return result.ToString();
    }

    private static int FindUnique(byte[] rom, int bank, byte[] signature)
    {
        int found = -1;
        for (int offset = bank * 0x4000; offset < (bank + 1) * 0x4000 - signature.Length; offset++)
        {
            if (!rom.AsSpan(offset, signature.Length).SequenceEqual(signature)) continue;
            if (found >= 0) throw new InvalidDataException($"Ambiguous frontend routine signature in bank ${bank:x2}.");
            found = bank == 0 ? offset : 0x4000 + (offset & 0x3fff);
        }
        return found >= 0 ? found : throw new InvalidDataException($"Missing frontend routine signature in bank ${bank:x2}.");
    }

    private void SetFile(int slot, int health, byte[] name)
    {
        byte[] file = new byte[0x550];
        "Z21216-0"u8.CopyTo(file.AsSpan(2));
        file[0xc6ab - 0xc5b0] = (byte)health;
        name.CopyTo(file, 0xc602 - 0xc5b0);
        int checksum = 0;
        for (int i = 2; i < file.Length; i += 2)
            checksum = (checksum + file[i] + (file[i + 1] << 8)) & 0xffff;
        file[0] = (byte)checksum;
        file[1] = (byte)(checksum >> 8);
        file.CopyTo(_memory, 0xa010 + slot * 0x550);
        file.CopyTo(_memory, 0xb000 + slot * 0x550);
    }

    private void Run(int entry)
    {
        _cpu.BeginCall(entry, stack: 0xc220);
        Execute(entry is 0x1a17 or 0x1a1f ? 0x1a29 : 0);
        Finish();
    }

    private void Finish()
    {
        Flush();
        if (_memory[0xffa5] != 0)
        {
            // runVBlankFunctions consumes the DMA commands queued while LCD
            // was enabled. The nonempty conditional CALL costs 12 extra clocks.
            int vblank = FindUnique(_rom, 0, [0x21, 0x00, 0xc4, 0x2a, 0xe5, 0x4f, 0x06, 0x00]);
            _cpu.BeginCall(vblank, stack: 0xc220);
            Execute(0);
            Event("vblank-work", _work + 12);
            _work = 0;
        }
    }

    private void Execute(int end)
    {
        for (int count = 0; count < 2_000_000; count++)
        {
            int pc = _cpu.ProgramCounter;
            if ((pc == 0 && _cpu.StackPointer == 0xc222) || (end != 0 && pc == end)) return;
            if (pc == 0x02c1)
            {
                Flush(); Event("lcd-off", 0); _memory[0xff40] = 0; _cpu.CompleteCall(); continue;
            }
            if (_bank == 0x39 && pc == 0x4009)
            {
                Flush(); Event("sound-stop", 0); _cpu.CompleteCall(); continue;
            }
            if (pc == 0x0c98) { Flush(); Event("sound", _cpu.Accumulator); }
            if (pc == 0x0626) { Flush(); Event("gfx", _cpu.Accumulator); }
            if (pc == 0x05da) { Flush(); Event("dma-gfx", _cpu.Accumulator); }
            _cpu.Step();
            _work += _dma;
            _dma = 0;
        }
        throw new InvalidDataException("Frontend initializer did not terminate.");
    }

    private void Event(string kind, long value) => _steps.Add((kind, value));
    private void Flush() { if (_work != 0) Event("cpu", _work); _work = 0; }
    private void Emit(StringBuilder result, string name)
    {
        string source = name.StartsWith("pregame-start", StringComparison.Ordinal)
            ? "code/bank0.s:mainThreadStart,bank1.s:initializeGame,ages/cutscenes/miscCutscenes.s:pregameIntroCutsceneHandler"
            : "code/bank0.s,bank2.s,bank3Cutscenes.s";
        for (int i = 0; i < _steps.Count; i++)
            result.Append($"{name}\t{i}\t{_steps[i].Kind}\t{_steps[i].Value}\t{source}\n");
    }

    private int Read(int address)
    {
        if (address < 0x4000) return _rom[address];
        if (address < 0x8000) return _rom[_bank * 0x4000 + address - 0x4000];
        if (address < 0xa000) return _vram[_vramBank * 0x2000 + address - 0x8000];
        if (address is >= 0xd000 and < 0xe000) return _wram[_wramBank * 0x1000 + address - 0xd000];
        if (address == 0xff44) return 145;
        return _memory[address];
    }

    private void Write(int address, int value)
    {
        if (address < 0x2000) return; // cartridge SRAM gate
        if (address < 0x4000) { _bank = value & 0x3f; return; }
        if (address is >= 0x8000 and < 0xa000) { _vram[_vramBank * 0x2000 + address - 0x8000] = (byte)value; return; }
        if (address is >= 0xd000 and < 0xe000) { _wram[_wramBank * 0x1000 + address - 0xd000] = (byte)value; return; }
        if (address == 0xff70) _wramBank = Math.Max(1, value & 7);
        if (address == 0xff4f) _vramBank = value & 1;
        if (address == 0xff55) _dma += ((value & 0x7f) + 1) * 64 + 4;
        if (address == 0xff07) { Flush(); Event("timer", value); }
        if (address == 0xff40) { Flush(); Event("lcd", value); }
        _memory[address] = (byte)value;
    }
}
