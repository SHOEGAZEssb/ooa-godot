using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported data for the pre-GLOBALFLAG_INTRO_DONE event in present room $39.
/// The original room record contains an unpositioned $6b:$01 spawner, so these
/// actors do not belong in the ordinary positioned-NPC database.
/// </summary>
public sealed class NayruIntroEventDatabase
{
    private readonly Dictionary<int, TextRecord> _texts = new();
    private readonly Dictionary<string, ActorRecord> _actors = new();
    private readonly Dictionary<string, EffectRecord> _effects = new();
    private readonly Dictionary<string, FleeRecord> _flee = new();
    private readonly Dictionary<int, VignetteRecord> _vignettes = new();
    private readonly List<VignetteMonkeyRecord> _vignetteMonkeys = new();

    public NayruIntroEventDatabaseEventRecord Event { get; }
    internal IReadOnlyList<CutsceneCommand> Commands { get; }
    public IReadOnlyList<SingingOamRecord> SingingOam { get; }
    public Color[,] SingingBackgroundPalettes { get; }
    public Color[,] SingingSpritePalettes { get; }
    public Color[,] DarkBackgroundPalettes { get; }
    public Color[] PossessedSpritePalette { get; }
    public Color[] StoneSpritePalette { get; }
    public Color[] BoySpritePalette { get; }
    public IReadOnlyList<VignetteMonkeyRecord> VignetteMonkeys => _vignetteMonkeys;

    public NayruIntroEventDatabase()
    {
        GeneratedTableRow eventRow = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/nayru_intro_event.tsv",
            new GeneratedTableSchema(
                "initial Nayru event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "intro-flag", "completion-room-flag", "bear-room-flag",
                    "trigger-x", "trigger-y", "bear-delay", "post-bear-text", "singing-frames",
                    "skip-window", "sprite-scroll-period", "sprite-scroll-steps",
                    "possession-fade-hold", "portal-position", "portal-tile", "vignette-count",
                    "npc-jump-speed-z", "npc-jump-gravity", "dark-fade-frames",
                    "white-fade-out-frames", "white-fade-in-frames", "nayru-ascent-speed-z",
                    "nayru-transfer-z", "nayru-landing-delay", "nayru-fall-speed-z",
                    "nayru-fall-gravity"
                ],
                headerRequired: true)).SingleRow();
        Event = new NayruIntroEventDatabaseEventRecord(
            eventRow.Decimal(0, 0, 7),
            eventRow.HexByte(1),
            eventRow.HexByte(2),
            eventRow.HexByte(3),
            eventRow.HexByte(4),
            eventRow.Decimal(5),
            eventRow.Decimal(6),
            eventRow.UnsignedDecimal(7),
            eventRow.UnsignedDecimal(8),
            eventRow.UnsignedDecimal(9),
            eventRow.UnsignedDecimal(10),
            eventRow.UnsignedDecimal(11),
            eventRow.UnsignedDecimal(12),
            eventRow.UnsignedDecimal(13),
            eventRow.HexByte(14),
            eventRow.HexByte(15),
            eventRow.UnsignedDecimal(16),
            eventRow.Decimal(17),
            eventRow.Decimal(18),
            eventRow.UnsignedDecimal(19),
            eventRow.UnsignedDecimal(20),
            eventRow.UnsignedDecimal(21),
            eventRow.Decimal(22),
            eventRow.Decimal(23),
            eventRow.UnsignedDecimal(24),
            eventRow.Decimal(25),
            eventRow.Decimal(26));
        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/nayru_intro_commands.tsv");
        if (Commands.Count != 235 || Commands[^1] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                $"Initial Nayru command stream should contain 235 records ending in scriptend, " +
                $"got {Commands.Count}.");
        }

        GeneratedTable actorTable = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/nayru_intro_actors.tsv",
            new GeneratedTableSchema(
                "initial Nayru actors",
                GeneratedTableKeySemantics.Unique,
                [
                    "index", "id", "subid", "y", "x", "var03", "name", "sprite",
                    "tile-base", "palette", "default-animation", "animation-0", "animation-1",
                    "animation-2", "animation-3", "animation-4", "animation-5", "animation-6",
                    "animation-7", "animation-8", "animation-9", "animation-10",
                    "initial-animation", "extra-sprite"
                ],
                ["name"],
                headerRequired: true));
        foreach (GeneratedTableRow row in actorTable.Rows)
        {
            string[] animations = new string[11];
            for (int index = 0; index < animations.Length; index++)
                animations[index] = row.String(11 + index);
            ActorRecord actor = new ActorRecord(
                row.UnsignedDecimal(0),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                row.HexByte(4),
                row.HexByte(5),
                row.RequiredString(6),
                row.RequiredString(7),
                row.UnsignedDecimal(8),
                row.UnsignedDecimal(9),
                row.UnsignedDecimal(10),
                animations,
                row.UnsignedDecimal(22),
                row.String(23));
            _actors.Add(actor.Name, actor);
        }
        if (_actors.Count != 18)
            throw new InvalidOperationException(
                $"Expected 18 initial Nayru cutscene actor templates, got {_actors.Count}.");

        GeneratedTable vignetteTable = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/nayru_intro_vignettes.tsv",
            new GeneratedTableSchema(
                "initial Nayru vignettes",
                GeneratedTableKeySemantics.Unique,
                ["index", "group", "room", "duration"],
                ["index"],
                headerRequired: true));
        foreach (GeneratedTableRow row in vignetteTable.Rows)
        {
            VignetteRecord vignette = new VignetteRecord(
                row.UnsignedDecimal(0),
                row.Decimal(1, 0, 7),
                row.HexByte(2),
                row.UnsignedDecimal(3));
            _vignettes.Add(vignette.Index, vignette);
        }
        if (_vignettes.Count != 3)
            throw new InvalidOperationException(
                $"Expected three initial Nayru vignette records, got {_vignettes.Count}.");

        GeneratedTable monkeyTable = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/nayru_intro_vignette_monkeys.tsv",
            new GeneratedTableSchema(
                "initial Nayru vignette monkeys",
                GeneratedTableKeySemantics.Unique,
                ["index", "y", "x", "stone-counter", "animation"],
                ["index"],
                headerRequired: true));
        foreach (GeneratedTableRow row in monkeyTable.Rows)
        {
            _vignetteMonkeys.Add(new VignetteMonkeyRecord(
                row.UnsignedDecimal(0),
                row.HexByte(1),
                row.HexByte(2),
                row.UnsignedDecimal(3),
                row.UnsignedDecimal(4)));
        }
        if (_vignetteMonkeys.Count != 10)
            throw new InvalidOperationException(
                $"Expected ten initial Nayru vignette monkeys, got {_vignetteMonkeys.Count}.");

        GeneratedTable fleeTable = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/nayru_intro_flee.tsv",
            new GeneratedTableSchema(
                "initial Nayru audience escape",
                GeneratedTableKeySemantics.Unique,
                [
                    "actor", "delay", "angle", "speed", "wait-jump-speed-z", "wait-gravity",
                    "repeat-wait-jump", "escape-jump-speed-z", "escape-gravity",
                    "repeat-escape-jump", "wait-for-landing", "wait-animation", "escape-animation"
                ],
                ["actor"],
                headerRequired: true));
        foreach (GeneratedTableRow row in fleeTable.Rows)
        {
            FleeRecord record = new FleeRecord(
                row.RequiredString(0), row.UnsignedDecimal(1), row.Decimal(2),
                row.FiniteFloat(3), row.Decimal(4), row.Decimal(5), row.Boolean01(6),
                row.Decimal(7), row.Decimal(8), row.Boolean01(9),
                row.Boolean01(10), row.UnsignedDecimal(11), row.UnsignedDecimal(12));
            _flee.Add(record.Actor, record);
        }
        if (_flee.Count != 5)
            throw new InvalidOperationException(
                $"Expected five initial Nayru audience escape records, got {_flee.Count}.");

        GeneratedTable effectTable = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/nayru_intro_effects.tsv",
            new GeneratedTableSchema(
                "initial Nayru effects",
                GeneratedTableKeySemantics.Unique,
                [
                    "name", "sprite", "tile-base", "palette", "duration", "speed", "angle",
                    "sway", "velocity-x-fixed", "velocity-y-fixed", "animation"
                ],
                ["name"],
                headerRequired: true));
        foreach (GeneratedTableRow row in effectTable.Rows)
        {
            EffectRecord effect = new EffectRecord(
                row.RequiredString(0),
                row.RequiredString(1),
                row.UnsignedDecimal(2),
                row.UnsignedDecimal(3),
                row.UnsignedDecimal(4),
                row.FiniteFloat(5),
                row.Decimal(6),
                row.Boolean01(7),
                row.Decimal(8),
                row.Decimal(9),
                row.RequiredString(10));
            _effects.Add(effect.Name, effect);
        }
        if (_effects.Count != 2)
            throw new InvalidOperationException(
                $"Expected 2 initial Nayru cutscene effect templates, got {_effects.Count}.");

        GeneratedTable textTable = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/nayru_intro_text.tsv",
            new GeneratedTableSchema(
                "initial Nayru text",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "textbox-position", "utf8-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in textTable.Rows)
        {
            int id = row.HexWord(0);
            _texts.Add(id, new TextRecord(
                id,
                row.Decimal(1),
                row.Base64Utf8(2)));
        }
        if (_texts.Count != 30)
            throw new InvalidOperationException(
                $"Expected 30 initial Nayru cutscene texts, got {_texts.Count}.");

        var oam = new List<SingingOamRecord>();
        GeneratedTable oamTable = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/nayru_singing_oam.tsv",
            new GeneratedTableSchema(
                "Nayru singing OAM",
                GeneratedTableKeySemantics.Repeated,
                ["y", "x", "tile", "flags"],
                headerRequired: true));
        foreach (GeneratedTableRow row in oamTable.Rows)
        {
            oam.Add(new SingingOamRecord(
                row.HexByte(0),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3)));
        }
        if (oam.Count != 39)
            throw new InvalidOperationException(
                $"Expected 39 Nayru singing sprites, got {oam.Count}.");
        SingingOam = oam;
        SingingBackgroundPalettes = ReadPalettes(
            "res://assets/oracle/cutscenes/nayru_singing_bg_palette.bin", 8, false);
        SingingSpritePalettes = ReadPalettes(
            "res://assets/oracle/cutscenes/nayru_singing_sprite_palette.bin", 7, true);
        DarkBackgroundPalettes = ReadPalettes(
            "res://assets/oracle/cutscenes/nayru_intro_dark_bg_palette.bin", 6, false);
        Color[,] possessed = ReadPalettes(
            "res://assets/oracle/cutscenes/nayru_possessed_sprite_palette.bin", 1, true);
        PossessedSpritePalette = new Color[4];
        for (int color = 0; color < PossessedSpritePalette.Length; color++)
            PossessedSpritePalette[color] = possessed[0, color];
        Color[,] stone = ReadPalettes(
            "res://assets/oracle/cutscenes/nayru_stone_sprite_palette.bin", 1, true);
        StoneSpritePalette = CopyPalette(stone, 0);
        Color[,] standard = ReadPalettes(
            "res://assets/oracle/inventory/palette_sprites.bin", 6, true);
        BoySpritePalette = CopyPalette(standard, 2);
    }

    public ActorRecord Actor(string name) => _actors.TryGetValue(name, out ActorRecord actor)
        ? actor
        : throw new InvalidOperationException($"Unknown initial Nayru cutscene actor '{name}'.");

    internal bool HasActor(string name) => _actors.ContainsKey(name);

    public TextRecord Text(int id) => _texts.TryGetValue(id, out TextRecord text)
        ? text
        : throw new InvalidOperationException(
            $"Initial Nayru cutscene text TX_{id:x4} was not imported.");

    public EffectRecord Effect(string name) =>
        _effects.TryGetValue(name, out EffectRecord effect)
            ? effect
            : throw new InvalidOperationException(
                $"Unknown initial Nayru cutscene effect '{name}'.");

    public FleeRecord Flee(string actor) => _flee.TryGetValue(actor, out FleeRecord record)
        ? record
        : throw new InvalidOperationException(
                $"Unknown initial Nayru audience escape actor '{actor}'.");

    public VignetteRecord Vignette(int index) =>
        _vignettes.TryGetValue(index, out VignetteRecord record)
            ? record
            : throw new InvalidOperationException(
                $"Unknown initial Nayru vignette index {index}.");

    private static Color[,] ReadPalettes(string path, int count, bool transparentZero)
    {
        byte[] bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length != count * 4 * 3)
            throw new InvalidOperationException(
                $"{path} should contain {count * 12} palette bytes, got {bytes.Length}.");
        var palettes = new Color[count, 4];
        for (int palette = 0; palette < count; palette++)
        for (int color = 0; color < 4; color++)
        {
            int offset = (palette * 4 + color) * 3;
            palettes[palette, color] = new Color(
                bytes[offset] / 31.0f,
                bytes[offset + 1] / 31.0f,
                bytes[offset + 2] / 31.0f,
                transparentZero && color == 0 ? 0.0f : 1.0f);
        }
        return palettes;
    }

    private static Color[] CopyPalette(Color[,] palettes, int index)
    {
        var palette = new Color[4];
        for (int color = 0; color < palette.Length; color++)
            palette[color] = palettes[index, color];
        return palette;
    }
}

public readonly record struct VignetteRecord(int Index, int Group, int Room, int Duration);

public readonly record struct VignetteMonkeyRecord(int Index, int Y, int X, int StoneCounter, int Animation);

public readonly record struct TextRecord(int Id, int TextboxPosition, string Message);

public readonly record struct SingingOamRecord(int Y, int X, int Tile, int Flags);

public readonly record struct NayruIntroEventDatabaseEventRecord(int Group, int Room, int IntroFlag, int CompletionRoomFlag, int BearRoomFlag, int TriggerX, int TriggerY, int BearDelayFrames, int PostBearTextFrames, int SingingFrames, int SingingSkipWindow, int SingingScrollPeriod, int SingingScrollSteps, int PossessionFadeHoldFrames, int PortalPosition, int PortalTile, int VignetteCount, int NpcJumpSpeedZ, int NpcJumpGravity, int DarkFadeFrames, int WhiteFadeOutFrames, int WhiteFadeInFrames, int NayruAscentSpeedZ, int NayruTransferZ, int NayruLandingDelay, int NayruFallSpeedZ, int NayruFallGravity);

public readonly record struct FleeRecord(string Actor, int Delay, int Angle, float Speed, int WaitJumpSpeedZ, int WaitGravity, bool RepeatWaitJump, int EscapeJumpSpeedZ, int EscapeGravity, bool RepeatEscapeJump, bool WaitForLanding, int WaitAnimation, int EscapeAnimation);

public readonly record struct EffectRecord(string Name, string SpriteName, int TileBase, int Palette, int Duration, float Speed, int Angle, bool Sway, int VelocityXFixed, int VelocityYFixed, string Animation)
{
    public NpcRecord ToNpcRecord(int group, int room, int y, int x) => new(group, room, 0, 0, y, x, 0, 0, SpriteName, TileBase, Palette, 0, false, Animation, Animation, Animation, Animation, string.Empty);
}

public readonly record struct ActorRecord(int Index, int Id, int SubId, int Y, int X, int Var03, string Name, string SpriteName, int TileBase, int Palette, int DefaultAnimation, string[] Animations, int InitialAnimation, string ExtraSprite)
{
    public string Animation(int index)
    {
        if (index < 0 || index >= Animations.Length || string.IsNullOrEmpty(Animations[index]))
            throw new InvalidOperationException($"Initial Nayru actor {Name} ${Id:x2}:${SubId:x2} has no animation ${index:x2}.");
        return Animations[index];
    }

    public NpcRecord ToNpcRecord(int group, int room)
    {
        string animation = Animation(DefaultAnimation);
        return new NpcRecord(group, room, Id, SubId, Y, X, Var03, 0, SpriteName, TileBase, Palette, DefaultAnimation, false, animation, animation, animation, animation, string.Empty);
    }
}
