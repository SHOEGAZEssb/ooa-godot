using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Importer-owned records for room 3:ae's INTERAC_HARP_OF_AGES_SPAWNER
/// $b3:$00, the $36:$07 Nayru vision, and its script/native song handoff.
/// </summary>
internal sealed class HarpOfAgesEventDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly Dictionary<string, HarpOfAgesVisualRecord> _visuals =
        new(StringComparer.Ordinal);

    internal HarpOfAgesEventRecord Record { get; }
    internal IReadOnlyList<CutsceneCommand> Commands { get; }
    internal EffectRecord MusicNote { get; }
    internal IntroSpriteFrame[] LinkHarpFrames { get; }

    internal HarpOfAgesEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "harp_of_ages_event.tsv",
            new GeneratedTableSchema(
                "room 3:ae Harp of Ages event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "spawner-id", "spawner-subid",
                    "spawner-y", "spawner-x", "harp-y", "harp-x",
                    "room-flag", "harp-treasure", "harp-object",
                    "sparkle-id", "sparkle-subid", "fade-delay",
                    "fade-frames", "black-hold", "nayru-id", "nayru-subid",
                    "nayru-flicker", "nayru-music", "textbox-flags",
                    "song-sound", "song-initial-delay", "song-phase-frames",
                    "song-phases", "song-native-frames", "final-fade-delay",
                    "final-fade-frames", "echoes-treasure", "echoes-object"
                ],
                headerRequired: true)).SingleRow();
        Record = new HarpOfAgesEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.HexByte(6),
            row.HexByte(7),
            row.HexByte(8),
            row.HexByte(9),
            row.RequiredString(10),
            row.HexByte(11),
            row.HexByte(12),
            row.UnsignedDecimal(13),
            row.UnsignedDecimal(14),
            row.UnsignedDecimal(15),
            row.HexByte(16),
            row.HexByte(17),
            row.UnsignedDecimal(18),
            row.HexByte(19),
            row.HexByte(20),
            row.HexByte(21),
            row.UnsignedDecimal(22),
            row.UnsignedDecimal(23),
            row.UnsignedDecimal(24),
            row.UnsignedDecimal(25),
            row.UnsignedDecimal(26),
            row.UnsignedDecimal(27),
            row.HexByte(28),
            row.RequiredString(29));
        LoadVisuals();
        Commands = CutsceneCommandCatalog.Load(
            Root + "harp_of_ages_commands.tsv");
        MusicNote = LoadMusicNote();
        LinkHarpFrames =
            new NewGameIntroDatabase().SpriteFrames("link-harp");
        Validate();
    }

    internal HarpOfAgesVisualRecord Visual(string key) =>
        _visuals.TryGetValue(key, out HarpOfAgesVisualRecord visual)
            ? visual
            : throw new KeyNotFoundException(
                $"Room 3:ae visual '{key}' was not imported.");

    private void LoadVisuals()
    {
        GeneratedTable table = GeneratedTable.Load(
            Root + "harp_of_ages_visuals.tsv",
            new GeneratedTableSchema(
                "room 3:ae Harp of Ages visuals",
                GeneratedTableKeySemantics.Unique,
                [
                    "key", "id", "subid", "sprite", "extra-sprite",
                    "tile-base", "palette", "source-offset", "animation-0",
                    "animation-2", "animation-7"
                ],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            HarpOfAgesVisualRecord visual = new(
                row.RequiredString(0),
                row.HexByte(1),
                row.HexByte(2),
                row.RequiredString(3),
                row.String(4),
                row.UnsignedDecimal(5),
                row.UnsignedDecimal(6),
                row.HexWord(7),
                row.String(8),
                row.String(9),
                row.String(10));
            if (!_visuals.TryAdd(visual.Key, visual))
                throw row.Invalid(0, "a unique room 3:ae visual key");
        }
    }

    private static EffectRecord LoadMusicNote()
    {
        GeneratedTable table = GeneratedTable.Load(
            Root + "nayru_intro_effects.tsv",
            new GeneratedTableSchema(
                "shared Nayru effects",
                GeneratedTableKeySemantics.Unique,
                [
                    "name", "sprite", "tile-base", "palette", "duration",
                    "speed", "angle", "sway", "velocity-x-fixed",
                    "velocity-y-fixed", "animation"
                ],
                ["name"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            if (row.RequiredString(0) != "MusicNote")
                continue;
            return new EffectRecord(
                "MusicNote",
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
        }
        throw new InvalidOperationException(
            "The shared Nayru effect table has no MusicNote record.");
    }

    private void Validate()
    {
        if (Record is not
            {
                Group: 3, Room: 0xae,
                SpawnerId: 0xb3, SpawnerSubId: 0,
                SpawnerY: 0x28, SpawnerX: 0x58,
                HarpY: 0x38, HarpX: 0x58,
                RoomFlag: OracleSaveData.RoomFlagItem,
                HarpTreasure: TreasureDatabase.TreasureHarp,
                HarpObject: "TREASURE_OBJECT_HARP_00",
                SparkleId: 0x84, SparkleSubId: 0x0c,
                FadeDelay: 2, FadeFrames: 65, BlackHold: 40,
                NayruId: 0x36, NayruSubId: 0x07,
                NayruFlicker: 30, NayruMusic: OracleSoundEngine.MusNayru,
                TextboxFlags: 0x04,
                SongSound: OracleSoundEngine.SndTuneOfEchoes,
                SongInitialDelay: 4, SongPhaseFrames: 52, SongPhases: 4,
                SongNativeFrames: 214,
                FinalFadeDelay: 4, FinalFadeFrames: 129,
                EchoesTreasure: TreasureDatabase.TreasureTuneOfEchoes,
                EchoesObject: "TREASURE_OBJECT_TUNE_OF_ECHOES_00"
            } ||
            _visuals.Count != 2 ||
            Visual("Nayru") is not
                {
                    Id: 0x36, SubId: 0x07,
                    Sprite: "spr_nayru_1", ExtraSprite: "spr_nayru_2",
                    TileBase: 0, Palette: 1, SourceOffset: 0
                } ||
            string.IsNullOrEmpty(Visual("Nayru").Animation2) ||
            string.IsNullOrEmpty(Visual("Nayru").Animation7) ||
            Visual("Sparkle") is not
                {
                    Id: 0x84, SubId: 0x0c,
                    Sprite: "spr_link", ExtraSprite: "",
                    TileBase: 0, Palette: 0, SourceOffset: 0x1c00
                } ||
            string.IsNullOrEmpty(Visual("Sparkle").Animation0) ||
            MusicNote is not
                {
                    SpriteName: "spr_common_sprites",
                    TileBase: 0x44, Palette: 1, Duration: 70,
                    Sway: true, VelocityXFixed: 53, VelocityYFixed: -79
                } ||
            LinkHarpFrames.Length != 13 ||
            !LinkHarpGraphicsMatch(LinkHarpFrames) ||
            Commands.Count != 22 ||
            Commands[0] is not CutsceneWaitCommand { Frames: 12 } ||
            Commands[1] is not CutsceneWriteMemoryCommand
                { Binding: "TextboxFlags", Value: 0x04 } ||
            Commands[2] is not CutsceneShowTextCommand { TextId: 0x1d10 } ||
            Commands[4] is not CutsceneSetAnimationCommand
                { Actor: "Nayru", Animation: 0x07 } ||
            Commands[6] is not CutscenePlaySoundCommand
                { Sound: OracleSoundEngine.SndTuneOfEchoes } ||
            Commands[7] is not CutsceneWaitCommand { Frames: 210 } ||
            Commands[8] is not CutsceneNativeCommand
                { Handler: "ToggleNayruAnimation" } ||
            Commands[9] is not CutsceneWaitCommand { Frames: 75 } ||
            Commands[11] is not CutsceneSetAnimationCommand
                { Actor: "Nayru", Animation: 0x02 } ||
            Commands[15] is not CutsceneShowTextCommand { TextId: 0x1d11 } ||
            Commands[16] is not CutsceneNativeBlockingCommand
                { Handler: "PlayHarpSong", Frames: 214 } ||
            Commands[17] is not CutsceneWaitCommand { Frames: 36 } ||
            Commands[19] is not CutsceneGiveItemCommand
                { TreasureId: TreasureDatabase.TreasureTuneOfEchoes,
                    Parameter: 0 } ||
            Commands[20] is not CutsceneWaitCommand { Frames: 16 } ||
            Commands[21] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "Room 3:ae Harp of Ages imported data no longer matches " +
                "the supported interaction/script contract.");
        }
    }

    private static bool LinkHarpGraphicsMatch(
        IReadOnlyList<IntroSpriteFrame> frames)
    {
        int[] durations =
            [20, 20, 12, 20, 20, 12, 20, 20, 12, 20, 20, 12, 20];
        int[][] tiles =
        [
            [0x30, 0x32, 0x34],
            [0x36, 0x38, 0x34],
            [0x30, 0x32, 0x34],
            [0x32, 0x30, 0x34],
            [0x38, 0x36, 0x34],
            [0x32, 0x30, 0x34],
            [0x30, 0x32, 0x34],
            [0x36, 0x38, 0x34],
            [0x30, 0x32, 0x34],
            [0x32, 0x30, 0x34],
            [0x38, 0x36, 0x34],
            [0x32, 0x30, 0x34],
            [0x32, 0x30, 0x34]
        ];
        if (frames.Count != durations.Length)
            return false;
        for (int frame = 0; frame < frames.Count; frame++)
        {
            IntroSpriteFrame actual = frames[frame];
            if (actual.Duration != durations[frame] ||
                actual.SourceOffset != 0 ||
                actual.BasePalette != 0 ||
                actual.Parts.Length != tiles[frame].Length)
            {
                return false;
            }
            for (int part = 0; part < actual.Parts.Length; part++)
            {
                if (actual.Parts[part].Tile != tiles[frame][part])
                    return false;
            }
        }
        return true;
    }
}

internal readonly record struct HarpOfAgesEventRecord(
    int Group,
    int Room,
    int SpawnerId,
    int SpawnerSubId,
    int SpawnerY,
    int SpawnerX,
    int HarpY,
    int HarpX,
    int RoomFlag,
    int HarpTreasure,
    string HarpObject,
    int SparkleId,
    int SparkleSubId,
    int FadeDelay,
    int FadeFrames,
    int BlackHold,
    int NayruId,
    int NayruSubId,
    int NayruFlicker,
    int NayruMusic,
    int TextboxFlags,
    int SongSound,
    int SongInitialDelay,
    int SongPhaseFrames,
    int SongPhases,
    int SongNativeFrames,
    int FinalFadeDelay,
    int FinalFadeFrames,
    int EchoesTreasure,
    string EchoesObject);

internal readonly record struct HarpOfAgesVisualRecord(
    string Key,
    int Id,
    int SubId,
    string Sprite,
    string ExtraSprite,
    int TileBase,
    int Palette,
    int SourceOffset,
    string Animation0,
    string Animation2,
    string Animation7)
{
    internal string Animation(int animation) => animation switch
    {
        0 when !string.IsNullOrEmpty(Animation0) => Animation0,
        2 when !string.IsNullOrEmpty(Animation2) => Animation2,
        7 when !string.IsNullOrEmpty(Animation7) => Animation7,
        _ => throw new InvalidOperationException(
            $"Room 3:ae visual {Key} has no animation ${animation:x2}.")
    };

    internal NpcRecord ToNpcRecord(
        int group,
        int room,
        int y,
        int x,
        int animation)
    {
        string encoded = Animation(animation);
        return new NpcRecord(
            group, room, Id, SubId, y, x, 0, 0,
            Sprite, TileBase, Palette, animation, false,
            encoded, encoded, encoded, encoded, string.Empty,
            NpcImplementationClassification.EventOwned);
    }
}
