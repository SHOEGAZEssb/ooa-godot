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
        CompileScrollSelection(rom, rows);
        CompileRoomSpawnChecks(rom, rows);
        return rows.ToString();
    }

    private static void CompileScrollSelection(byte[] rom, StringBuilder rows)
    {
        Guard(rom, 0x5f45, [0xfa, 0x00, 0xcd, 0xe6, 0x04, 0xc8], "getNextActiveRoom");
        Guard(rom, 0x5f13, [0xfa, 0x39, 0xcc, 0x3c, 0x20, 0x0d], "updateActiveRoom");
        Guard(rom, 0x5edd, [0xfa, 0x2d, 0xcc, 0xfe, 0x02], "checkRoomPack");
        for (int group = 0; group < 8; group++)
        {
            for (int room = 0; room < 256; room++)
            {
                var lookup = new OriginalRoomLoadingWork(rom);
                lookup._memory[0xcd00] = 4;
                lookup._memory[0xcc2d] = (byte)group;
                lookup._memory[0xcc30] = (byte)room;
                // Retain the ordered room search and standard dispatch.
                // The forest/eye handlers and actual map advance are separate.
                rows.Append($"next-room\t{group * 256 + room:x4}\t0\t{lookup.Run(0x5f45, 0x5f96, 0x5feb, 0x5f13)}\tcode/bank1.s:getNextActiveRoom\n");
            }
            for (int direction = 0; direction < 4; direction++)
            {
                var advance = new OriginalRoomLoadingWork(rom);
                advance._memory[0xcc2d] = (byte)group;
                advance._memory[0xcc30] = 0x88;
                advance._memory[0xcc39] = 0xff; // wDungeonIndex: ordinary map arithmetic.
                advance._memory[0xcd02] = (byte)direction;
                rows.Append($"room-advance\t{group:x2}\t{direction}\t{advance.Run(0x5f13)}\tcode/bank1.s:updateActiveRoom\n");
            }
            for (int changed = 0; changed < 2; changed++)
            {
                var pack = new OriginalRoomLoadingWork(rom);
                pack._memory[0xcc2d] = (byte)group;
                pack._memory[0xcc45] = (byte)changed;
                rows.Append($"room-pack\t{group:x2}\t{changed}\t{pack.Run(0x5edd)}\tcode/bank1.s:checkRoomPack\n");
            }
        }
    }

    private static void CompileRoomSpawnChecks(byte[] rom, StringBuilder rows)
    {
        Guard(rom, 0x7de1, [0x3e, 0x34, 0xcd, 0xf3, 0x31, 0xc0], "checkLoadPirateShip");
        Guard(rom, 0x7e07, [0x21, 0x40, 0xd1], "pirate reserved-slot boundary");
        for (int variant = 0; variant < 64; variant++)
        {
            var ship = new OriginalRoomLoadingWork(rom);
            ship._memory[0xcc34] = (byte)((variant & 1) | ((variant & 2) << 5) | ((variant & 4) << 5));
            ship._memory[0xcc01] = (byte)((variant >> 3) & 1);
            ship._memory[0xc6d6] = (byte)(variant & 16); // GLOBALFLAG_PIRATES_GONE $34.
            ship._memory[0xcc30] = (byte)((variant >> 5) & 1);
            ship._memory[0xc6ec] = 1;
            // Stop before the live reserved-slot check and spawning body.
            rows.Append($"pirate-load\t00\t{variant}\t{ship.RunUntil(0x7de1, 0x7e07, [])}\tcode/ages/pirateShip.s:checkLoadPirateShip\n");
        }
        Guard(rom, 0x321d, [0x26, 0x06, 0x6f], "checkSpawnTimeportalInteraction");
        Guard(rom, 0xb9be, [0xaf, 0xea, 0xdd, 0xcd], "timeportal spawn body");
        for (int match = 0; match < 3; match++)
        {
            var portal = new OriginalRoomLoadingWork(rom);
            portal._memory[0xcc30] = 1;
            portal._memory[0xc63e] = (byte)(match == 0 ? 1 : 0);
            portal._memory[0xc63f] = (byte)(match == 2 ? 1 : 0);
            // Group miss, room miss, or matching portal before allocation.
            rows.Append($"portal-spawn\t00\t{match}\t{portal.RunUntil(0x321d, 0x3aef, [])}\tcode/roomInitialization.s:checkSpawnTimeportalInteraction\n");
        }
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
        Guard(rom, 0x54315, [0xfa, 0x2d, 0xcc, 0x21], "getObjectDataAddress");
        Guard(rom, 0x495d4, [0x1a, 0xfe, 0xfe], "parseGivenObjectData boundary");
        // The lookup's double-index table stays within one page for all
        // eight groups; room indexing uses ADD HL,DE with fixed timing.
        // Include that lookup and its bank-switch wrapper before opcodes.
        rows.Append($"object-parse\t00\t0\t{parse.RunUntil(0x55b7, 0x55d4, [0x3209, 0x3215])}\tcode/objectLoading.s:parseObjectData\n");
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

    internal static string CompileLinkWallWork(byte[] rom)
    {
        Guard(rom, 0x15e62, [0x1e, 0x33, 0xaf, 0x12, 0xfa, 0x2c, 0xcc, 0x0f, 0xd8], "specialObjectUpdateAdjacentWallsBitset");
        Guard(rom, 0x15e75, [0x47, 0x21, 0x8c, 0x5e], "adjacent-wall normalization");
        Guard(rom, 0x15ea3, [0x3e, 0x01, 0xe0, 0x8b, 0x21, 0xd2, 0x5e], "calculateAdjacentWallsBitset");
        Guard(rom, 0x14d1, [0x78, 0xe6, 0xf0, 0x6f, 0x79, 0xcb, 0x37], "checkTileCollisionAt_allowHoles");
        Guard(rom, 0x15ef2, [0x78, 0xe6, 0xf0, 0x6f, 0x79, 0xcb, 0x37], "raised-floor collision probe");
        var rows = new StringBuilder("# operation\tindex\tvariant\tcpu-cycles\tblocked\tsource\n");
        for (int raised = 0; raised < 2; raised++)
        for (int collision = 0; collision < 32; collision++)
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            var probe = new OriginalRoomLoadingWork(rom) { _bank = 5 };
            probe._cpu.SetBc((y * 2 << 8) | x * 2);
            probe._memory[0xce00] = (byte)collision;
            long clocks = probe.Run(raised == 0 ? 0x14d1 : 0x5ef2);
            rows.Append($"probe\t{collision:x2}\t{raised * 64 + y * 8 + x}\t{clocks}\t{(probe._cpu.CarrySet ? 1 : 0)}\tcode/bank0.s:checkTileCollisionAt_allowHoles;object_code/common/specialObjects/link.s\n");
        }
        for (int variant = 0; variant < 4; variant++)
        {
            var loop = new OriginalRoomLoadingWork(rom) { _bank = 5 };
            loop._memory[0xcc34] = (byte)((variant & 2) * 0x10);
            loop._memory[0xcc69] = (byte)((variant & 1) == 0 ? 0 : 0xfd);
            int probes = 0;
            long clocks = loop.RunUntil(0x5ea3, -1, [0x14d1, 0x5ef2],
                pc => { if (pc is 0x14d1 or 0x5ef2) probes++; });
            if (probes != 8) throw new InvalidDataException("calculateAdjacentWallsBitset must execute eight probes.");
            rows.Append($"loop\t00\t{variant}\t{clocks}\t0\tobject_code/common/specialObjects/link.s:calculateAdjacentWallsBitset\n");
        }
        var prefix = new OriginalRoomLoadingWork(rom) { _bank = 5 };
        prefix._memory[0xcc2c] = 0xd0;
        prefix._cpu.SetDe(0xd000);
        long prefixClocks = prefix.RunUntil(0x5e62, 0x5ea3, []);
        for (int walls = 0; walls < 256; walls++)
        {
            var suffix = new OriginalRoomLoadingWork(rom) { _bank = 5 };
            suffix._cpu.SetDe(0xd000);
            long clocks = suffix.RunUntil(0x5e75, -1, [], argument: walls);
            rows.Append($"wrapper\t{walls:x2}\t0\t{prefixClocks + clocks}\t0\tobject_code/common/specialObjects/link.s:specialObjectUpdateAdjacentWallsBitset\n");
        }
        return rows.ToString();
    }

    internal static string CompileActiveTileWork(byte[] rom)
    {
        Guard(rom, 0x14406, [0x01, 0x00, 0x05, 0xcd, 0x35, 0x14], "linkGetActiveTileType");
        var rows = new StringBuilder("# collisions\ttile\tchange\tcpu-cycles\tsource\n");
        for (int collisions = 0; collisions < 6; collisions++)
        for (int tile = 0; tile < 256; tile++)
        for (int change = 0; change < 3; change++)
        {
            var work = new OriginalRoomLoadingWork(rom) { _bank = 5 };
            work._cpu.SetDe(0xd000);
            work._wram[0x100b] = 8;
            work._wram[0x100d] = 8;
            work._memory[0xcc33] = (byte)collisions;
            work._memory[0xcf00] = (byte)tile;
            work._memory[0xcc99] = (byte)(change == 1 ? 1 : 0);
            work._memory[0xcc9a] = (byte)(change == 2 ? tile ^ 1 : tile);
            rows.Append($"{collisions:x2}\t{tile:x2}\t{change}\t{work.Run(0x4406)}\tobject_code/common/specialObjects/commonCode.s:linkGetActiveTileType\n");
        }
        return rows.ToString();
    }

    internal static string CompileTileInteractionWork(byte[] rom)
    {
        Guard(rom, 0x1280, [0xf0, 0x97, 0xf5, 0x3e, 0x06], "interactWithTileBeforeLink bank wrapper");
        Guard(rom, 0x18000, [0xfa, 0x5a, 0xcc, 0xb7, 0xc0, 0xcd, 0x73, 0x43], "interactWithTileBeforeLink");
        Guard(rom, 0x18017, [0x47, 0xe6, 0x0f, 0xc7], "interactable tile handler dispatch");
        var rows = new StringBuilder("# collisions\ttile\tcpu-cycles\tsource\n");
        for (int collisions = 0; collisions < 6; collisions++)
        for (int tile = 0; tile < 256; tile++)
        {
            long clocks = -1;
            for (int direction = 0; direction < 4; direction++)
            {
                var work = new OriginalRoomLoadingWork(rom) { _bank = 5 };
                work._memory[0xff97] = 5;
                work._memory[0xcc33] = (byte)collisions;
                work._cpu.SetDe(0xd000);
                work._wram[0x100b] = 0x48;
                work._wram[0x100d] = 0x48;
                work._wram[0x1006] = (byte)direction;
                Array.Fill(work._memory, (byte)tile, 0xcf00, 256);
                // A miss includes resetPushingAgainstTileCounter and the bank
                // wrapper's return. A match stops before the tile handler:
                // its effects and return path remain separate work.
                long current = work.RunUntil(0x1280, 0x4017, []);
                if (clocks >= 0 && current != clocks)
                    throw new InvalidDataException("Front-tile lookup work depends on direction.");
                clocks = current;
            }
            rows.Append($"{collisions:x2}\t{tile:x2}\t{clocks}\tcode/bank0.s:interactWithTileBeforeLink;code/interactableTiles.s\n");
        }
        return rows.ToString();
    }

    internal static string CompilePegasusWork(byte[] rom)
    {
        Guard(rom, 0x2bbd, [0x21, 0x6d, 0xcc, 0xcb, 0xbe, 0x2d, 0x06, 0x00,
            0x0e, 0x07, 0x3e, 0x11, 0xcd, 0xb0, 0x23], "decPegasusSeedCounter");
        var rows = new StringBuilder("# ring\tcounter\tcpu-cycles\tsource\n");
        for (int ring = 0; ring < 2; ring++)
        for (int counter = 0; counter < 512; counter++)
        {
            long clocks = -1;
            foreach (int high in counter < 256 ? new[] { 0 } : new[] { 1, 0x3f, 0x7f })
            foreach (int signal in new[] { 0, 0x80 })
            {
                var work = new OriginalRoomLoadingWork(rom);
                work._memory[0xc6cb] = (byte)(ring == 0 ? 0xff : 0x11);
                work._memory[0xcc6c] = (byte)counter;
                work._memory[0xcc6d] = (byte)(high | signal);
                long current = work.Run(0x2bbd);
                if (clocks >= 0 && current != clocks)
                    throw new InvalidDataException("Pegasus work needs more than the low byte and nonzero-high-byte cases.");
                clocks = current;
            }
            rows.Append($"{ring}\t{counter}\t{clocks}\tcode/bank0.s:decPegasusSeedCounter\n");
        }
        return rows.ToString();
    }

    internal static string CompileLinkStateWork(byte[] rom)
    {
        Guard(rom, 0x154dd, [0x3e, 0x80, 0xea, 0x66, 0xcc, 0xfa, 0xab, 0xc4], "linkState01");
        Guard(rom, 0x15604, [0xfa, 0x95, 0xcc, 0x47, 0x1e, 0x09], "linkState01 normal movement");
        Guard(rom, 0x15624, [0xfa, 0x60, 0xcc, 0xb7, 0xc0, 0xc3, 0x64, 0x2b], "linkState01 direction tail");
        var rows = new StringBuilder("# moving\tcpu-cycles\tsource\n");
        for (int moving = 0; moving < 2; moving++)
        {
            var work = new OriginalRoomLoadingWork(rom) { _bank = 5 };
            work._memory[0xff97] = 5;
            work._memory[0xcc2c] = 0xd0;
            work._memory[0xcc2b] = (byte)(moving == 0 ? 0xff : 0);
            work._memory[0xcc95] = 0x7f;
            work._cpu.SetDe(0xd000);
            // Ordinary grounded, unladen, non-slippery normal Link. Retain
            // only this handler's instructions, including CALL/JP fetches.
            // Every child body (including the banked transformation query)
            // is separate work; the skipped query's ordinary result is B=0.
            long clocks = work.Run(0x54dd, 0x4268, 0x54c0, 0x1859, 0x2bbd,
                0x1b5d, 0x1280, 0x42b7, 0x1255, 0x2c18, 0x5e62, 0x5d5b,
                0x5faf, 0x6034, 0x5af3, 0x1e3c, 0x516c, 0x008a, 0x5ce6,
                0x5ad0, 0x2b64);
            rows.Append($"{moving}\t{clocks}\tobject_code/common/specialObjects/link.s:linkState01\n");
        }
        return rows.ToString();
    }

    internal static string CompileIdleItemWork(byte[] rom)
    {
        Guard(rom, 0x2c18, [0x0e, 0x02, 0xf0, 0x97, 0xf5, 0x3e, 0x06], "checkUseItems wrapper");
        Guard(rom, 0x18954, [0x26, 0xc6, 0x6b, 0x7e, 0xb7, 0x20, 0x11], "checkItemUsed");
        var rows = new StringBuilder("# operation\tindex\tvariant\tcpu-cycles\tsource\n");
        var body = new OriginalRoomLoadingWork(rom) { _bank = 5 };
        body._memory[0xff97] = 5;
        // Ordinary grounded path with no shop/grab/spinner/item-disable gate.
        // Item checks are separate; active parent bodies and their taken-CALL
        // overhead remain separate from this shared traversal.
        rows.Append($"body\t00\t0\t{body.Run(0x2c18, 0x4954)}\tcode/parentItemUsage.s:checkUseItems\n");
        for (int item = 0; item < 256; item++)
        {
            var work = new OriginalRoomLoadingWork(rom) { _bank = 6 };
            work._cpu.SetDe(0x0189);
            work._memory[0xc689] = (byte)item;
            work._memory[0xc6cb] = 0xff;
            rows.Append($"item\t{item:x2}\t0\t{work.Run(0x4954)}\tcode/parentItemUsage.s:checkItemUsed\n");
        }
        foreach (int ring in new[] { 0x0b, 0x3d })
        for (int otherEquipped = 0; otherEquipped < 2; otherEquipped++)
        {
            var work = new OriginalRoomLoadingWork(rom) { _bank = 6 };
            work._cpu.SetDe(0x0189);
            work._memory[0xc688] = (byte)otherEquipped;
            work._memory[0xc6cb] = (byte)ring;
            rows.Append($"punch\t{ring:x2}\t{otherEquipped}\t{work.Run(0x4954)}\tcode/parentItemUsage.s:checkItemUsed\n");
        }
        return rows.ToString();
    }

    internal static string CompilePirateCourseWork(byte[] rom)
    {
        Guard(rom, 0x7dcc, [0x3e, 0x34, 0xcd, 0xf3, 0x31, 0xc0], "updatePirateShip");
        Guard(rom, 0x7eaa, [0xfa, 0xe1, 0xcd, 0xb7, 0xc8], "updatePirateShipAngle");
        Guard(rom, 0x7e40, [0xcd, 0x59, 0x18, 0xfa, 0x8d, 0xcc], "updatePirateShipPosition");
        Guard(rom, 0x7e6b, [0xfa, 0xef, 0xc6, 0xe6, 0x03], "updatePirateShipRoom");
        var rows = new StringBuilder("# operation\tindex\tvariant\tcpu-cycles\tsource\n");
        void Add(string operation, int index, int variant, long clocks, string source) =>
            rows.Append($"{operation}\t{index:x2}\t{variant}\t{clocks}\tcode/ages/pirateShip.s:{source}\n");
        for (int gone = 0; gone < 2; gone++)
        {
            var work = new OriginalRoomLoadingWork(rom);
            work._memory[0xc6d6] = (byte)(gone * 16);
            Add("dispatch", 0, gone, work.Run(0x7dcc, 0x7de1, 0x7e1e, 0x7eaa, 0x7e40, 0x7e6b), "updatePirateShip");
        }
        for (int centered = 0; centered < 3; centered++)
        {
            var work = new OriginalRoomLoadingWork(rom);
            work._memory[0xc6ed] = (byte)(centered > 0 ? 8 : 0);
            work._memory[0xc6ee] = (byte)(centered > 1 ? 8 : 0);
            Add("tile", 0, centered, work.Run(0x7e1e), "updatePirateShipChangedTile");
        }
        for (int linked = 0; linked < 2; linked++)
        {
            var tiles = new HashSet<int> { 0, 0xff };
            int pointer = linked == 0 ? 0x7f02 : 0x7edd, count = 0;
            for (; rom[pointer] != 0; pointer += 3)
            {
                if (++count > 12 || rom[pointer + 1] == 0xff)
                    throw new InvalidDataException("Unexpected pirate course or reserved unmatched-tile value.");
                tiles.Add(rom[pointer + 1]);
            }
            if (count != (linked == 0 ? 6 : 12)) throw new InvalidDataException("Incomplete source pirate course.");
            // Nonzero tiles absent from the route all follow the same search.
            // Retain every room, so repeated room rows keep their source order.
            foreach (int tile in tiles.Order())
            for (int room = 0; room < 256; room++)
            {
                var work = new OriginalRoomLoadingWork(rom);
                work._memory[0xcc01] = (byte)linked;
                work._memory[0xcde1] = (byte)tile;
                work._memory[0xc6ec] = (byte)room;
                Add("angle", room, linked * 256 + tile, work.Run(0x7eaa), "updatePirateShipAngle");
            }
        }
        for (int gate = 0; gate < 4; gate++)
        {
            var work = new OriginalRoomLoadingWork(rom);
            work._memory[0xcba0] = (byte)(gate == 0 ? 1 : 0);
            work._memory[0xcc8d] = (byte)(gate == 1 ? 1 : 0);
            work._memory[0xcc00] = (byte)(gate == 2 ? 1 : 0);
            Add("position", 0, gate, work.Run(0x7e40), "updatePirateShipPosition");
        }
        for (int direction = 0; direction < 4; direction++)
        for (int crossed = 0; crossed < 2; crossed++)
        {
            var work = new OriginalRoomLoadingWork(rom);
            work._memory[0xc6ef] = (byte)direction;
            work._memory[direction % 2 == 0 ? 0xc6ed : 0xc6ee] =
                (byte)(crossed == 0 ? 0 : direction switch { 0 or 3 => 0xf8, 1 => 0x98, _ => 0x88 });
            Add("room", direction, crossed, work.Run(0x7e6b), "updatePirateShipRoom");
        }
        return rows.ToString();
    }

    internal static string CompileGameplayDispatch(byte[] rom)
    {
        Guard(rom, 0x345b, [0xf0, 0x97, 0xf5, 0x3e, 0x05, 0xe0, 0x97], "updateAllObjects");
        Guard(rom, 0x0d9a, [0x21, 0xb6, 0xc4, 0xcb, 0x46, 0xc0, 0x36, 0xff], "drawAllSprites");
        Guard(rom, 0x0e2f, [0x21, 0xc0, 0xc4, 0xf0, 0xa0], "drawAllSprites terrain-effects boundary");
        var rows = new StringBuilder("# operation\tvariant\tcpu-cycles\tsource\n");
        Guard(rom, 0x0933, [0xcd, 0x6d, 0x02, 0xf0, 0xb9, 0x87, 0x28], "_mainLoop");
        Guard(rom, 0x0952, [0x3d, 0x28], "_mainLoop second thread-state test");
        Guard(rom, 0x097e, [0x21, 0x9d, 0xc4, 0x36, 0xff, 0x76], "_mainLoop VBlank wait");
        var frame = new OriginalRoomLoadingWork(rom);
        int threadTests = 0;
        long frameClocks = frame.RunUntil(0x0933, 0x0983, [0x4016],
            pc => { if (pc == 0x0952) threadTests++; });
        if (threadTests != 4) throw new InvalidDataException("_mainLoop must visit four thread slots.");
        // Every frame polls input, walks four thread slots, and copies the
        // six display registers before HALT. Remove the second state test
        // (not reached by state1) and intro gate's taken-JR extra. Thread
        // continuations, reset handling, and palette refresh are separate.
        frameClocks -= threadTests * 12 + 4;
        Guard(rom, 0x098b, [0x2c, 0x35, 0x20, 0xc6], "_countdownToRunThread");
        Guard(rom, 0x33a7, [0x21, 0x22, 0xc6, 0x34, 0x2a, 0xea, 0x00, 0xcc], "mainThread playtime counter");
        Guard(rom, 0x33cd, [0x18, 0xd8], "mainThread resumed continuation");
        Guard(rom, 0x08fe, [0x3e, 0x01, 0xe5, 0xd5, 0xc5], "resumeThreadNextFrame");
        var resume = new OriginalRoomLoadingWork(rom);
        resume._memory[0xc2e9] = 1;
        resume._memory[0xc2ea] = 0xea;
        resume._memory[0xc2eb] = 0xc1;
        resume._cpu.SetHl(0xc2e8);
        // A completed gameplay update resumes/yields the main thread once.
        // Its taken first state-test JR adds four clocks beyond the common
        // fetch above. Other threads' continuation work stays separate.
        frameClocks += resume.Run(0x098b) + 4;
        var main = new OriginalRoomLoadingWork(rom);
        main._memory[0xff9e] = 0xe8;
        // Include the playtime prefix and main-thread CALLs, excluding game
        // logic, sprites, and HUD bodies. Counter-carry tails stay separate;
        // remove the low-byte nonzero branch's four extra JR clocks.
        frameClocks += main.RunUntil(0x33cd, 0x0955, [0x596a, 0x0d9a, 0x1a71]) - 4;
        rows.Append($"frame\t0\t{frameClocks}\tcode/bank0.s:_mainLoop,mainThreadStart\n");
        var dispatch = new OriginalRoomLoadingWork(rom);
        dispatch._memory[0xcc2c] = 0xd0;
        // All called bodies are independent work. The optional mounted/grab
        // CALLs retain their common instruction-fetch cost; their taken-call
        // overhead belongs with the omitted state-dependent bodies.
        long clocks = dispatch.Run(0x345b, 0x4000, 0x4872, 0x3616, 0x2ea5,
            0x5e58, 0x3b36, 0x410d, 0x54df, 0x2b25, 0x491a, 0x494d, 0x12ae, 0x6c32, 0x5906);
        rows.Append($"objects\t0\t{clocks}\tcode/bank0.s:updateAllObjects\n");
        Guard(rom, 0x14000, [0x21, 0x57, 0xcc], "updateSpecialObjects");
        Guard(rom, 0x1407d, [0x7e, 0xb7, 0xc8], "updateSpecialObject enabled gate");
        var enabled = new OriginalRoomLoadingWork(rom) { _bank = 5 };
        enabled._wram[0x1000] = 1;
        enabled._cpu.SetHl(0xd000);
        long enabledPrefix = enabled.RunUntil(0x407d, 0x4080, []);
        for (int suit = 0; suit < 2; suit++)
        {
            var special = new OriginalRoomLoadingWork(rom) { _bank = 5 };
            special._memory[0xff97] = 5;
            special._memory[0xc6a3] = (byte)(suit * 4); // TREASURE_MERMAID_SUIT $4a.
            // Retain the full treasure lookup, including its obtained path.
            // Four conditional JR gates contribute their common fetch;
            // branch extras and optional writes remain separate work. An
            // owned suit executes SET 6,(HL) instead of a taken JR.
            clocks = special.Run(0x4000, 0x40b3, 0x407d, 0x4279) - 12 - (suit == 0 ? 4 : 16);
            clocks += 2 * enabledPrefix;
            rows.Append($"special\t{suit}\t{clocks}\tcode/specialObjects.s:updateSpecialObjects\n");
        }
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
            // enqueue/draw handlers, shadows and individual OAM clearing.
            // The conditional draw CALL contributes its shared fetch clocks;
            // taken-call overhead and the rendered object's work remain separate.
            int queued = 0;
            clocks = drawing.RunUntil(0x0d9a, -1, [0x10a8], pc => {
                if (pc == 0x10a8) queued++;
                if (pc == 0x0e2f)
                {
                    // Cover the shared suffix without assuming how many
                    // shadows/sprites the omitted handlers emitted. These
                    // two taken JR gates each add four non-shared clocks.
                    drawing._memory[0xffa0] = 0;
                    drawing._memory[0xff9f] = 0xa0;
                }
            }) - 8;
            var enqueue = new OriginalRoomLoadingWork(rom);
            enqueue._wram[0x1000] = 1;
            enqueue._cpu.SetDe(0xd000);
            Guard(rom, 0x10a8, [0x1a, 0xb7, 0xc8, 0x7b, 0xf6, 0x1a], "objectQueueDraw initial branches");
            // Empty objects return in 32 clocks. Enabled objects take the
            // shorter conditional RET, then LD A,E / OR visible reaches the
            // same 32-clock boundary. Only enabled objects have more work.
            long enqueuePrefix = enqueue.RunUntil(0x10a8, 0x10ae, []);
            var empty = new OriginalRoomLoadingWork(rom);
            empty._cpu.SetDe(0xd000);
            if (empty.Run(0x10a8) != enqueuePrefix)
                throw new InvalidDataException("objectQueueDraw's empty/enabled prefixes no longer share their source cost.");
            clocks += queued * enqueuePrefix;
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
        // pollInput has no input-dependent branches. Preserve its P1 bus
        // instructions with both unpressed button rows for timing import.
        if (address == 0xff00) return (_memory[address] & 0x30) | 0xcf;
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
        else if (address is not (>= 0xc000 and < 0xd000 or >= 0xff80 or 0xff4f or 0xff00))
            throw new InvalidDataException($"Room loader wrote unsupported memory ${address:x4}.");
        _memory[address] = (byte)value;
    }
}
