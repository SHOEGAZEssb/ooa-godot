using System.Text;
using oracleofages;

namespace OracleOfAges.Importer;

/// <summary>Imports blocking room decoders and buffer construction, without interrupts.</summary>
internal sealed class OriginalRoomLoadingWork
{
    private readonly byte[] _rom;
    private readonly byte[] _memory = new byte[0x10000];
    private readonly byte[] _wram = new byte[0x8000];
    private readonly OracleCpu _cpu;
    private int _bank = 1, _wramBank = 1;

    private OriginalRoomLoadingWork(byte[] rom)
    {
        _rom = rom;
        _memory[0xff97] = 1;
        _memory[0xff70] = 1;
        _cpu = new OracleCpu(Read, Write, _ => { }, (pc, detail) =>
            new InvalidDataException($"Room loading ${_bank:x2}:${pc:x4}: {detail}."));
    }

    internal static string Compile(byte[] rom, int[] layouts)
    {
        Guard(rom, 0x3712, [0xfa, 0x23, 0xcd, 0xcd, 0x99, 0x07], "loadTilesetLayout");
        Guard(rom, 0x38dc, [0x21, 0x00, 0xcf, 0x06, 0xc0], "loadRoomLayout");
        Guard(rom, 4 * 0x4000 + 0x2bf1, [0x3e, 0x03, 0xe0, 0x70, 0x21, 0x00, 0xcf],
            "tilesets.generateW3VramTilesAndAttributes");
        int collision = Find(rom, [0x3e, 0x03, 0xe0, 0x70, 0x16, 0xdb, 0x21, 0x00, 0xcf, 0x06, 0xb0]);
        var rows = new StringBuilder("# operation\tindex\tvariant\tcpu-cycles\tsource\n");
        foreach (int layout in layouts.Distinct().Order())
        {
            for (int variant = 0; variant < 4; variant++)
            {
                var work = new OriginalRoomLoadingWork(rom);
                work._memory[0xcd23] = checked((byte)layout);
                // setPastCliffPalettesToRed: indoor, present, past, room $38.
                work._memory[0xcc33] = (byte)(variant == 0 ? 1 : 0);
                work._memory[0xcc34] = (byte)(variant >= 2 ? 0x80 : 0);
                work._memory[0xcc30] = (byte)(variant == 3 ? 0x38 : 0);
                rows.Append($"tileset\t{layout:x2}\t{variant}\t{work.Run(0x3712)}\tcode/bank0.s:loadTilesetLayout\n");
            }
        }
        // The stage verifies all six original layout groups. The decoder
        // uses these source identities, including swapped/companion layouts.
        for (int group = 0; group < 6; group++)
            for (int room = 0; room < 256; room++)
            {
                var work = new OriginalRoomLoadingWork(rom);
                work._memory[0xcd24] = (byte)group;
                work._memory[0xcc2f] = (byte)room;
                rows.Append($"room\t{group * 256 + room:x4}\t0\t{work.Run(0x38dc)}\tcode/bank0.s:loadRoomLayout\n");
            }
        for (int large = 0; large < 2; large++)
        {
            var work = new OriginalRoomLoadingWork(rom);
            work._memory[0xcc2d] = (byte)(large * 4);
            rows.Append($"collisions\t00\t{large}\t{work.Run(collision)}\tcode/bank0.s:loadRoomCollisions\n");
        }
        var tiles = new OriginalRoomLoadingWork(rom) { _bank = 4 };
        tiles._memory[0xff97] = 4;
        rows.Append($"vram\t00\t0\t{tiles.Run(0x6bf1)}\tcode/loadTilesToRam.s:generateW3VramTilesAndAttributes\n");
        CompileWrappers(rom, rows);
        CompileSeedTreeWork(rom, rows);
        CompileEdgeWarpSearch(rom, rows);
        CompileRoomInitialization(rom, rows);
        CompileScreenData(rom, rows);
        CompileSingleTileChanges(rom, rows);
        CompileTileSubstitutionDispatch(rom, rows);
        CompileObjectEntryWork(rom, rows);
        return rows.ToString();
    }

    private static void CompileObjectEntryWork(byte[] rom, StringBuilder rows)
    {
        Guard(rom, 0x49d7, [0xcd, 0xf1, 0x49, 0xcd, 0xf8, 0x49], "setObjectsEnabledTo2");
        Guard(rom, 0x4a04, [0x7e, 0xe6, 0x03, 0xfe, 0x01, 0x20, 0x06], "object-enabled branch");
        var freeze = new OriginalRoomLoadingWork(rom);
        int branches = 0;
        long clocks = freeze.RunUntil(0x49d7, -1, [], pc => { if (pc == 0x4a09) branches++; });
        if (branches != 60) throw new InvalidDataException("setObjectsEnabledTo2 must traverse sixty source slots.");
        // Retain every common instruction. Each taken JR contributes four
        // extra clocks; each enabled=1 slot instead executes a 32-clock write.
        // Those per-slot branches are separate from this shared traversal.
        rows.Append($"object-freeze\t00\t0\t{clocks - branches * 4}\tcode/bank1.s:setObjectsEnabledTo2\n");
        Guard(rom, 0x495b7, [0xaf, 0xea, 0xd1, 0xcd, 0xea, 0xc0, 0xcf, 0x21, 0xc0, 0xce], "parseObjectData");
        Guard(rom, 0x495cc, [0x21, 0x15, 0x43, 0x1e, 0x15], "parseObjectData lookup boundary");
        var parse = new OriginalRoomLoadingWork(rom) { _bank = 0x12 };
        parse._memory[0xff97] = 0x12;
        rows.Append($"object-parse\t00\t0\t{parse.RunUntil(0x55b7, 0x55cc, [0x3209, 0x3215])}\tcode/objectLoading.s:parseObjectData\n");
        Guard(rom, 0x3209, [0x26, 0x01, 0x18], "addRoomToEnemiesKilledList");
        for (int found = -1; found < 8; found++)
        {
            var history = new OriginalRoomLoadingWork(rom);
            history._memory[0xcc30] = 0x7a;
            if (found >= 0) history._memory[0xcdc0 + found * 2] = 0x7a;
            rows.Append($"enemy-history\t00\t{found + 1}\t{history.Run(0x3209)}\tcode/roomInitialization.s:addRoomToEnemiesKilledList\n");
        }
    }

    private static void CompileTileSubstitutionDispatch(byte[] rom, StringBuilder rows)
    {
        Guard(rom, 0x11fef, [0xcd, 0x78, 0x60, 0xcd, 0xde, 0x62, 0xcd, 0xb7, 0x60], "applyAllTileSubstitutions");
        Guard(rom, 0x1201f, [0xfa, 0xdc, 0xcd, 0xb7, 0xc8], "timewarp-return tile gate");
        Guard(rom, 0x12078, [0x3e, 0x30, 0xcd, 0xf3, 0x31, 0xc8], "pollution replacement gate");
        Guard(rom, 0x120b7, [0xcd, 0x7d, 0x19, 0xe0, 0x8b], "applyStandardTileSubstitutions");
        Guard(rom, 0x1226f, [0xcd, 0x7d, 0x19, 0xcb, 0x6f, 0xc8], "replaceOpenedChest");
        for (int group = 0; group < 8; group++)
        {
            var work = new OriginalRoomLoadingWork(rom) { _bank = 4 };
            work._memory[0xff97] = 4;
            work._memory[0xcc2d] = (byte)group;
            // Children have separate work. The overworld-only return-tile
            // predicate follows this prefix and remains separate too.
            long clocks = work.RunUntil(0x5fef, 0x601f,
                [0x6078, 0x62de, 0x60b7, 0x626f, 0x61d8, 0x627e, 0x617c, 0x61a1, 0x642c, 0x6046, 0x606a]);
            rows.Append($"tile-dispatch\t{group:x2}\t0\t{clocks}\tcode/ages/tileSubstitutions.s:applyAllTileSubstitutions\n");
        }
        for (int flags = 0; flags < 256; flags++)
        {
            if ((flags & ~0x8f) != 0) continue;
            var work = new OriginalRoomLoadingWork(rom) { _bank = 4 };
            work._memory[0xff97] = 4;
            work._memory[0xc700] = (byte)flags;
            rows.Append($"standard-tiles\t00\t{flags}\t{work.Run(0x60b7, 0x60ea)}\tcode/ages/tileSubstitutions.s:applyStandardTileSubstitutions\n");
        }
        for (int variant = 0; variant < 8; variant++)
        {
            var work = new OriginalRoomLoadingWork(rom) { _bank = 4 };
            work._memory[0xff97] = 4;
            work._memory[0xcc34] = (byte)((variant & 1) | ((variant & 2) << 5));
            work._memory[0xc6d6] = (byte)((variant >> 2) & 1);
            rows.Append($"pollution-gate\t00\t{variant}\t{work.Run(0x6078, 0x6096)}\tcode/ages/tileSubstitutions.s:replacePollutionWithWaterIfPollutionFixed\n");
        }
        for (int group = 0; group < 8; group++)
        for (int room = 0; room < 256; room++)
        for (int opened = 0; opened < 2; opened++)
        {
            var work = new OriginalRoomLoadingWork(rom) { _bank = 4 };
            work._memory[0xff97] = 4;
            work._memory[0xcc2d] = (byte)group;
            work._memory[0xcc30] = (byte)room;
            work._memory[(rom[0x09cc + group] << 8) + room] = (byte)(opened * 0x20);
            rows.Append($"opened-chest\t{group * 256 + room:x4}\t{opened}\t{work.Run(0x626f)}\tcode/commonTileSubstitutions.s:replaceOpenedChest\n");
        }
    }

    private static void CompileSingleTileChanges(byte[] rom, StringBuilder rows)
    {
        Guard(rom, 0x122de, [0xfa, 0x30, 0xcc, 0x47, 0xcd, 0x7d, 0x19, 0x4f, 0x16, 0xcf,
            0xfa, 0x2d, 0xcc, 0x21, 0x2e, 0x63], "applySingleTileChanges");
        Guard(rom, 0x1992, [0xfa, 0x01, 0xcc, 0xb7, 0xc9], "checkIsLinkedGame");
        Guard(rom, 0x12324, [0x3e, 0x14, 0xe5], "single-tile finished-game predicate");
        for (int group = 0; group < 8; group++)
        for (int room = 0; room < 256; room++)
        {
            int pointer = 0x1232e + group * 2;
            pointer = 0xc000 + rom[pointer] + (rom[pointer + 1] << 8);
            int selector = 0;
            for (int count = 0; ; count++, pointer += 4)
            {
                if (count == 1024) throw new InvalidDataException("Unterminated singleTileChangeGroupTable.");
                int mask = rom[pointer + 1];
                if (mask == 0) break;
                if (mask > 0x80 && mask is not (0xf0 or 0xf1 or 0xf2))
                    throw new InvalidDataException($"singleTileChangeGroupTable ${group:x2} has unsupported mask ${mask:x2}.");
                if (rom[pointer] == room)
                    selector |= mask switch { 0xf0 or 0xf1 => 0x100, 0xf2 => 0x200, _ => mask };
            }
            // Only material inputs enter the key. Retain ordered scans of
            // nonmatching rooms and the $f2 early return in the executed code.
            for (int variant = 0; variant < 1024; variant++)
            {
                if ((variant & ~selector) != 0) continue;
                var work = new OriginalRoomLoadingWork(rom) { _bank = 4 };
                work._memory[0xff97] = 4;
                work._memory[0xcc2d] = (byte)group;
                work._memory[0xcc30] = (byte)room;
                work._memory[(rom[0x09cc + group] << 8) + room] = (byte)variant;
                work._memory[0xcc01] = (byte)((variant >> 8) & 1);
                work._memory[0xc6d2] = (byte)((variant & 0x200) != 0 ? 0x10 : 0);
                rows.Append($"single-tiles\t{group * 256 + room:x4}\t{variant}\t{work.Run(0x62de)}\tcode/commonTileSubstitutions.s:applySingleTileChanges\n");
            }
        }
    }

    private static void CompileScreenData(byte[] rom, StringBuilder rows)
    {
        Guard(rom, 0x33cf, [0xf0, 0x97, 0xf5, 0x3e, 0x04], "loadScreenMusic");
        Guard(rom, 0x3889, [0xf0, 0x97, 0xf5, 0x3e, 0x04], "loadTilesetData");
        Guard(rom, 0x12d7a, [0xcd, 0xd6, 0x6d, 0x21], "loadTilesetData_body");
        for (int group = 0; group < 8; group++)
        for (int room = 0; room < 256; room++)
        {
            var music = new OriginalRoomLoadingWork(rom);
            music._memory[0xcc2d] = (byte)group;
            music._memory[0xcc30] = (byte)room;
            long musicClocks = music.Run(0x33cf);
            bool companionRegion = group == 0 && music._memory[0xcc45] == 0x7f;
            for (int swapped = 0; swapped < (group < 2 ? 2 : 1); swapped++)
            for (int maku = 0; maku < (group == 0 && room == 0x38 ? 2 : 1); maku++)
            for (int companion = 0; companion < (companionRegion ? 4 : 1); companion++)
            for (int water = 0; water < (group >= 4 ? 3 : 1); water++)
            {
                var work = new OriginalRoomLoadingWork(rom);
                work._memory[0xcc2d] = (byte)group;
                work._memory[0xcc30] = (byte)room;
                work._memory[0xcc45] = music._memory[0xcc45];
                work._memory[(rom[0x09cc + group] << 8) + room] = (byte)swapped;
                if (group == 0 && room == 0x38) work._memory[0xc848] = (byte)maku;
                work._memory[0xc610] = (byte)(companion == 0 ? 0 : companion + 0x0a);
                work._memory[0xc6e9] = (byte)water;
                int variant = swapped | maku << 1 | companion << 2 | water << 4;
                rows.Append($"screen-data\t{group * 256 + room:x4}\t{variant}\t{musicClocks + work.Run(0x3889)}\tcode/bank0.s:loadScreenMusic,loadTilesetData\n");
            }
        }
    }

    private static void CompileRoomInitialization(byte[] rom, StringBuilder rows)
    {
        Guard(rom, 0x30fe, [0x21, 0xda, 0x62, 0x1e, 0x01, 0xcd, 0x8a, 0x00], "initializeRoom");
        Guard(rom, 0xb9e2, [0xfa, 0x2d, 0xcc, 0xb7, 0x20, 0x07], "calculateRoomStateModifier");
        Guard(rom, 0x198a, [0x21, 0xcc, 0x09, 0xd7, 0x66, 0x68, 0x7e, 0xc9], "getRoomFlags");
        var initialize = new OriginalRoomLoadingWork(rom);
        // Normal scrolling cannot run while Link's strange-force return owns
        // him. wcc05 starts at $ff; its only later changes clear/set bit 1
        // for soldierSubid0a. initializeRoom reads bits 0, 2 and 3, all set.
        initialize._memory[0xcc05] = 0xff;
        rows.Append($"initialization\t00\t0\t{initialize.Run(0x30fe, 0x79e2, 0x1618, 0x5872, 0x7a12, 0x7de1, 0x321d, 0x768a, 0x76d3, 0x55b7, 0x5015)}\tcode/bank0.s:initializeRoom\n");
        for (int group = 0; group < 8; group++)
        for (int variant = 0; variant < 16; variant++)
        {
            var work = new OriginalRoomLoadingWork(rom) { _bank = 2 };
            work._memory[0xff97] = 2;
            work._memory[0xcc2d] = (byte)group;
            work._memory[0xcc34] = (byte)((variant & 1) != 0 ? 0x40 : 0);
            work._memory[rom[0x09cc + group] << 8] = (byte)((variant >> 1) & 1);
            work._memory[0xcc31] = (byte)((variant & 4) != 0 ? 0x7f : 0);
            work._memory[0xc610] = (byte)((variant & 8) != 0 ? 0x0b : 0);
            rows.Append($"room-state\t{group:x2}\t{variant}\t{work.Run(0x79e2)}\tcode/roomInitialization.s:calculateRoomStateModifier\n");
        }
        // Both functions first scan a group-specific room list. Stop before
        // the selected handler; its save-dependent work is separate.
        foreach (var (operation, bank, entry, source) in new[] {
            ("room-specific", 0x12, 0x5872, "roomSpecificCode"),
            ("vram-specific", 2, 0x7a88, "roomGfxChanges"),
            ("tile-specific", 4, 0x642c, "roomSpecificTileChanges") })
        {
            int offset = bank * 0x4000 + entry - 0x4000;
            Guard(rom, offset, [0xfa, 0x30, 0xcc, 0x21], operation);
            Guard(rom, offset + 6, [0xcd, 0xfe, 0x1d, 0xd0, 0xc7], operation + " dispatch");
            for (int group = 0; group < 8; group++)
            for (int room = 0; room < 256; room++)
            {
                var work = new OriginalRoomLoadingWork(rom) { _bank = bank };
                work._memory[0xff97] = (byte)bank;
                work._memory[0xcc2d] = (byte)group;
                work._memory[0xcc30] = (byte)room;
                rows.Append($"{operation}\t{group * 256 + room:x4}\t0\t{work.RunUntil(entry, entry + 10, [])}\tcode/ages/{source}.s\n");
            }
        }
    }

    private static void CompileEdgeWarpSearch(byte[] rom, StringBuilder rows)
    {
        Guard(rom, 0x621a, [0x3e, 0xff, 0xea, 0xc0, 0xce, 0x21, 0xc8, 0x46], "checkScreenEdgeWarps");
        Guard(rom, 0x106c8, [0xfa, 0x00, 0xcd, 0xe6, 0x04, 0xc8, 0xfa, 0x02, 0xcd], "findScreenEdgeWarpSource");
        for (int group = 0; group < 8; group++)
        for (int room = 0; room < 256; room++)
        for (int variant = 0; variant < 8; variant++)
        {
            var work = new OriginalRoomLoadingWork(rom);
            work._memory[0xcc2d] = (byte)group;
            work._memory[0xcc30] = (byte)room;
            work._memory[0xcd00] = 4; // Link has reached a screen boundary.
            work._memory[0xcd02] = (byte)(variant & 2);
            work._memory[0xcc2c] = (byte)(variant >= 4 ? 0xd1 : 0xd0);
            work._wram[0x100d] = (byte)((variant & 1) == 0 ? 0 : 0xff);
            // Lookup/copy timing includes pointed source lists and no-match
            // scans. Companion dismount is a separate gameplay operation.
            rows.Append($"edge-warp\t{group * 256 + room:x4}\t{variant}\t{work.Run(0x621a, 0x4732)}\tcode/bank4.s:findScreenEdgeWarpSource\n");
        }
    }

    private static void CompileWrappers(byte[] rom, StringBuilder rows)
    {
        Guard(rom, 0x38a5, [0xf0, 0x97, 0xf5, 0xfa, 0x2a, 0xcd], "loadTilesetAndRoomLayout");
        Guard(rom, 0x3a4e, [0xf0, 0x70, 0x4f, 0xf0, 0x97, 0x47, 0xc5], "generateVramTilesWithRoomChanges");
        Guard(rom, 0x49c9, [0x21, 0x8a, 0xcc, 0x06, 0x57], "func_49c9");
        for (int changed = 0; changed < 2; changed++)
        {
            var layout = new OriginalRoomLoadingWork(rom);
            layout._memory[0xcd23] = (byte)changed;
            // Bodies are charged separately. Retain their CALL instructions,
            // bank changes, and the full 192-byte room-buffer copy here.
            rows.Append($"layout-wrapper\t00\t{changed}\t{layout.Run(0x38a5, 0x3712, 0x38dc, 0x5fef)}\tcode/bank0.s:loadTilesetAndRoomLayout\n");
        }
        var vram = new OriginalRoomLoadingWork(rom);
        rows.Append($"vram-wrapper\t00\t0\t{vram.Run(0x3a4e, 0x6bf1, 0x7a88)}\tcode/bank0.s:generateVramTilesWithRoomChanges\n");
        var clear = new OriginalRoomLoadingWork(rom);
        rows.Append($"scroll-clear\t00\t0\t{clear.Run(0x49c9)}\tcode/bank1.s:func_49c9\n");
        Guard(rom, 0x1618, [0xf0, 0x97, 0xf5, 0x3e, 0x3f], "refreshObjectGfx");
        Guard(rom, 0xfc154, [0xcd, 0x27, 0x43, 0x16, 0xd0], "refreshObjectGfx_body");
        Guard(rom, 0xfc18f, [0xfa, 0x1d, 0xcc], "refreshObjectGfx_body extra-header boundary");
        Guard(rom, 0xfc355, [0xc5, 0xcd, 0x37, 0x44, 0xc1, 0x2a, 0xc9], "interactionGetObjectGfxIndex");
        var graphicsWrapper = new OriginalRoomLoadingWork(rom);
        var graphics = new OriginalRoomLoadingWork(rom) { _bank = 0x3f };
        graphics._memory[0xff97] = 0x3f;
        // Count the fixed traversal and enemy/part lookups. Residency scans,
        // indirect interaction data, item lookup page crossings, and extra
        // header loads are separate, still-unmodeled work. Do not bake the
        // empty-room costs of those state-dependent bodies into this row.
        long graphicsClocks = graphicsWrapper.Run(0x1618, 0x4154) +
            graphics.RunUntil(0x4154, 0x418f, [0x430e, 0x4437, 0x435c]);
        // Common prefixes of the skipped metadata/residency queries. The
        // conditional RET fetch is shared; its taken return or fallthrough
        // body remains separate. Use a source indirect interaction and a
        // page-crossing item to execute each RET's untaken prefix.
        var markPrefix = new OriginalRoomLoadingWork(rom) { _bank = 0x3f };
        long mark = markPrefix.RunUntil(0x430e, 0x4310, [], argument: 1);
        Guard(rom, 0xfc447, [0xd0], "interactionGetData conditional return");
        int indirect = Enumerable.Range(0, 256).First(id => (rom[0xfe427 + id * 3] & 0x80) != 0);
        var interactionPrefix = new OriginalRoomLoadingWork(rom) { _bank = 0x3f };
        interactionPrefix._wram[0x1041] = (byte)indirect;
        interactionPrefix._cpu.SetDe(0xd040);
        long interaction = interactionPrefix.RunUntil(0x4437, 0x4448, []);
        Guard(rom, 0x0010, [0x85, 0x6f, 0xd0, 0x24, 0xc9], "rst_addAToHl page crossing");
        var itemPrefix = new OriginalRoomLoadingWork(rom) { _bank = 0x3f };
        itemPrefix._wram[0x1001] = 0x1f;
        itemPrefix._cpu.SetDe(0xd000);
        long item = itemPrefix.Run(0x435c, 0x0013);
        var extraPrefix = new OriginalRoomLoadingWork(rom) { _bank = 0x3f };
        graphicsClocks += 58 * mark + 16 * interaction + 10 * item + extraPrefix.RunUntil(0x418f, 0x4193, []);
        rows.Append($"graphics-traversal\t00\t0\t{graphicsClocks}\tcode/loadGraphics.s:refreshObjectGfx_body\n");
    }

    private static void CompileSeedTreeWork(byte[] rom, StringBuilder rows)
    {
        Guard(rom, 0x6016, [0xfa, 0x34, 0xcc, 0xe6, 0x01, 0xc8, 0x3e, 0x02], "updateSeedTreeRefillData");
        Guard(rom, 0x6056, [0x78, 0xe0, 0x8d, 0x7b, 0xcb, 0x83, 0xe6, 0x01], "checkSeedTreeRefillIndex");
        var wrapper = new OriginalRoomLoadingWork(rom);
        wrapper._memory[0xcc34] = 1;
        rows.Append($"seed-wrapper\t00\t0\t{wrapper.Run(0x6016, 0x6056)}\tcode/bank1.s:updateSeedTreeRefillData\n");
        // Each history has eight slots. Cases: refilled, empty slot, duplicate,
        // full history, tree visit with an empty slot, tree visit when full.
        for (int index = 0; index < 16; index++)
        for (int kind = 0; kind < 6; kind++)
        for (int slot = 0; slot < (kind is 1 or 2 or 4 ? 8 : 1); slot++)
        {
            var work = new OriginalRoomLoadingWork(rom) { _wramBank = 2 };
            work._memory[0xff70] = 2;
            work._memory[0xcc2d] = (byte)(kind >= 4 ? 0 : 1);
            work._memory[0xcc30] = 0x7a;
            if (kind == 0) work._memory[0xcc4d + (index >> 3)] = (byte)(1 << (index & 7));
            Array.Fill(work._wram, (byte)1, 0x2900, 8);
            if (kind is 1 or 4) work._wram[0x2900 + slot] = 0;
            if (kind == 2) work._wram[0x2900 + slot] = 0x7a;
            work._cpu.SetBc((16 - index) * 256 + (kind >= 4 ? 0x7a : 0x7b));
            work._cpu.SetDe(0);
            long clocks = work.Run(0x6056);
            rows.Append($"seed-entry\t{index * 16 + kind:x2}\t{slot}\t{clocks}\tcode/bank1.s:checkSeedTreeRefillIndex\n");
        }
    }

    internal static string CompileSeaSearch(byte[] rom)
    {
        Guard(rom, 0xba12, [0xfa, 0x33, 0xcc, 0x21, 0x2c, 0x7a, 0xd7, 0x7e, 0xd7],
            "createSeaEffectsPartIfApplicable");
        Guard(rom, 0xba25, [0xcd, 0x8e, 0x3e, 0xc0, 0x36, 0x2e, 0xc9], "sea-effects allocation");
        var rows = new StringBuilder("# collisions\torder\ttile\tposition\tcpu-cycles\tsource\n");
        for (int collisions = 0; collisions < 6; collisions++)
        {
            int pointer = 0xba2c + collisions;
            pointer += rom[pointer];
            for (int order = 0; ; order++)
            {
                int tile = rom[pointer + order];
                for (int position = 0; position < (tile == 0 ? 1 : 192); position++)
                {
                    var work = new OriginalRoomLoadingWork(rom) { _bank = 2 };
                    work._memory[0xff97] = 2;
                    work._memory[0xcc33] = (byte)collisions;
                    Array.Fill(work._memory, (byte)0xf0, 0xcf00, 192);
                    if (tile != 0) work._memory[0xcf00 + position] = (byte)tile;
                    // Search time only: a match stops before the allocator.
                    // No match includes the normal return at the list's end.
                    long clocks = work.RunUntil(0x7a12, 0x7a25, []);
                    rows.Append($"{collisions:x2}\t{order}\t{tile:x2}\t{position}\t{clocks}\tcode/roomInitialization.s:createSeaEffectsPartIfApplicable\n");
                }
                if (tile == 0) break;
                if (order >= 15) throw new InvalidDataException("Unterminated seaEffectTileTable list.");
            }
        }
        return rows.ToString();
    }

    internal static string CompileGameplayDispatch(byte[] rom)
    {
        Guard(rom, 0x345b, [0xf0, 0x97, 0xf5, 0x3e, 0x05, 0xe0, 0x97], "updateAllObjects");
        Guard(rom, 0x0d9a, [0x21, 0xb6, 0xc4, 0xcb, 0x46, 0xc0, 0x36, 0xff], "drawAllSprites");
        Guard(rom, 0x0e2f, [0x21, 0xc0, 0xc4, 0xf0, 0xa0], "drawAllSprites terrain-effects boundary");
        var rows = new StringBuilder("# operation\tvariant\tcpu-cycles\tsource\n");
        var dispatch = new OriginalRoomLoadingWork(rom);
        dispatch._memory[0xcc2c] = 0xd0;
        // All called bodies are independent work. The optional mounted/grab
        // CALLs retain their common instruction-fetch cost; their taken-call
        // overhead belongs with the omitted state-dependent bodies.
        long clocks = dispatch.Run(0x345b, 0x4000, 0x4872, 0x3616, 0x2ea5,
            0x5e58, 0x3b36, 0x410d, 0x54df, 0x2b25, 0x491a, 0x494d, 0x12ae, 0x6c32, 0x5906);
        rows.Append($"objects\t0\t{clocks}\tcode/bank0.s:updateAllObjects\n");
        foreach (var (operation, bank, entry, branch, slots) in new[] {
            ("enemies", 0, 0x2ea5, 0x2ecc, 16), ("parts", 0x11, 0x5e58, 0x5e79, 16),
            ("interactions", 0, 0x3b36, -1, 16), ("items", 7, 0x4872, 0x48a8, 10),
            ("items-post", 7, 0x491a, -1, 10) })
        {
            var loop = new OriginalRoomLoadingWork(rom) { _bank = bank };
            loop._memory[0xff97] = (byte)bank;
            if (branch >= 0)
                Guard(rom, (bank == 0 ? 0 : bank * 0x4000 - 0x4000) + branch,
                    [0x28], operation + " empty-slot branch");
            int branches = 0;
            clocks = loop.RunUntil(entry, -1, [], pc => { if (pc == branch) branches++; });
            if (branch >= 0 && branches != slots)
                throw new InvalidDataException($"{operation} expected {slots} source slots, got {branches}.");
            // Normal (unfrozen) headers and slot walks, excluding handlers.
            // JR's per-empty-slot extra four clocks remain separate, as do
            // conditional CALL's per-enabled-slot extra twelve clocks.
            rows.Append($"{operation}\t0\t{clocks - branches * 4}\tcode/{(operation == "parts" ? "updateParts" : operation.StartsWith("items") ? "updateItems" : "bank0")}.s\n");
        }
        for (int variant = 0; variant < 6; variant++)
        {
            var drawing = new OriginalRoomLoadingWork(rom);
            drawing._memory[0xcbae] = (byte)((variant & 1) * 4);
            drawing._memory[0xcd00] = (byte)(variant >= 2 ? 8 : 1);
            drawing._memory[0xcc2e] = (byte)(variant >= 4 ? 1 : 0);
            // queueDrawEverything's 58 slots, Link's 1/6 slots, and the 64
            // draw-queue entries are always walked. Exclude individual
            // enqueue/draw handlers and stop before shadows and OAM clearing.
            // The conditional draw CALL contributes its shared fetch clocks;
            // taken-call overhead and the rendered object's work remain separate.
            int queued = 0;
            clocks = drawing.RunUntil(0x0d9a, 0x0e2f, [0x10a8], pc => { if (pc == 0x10a8) queued++; });
            var enqueue = new OriginalRoomLoadingWork(rom);
            enqueue._wram[0x1000] = 1;
            enqueue._cpu.SetDe(0xd000);
            Guard(rom, 0x10a8, [0x1a, 0xb7, 0xc8], "objectQueueDraw enabled gate");
            clocks += queued * enqueue.RunUntil(0x10a8, 0x10ab, []);
            rows.Append($"sprites\t{variant}\t{clocks}\tcode/bank0.s:drawAllSprites\n");
        }
        return rows.ToString();
    }

    private long Run(int entry, params int[] skippedBodies)
        => RunUntil(entry, -1, skippedBodies);

    private long RunUntil(int entry, int stop, int[] skippedBodies, Action<int>? visit = null, int argument = 0)
    {
        _cpu.BeginCall(entry, argument, stack: 0xc1f0);
        for (int count = 0; _cpu.ProgramCounter != 0 || _cpu.StackPointer != 0xc1f2; count++)
        {
            if (_cpu.ProgramCounter == stop) break;
            if (count == 500_000)
                throw new InvalidDataException($"Room loader ${entry:x4} did not return.");
            visit?.Invoke(_cpu.ProgramCounter);
            if (skippedBodies.Contains(_cpu.ProgramCounter)) _cpu.CompleteCall();
            else _cpu.Step();
        }
        return _cpu.Cycles;
    }

    private static void Guard(byte[] rom, int address, byte[] signature, string source)
    {
        if (!rom.AsSpan(address, signature.Length).SequenceEqual(signature))
            throw new InvalidDataException($"Room-loading source {source} clean-US ROM entry ${address:x6} changed.");
    }

    private static int Find(byte[] rom, byte[] signature)
    {
        int match = -1;
        for (int offset = 0; offset < 0x4000 - signature.Length; offset++)
            if (rom.AsSpan(offset, signature.Length).SequenceEqual(signature))
            {
                if (match != -1) throw new InvalidDataException("Ambiguous loadRoomCollisions entry.");
                match = offset;
            }
        return match >= 0 ? match : throw new InvalidDataException("Missing loadRoomCollisions entry.");
    }

    private int Read(int address)
    {
        if (address < 0x4000) return _rom[address];
        if (address < 0x8000) return _rom[_bank * 0x4000 + address - 0x4000];
        if (address is >= 0xd000 and < 0xe000) return _wram[_wramBank * 0x1000 + address - 0xd000];
        if (address is >= 0xc000 and < 0xd000 or >= 0xff80 || address is 0xff70 or 0xff4f) return _memory[address];
        throw new InvalidDataException($"Room loader read unsupported memory ${address:x4}.");
    }

    private void Write(int address, int value)
    {
        if (address is >= 0x2000 and < 0x4000) { _bank = value & 0x3f; return; }
        if (address is >= 0xd000 and < 0xe000) { _wram[_wramBank * 0x1000 + address - 0xd000] = (byte)value; return; }
        if (address == 0xff70) _wramBank = Math.Max(1, value & 7);
        else if (address is not (>= 0xc000 and < 0xd000 or >= 0xff80 or 0xff4f))
            throw new InvalidDataException($"Room loader wrote unsupported memory ${address:x4}.");
        _memory[address] = (byte)value;
    }
}
