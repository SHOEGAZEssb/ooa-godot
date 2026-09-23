using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SeedShooterEyeStatueDatabase
{
    private readonly Lookup<int,SeedShooterEyeStatueRecord> _records = new();
    private readonly int?[] _hitLockouts = new int?[32];

    internal SeedShooterEyeStatueDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/seed_shooter_eye_statues.tsv",
            new GeneratedTableSchema("Seed shooter eye statue placements",GeneratedTableKeySemantics.Unique,
                ["group","room","order","subid","packed-position","active-counter","gfx","enemy-collision-mode","radius-y","radius-x","source"],
                ["group","room","order"],headerRequired:true));
        foreach (var row in table.Rows)
        {
            int group = row.Decimal(0,0,7), room = row.HexByte(1);
            byte subid = (byte)row.HexByte(3), counter = (byte)row.HexByte(5);
            if (subid > 7 || counter == 0) throw row.Invalid(3,"PART $46 subid $00-$07 and nonzero activation counter");
            _records.GetOrAdd((group << 8) | room).Add(new(
                row.UnsignedDecimal(2),subid,(byte)row.HexByte(4),counter,(byte)row.HexByte(6),(byte)row.HexByte(7),
                (byte)row.HexByte(8),(byte)row.HexByte(9),row.RequiredString(10)));
        }
        if (table.Rows.Count != 7) throw new InvalidOperationException("PART $46 requires seven source placements.");
        var hits = GeneratedTable.Load("res://assets/oracle/objects/seed_shooter_eye_statue_collisions.tsv",
            new GeneratedTableSchema("PART $46 collision responses",GeneratedTableKeySemantics.Unique,
                ["item","enabled","effect","invincibility","source"],["item"],headerRequired:true));
        if (hits.Rows.Count != 32) throw new InvalidOperationException("PART $46 requires all collision indices $00-$1f.");
        for (int i = 0; i < 32; i++)
        {
            var row = hits.Rows[i];
            if (row.HexByte(0) != i) throw row.Invalid(0,"ordered collision indices $00-$1f");
            if (!row.Boolean01(1)) continue;
            if (row.HexByte(2) != 0x31 || row.HexByte(3) != 0xe4)
                throw row.Invalid(2,"PART $46 effect $31 and signed invincibility $e4");
            _hitLockouts[i] = unchecked((sbyte)row.HexByte(3));
        }
    }

    internal IReadOnlyList<SeedShooterEyeStatueRecord> GetRoomRecords(int group,int room) =>
        _records.ValuesOrEmpty((group << 8) | room);
    internal int? HitLockout(int collision) => collision is >= 0 and < 32 ? _hitLockouts[collision] : null;
}

internal readonly record struct SeedShooterEyeStatueRecord(
    int Order,byte Subid,byte PackedPosition,byte ActiveCounter,byte Graphics,
    byte CollisionDefinition,byte RadiusY,byte RadiusX,string Source);
