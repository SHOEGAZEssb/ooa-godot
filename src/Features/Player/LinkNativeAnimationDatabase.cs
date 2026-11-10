using System.Collections.Concurrent;

namespace Oracles;

// code/specialObjectAnimationsAndDamage.s: the cursor points past the current triple.
internal sealed class LinkNativeAnimationDatabase
{
    private static readonly ConcurrentDictionary<string, Lazy<LinkNativeAnimationDatabase>> Packs = new(StringComparer.Ordinal);
    internal static LinkNativeAnimationDatabase For(string root) => Packs.GetOrAdd(root,
        static pack => new Lazy<LinkNativeAnimationDatabase>(() => new(pack))).Value;

    private readonly OracleGame _game;
    private readonly byte[] _bank;
    private readonly int[] _pointers = new int[0x38];
    private readonly string[] _sources = new string[0x38];
    private readonly (int SourceOffset, string Oam, bool SelfContained)[] _graphics;

    private LinkNativeAnimationDatabase(string root)
    {
        _game = OracleAssetPack.GameFor(root);
        _bank = OracleAssetCache.ReadBytes(root + "metadata/link_native_animation.bin");
        if (_bank.Length != 0x4002 || _bank[0] != 0 || _bank[1] != 0x40)
            throw new InvalidOperationException($"{root}metadata/link_native_animation.bin: expected the native $4000 animation bank.");
        var table = GeneratedTable.Load(root + "metadata/link_native_animation.tsv",
            new("native Link animations", GeneratedTableKeySemantics.Unique,
                ["mode", "pointer", "source"], ["mode"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            int mode = row.HexByte(0), pointer = row.HexWord(1);
            if (mode >= _pointers.Length || pointer is < 0x4000 or > 0x7ffd)
                throw row.Invalid(0, "Link mode $00..$37 and a native frame pointer");
            _pointers[mode] = pointer;
            _sources[mode] = row.RequiredString(2);
        }
        for (int mode = 0; mode < _pointers.Length; mode++)
            if (_pointers[mode] == 0)
                throw new InvalidOperationException($"{root}metadata/link_native_animation.tsv: missing Link mode ${mode:x2}.");
        var graphics = GeneratedTable.Load(root + "metadata/link_native_graphics.tsv",
            new("native Link graphics", GeneratedTableKeySemantics.Unique,
                ["index", "source-offset", "upload-tiles", "oam", "source"], ["index"], headerRequired: true));
        _graphics = new (int, string, bool)[_game == OracleGame.Ages ? 256 : 248];
        if (graphics.Rows.Count != _graphics.Length)
            throw new InvalidOperationException($"{root}metadata/link_native_graphics.tsv: incomplete native graphics table.");
        for (int index = 0; index < _graphics.Length; index++)
        {
            var row = graphics.Rows[index];
            if (row.HexByte(0) != index) throw row.Invalid(0, "native graphics order");
            int tiles = row.Decimal(2, 2, 30);
            string oam = row.RequiredString(3);
            bool selfContained = true;
            foreach (string part in oam.Split(';'))
            {
                string[] fields = part.Split(',');
                if (fields.Length != 4 || fields.Any(field => !byte.TryParse(field, out _)))
                    throw row.Invalid(3, "native Y,X,tile,flags bytes");
                selfContained &= (int.Parse(fields[2]) & 0xfe) + 1 < tiles;
            }
            _graphics[index] = (row.HexWord(1), oam, selfContained);
            _ = row.RequiredString(4);
        }
    }

    internal (int SourceOffset, string Oam) Graphic(int graphic, int direction)
    {
        if ((uint)direction >= 4) throw new ArgumentOutOfRangeException(nameof(direction));
        // loadLinkAndCompanionAnimationFrame_body adds direction only at $54+.
        int index = graphic < 0x54 ? graphic : graphic + direction;
        string source = $"data/{_game.ToString().ToLowerInvariant()}/specialObjectAnimationData.s: native Link graphic ${index:x2}";
        if ((uint)index >= _graphics.Length) throw new NotSupportedException($"{source} has no graphics pointer.");
        var frame = _graphics[index];
        // shortcut: retained-tile poses need live OBJ uploads when a consumer uses them.
        if (!frame.SelfContained) throw new NotSupportedException($"{source} requires retained OBJ tiles outside its upload.");
        return (frame.SourceOffset, frame.Oam);
    }

    internal void Set(OracleRuntimeState memory, int mode)
    {
        RequireGame(memory);
        if ((uint)mode >= _pointers.Length)
            throw new NotSupportedException($"specialObjectSetAnimation: Link mode ${mode:x2} is outside the native $00..$37 table.");
        var frame = ReadFrame(_pointers[mode], _sources[mode]);
        memory.SetWramByte(0xd030, (byte)mode);
        Publish(memory, frame);
    }

    internal void Animate(OracleRuntimeState memory)
    {
        RequireGame(memory);
        byte counter = unchecked((byte)(memory.ReadWramByte(0xd020) - 1));
        if (counter != 0) { memory.SetWramByte(0xd020, counter); return; }
        int cursor = memory.ReadWramByte(0xd022) | memory.ReadWramByte(0xd023) << 8;
        Publish(memory, ReadFrame(cursor, "specialObjectAnimate: w1Link.animPointer"));
    }

    internal int FrameIndex(OracleRuntimeState memory, int mode, int frameCount)
    {
        // JUMP/CAPE atlases follow contiguous native triples; the live cursor owns their phase.
        RequireGame(memory);
        if ((uint)mode >= _pointers.Length || frameCount < 1)
            throw new ArgumentOutOfRangeException(nameof(mode));
        int cursor = memory.ReadWramByte(0xd022) | memory.ReadWramByte(0xd023) << 8;
        int offset = cursor - _pointers[mode] - 3;
        if (offset < 0 || offset % 3 != 0 || offset / 3 >= frameCount)
            throw new NotSupportedException($"{_sources[mode]}: native cursor ${cursor:x4} has no represented frame in mode ${mode:x2}.");
        return offset / 3;
    }

    private (byte Counter, byte Graphics, byte Parameter, int Cursor) ReadFrame(int pointer, string source)
    {
        byte Read(int address)
        {
            if (address is < 0x4000 or > 0x7fff)
                throw new NotSupportedException($"{source}: native Link animation read ${address:x4} leaves its ROM bank.");
            return _bank[address - 0x4000 + 2];
        }
        if (Read(pointer) == 0xff)
            pointer += 1 + Read(pointer + 1) - 0x100;
        return (Read(pointer), Read(pointer + 1), Read(pointer + 2), pointer + 3);
    }

    private static void Publish(OracleRuntimeState memory, (byte Counter, byte Graphics, byte Parameter, int Cursor) frame)
    {
        memory.SetWramByte(0xd020, frame.Counter);
        memory.SetWramByte(0xd021, frame.Parameter);
        memory.SetWramByte(0xd022, (byte)frame.Cursor);
        memory.SetWramByte(0xd023, (byte)(frame.Cursor >> 8));
        memory.SetWramByte(0xd031, frame.Graphics);
    }

    private void RequireGame(OracleRuntimeState memory)
    {
        if (memory.Game != _game)
            throw new ArgumentException($"Native Link animation pack {_game} cannot animate {memory.Game}.", nameof(memory));
    }
}
