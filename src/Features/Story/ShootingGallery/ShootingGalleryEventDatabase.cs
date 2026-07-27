using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_SHOOTING_GALLERY $30:$00 scripts and the native tables
/// consumed by its dynamically created $30:$03/PART_BALL $38 game objects.
/// </summary>
internal sealed class ShootingGalleryEventDatabase
{
    private readonly ShootingGalleryTargetRecord[] _targets;
    private readonly byte[][] _layouts;
    private readonly ShootingGalleryResultRecord[] _results;
    private readonly int[] _rings;
    private readonly ShootingGalleryResultScriptRecord _resultScript;

    internal ShootingGalleryEventRecord Record { get; }
    internal ShootingGalleryDebrisRecord Debris { get; }
    internal IReadOnlyList<CutsceneCommand> MainCommands { get; }
    internal IReadOnlyList<CutsceneCommand> CleanupCommands { get; }

    internal ShootingGalleryEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/shooting_gallery_event.tsv",
            new GeneratedTableSchema(
                "room 2:e9 shooting gallery",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "cost", "rounds",
                    "retry-command", "controller-y", "controller-x",
                    "initial-delay", "pitch-delay", "puff-delay",
                    "layout-delay", "between-round-delay", "entrance0",
                    "entrance1", "open0", "open1", "closed0", "closed1",
                    "floor", "target-blue", "target-fairy", "target-red",
                    "target-imp", "ball-fast", "ball-slow", "ball-reflected",
                    "ball-angle", "ball-radius-y", "ball-radius-x",
                    "ball-sprite", "ball-tile-base", "ball-palette",
                    "ball-animation", "fade-frames", "minigame-music",
                    "whistle-sound", "baseball-sound", "throw-sound",
                    "slow-sound", "clink-sound", "switch-sound", "error-sound",
                    "strike-sound", "poof-sound", "can-buy-flute-flag",
                    "flute-score", "ring-score", "gasha-score", "rupee-score",
                    "heart-score", "flute-object", "flute-object-parameter",
                    "gasha-object", "gasha-object-parameter", "source"
                ],
                headerRequired: true)).SingleRow();
        Record = new ShootingGalleryEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.UnsignedDecimal(4),
            row.UnsignedDecimal(5),
            row.UnsignedDecimal(6),
            row.HexByte(7),
            row.HexByte(8),
            row.UnsignedDecimal(9),
            row.UnsignedDecimal(10),
            row.UnsignedDecimal(11),
            row.UnsignedDecimal(12),
            row.UnsignedDecimal(13),
            row.HexByte(14),
            row.HexByte(15),
            row.HexByte(16),
            row.HexByte(17),
            row.HexByte(18),
            row.HexByte(19),
            row.HexByte(20),
            row.HexByte(21),
            row.HexByte(22),
            row.HexByte(23),
            row.HexByte(24),
            row.HexByte(25),
            row.HexByte(26),
            row.HexByte(27),
            row.HexByte(28),
            row.HexByte(29),
            row.HexByte(30),
            row.RequiredString(31),
            row.UnsignedDecimal(32),
            row.UnsignedDecimal(33),
            row.RequiredString(34),
            row.UnsignedDecimal(35),
            row.HexByte(36),
            row.HexByte(37),
            row.HexByte(38),
            row.HexByte(39),
            row.HexByte(40),
            row.HexByte(41),
            row.HexByte(42),
            row.HexByte(43),
            row.HexByte(44),
            row.HexByte(45),
            row.HexByte(46),
            row.UnsignedDecimal(47),
            row.UnsignedDecimal(48),
            row.UnsignedDecimal(49),
            row.UnsignedDecimal(50),
            row.UnsignedDecimal(51),
            row.RequiredString(52),
            row.HexByte(53),
            row.RequiredString(54),
            row.HexByte(55),
            row.RequiredString(56));

        MainCommands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/shooting_gallery_main.tsv");
        CleanupCommands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/shooting_gallery_cleanup.tsv");
        _targets = LoadTargets();
        _layouts = LoadLayouts();
        _results = LoadResults();
        _resultScript = LoadResultScript();
        _rings = LoadRings();
        Debris = LoadDebris();
        Validate();
    }

    internal int TargetCount => _targets.Length;
    internal ShootingGalleryTargetRecord Target(int index) => _targets[index];
    internal IReadOnlyList<byte> Layout(int index) => _layouts[index];
    internal ShootingGalleryResultRecord Result(int index) => _results[index];
    internal int Ring(int index) => _rings[index];

    internal IReadOnlyList<CutsceneCommand> BuildResultCommands(int resultIndex)
    {
        ShootingGalleryResultRecord result = Result(resultIndex);
        ShootingGalleryResultScriptRecord common = _resultScript;
        return new CutsceneCommand[]
        {
            new CutsceneShowTextCommand(
                Source(result.SourceLabel, 0, result.SourceLine, "showtext"),
                result.TextId,
                result.Message),
            new CutsceneWaitCommand(
                Source("shootingGallery_printTotalPoints", 1, common.WaitLine, "wait"),
                15),
            new CutsceneMemoryBranchCommand(
                Source("shootingGallery_printTotalPoints", 2, common.BranchLine,
                    "jumpifobjectbyteeq"),
                "FinalRound",
                1,
                6),
            new CutsceneShowTextCommand(
                Source("shootingGallery_printTotalPoints", 3,
                    common.OngoingTextLine, "showtext"),
                common.OngoingTextId,
                common.OngoingMessage),
            new CutsceneNativeCommand(
                Source("shootingGallery_printTotalPoints", 4,
                    common.OngoingEnableLine, "enableallobjects"),
                "EnableAllObjects"),
            new CutsceneEndCommand(
                Source("shootingGallery_printTotalPoints", 5,
                    common.OngoingEndLine, "scriptend")),
            new CutsceneShowTextCommand(
                Source("@gameDone", 6, common.FinalTextLine, "showtext"),
                common.FinalTextId,
                common.FinalMessage),
            new CutsceneNativeCommand(
                Source("@gameDone", 7, common.FinalEnableLine,
                    "enableallobjects"),
                "EnableAllObjects"),
            new CutsceneEndCommand(
                Source("@gameDone", 8, common.FinalEndLine, "scriptend"))
        };

        static CutsceneCommandSource Source(
            string label,
            int index,
            int line,
            string opcode) =>
            new("shootingGalleryResult", label, index, line, opcode);
    }

    private static ShootingGalleryTargetRecord[] LoadTargets()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/shooting_gallery_targets.tsv",
            new GeneratedTableSchema(
                "shooting-gallery target positions",
                GeneratedTableKeySemantics.Ordered,
                ["index", "packed-position", "source"],
                headerRequired: true));
        var records = new ShootingGalleryTargetRecord[table.Rows.Count];
        for (int index = 0; index < records.Length; index++)
        {
            GeneratedTableRow row = table.Rows[index];
            if (row.UnsignedDecimal(0) != index)
                throw new InvalidOperationException(
                    $"Shooting-gallery target row {index} is out of order.");
            records[index] = new ShootingGalleryTargetRecord(
                row.HexByte(1), row.RequiredString(2));
        }
        return records;
    }

    private static byte[][] LoadLayouts()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/shooting_gallery_layouts.tsv",
            new GeneratedTableSchema(
                "shooting-gallery target layouts",
                GeneratedTableKeySemantics.Ordered,
                [
                    "index", "tile0", "tile1", "tile2", "tile3", "tile4",
                    "tile5", "tile6", "tile7", "tile8", "tile9", "source"
                ],
                headerRequired: true));
        var layouts = new byte[table.Rows.Count][];
        for (int index = 0; index < layouts.Length; index++)
        {
            GeneratedTableRow row = table.Rows[index];
            if (row.UnsignedDecimal(0) != index)
                throw new InvalidOperationException(
                    $"Shooting-gallery layout row {index} is out of order.");
            layouts[index] = new byte[10];
            for (int tile = 0; tile < layouts[index].Length; tile++)
                layouts[index][tile] = (byte)row.HexByte(tile + 1);
            _ = row.RequiredString(11);
        }
        return layouts;
    }

    private static ShootingGalleryResultRecord[] LoadResults()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/shooting_gallery_results.tsv",
            new GeneratedTableSchema(
                "shooting-gallery result scripts",
                GeneratedTableKeySemantics.Ordered,
                [
                    "index", "score-delta", "text-id", "source-line",
                    "utf8-base64", "source"
                ],
                headerRequired: true));
        var results = new ShootingGalleryResultRecord[table.Rows.Count];
        for (int index = 0; index < results.Length; index++)
        {
            GeneratedTableRow row = table.Rows[index];
            if (row.UnsignedDecimal(0) != index)
                throw new InvalidOperationException(
                    $"Shooting-gallery result row {index} is out of order.");
            string source = row.RequiredString(5);
            int separator = source.IndexOf(':');
            results[index] = new ShootingGalleryResultRecord(
                row.Decimal(1),
                row.HexWord(2),
                row.UnsignedDecimal(3),
                row.Base64Utf8(4),
                separator < 0 ? source : source[(separator + 1)..]);
        }
        return results;
    }

    private static ShootingGalleryResultScriptRecord LoadResultScript()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/shooting_gallery_result_script.tsv",
            new GeneratedTableSchema(
                "shooting-gallery total-points script",
                GeneratedTableKeySemantics.Ordered,
                [
                    "wait-line", "branch-line", "ongoing-text-line",
                    "ongoing-enable-line", "ongoing-end-line",
                    "final-text-line", "final-enable-line", "final-end-line",
                    "ongoing-text-id", "final-text-id",
                    "ongoing-utf8-base64", "final-utf8-base64"
                ],
                headerRequired: true)).SingleRow();
        return new ShootingGalleryResultScriptRecord(
            row.UnsignedDecimal(0),
            row.UnsignedDecimal(1),
            row.UnsignedDecimal(2),
            row.UnsignedDecimal(3),
            row.UnsignedDecimal(4),
            row.UnsignedDecimal(5),
            row.UnsignedDecimal(6),
            row.UnsignedDecimal(7),
            row.HexWord(8),
            row.HexWord(9),
            row.Base64Utf8(10),
            row.Base64Utf8(11));
    }

    private static int[] LoadRings()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/shooting_gallery_rings.tsv",
            new GeneratedTableSchema(
                "shooting-gallery random rings",
                GeneratedTableKeySemantics.Ordered,
                ["index", "ring", "source"],
                headerRequired: true));
        var rings = new int[table.Rows.Count];
        for (int index = 0; index < rings.Length; index++)
        {
            GeneratedTableRow row = table.Rows[index];
            if (row.UnsignedDecimal(0) != index)
                throw new InvalidOperationException(
                    $"Shooting-gallery ring row {index} is out of order.");
            rings[index] = row.HexByte(1);
            _ = row.RequiredString(2);
        }
        return rings;
    }

    private static ShootingGalleryDebrisRecord LoadDebris()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/shooting_gallery_debris.tsv",
            new GeneratedTableSchema(
                "shooting-gallery target debris",
                GeneratedTableKeySemantics.Ordered,
                [
                    "sprite", "tile-base", "blue-palette", "red-palette",
                    "animation", "count", "lifetime", "speed", "angle0",
                    "angle1", "angle2", "angle3", "source"
                ],
                headerRequired: true)).SingleRow();
        return new ShootingGalleryDebrisRecord(
            row.RequiredString(0),
            row.UnsignedDecimal(1),
            row.UnsignedDecimal(2),
            row.UnsignedDecimal(3),
            row.RequiredString(4),
            row.UnsignedDecimal(5),
            row.UnsignedDecimal(6),
            row.UnsignedDecimal(7),
            row.HexByte(8),
            row.HexByte(9),
            row.HexByte(10),
            row.HexByte(11),
            row.RequiredString(12));
    }

    private void Validate()
    {
        if (Record is not
            {
                Group: 2,
                Room: 0xe9,
                InteractionId: 0x30,
                SubId: 0,
                Cost: 10,
                Rounds: 10,
                RetryCommand: 12,
                ControllerY: 0x2a,
                ControllerX: 0x50,
                InitialDelay: 0x78,
                PitchDelay: 0x28,
                PuffDelay: 0x0a,
                LayoutDelay: 0x5a,
                BetweenRoundDelay: 0x14,
                FloorTile: 0xa0,
                TargetBlue: 0xd9,
                TargetFairy: 0xd7,
                TargetRed: 0xdc,
                TargetImp: 0xd8,
                BallFastSpeed: 0x64,
                BallSlowSpeed: 0x3c,
                BallReflectedSpeed: 0x78,
                BallAngle: 0x10,
                BallRadiusY: 2,
                BallRadiusX: 2,
                FadeFrames: 32,
                MinigameMusic: OracleSoundEngine.MusMinigame,
                WhistleSound: OracleSoundEngine.SndWhistle,
                BaseballSound: OracleSoundEngine.SndBaseball,
                CanBuyFluteFlag: 0x1d,
                FluteScore: 50,
                RingScore: 350,
                GashaScore: 250,
                RupeeScore: 150,
                HeartScore: 50
            } ||
            MainCommands.Count != 48 ||
            CleanupCommands.Count != 55 ||
            _targets.Length != 10 ||
            _layouts.Length != 10 ||
            _results.Length != 22 ||
            _rings.Length != 16 ||
            Debris is not
            {
                Sprite: "spr_common_sprites",
                TileBase: 2,
                BluePalette: 1,
                RedPalette: 2,
                Count: 4,
                Lifetime: 12,
                Speed: 0x28,
                Angle0: 0x04,
                Angle1: 0x0c,
                Angle2: 0x14,
                Angle3: 0x1c
            } ||
            MainCommands[0] is not CutsceneSetCollisionRadiiCommand
                { Actor: "GalleryKeeper", RadiusY: 6, RadiusX: 0x16 } ||
            MainCommands[12] is not CutsceneDisableInputCommand ||
            MainCommands[45] is not CutsceneNativeCommand
                { Handler: "SpawnGame" } ||
            CleanupCommands[26] is not CutsceneNativeCommand
                { Handler: "CheckScore0" } ||
            CleanupCommands[54] is not CutsceneEndCommand ||
            _results[20] is not { ScoreDelta: 0, TextId: 0x0806 } ||
            _results[21] is not { ScoreDelta: -50, TextId: 0x081c })
        {
            throw new InvalidOperationException(
                "Room 2:e9 shooting-gallery data diverges from its source closure.");
        }
    }
}

internal readonly record struct ShootingGalleryDebrisRecord(
    string Sprite,
    int TileBase,
    int BluePalette,
    int RedPalette,
    string Animation,
    int Count,
    int Lifetime,
    int Speed,
    int Angle0,
    int Angle1,
    int Angle2,
    int Angle3,
    string Source)
{
    internal int Angle(int index) => index switch
    {
        0 => Angle0,
        1 => Angle1,
        2 => Angle2,
        3 => Angle3,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

internal readonly record struct ShootingGalleryTargetRecord(
    int PackedPosition,
    string Source);

internal readonly record struct ShootingGalleryResultRecord(
    int ScoreDelta,
    int TextId,
    int SourceLine,
    string Message,
    string SourceLabel);

internal readonly record struct ShootingGalleryResultScriptRecord(
    int WaitLine,
    int BranchLine,
    int OngoingTextLine,
    int OngoingEnableLine,
    int OngoingEndLine,
    int FinalTextLine,
    int FinalEnableLine,
    int FinalEndLine,
    int OngoingTextId,
    int FinalTextId,
    string OngoingMessage,
    string FinalMessage);

internal readonly record struct ShootingGalleryEventRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    int Cost,
    int Rounds,
    int RetryCommand,
    int ControllerY,
    int ControllerX,
    int InitialDelay,
    int PitchDelay,
    int PuffDelay,
    int LayoutDelay,
    int BetweenRoundDelay,
    int EntrancePosition0,
    int EntrancePosition1,
    int EntranceOpenTile0,
    int EntranceOpenTile1,
    int EntranceClosedTile0,
    int EntranceClosedTile1,
    int FloorTile,
    int TargetBlue,
    int TargetFairy,
    int TargetRed,
    int TargetImp,
    int BallFastSpeed,
    int BallSlowSpeed,
    int BallReflectedSpeed,
    int BallAngle,
    int BallRadiusY,
    int BallRadiusX,
    string BallSprite,
    int BallTileBase,
    int BallPalette,
    string BallAnimation,
    int FadeFrames,
    int MinigameMusic,
    int WhistleSound,
    int BaseballSound,
    int ThrowSound,
    int SlowSound,
    int ClinkSound,
    int SwitchSound,
    int ErrorSound,
    int StrikeSound,
    int PoofSound,
    int CanBuyFluteFlag,
    int FluteScore,
    int RingScore,
    int GashaScore,
    int RupeeScore,
    int HeartScore,
    string FluteObject,
    int FluteObjectParameter,
    string GashaObject,
    int GashaObjectParameter,
    string Source)
{
    internal int TargetType(byte tile) =>
        tile == TargetBlue ? 0 :
        tile == TargetFairy ? 1 :
        tile == TargetRed ? 2 :
        tile == TargetImp ? 3 :
        -1;
}
