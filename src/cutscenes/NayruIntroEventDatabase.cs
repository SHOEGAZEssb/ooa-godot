using Godot;
using System;
using System.Collections.Generic;
using System.Text;

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

    public EventRecord Event { get; }
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
        string[] eventColumns = ReadSingleRow(
            "res://assets/oracle/cutscenes/nayru_intro_event.tsv").Split('\t');
        if (eventColumns.Length != 27)
            throw new InvalidOperationException(
                $"Initial Nayru event row should contain 27 columns, got {eventColumns.Length}.");
        Event = new EventRecord(
            int.Parse(eventColumns[0]),
            Convert.ToInt32(eventColumns[1], 16),
            Convert.ToInt32(eventColumns[2], 16),
            Convert.ToInt32(eventColumns[3], 16),
            Convert.ToInt32(eventColumns[4], 16),
            int.Parse(eventColumns[5]),
            int.Parse(eventColumns[6]),
            int.Parse(eventColumns[7]),
            int.Parse(eventColumns[8]),
            int.Parse(eventColumns[9]),
            int.Parse(eventColumns[10]),
            int.Parse(eventColumns[11]),
            int.Parse(eventColumns[12]),
            int.Parse(eventColumns[13]),
            Convert.ToInt32(eventColumns[14], 16),
            Convert.ToInt32(eventColumns[15], 16),
            int.Parse(eventColumns[16]),
            int.Parse(eventColumns[17]),
            int.Parse(eventColumns[18]),
            int.Parse(eventColumns[19]),
            int.Parse(eventColumns[20]),
            int.Parse(eventColumns[21]),
            int.Parse(eventColumns[22]),
            int.Parse(eventColumns[23]),
            int.Parse(eventColumns[24]),
            int.Parse(eventColumns[25]),
            int.Parse(eventColumns[26]));
        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/nayru_intro_commands.tsv");
        if (Commands.Count != 235 || Commands[^1] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                $"Initial Nayru command stream should contain 235 records ending in scriptend, " +
                $"got {Commands.Count}.");
        }

        foreach (string line in ReadRows(
            "res://assets/oracle/cutscenes/nayru_intro_actors.tsv"))
        {
            string[] columns = line.Split('\t');
            if (columns.Length != 24)
                throw new InvalidOperationException(
                    $"Initial Nayru actor row should contain 24 columns, got {columns.Length}.");
            string[] animations = new string[11];
            Array.Copy(columns, 11, animations, 0, animations.Length);
            var actor = new ActorRecord(
                int.Parse(columns[0]),
                Convert.ToInt32(columns[1], 16),
                Convert.ToInt32(columns[2], 16),
                Convert.ToInt32(columns[3], 16),
                Convert.ToInt32(columns[4], 16),
                Convert.ToInt32(columns[5], 16),
                columns[6],
                columns[7],
                int.Parse(columns[8]),
                int.Parse(columns[9]),
                int.Parse(columns[10]),
                animations,
                int.Parse(columns[22]),
                columns[23]);
            _actors.Add(actor.Name, actor);
        }
        if (_actors.Count != 18)
            throw new InvalidOperationException(
                $"Expected 18 initial Nayru cutscene actor templates, got {_actors.Count}.");

        foreach (string line in ReadRows(
            "res://assets/oracle/cutscenes/nayru_intro_vignettes.tsv"))
        {
            string[] columns = line.Split('\t');
            if (columns.Length != 4)
                throw new InvalidOperationException($"Malformed initial Nayru vignette row: {line}");
            var vignette = new VignetteRecord(
                int.Parse(columns[0]),
                int.Parse(columns[1]),
                Convert.ToInt32(columns[2], 16),
                int.Parse(columns[3]));
            _vignettes.Add(vignette.Index, vignette);
        }
        if (_vignettes.Count != 3)
            throw new InvalidOperationException(
                $"Expected three initial Nayru vignette records, got {_vignettes.Count}.");

        foreach (string line in ReadRows(
            "res://assets/oracle/cutscenes/nayru_intro_vignette_monkeys.tsv"))
        {
            string[] columns = line.Split('\t');
            if (columns.Length != 5)
                throw new InvalidOperationException(
                    $"Malformed initial Nayru vignette monkey row: {line}");
            _vignetteMonkeys.Add(new VignetteMonkeyRecord(
                int.Parse(columns[0]),
                Convert.ToInt32(columns[1], 16),
                Convert.ToInt32(columns[2], 16),
                int.Parse(columns[3]),
                int.Parse(columns[4])));
        }
        if (_vignetteMonkeys.Count != 10)
            throw new InvalidOperationException(
                $"Expected ten initial Nayru vignette monkeys, got {_vignetteMonkeys.Count}.");

        foreach (string line in ReadRows(
            "res://assets/oracle/cutscenes/nayru_intro_flee.tsv"))
        {
            string[] columns = line.Split('\t');
            if (columns.Length != 13)
                throw new InvalidOperationException(
                    $"Initial Nayru flee row should contain 13 columns, got {columns.Length}.");
            var record = new FleeRecord(
                columns[0], int.Parse(columns[1]), int.Parse(columns[2]),
                float.Parse(columns[3], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(columns[4]), int.Parse(columns[5]), columns[6] == "1",
                int.Parse(columns[7]), int.Parse(columns[8]), columns[9] == "1",
                columns[10] == "1", int.Parse(columns[11]), int.Parse(columns[12]));
            _flee.Add(record.Actor, record);
        }
        if (_flee.Count != 5)
            throw new InvalidOperationException(
                $"Expected five initial Nayru audience escape records, got {_flee.Count}.");

        foreach (string line in ReadRows(
            "res://assets/oracle/cutscenes/nayru_intro_effects.tsv"))
        {
            string[] columns = line.Split('\t');
            if (columns.Length != 11)
                throw new InvalidOperationException(
                    $"Initial Nayru effect row should contain 11 columns, got {columns.Length}.");
            var effect = new EffectRecord(
                columns[0],
                columns[1],
                int.Parse(columns[2]),
                int.Parse(columns[3]),
                int.Parse(columns[4]),
                float.Parse(columns[5], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(columns[6]),
                columns[7] == "1",
                int.Parse(columns[8]),
                int.Parse(columns[9]),
                columns[10]);
            _effects.Add(effect.Name, effect);
        }
        if (_effects.Count != 2)
            throw new InvalidOperationException(
                $"Expected 2 initial Nayru cutscene effect templates, got {_effects.Count}.");

        foreach (string line in ReadRows(
            "res://assets/oracle/cutscenes/nayru_intro_text.tsv"))
        {
            string[] columns = line.Split('\t');
            if (columns.Length != 3)
                throw new InvalidOperationException($"Malformed initial Nayru text row: {line}");
            int id = Convert.ToInt32(columns[0], 16);
            _texts.Add(id, new TextRecord(
                id,
                int.Parse(columns[1]),
                Encoding.UTF8.GetString(Convert.FromBase64String(columns[2]))));
        }
        if (_texts.Count != 30)
            throw new InvalidOperationException(
                $"Expected 30 initial Nayru cutscene texts, got {_texts.Count}.");

        var oam = new List<SingingOamRecord>();
        foreach (string line in ReadRows(
            "res://assets/oracle/cutscenes/nayru_singing_oam.tsv"))
        {
            string[] columns = line.Split('\t');
            if (columns.Length != 4)
                throw new InvalidOperationException($"Malformed Nayru singing OAM row: {line}");
            oam.Add(new SingingOamRecord(
                Convert.ToInt32(columns[0], 16),
                Convert.ToInt32(columns[1], 16),
                Convert.ToInt32(columns[2], 16),
                Convert.ToInt32(columns[3], 16)));
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

    private static IEnumerable<string> ReadRows(string path)
    {
        string source = FileAccess.GetFileAsString(path);
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (!line.StartsWith('#'))
                yield return line;
        }
    }

    private static string ReadSingleRow(string path)
    {
        foreach (string row in ReadRows(path))
            return row;
        throw new InvalidOperationException($"Cutscene data is empty: {path}");
    }

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

    public readonly record struct EventRecord(
        int Group,
        int Room,
        int IntroFlag,
        int CompletionRoomFlag,
        int BearRoomFlag,
        int TriggerX,
        int TriggerY,
        int BearDelayFrames,
        int PostBearTextFrames,
        int SingingFrames,
        int SingingSkipWindow,
        int SingingScrollPeriod,
        int SingingScrollSteps,
        int PossessionFadeHoldFrames,
        int PortalPosition,
        int PortalTile,
        int VignetteCount,
        int NpcJumpSpeedZ,
        int NpcJumpGravity,
        int DarkFadeFrames,
        int WhiteFadeOutFrames,
        int WhiteFadeInFrames,
        int NayruAscentSpeedZ,
        int NayruTransferZ,
        int NayruLandingDelay,
        int NayruFallSpeedZ,
        int NayruFallGravity);

    public readonly record struct FleeRecord(
        string Actor,
        int Delay,
        int Angle,
        float Speed,
        int WaitJumpSpeedZ,
        int WaitGravity,
        bool RepeatWaitJump,
        int EscapeJumpSpeedZ,
        int EscapeGravity,
        bool RepeatEscapeJump,
        bool WaitForLanding,
        int WaitAnimation,
        int EscapeAnimation);

    public readonly record struct VignetteRecord(
        int Index,
        int Group,
        int Room,
        int Duration);

    public readonly record struct VignetteMonkeyRecord(
        int Index,
        int Y,
        int X,
        int StoneCounter,
        int Animation);

    public readonly record struct TextRecord(int Id, int TextboxPosition, string Message);

    public readonly record struct EffectRecord(
        string Name,
        string SpriteName,
        int TileBase,
        int Palette,
        int Duration,
        float Speed,
        int Angle,
        bool Sway,
        int VelocityXFixed,
        int VelocityYFixed,
        string Animation)
    {
        public NpcDatabase.NpcRecord ToNpcRecord(int group, int room, int y, int x) =>
            new(
                group, room, 0, 0, y, x, 0, 0, SpriteName, TileBase, Palette, 0,
                false, Animation, Animation, Animation, Animation, string.Empty);
    }

    public readonly record struct SingingOamRecord(int Y, int X, int Tile, int Flags);

    public readonly record struct ActorRecord(
        int Index,
        int Id,
        int SubId,
        int Y,
        int X,
        int Var03,
        string Name,
        string SpriteName,
        int TileBase,
        int Palette,
        int DefaultAnimation,
        string[] Animations,
        int InitialAnimation,
        string ExtraSprite)
    {
        public string Animation(int index)
        {
            if (index < 0 || index >= Animations.Length || string.IsNullOrEmpty(Animations[index]))
                throw new InvalidOperationException(
                    $"Initial Nayru actor {Name} ${Id:x2}:${SubId:x2} has no animation ${index:x2}.");
            return Animations[index];
        }

        public NpcDatabase.NpcRecord ToNpcRecord(int group, int room)
        {
            string animation = Animation(DefaultAnimation);
            return new NpcDatabase.NpcRecord(
                group, room, Id, SubId, Y, X, Var03, 0, SpriteName, TileBase, Palette,
                DefaultAnimation, false, animation, animation, animation, animation, string.Empty);
        }
    }
}
