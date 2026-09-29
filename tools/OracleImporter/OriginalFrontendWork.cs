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
        _cpu = CreateCpu();
    }

    private OracleCpu CreateCpu() => new(Read, Write, clocks => _work += clocks,
        (pc, message) => new InvalidDataException($"Frontend loading ${_bank:x2}:${pc:x4}: {message}."));

    internal static string CompileTextboxes(byte[] rom, IReadOnlyList<int> ids)
    {
        var result = new StringBuilder("# text-id\tside\tcpu\tvblank\tstatus\tsource\n");
        int showText = FindUnique(rom, 0, [0x2e, 0x00, 0x1e, 0x00, 0xfa, 0xae, 0xcb, 0xb5]);
        foreach (int id in ids)
        foreach (int side in new[] { 0, 2 })
        {
            if (id is < 0 or > 0xffff) throw new InvalidDataException($"Invalid text index {id}.");
            var work = new OriginalFrontendWork(rom);
            work._memory[0xff40] = 0xef; // gameplay LCD/window, HUD at top
            work._memory[0xff96] = 0xff;
            work._memory[0xcd00] = 1; // ordinary scroll mode
            work._memory[0xc629] = 3;
            work.Write(0xd00b, side == 0 ? 0x48 : 0x38);
            work._cpu.BeginCall(showText, stack: 0xc220);
            work._cpu.SetBc(id);
            work.Execute(0);
            int thread = work.Read(0xc2f4) | work.Read(0xc2f5) << 8;
            if (thread == 0) throw new InvalidDataException($"TX_{id:x4} did not start THREAD_2.");
            work._cpu.BeginCall(thread, stack: 0xc270);
            work.Execute(0x0900);
            work.Finish();
            string status = work.Read(0xcba2) != (id & 0xff) || work._wram[0x70c0] != 1
                ? "dynamic-extra-text" : "standard";
            if (work._steps.Any(step => step.Kind is not ("cpu" or "vblank-work")))
                throw new InvalidDataException($"Unexpected loading side effect in TX_{id:x4}.");
            result.Append($"{id:x4}\t{side}\t{work._steps.Where(step => step.Kind == "cpu").Sum(step => step.Value)}\t" +
                $"{work._steps.Where(step => step.Kind == "vblank-work").Sum(step => step.Value)}\t{status}\t" +
                "code/bank0.s:showText,textThreadStart;code/textbox.s:initTextbox,standardTextState0\n");
        }
        return result.ToString();
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
            if (name == "pregame-start")
            {
                // The sparkle object queues graphics and suspends THREAD_1
                // inside updateAllObjects. Resume that stack before the next
                // mainThread iteration increments playtime or updates Link.
                game._steps.Clear();
                game.Write(0xff70, 1); // _nextThread restores the object bank before resuming.
                game._cpu.CompleteCall(); // resumeThreadInAFrames returns on the next dispatch
                game.Execute(0x0900);
                game.Finish();
                game.Emit(result, "pregame-graphics");
                // Resume the source Link handler at the final voice-delay
                // decrement. It starts THREAD_2 with its own TX_1213 operand;
                // execute that thread's initializer and first tilemap upload.
                game._steps.Clear();
                game.Write(0xff70, 1);
                game.Write(0xd005, 1); // linkCutsceneB substate 1
                game.Write(0xd006, 1);
                game.Write(0xd007, 0);
                game._cpu.BeginCall(gameEntry + 6, stack: 0xc220);
                game.Execute(0x0900);
                int textEntry = game.Read(0xc2f4) | game.Read(0xc2f5) << 8;
                if (textEntry == 0) throw new InvalidDataException("linkCutsceneB did not start the text thread.");
                game.Write(0xff70, 1);
                game._cpu.BeginCall(textEntry, stack: 0xc270);
                game.Execute(0x0900);
                game.Finish();
                game.Emit(result, "pregame-text");
            }
        }
        // initializeFile supplies the source's initial checkpoint and flags.
        // The post-vanish main thread first reruns initializeGame, then on the
        // next dispatch loads the arrival graphics with Link still disabled.
        var arrival = new OriginalFrontendWork(rom);
        arrival._memory[0xcbb3] = 2;
        arrival._memory[0xcbb4] = 2;
        Array.Clear(arrival._wram, 0x47a0, 0x20);
        arrival._wram[0x47a0] = 0x41;
        arrival.Run(0x1a1f);
        arrival.Run(0x1a17);
        arrival._memory[0xc482] = 8;
        arrival.Run(0x1a1f);
        arrival.Run(0x1a1f);
        arrival._memory[0xc6d7] |= 0x20; // GLOBALFLAG_3d set by pregame state C
        arrival._memory[0xc4ab] = 0;
        arrival._memory[0xff96] = 0xff;
        arrival._steps.Clear();
        int mainLoop = FindUnique(rom, 0, [0xcd, 0xb2, 0x0c, 0xcd, 0x4d, 0x18, 0x21, 0x22, 0xc6]) + 6;
        arrival._cpu.BeginCall(mainLoop, stack: 0xc220);
        arrival.Execute(0x0900);
        arrival.Finish();
        arrival.Emit(result, "arrival-init");
        arrival._steps.Clear();
        arrival.Write(0xff70, 1);
        arrival._cpu.CompleteCall();
        arrival.Execute(0x0900);
        arrival.Finish();
        arrival.Emit(result, "arrival-load");
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
            // VBlank runs on a separate stack while the foreground thread is
            // suspended. Preserve its registers and stack for continuation.
            OracleCpu interrupt = CreateCpu();
            interrupt.BeginCall(vblank, stack: 0xc280);
            Execute(0, interrupt, returnStack: 0xc282);
            Event("vblank-work", _work + 12);
            _work = 0;
        }
    }

    private void Execute(int end, OracleCpu? execution = null, int returnStack = 0xc222)
    {
        OracleCpu cpu = execution ?? _cpu;
        for (int count = 0; count < 2_000_000; count++)
        {
            int pc = cpu.ProgramCounter;
            if ((pc == 0 && cpu.StackPointer == returnStack) || (end != 0 && pc == end)) return;
            if (pc == 0x02c1)
            {
                Flush(); Event("lcd-off", 0); _memory[0xff40] = 0; cpu.CompleteCall(); continue;
            }
            if (_bank == 0x39 && pc == 0x4009)
            {
                Flush(); Event("sound-stop", 0); cpu.CompleteCall(); continue;
            }
            if (pc == 0x0c98) { Flush(); Event("sound", cpu.Accumulator); }
            if (pc == 0x0626) { Flush(); Event("gfx", cpu.Accumulator); }
            if (pc == 0x05da) { Flush(); Event("dma-gfx", cpu.Accumulator); }
            cpu.Step();
            _work += _dma;
            _dma = 0;
        }
        throw new InvalidDataException("Frontend initializer did not terminate.");
    }

    private void Event(string kind, long value) => _steps.Add((kind, value));
    private void Flush() { if (_work != 0) Event("cpu", _work); _work = 0; }
    private void Emit(StringBuilder result, string name)
    {
        string source = name.StartsWith("arrival-", StringComparison.Ordinal)
            ? "code/bank0.s:mainThread,bank1.s:initializeGame,linkSummonedCutscene"
            : name.StartsWith("pregame-", StringComparison.Ordinal)
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
