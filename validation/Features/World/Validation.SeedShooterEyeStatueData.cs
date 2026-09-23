using System;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSeedShooterEyeStatueData()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/seed_shooter_eye_statues.tsv",
            new GeneratedTableSchema("Seed shooter eye statue placements",GeneratedTableKeySemantics.Unique,
                ["group","room","order","subid","packed-position","active-counter","gfx","enemy-collision-mode","radius-y","radius-x","source"],
                ["group","room","order"],headerRequired:true));
        // Independent transcription of all source PART $46 placements. Keep
        // the Mermaid's Cave rows too: this is a shared native part.
        (int Room,int Order,int Subid,int Packed)[] expected = [
            (0xba,1,0,0x37),(0xba,2,1,0x65),(0xba,3,2,0x69),
            (0xc6,2,0,0x44),(0xc6,3,1,0x27),(0xc6,4,2,0x77),(0xc6,5,3,0x49)];
        FailIf(table.Rows.Count != expected.Length,"PART $46 import must preserve all seven placements.");
        for (int i = 0; i < expected.Length; i++)
        {
            var row = table.Rows[i];
            var value = expected[i];
            FailIf(row.Decimal(0,0,7) != 4 || row.HexByte(1) != value.Room ||
                row.UnsignedDecimal(2) != value.Order || row.HexByte(3) != value.Subid || row.HexByte(4) != value.Packed,
                $"PART $46 placement/order mismatch in room4:${value.Room:x2}, subid${value.Subid:x2}.");
            FailIf(row.HexByte(5) != 0x2d || row.HexByte(6) != 0x8c || row.HexByte(7) != 0xf9 ||
                row.HexByte(8) != 8 || row.HexByte(9) != 8 ||
                !row.RequiredString(10).Contains("seedShooterEyeStatue.s:partCode46",StringComparison.Ordinal),
                "PART $46 must retain its $2d activation counter, graphics, collision mode, radii and source identity.");
        }
        Godot.GD.Print("Validated source-derived PART $46 placement and definition data; runtime behavior is not asserted.");
    }
}
