
namespace Oracles;

internal sealed class RickyDatabase
{
    internal CompanionVisualDatabase Visual { get; }
    internal RickyCompanionBehaviorRecord Behavior { get; }
    internal int IdleHopSpeedZ { get; }
    internal int IdleGravity { get; }
    internal RickyDatabase(string assetRoot)
    {
        Visual=new(assetRoot,SpecialObjectId.Ricky);
        GeneratedTableRow behavior = GeneratedTable.Load(
            assetRoot + "metadata/ricky_behavior.tsv",
            new GeneratedTableSchema(
                "Ricky companion behavior",
                GeneratedTableKeySemantics.Ordered,
                [
                    "idle-animation", "cancel-animation", "punch-animation",
                    "charge-animation", "hop-animation", "ground-speed",
                    "hop-delay", "hop-speed-z", "hop-gravity", "hop-speed",
                    "landing-delay", "punch-lifetime", "punch-damage",
                    "punch-boxes", "charge-updates", "tornado-speed", "tornado-radius-y",
                    "tornado-radius-x", "tornado-damage", "tornado-offsets",
                    "tornado-sprite", "tornado-tile-base", "tornado-palette",
                    "tornado-animation-base64", "jump-sound", "charge-sound",
                    "sword-spin-sound", "sword-slash-sound",
                    "punch-cue-sound", "long-jump-animation",
                    "long-jump-speed-z", "long-jump-delay",
                    "long-jump-speed", "cliff-down-delay",
                    "cliff-down-speed-z", "vine-top-tile",
                    "companion-collision-masks", "hole-tiles",
                    "hole-offsets", "cliff-up-probes", "landing-probes",
                    "water-animation", "hole-animation",
                    "idle-hop-speed-z", "idle-gravity", "source"
                ],
                headerRequired: true)).SingleRow();
        Behavior = new RickyCompanionBehaviorRecord(
            behavior.HexByte(0), behavior.HexByte(1), behavior.HexByte(2),
            behavior.HexByte(3), behavior.HexByte(4), behavior.HexByte(5),
            behavior.UnsignedDecimal(6), behavior.Decimal(7, -0x8000, 0x7fff),
            behavior.HexByte(8), behavior.HexByte(9),
            behavior.UnsignedDecimal(10), behavior.UnsignedDecimal(11),
            behavior.UnsignedDecimal(12),
            ParsePunchBoxes(behavior.RequiredString(13)),
            behavior.UnsignedDecimal(14), behavior.HexByte(15),
            behavior.UnsignedDecimal(16), behavior.UnsignedDecimal(17),
            behavior.UnsignedDecimal(18),
            ParseOffsets(behavior.RequiredString(19)),
            behavior.RequiredString(20), behavior.HexByte(21),
            behavior.UnsignedDecimal(22), behavior.Base64Utf8(23),
            behavior.HexByte(24), behavior.HexByte(25), behavior.HexByte(26),
            behavior.HexByte(27), behavior.HexByte(28),
            behavior.HexByte(29), behavior.Decimal(30, -0x8000, 0x7fff),
            behavior.UnsignedDecimal(31), behavior.HexByte(32),
            behavior.UnsignedDecimal(33),
            behavior.Decimal(34, -0x8000, 0x7fff), behavior.HexByte(35),
            ParseHexOffsets(behavior.RequiredString(36)),
            ParseHexOffsets(behavior.RequiredString(37)),
            ParseOffsets(behavior.RequiredString(38)),
            ParseOffsets(behavior.RequiredString(39)),
            ParseOffsets(behavior.RequiredString(40)),
            behavior.HexByte(41), behavior.HexByte(42),
            behavior.RequiredString(45));
        IdleHopSpeedZ=behavior.Decimal(43,-0x8000,0x7fff);IdleGravity=behavior.HexByte(44);
    }

    private static Godot.Vector2[] ParseOffsets(string encoded) =>
        encoded.Split(';').Select(value =>
        {
            string[] pair = value.Split(',');
            if (pair.Length != 2 ||
                !int.TryParse(pair[0], out int y) ||
                !int.TryParse(pair[1], out int x))
            {
                throw new InvalidOperationException(
                    $"Malformed Ricky tornado offset '{value}'.");
            }
            return new Godot.Vector2(x, y);
        }).ToArray();

    private static int[] ParseHexOffsets(string encoded) =>
        encoded.Split(',').Select(value =>
            Convert.ToInt32(value, 16)).ToArray();

    private static RickyPunchBox[] ParsePunchBoxes(string encoded) =>
        encoded.Split(';').Select(value =>
        {
            string[] fields = value.Split(',');
            if (fields.Length != 4 ||
                !int.TryParse(fields[0], out int radiusY) ||
                !int.TryParse(fields[1], out int radiusX) ||
                !int.TryParse(fields[2], out int offsetY) ||
                !int.TryParse(fields[3], out int offsetX))
            {
                throw new InvalidOperationException(
                    $"Malformed Ricky punch box '{value}'.");
            }
            return new RickyPunchBox(radiusY, radiusX, offsetY, offsetX);
        }).ToArray();
}

internal readonly record struct RickyCompanionBehaviorRecord(
    int IdleAnimation,
    int CancelAnimation,
    int PunchAnimation,
    int ChargeAnimation,
    int HopAnimation,
    int GroundSpeed,
    int HopDelay,
    int HopSpeedZ,
    int HopGravity,
    int HopSpeed,
    int LandingDelay,
    int PunchLifetime,
    int PunchDamage,
    RickyPunchBox[] PunchBoxes,
    int ChargeUpdates,
    int TornadoSpeed,
    int TornadoRadiusY,
    int TornadoRadiusX,
    int TornadoDamage,
    Godot.Vector2[] TornadoOffsets,
    string TornadoSprite,
    int TornadoTileBase,
    int TornadoPalette,
    string TornadoAnimation,
    int JumpSound,
    int ChargeSound,
    int SwordSpinSound,
    int SwordSlashSound,
    int PunchCueSound,
    int LongJumpAnimation,
    int LongJumpSpeedZ,
    int LongJumpDelay,
    int LongJumpSpeed,
    int CliffDownDelay,
    int CliffDownSpeedZ,
    int VineTopTile,
    int[] CompanionCollisionMasks,
    int[] HoleTiles,
    Godot.Vector2[] HoleOffsets,
    Godot.Vector2[] CliffUpProbes,
    Godot.Vector2[] LandingProbes,
    int WaterAnimation,
    int HoleAnimation,
    string Source);

internal readonly record struct RickyPunchBox(
    int RadiusY,
    int RadiusX,
    int OffsetY,
    int OffsetX);
