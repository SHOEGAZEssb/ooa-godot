using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class EnemyDatabase
{
    private readonly Dictionary<int, List<EnemyDatabaseEnemyRecord>> _keeseByRoom = new();
    private readonly Dictionary<int, List<OctorokRecord>> _octoroksByRoom = new();
    private readonly Dictionary<int, List<StalfosRecord>> _stalfosByRoom = new();
    private readonly Dictionary<int, List<ZolRecord>> _zolsByRoom = new();
    private readonly Dictionary<int, List<GelRecord>> _gelsByRoom = new();
    private readonly Dictionary<int, List<CrowRecord>> _crowsByRoom = new();
    private readonly Dictionary<int, List<RoomObjectRecord>> _roomObjectsByRoom = new();
    private readonly Dictionary<int, EnemyDatabaseEnemyRecord> _keeseDefinitions = new();
    private readonly Dictionary<int, OctorokRecord> _octorokDefinitions = new();
    private readonly Dictionary<int, StalfosRecord> _stalfosDefinitions = new();
    private readonly Dictionary<int, ZolRecord> _zolDefinitions = new();
    private readonly Dictionary<int, CrowRecord> _crowDefinitions = new();
    private readonly Dictionary<(int Id, int SubId), ImportedEnemyDefinition>
        _importedDefinitions = new();

    public int KeeseRecordCount { get; }
    public int KeeseInstanceCount { get; }
    public int OctorokRecordCount { get; }
    public int OctorokInstanceCount { get; }
    public int StalfosRecordCount { get; }
    public int StalfosInstanceCount { get; }
    public int ZolRecordCount { get; }
    public int ZolInstanceCount { get; }
    public int GelRecordCount { get; }
    public int GelInstanceCount { get; }
    public int CrowRecordCount { get; }
    public int CrowInstanceCount { get; }
    public int RoomObjectRecordCount { get; }
    public OctorokProjectileRecord OctorokProjectile { get; }
    public MaskedMoblinRecord MaskedMoblin { get; }
    public EnemyArrowRecord EnemyArrow { get; }
    internal EnemyProjectileVisualRecord MoblinBoomerang { get; }
    public GelDefinition Gel { get; }

    public EnemyDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/common_enemies.tsv",
            new GeneratedTableSchema(
                "common enemy definitions",
                GeneratedTableKeySemantics.Unique,
                [
                    "id", "subid", "sprites", "tile-base", "palette",
                    "source-grayscale-inverted", "radius-y", "radius-x",
                    "damage-quarters", "health", "animations-base64"
                ],
                ["id", "subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            ImportedEnemyDefinition record = new ImportedEnemyDefinition(
                row.HexByte(0),
                row.HexByte(1),
                SplitRequired(row, 2, ','),
                row.UnsignedDecimal(3),
                row.UnsignedDecimal(4),
                row.Boolean01(5),
                row.UnsignedDecimal(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.UnsignedDecimal(9),
                SplitDecoded(row, 10));
            if (!_importedDefinitions.TryAdd((record.Id, record.SubId), record))
            {
                throw new InvalidOperationException(
                    $"Duplicate common enemy ${record.Id:x2}:${record.SubId:x2}.");
            }
        }
        if (_importedDefinitions.Count != 5 ||
            ImportedEnemy(0x0a) is not
                { Health: 3, DamageQuarters: 2, Animations.Length: 4 } ||
            ImportedEnemy(0x0c) is not
                { Health: 3, DamageQuarters: 2, Animations.Length: 4 } ||
            ImportedEnemy(0x10) is not { Health: 2, DamageQuarters: 2 } ||
            ImportedEnemy(0x17) is not { Health: 10, DamageQuarters: 2 } ||
            ImportedEnemy(0x28) is not { Health: 5, DamageQuarters: 2 })
        {
            throw new InvalidOperationException(
                "Imported common-enemy contract is incomplete.");
        }

        table = GeneratedTable.Load(
            "res://assets/oracle/effects/moblin_boomerang.tsv",
            new GeneratedTableSchema(
                "Moblin boomerang",
                GeneratedTableKeySemantics.Ordered,
                [
                    "sprites", "tile-base", "palette",
                    "source-grayscale-inverted", "animations-base64"
                ],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"Moblin boomerang data should have one row, got {table.Rows.Count}.");
        }
        GeneratedTableRow boomerang = table.Rows[0];
        MoblinBoomerang = new EnemyProjectileVisualRecord(
            SplitRequired(boomerang, 0, ','),
            boomerang.UnsignedDecimal(1),
            boomerang.UnsignedDecimal(2),
            boomerang.Boolean01(3),
            SplitDecoded(boomerang, 4));
        if (MoblinBoomerang is not
            {
                Sprites: ["spr_projectiles_1"],
                TileBase: 10,
                Palette: 4,
                SourceGrayscaleInverted: true,
                Animations.Length: 1
            })
        {
            throw new InvalidOperationException(
                "Imported PART_MOBLIN_BOOMERANG $21 visual is incomplete.");
        }

        table = GeneratedTable.Load(
            "res://assets/oracle/objects/keese.tsv",
            new GeneratedTableSchema(
                "Keese room records",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "id", "subid", "flags", "count", "sprite",
                    "tile-base", "palette", "radius-y", "radius-x", "damage-quarters",
                    "health", "idle-animation", "fly-animation"
                ],
                ["group", "room"],
                headerRequired: true));
        int records = 0;
        int instances = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            EnemyDatabaseEnemyRecord record = new EnemyDatabaseEnemyRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                row.HexByte(4),
                row.UnsignedDecimal(5),
                row.RequiredString(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.UnsignedDecimal(9),
                row.UnsignedDecimal(10),
                row.UnsignedDecimal(11),
                row.UnsignedDecimal(12),
                row.RequiredString(13),
                row.RequiredString(14));

            int key = MakeKey(record.Group, record.Room);
            if (!_keeseByRoom.TryGetValue(key, out List<EnemyDatabaseEnemyRecord>? roomRecords))
            {
                roomRecords = new List<EnemyDatabaseEnemyRecord>();
                _keeseByRoom.Add(key, roomRecords);
            }
            roomRecords.Add(record);
            _keeseDefinitions.TryAdd(record.SubId, record);
            records++;
            instances += record.Count;
        }

        KeeseRecordCount = records;
        KeeseInstanceCount = instances;

        table = GeneratedTable.Load(
            "res://assets/oracle/objects/crows.tsv",
            new GeneratedTableSchema(
                "Crow room records",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "id", "subid", "flags", "count",
                    "position-mode", "y", "x", "sprite", "tile-base", "palette",
                    "radius-y", "radius-x", "damage-quarters", "health", "speed-raw",
                    "perched-right", "perched-left", "flight-right", "flight-left"
                ],
                ["group", "room"],
                headerRequired: true));
        records = 0;
        instances = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            CrowRecord record = new CrowRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                row.HexByte(4),
                row.UnsignedDecimal(5),
                FixedPosition(row, 6),
                row.HexByteOrSentinel(7, "-1", -1),
                row.HexByteOrSentinel(8, "-1", -1),
                row.RequiredString(9),
                row.UnsignedDecimal(10),
                row.UnsignedDecimal(11),
                row.UnsignedDecimal(12),
                row.UnsignedDecimal(13),
                row.UnsignedDecimal(14),
                row.UnsignedDecimal(15),
                row.UnsignedDecimal(16),
                row.RequiredString(17),
                row.RequiredString(18),
                row.RequiredString(19),
                row.RequiredString(20));
            int key = MakeKey(record.Group, record.Room);
            if (!_crowsByRoom.TryGetValue(key, out List<CrowRecord>? roomRecords))
            {
                roomRecords = new List<CrowRecord>();
                _crowsByRoom.Add(key, roomRecords);
            }
            roomRecords.Add(record);
            _crowDefinitions.TryAdd(record.SubId, record);
            records++;
            instances += record.Count;
        }
        CrowRecordCount = records;
        CrowInstanceCount = instances;

        table = GeneratedTable.Load(
            "res://assets/oracle/objects/octoroks.tsv",
            new GeneratedTableSchema(
                "Octorok room records",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "id", "subid", "flags", "count", "position-mode",
                    "y", "x", "sprite", "tile-base", "palette", "radius-y", "radius-x",
                    "damage-quarters", "health", "speed-raw", "counter-mask", "up-animation",
                    "right-animation", "down-animation", "left-animation"
                ],
                ["group", "room"],
                headerRequired: true));
        records = 0;
        instances = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            OctorokRecord record = new OctorokRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                row.HexByte(4),
                row.UnsignedDecimal(5),
                FixedPosition(row, 6),
                row.HexByteOrSentinel(7, "-1", -1),
                row.HexByteOrSentinel(8, "-1", -1),
                row.RequiredString(9),
                row.UnsignedDecimal(10),
                row.UnsignedDecimal(11),
                row.UnsignedDecimal(12),
                row.UnsignedDecimal(13),
                row.UnsignedDecimal(14),
                row.UnsignedDecimal(15),
                row.UnsignedDecimal(16),
                row.UnsignedDecimal(17),
                row.RequiredString(18),
                row.RequiredString(19),
                row.RequiredString(20),
                row.RequiredString(21));

            int key = MakeKey(record.Group, record.Room);
            if (!_octoroksByRoom.TryGetValue(key, out List<OctorokRecord>? roomRecords))
            {
                roomRecords = new List<OctorokRecord>();
                _octoroksByRoom.Add(key, roomRecords);
            }
            roomRecords.Add(record);
            _octorokDefinitions.TryAdd(record.SubId, record);
            records++;
            instances += record.Count;
        }
        OctorokRecordCount = records;
        OctorokInstanceCount = instances;

        table = GeneratedTable.Load(
            "res://assets/oracle/objects/stalfos.tsv",
            new GeneratedTableSchema(
                "Stalfos room records",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "id", "subid", "flags", "count", "position-mode",
                    "y", "x", "sprite", "tile-base", "palette", "radius-y", "radius-x",
                    "damage-quarters", "health", "speed-raw", "walk-animation", "jump-animation"
                ],
                ["group", "room"],
                headerRequired: true));
        records = 0;
        instances = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            StalfosRecord record = new StalfosRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                row.HexByte(4),
                row.UnsignedDecimal(5),
                FixedPosition(row, 6),
                row.HexByteOrSentinel(7, "-1", -1),
                row.HexByteOrSentinel(8, "-1", -1),
                row.RequiredString(9),
                row.UnsignedDecimal(10),
                row.UnsignedDecimal(11),
                row.UnsignedDecimal(12),
                row.UnsignedDecimal(13),
                row.UnsignedDecimal(14),
                row.UnsignedDecimal(15),
                row.UnsignedDecimal(16),
                row.RequiredString(17),
                row.RequiredString(18));
            int key = MakeKey(record.Group, record.Room);
            if (!_stalfosByRoom.TryGetValue(key, out List<StalfosRecord>? roomRecords))
            {
                roomRecords = new List<StalfosRecord>();
                _stalfosByRoom.Add(key, roomRecords);
            }
            roomRecords.Add(record);
            _stalfosDefinitions.TryAdd(record.SubId, record);
            records++;
            instances += record.Count;
        }
        StalfosRecordCount = records;
        StalfosInstanceCount = instances;

        table = GeneratedTable.Load(
            "res://assets/oracle/objects/zols.tsv",
            new GeneratedTableSchema(
                "Zol room records",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "id", "subid", "flags", "count", "position-mode",
                    "y", "x", "sprite", "tile-base", "palette", "radius-y", "radius-x",
                    "damage-quarters", "health", "animation-0", "animation-1", "animation-2",
                    "animation-3", "animation-4", "animation-5"
                ],
                ["group", "room"],
                headerRequired: true));
        records = 0;
        instances = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            ZolRecord record = new ZolRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                row.HexByte(4),
                row.UnsignedDecimal(5),
                FixedPosition(row, 6),
                row.HexByteOrSentinel(7, "-1", -1),
                row.HexByteOrSentinel(8, "-1", -1),
                row.RequiredString(9),
                row.UnsignedDecimal(10),
                row.UnsignedDecimal(11),
                row.UnsignedDecimal(12),
                row.UnsignedDecimal(13),
                row.UnsignedDecimal(14),
                row.UnsignedDecimal(15),
                row.RequiredString(16),
                row.RequiredString(17),
                row.RequiredString(18),
                row.RequiredString(19),
                row.RequiredString(20),
                row.RequiredString(21));
            int key = MakeKey(record.Group, record.Room);
            if (!_zolsByRoom.TryGetValue(key, out List<ZolRecord>? roomRecords))
            {
                roomRecords = new List<ZolRecord>();
                _zolsByRoom.Add(key, roomRecords);
            }
            roomRecords.Add(record);
            _zolDefinitions.TryAdd(record.SubId, record);
            records++;
            instances += record.Count;
        }
        ZolRecordCount = records;
        ZolInstanceCount = instances;

        table = GeneratedTable.Load(
            "res://assets/oracle/objects/gels.tsv",
            new GeneratedTableSchema(
                "Gel room records",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "id", "subid", "flags", "count", "position-mode",
                    "y", "x", "sprite", "tile-base", "palette", "radius-y", "radius-x",
                    "damage-quarters", "health", "animation-0", "animation-1", "animation-2"
                ],
                ["group", "room"],
                headerRequired: true));
        records = 0;
        instances = 0;
        GelDefinition? gelDefinition = null;
        foreach (GeneratedTableRow row in table.Rows)
        {
            GelDefinition definition = new GelDefinition(
                row.HexByte(2),
                row.RequiredString(9),
                row.UnsignedDecimal(10),
                row.UnsignedDecimal(11),
                row.UnsignedDecimal(12),
                row.UnsignedDecimal(13),
                row.UnsignedDecimal(14),
                row.UnsignedDecimal(15),
                row.RequiredString(16),
                row.RequiredString(17),
                row.RequiredString(18));
            gelDefinition ??= definition;
            if (gelDefinition.Value != definition)
                throw new InvalidOperationException("Room Gel records disagree on their shared definition.");

            GelRecord record = new GelRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.HexByte(4),
                row.UnsignedDecimal(5),
                FixedPosition(row, 6),
                row.HexByteOrSentinel(7, "-1", -1),
                row.HexByteOrSentinel(8, "-1", -1));
            int key = MakeKey(record.Group, record.Room);
            if (!_gelsByRoom.TryGetValue(key, out List<GelRecord>? roomRecords))
            {
                roomRecords = new List<GelRecord>();
                _gelsByRoom.Add(key, roomRecords);
            }
            roomRecords.Add(record);
            records++;
            instances += record.Count;
        }
        Gel = gelDefinition ?? throw new InvalidOperationException("Gel data is empty.");
        GelRecordCount = records;
        GelInstanceCount = instances;

        table = GeneratedTable.Load(
            "res://assets/oracle/objects/enemy_object_stream.tsv",
            new GeneratedTableSchema(
                "ordered enemy object stream",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "order", "kind", "id", "subid", "flags", "count",
                    "y", "x", "packed-position", "condition-mask"
                ],
                ["group", "room"],
                headerRequired: true));
        records = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            RoomObjectRecord record = new RoomObjectRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                ParseRoomObjectKind(row, 3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.UnsignedDecimal(7),
                row.HexByteOrSentinel(8, "-1", -1),
                row.HexByteOrSentinel(9, "-1", -1),
                row.HexByteOrSentinel(10, "-1", -1),
                row.HexByte(11));
            int key = MakeKey(record.Group, record.Room);
            if (!_roomObjectsByRoom.TryGetValue(key, out List<RoomObjectRecord>? roomRecords))
            {
                roomRecords = new List<RoomObjectRecord>();
                _roomObjectsByRoom.Add(key, roomRecords);
            }
            if (roomRecords.Count != record.Order)
            {
                throw new InvalidOperationException(
                    $"Room {record.Group:x1}:{record.Room:x2} object order jumped from " +
                    $"{roomRecords.Count} to {record.Order}.");
            }
            roomRecords.Add(record);
            records++;
        }
        RoomObjectRecordCount = records;

        table = GeneratedTable.Load(
            "res://assets/oracle/effects/octorok_projectile.tsv",
            new GeneratedTableSchema(
                "Octorok projectile",
                GeneratedTableKeySemantics.Ordered,
                [
                    "sprite", "tile-base", "palette", "radius-y", "radius-x",
                    "damage-quarters", "speed-raw", "normal-animation", "bounce-animation"
                ],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"Octorok projectile data should have one row, got {table.Rows.Count}.");
        }
        GeneratedTableRow projectile = table.Rows[0];
        OctorokProjectile = new OctorokProjectileRecord(
            projectile.RequiredString(0),
            projectile.UnsignedDecimal(1),
            projectile.UnsignedDecimal(2),
            projectile.UnsignedDecimal(3),
            projectile.UnsignedDecimal(4),
            projectile.UnsignedDecimal(5),
            projectile.UnsignedDecimal(6),
            projectile.RequiredString(7),
            projectile.RequiredString(8));

        table = GeneratedTable.Load(
            "res://assets/oracle/objects/masked_moblin.tsv",
            new GeneratedTableSchema(
                "masked Moblin",
                GeneratedTableKeySemantics.Ordered,
                [
                    "id", "subid", "sprite", "tile-base", "palette", "radius-y", "radius-x",
                    "damage-quarters", "health", "speed-raw", "move-base", "move-mask",
                    "turn-wait", "up-animation", "right-animation", "down-animation", "left-animation"
                ],
                headerRequired: true));
        if (table.Rows.Count != 1)
            throw new InvalidOperationException(
                $"Masked Moblin data should have one row, got {table.Rows.Count}.");
        GeneratedTableRow masked = table.Rows[0];
        MaskedMoblin = new MaskedMoblinRecord(
            masked.HexByte(0), masked.HexByte(1),
            masked.RequiredString(2), masked.UnsignedDecimal(3), masked.UnsignedDecimal(4),
            masked.UnsignedDecimal(5), masked.UnsignedDecimal(6), masked.UnsignedDecimal(7),
            masked.UnsignedDecimal(8), masked.UnsignedDecimal(9), masked.UnsignedDecimal(10),
            masked.UnsignedDecimal(11), masked.UnsignedDecimal(12),
            masked.RequiredString(13), masked.RequiredString(14),
            masked.RequiredString(15), masked.RequiredString(16));

        table = GeneratedTable.Load(
            "res://assets/oracle/effects/enemy_arrow.tsv",
            new GeneratedTableSchema(
                "enemy arrow",
                GeneratedTableKeySemantics.Ordered,
                [
                    "sprite", "tile-base", "palette", "damage-quarters", "speed-raw",
                    "up-animation", "right-animation", "down-animation", "left-animation",
                    "bounce-animation"
                ],
                headerRequired: true));
        if (table.Rows.Count != 1)
            throw new InvalidOperationException(
                $"Enemy arrow data should have one row, got {table.Rows.Count}.");
        GeneratedTableRow arrow = table.Rows[0];
        EnemyArrow = new EnemyArrowRecord(
            arrow.RequiredString(0), arrow.UnsignedDecimal(1), arrow.UnsignedDecimal(2),
            arrow.UnsignedDecimal(3), arrow.UnsignedDecimal(4),
            arrow.RequiredString(5), arrow.RequiredString(6), arrow.RequiredString(7),
            arrow.RequiredString(8), arrow.RequiredString(9));
    }

    public IReadOnlyList<EnemyDatabaseEnemyRecord> GetRoomKeese(int group, int room)
    {
        return _keeseByRoom.TryGetValue(MakeKey(group, room), out List<EnemyDatabaseEnemyRecord>? records)
            ? records
            : Array.Empty<EnemyDatabaseEnemyRecord>();
    }

    public IReadOnlyList<OctorokRecord> GetRoomOctoroks(int group, int room)
    {
        return _octoroksByRoom.TryGetValue(
            MakeKey(group, room), out List<OctorokRecord>? records)
            ? records
            : Array.Empty<OctorokRecord>();
    }

    public IReadOnlyList<ZolRecord> GetRoomZols(int group, int room)
    {
        return _zolsByRoom.TryGetValue(MakeKey(group, room), out List<ZolRecord>? records)
            ? records
            : Array.Empty<ZolRecord>();
    }

    public IReadOnlyList<StalfosRecord> GetRoomStalfos(int group, int room)
    {
        return _stalfosByRoom.TryGetValue(
            MakeKey(group, room), out List<StalfosRecord>? records)
            ? records
            : Array.Empty<StalfosRecord>();
    }

    public IReadOnlyList<GelRecord> GetRoomGels(int group, int room)
    {
        return _gelsByRoom.TryGetValue(MakeKey(group, room), out List<GelRecord>? records)
            ? records
            : Array.Empty<GelRecord>();
    }

    public IReadOnlyList<CrowRecord> GetRoomCrows(int group, int room)
    {
        return _crowsByRoom.TryGetValue(
            MakeKey(group, room), out List<CrowRecord>? records)
            ? records
            : Array.Empty<CrowRecord>();
    }

    public IReadOnlyList<RoomObjectRecord> GetRoomObjects(int group, int room)
    {
        return _roomObjectsByRoom.TryGetValue(
            MakeKey(group, room), out List<RoomObjectRecord>? records)
            ? records
            : Array.Empty<RoomObjectRecord>();
    }

    public bool TryGetKeeseDefinition(RoomObjectRecord source, out EnemyDatabaseEnemyRecord record)
    {
        if (!_keeseDefinitions.TryGetValue(source.SubId, out EnemyDatabaseEnemyRecord template))
        {
            record = default;
            return false;
        }
        record = template with
        {
            Group = source.Group,
            Room = source.Room,
            Flags = source.Flags,
            Count = source.Count
        };
        return true;
    }

    public bool TryGetOctorokDefinition(RoomObjectRecord source, out OctorokRecord record)
    {
        if (!_octorokDefinitions.TryGetValue(source.SubId, out OctorokRecord template))
        {
            record = default;
            return false;
        }
        record = template with
        {
            Group = source.Group,
            Room = source.Room,
            Flags = source.Flags,
            Count = source.Count,
            FixedPosition = source.Kind == RoomObjectKind.FixedEnemy,
            Y = source.Y,
            X = source.X
        };
        return true;
    }

    public bool TryGetZolDefinition(RoomObjectRecord source, out ZolRecord record)
    {
        if (!_zolDefinitions.TryGetValue(source.SubId, out ZolRecord template))
        {
            record = default;
            return false;
        }
        record = template with
        {
            Group = source.Group,
            Room = source.Room,
            Flags = source.Flags,
            Count = source.Count,
            FixedPosition = source.Kind == RoomObjectKind.FixedEnemy,
            Y = source.Y,
            X = source.X
        };
        return true;
    }

    public bool TryGetStalfosDefinition(RoomObjectRecord source, out StalfosRecord record)
    {
        if (!_stalfosDefinitions.TryGetValue(source.SubId, out StalfosRecord template))
        {
            record = default;
            return false;
        }
        record = template with
        {
            Group = source.Group,
            Room = source.Room,
            Flags = source.Flags,
            Count = source.Count,
            FixedPosition = source.Kind == RoomObjectKind.FixedEnemy,
            Y = source.Y,
            X = source.X
        };
        return true;
    }

    public bool TryGetCrowDefinition(RoomObjectRecord source, out CrowRecord record)
    {
        if (!_crowDefinitions.TryGetValue(source.SubId, out CrowRecord template))
        {
            record = default;
            return false;
        }
        record = template with
        {
            Group = source.Group,
            Room = source.Room,
            Flags = source.Flags,
            Count = source.Count,
            FixedPosition = source.Kind == RoomObjectKind.FixedEnemy,
            Y = source.Y,
            X = source.X
        };
        return true;
    }

    internal ImportedEnemyDefinition ImportedEnemy(int id, int subId = 0) =>
        _importedDefinitions.TryGetValue((id, subId), out ImportedEnemyDefinition record)
            ? record
            : throw new KeyNotFoundException(
                $"Common enemy ${id:x2}:${subId:x2} was not imported.");

    internal bool TryGetImportedEnemyDefinition(
        RoomObjectRecord source,
        out ImportedEnemyDefinition record) =>
        _importedDefinitions.TryGetValue((source.Id, source.SubId), out record);

    private static int MakeKey(int group, int room) => (group << 8) | room;

    private static bool FixedPosition(GeneratedTableRow row, int column) =>
        row.RequiredString(column) switch
        {
            "F" => true,
            "R" => false,
            _ => throw row.Invalid(column, "position mode F or R")
        };

    private static string[] SplitRequired(
        GeneratedTableRow row,
        int column,
        char separator)
    {
        string[] values = row.RequiredString(column).Split(
            separator,
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        if (values.Length == 0)
            throw row.Invalid(column, "one or more values");
        return values;
    }

    private static string[] SplitDecoded(GeneratedTableRow row, int column)
    {
        string[] values = row.Base64Utf8(column).Split(
            '\n', StringSplitOptions.RemoveEmptyEntries);
        if (values.Length == 0)
            throw row.Invalid(column, "one or more encoded animations");
        return values;
    }

    private static RoomObjectKind ParseRoomObjectKind(
        GeneratedTableRow row,
        int column) => row.RequiredString(column) switch
    {
        "R" => RoomObjectKind.RandomEnemy,
        "F" => RoomObjectKind.FixedEnemy,
        "B" => RoomObjectKind.ParameterEnemy,
        "P" => RoomObjectKind.ReservingPart,
        "Q" => RoomObjectKind.ParameterPart,
        "I" => RoomObjectKind.ItemDrop,
        _ => throw row.Invalid(column, "one of R, F, B, P, Q, I")
    };
}

public readonly record struct CrowRecord(int Group, int Room, int Id, int SubId, int Flags, int Count, bool FixedPosition, int Y, int X, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, int SpeedRaw, string PerchedRightAnimation, string PerchedLeftAnimation, string FlightRightAnimation, string FlightLeftAnimation);

public readonly record struct EnemyArrowRecord(string SpriteName, int TileBase, int Palette, int DamageQuarters, int SpeedRaw, string UpAnimation, string RightAnimation, string DownAnimation, string LeftAnimation, string BounceAnimation);

public readonly record struct EnemyDatabaseEnemyRecord(int Group, int Room, int Id, int SubId, int Flags, int Count, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, string IdleAnimation, string FlyAnimation);

internal readonly record struct ImportedEnemyDefinition(int Id, int SubId, string[] Sprites, int TileBase, int Palette, bool SourceGrayscaleInverted, int RadiusY, int RadiusX, int DamageQuarters, int Health, string[] Animations);

internal readonly record struct EnemyProjectileVisualRecord(string[] Sprites, int TileBase, int Palette, bool SourceGrayscaleInverted, string[] Animations);

public readonly record struct GelRecord(int Group, int Room, int Flags, int Count, bool FixedPosition, int Y, int X);

public readonly record struct GelDefinition(int Id, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, string NormalAnimation, string AttachedAnimation, string ShakeAnimation);

public readonly record struct MaskedMoblinRecord(int Id, int SubId, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, int SpeedRaw, int MoveCounterBase, int MoveCounterMask, int TurnWait, string UpAnimation, string RightAnimation, string DownAnimation, string LeftAnimation);

public readonly record struct OctorokProjectileRecord(string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int SpeedRaw, string NormalAnimation, string BounceAnimation);

public readonly record struct ZolRecord(int Group, int Room, int Id, int SubId, int Flags, int Count, bool FixedPosition, int Y, int X, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, string EmergeAnimation, string WaitAnimation, string HopAnimation, string DisappearAnimation, string RedIdleAnimation, string RedShakeAnimation);

public readonly record struct StalfosRecord(int Group, int Room, int Id, int SubId, int Flags, int Count, bool FixedPosition, int Y, int X, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, int SpeedRaw, string WalkAnimation, string JumpAnimation);

public readonly record struct RoomObjectRecord(int Group, int Room, int Order, RoomObjectKind Kind, int Id, int SubId, int Flags, int Count, int Y, int X, int PackedPosition, int ConditionMask);

public enum RoomObjectKind
{
    RandomEnemy,
    FixedEnemy,
    ParameterEnemy,
    ReservingPart,
    ParameterPart,
    ItemDrop
}

public readonly record struct OctorokRecord(int Group, int Room, int Id, int SubId, int Flags, int Count, bool FixedPosition, int Y, int X, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, int SpeedRaw, int CounterMask, string UpAnimation, string RightAnimation, string DownAnimation, string LeftAnimation);
