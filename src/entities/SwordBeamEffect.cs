using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed ITEM_SWORD_BEAM ($27) data. The importer preserves the original
/// offsets, attributes, speed, sound, and four directional OAM compositions.
/// </summary>
internal sealed class SwordBeamDatabase
{
    internal readonly record struct Record(
        int Direction,
        int OffsetY,
        int OffsetX,
        string Sprite,
        int TileBase,
        int Palette,
        int RadiusY,
        int RadiusX,
        int Damage,
        int SpeedRaw,
        int Sound,
        string Oam,
        string Source);

    private readonly Record[] _records = new Record[4];
    private readonly Texture2D[,] _textures = new Texture2D[4, 2];

    internal IReadOnlyList<Record> Records => _records;

    internal SwordBeamDatabase(
        string path = "res://assets/oracle/metadata/sword_beam.tsv")
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                "sword beam",
                GeneratedTableKeySemantics.Unique,
                [
                    "direction", "offset-y", "offset-x", "sprite", "tile-base",
                    "palette", "radius-y", "radius-x", "damage", "speed-raw", "sound", "oam"
                ],
                ["direction"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new Record(
                row.Decimal(0, 0, 3),
                row.Decimal(1),
                row.Decimal(2),
                row.RequiredString(3),
                row.UnsignedDecimal(4),
                row.UnsignedDecimal(5),
                row.UnsignedDecimal(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.HexByte(9),
                row.HexByte(10),
                row.RequiredString(11),
                "object_code/common/items/swordBeam.s:itemCode27");
            Validate(record);
            if (_records[record.Direction].Sprite is not null)
                throw new InvalidOperationException(
                    $"Duplicate sword-beam direction {record.Direction} in {path}.");
            _records[record.Direction] = record;
            count++;
        }

        if (count != 4)
            throw new InvalidOperationException(
                $"Expected four sword-beam directions, got {count}.");

        for (int direction = 0; direction < 4; direction++)
        {
            Record record = _records[direction];
            Image image = OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/{record.Sprite}.png");
            _textures[direction, 0] = NpcCharacter.BuildOamTexture(
                image, record.Oam, record.TileBase, record.Palette);
            _textures[direction, 1] = NpcCharacter.BuildOamTexture(
                image, record.Oam, record.TileBase, record.Palette ^ 1);
        }
    }

    internal Record Get(int direction) => direction is >= 0 and < 4
        ? _records[direction]
        : throw new ArgumentOutOfRangeException(nameof(direction));

    internal Texture2D Texture(int direction, int palettePhase) =>
        _textures[direction, palettePhase & 1];

    private static void Validate(Record record)
    {
        Vector2I[] expectedOffsets =
        [
            new(-4, -11), new(12, 0), new(3, 10), new(-13, 0)
        ];
        if (record.Direction is < 0 or > 3 ||
            new Vector2I(record.OffsetX, record.OffsetY) !=
                expectedOffsets[record.Direction] ||
            record.Sprite != "spr_common_items" || record.TileBase != 0x38 ||
            record.Palette != 4 || record.RadiusY != 2 || record.RadiusX != 2 ||
            record.Damage != 2 || record.SpeedRaw != 0x78 ||
            record.Sound != 0x5d || record.Oam.Length == 0)
        {
            throw new InvalidOperationException(
                $"Invalid ITEM_SWORD_BEAM direction {record.Direction} imported from {record.Source}.");
        }
    }

}

/// <summary>
/// ITEM_SWORD_BEAM ($27). State 0 performs setup without movement; subsequent
/// 60 Hz updates apply damage, move at SPEED_300, test tiles, flip palette on
/// global four-update boundaries, and enforce the original screen boundary.
/// </summary>
public partial class SwordBeamEffect : TransitionOffsetNode2D
{
    private SwordBeamDatabase _database = null!;
    private SwordBeamDatabase.Record _record;
    private OracleRoomData _room = null!;
    private Func<Vector2, Vector2> _worldToScreen = null!;
    private Vector2 _precisePosition;
    private bool _initialized;
    private int _palettePhase;

    public bool Finished { get; private set; }
    internal bool CollisionEnabled => _initialized && !Finished;
    internal int Damage => _record.Damage;
    internal int PalettePhase => _palettePhase;
    internal Vector2 PrecisePosition => _precisePosition;
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(_record.RadiusX, _record.RadiusY),
        new Vector2(_record.RadiusX * 2, _record.RadiusY * 2));

    internal void Initialize(
        SwordBeamDatabase database,
        OracleRoomData room,
        Vector2 linkPosition,
        int direction,
        Func<Vector2, Vector2> worldToScreen,
        Action<int> playSound)
    {
        _database = database;
        _record = database.Get(direction);
        _room = room;
        _worldToScreen = worldToScreen;
        _precisePosition = linkPosition +
            new Vector2(_record.OffsetX, _record.OffsetY);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        Visible = false;
        playSound(_record.Sound);
        QueueRedraw();
    }

    internal void UpdateFrame(int globalFrame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            _initialized = true;
            Visible = true;
            QueueRedraw();
            return;
        }

        Vector2 direction = _record.Direction switch
        {
            0 => Vector2.Up,
            1 => Vector2.Right,
            2 => Vector2.Down,
            _ => Vector2.Left
        };
        _precisePosition += direction * (_record.SpeedRaw / 40.0f);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);

        if (_room.IsSolid(Position))
        {
            Collide(spawns);
            return;
        }

        if ((globalFrame & 3) == 0)
            _palettePhase ^= 1;

        if (!OracleObjectMath.IsInsideOriginalScreenBoundary(
                _worldToScreen(Position)))
        {
            Finish();
            return;
        }
        QueueRedraw();
    }

    internal void OnEnemyCollision(ICollection<RoomEntitySpawn> spawns) =>
        Collide(spawns);

    public override void _Draw()
    {
        if (!Finished && Visible)
        {
            DrawTexture(
                _database.Texture(_record.Direction, _palettePhase),
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }

    private void Collide(ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        spawns.Add(new SwordBeamClinkSpawn(Position));
        Finish();
    }

    private void Finish()
    {
        Finished = true;
        Visible = false;
        QueueRedraw();
    }
}
