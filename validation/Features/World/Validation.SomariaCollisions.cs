using Godot;
using System;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSomariaCollisionData()
    {
        var data = SomariaCollisionDatabase.Shared;
        // Independent transcription of objectCollisionTable column $15, ordered
        // by collision MODE (not enemy ID). Column $12 is zero in every mode.
        int[] effects = (
            "00 00 1c 26 00 00 2f 2f 2d 2d 2d 2d 2d 2d 2d 2d " +
            "2f 2f 2f 2d 2f 00 2f 2d 0d 2d 2f 2f 2d 0d 2f 2f " +
            "2f 0d 2f 2d 2f 2f 2d 2d 2d 2f 2f 2f 2f 2f 2f 2f " +
            "00 2f 2f 2f 2f 2f 2f 2f 0d 2f 2f 2f 00 2f 0c 00 " +
            "00 2f 0d 00 2d 2d 2d 2d 2d 2d 2d 2d 2d 2d 2d 2d " +
            "2d 2f 00 2f 2f 2f 2f 2d 00 2f 2d 2d 2d 2d 2f 2d " +
            "2d 2d 2d 2d 2d 2f 2d 2d 2d 00 2d 2d 2d 2d 2d 00 " +
            "00 00 00 00 2d 00 00 00 00 00 00 2d 00 2f 2f 2d")
            .Split(' ').Select(value => Convert.ToInt32(value, 16)).ToArray();
        // enemyActiveCollisions dbrev character $15; all character $12 bits clear.
        int[] enabled = (
            "01 02 03 04 05 06 07 08 09 0a 0b 0c 0d 0e 0f 10 " +
            "12 13 14 15 17 18 19 1a 1b 1c 1d 1e 1f 20 21 22 23 24 25 26 " +
            "28 2a 2c 2e 2f 30 31 32 34 35 36 39 3a 3b 3c 3d 3e 3f " +
            "41 42 43 45 47 48 49 4a 4b 4c 4d 4e 4f 51 52 54 55 5e 5f 61 64 " +
            "70 71 72 73 74 75 76 77 78 79 7a 7b 7d 7e 7f")
            .Split(' ').Select(value => Convert.ToInt32(value, 16)).ToArray();
        for (int id = 0; id < 128; id++)
        {
            var effect = data.Effects((byte)id);
            var enemy = data.Enemy((byte)id);
            FailIf(effect.Swing != 0 || effect.Block != effects[id],
                $"Somaria objectCollisionTable mode ${id:x2} lost column $12/$15 effects.");
            FailIf(enemy.Swing || enemy.Block != enabled.Contains(id),
                $"Somaria enemyActiveCollisions type ${id:x2} eligibility differs from source.");
            FailIf(data.Effects((byte)(id | 0x80)) != effect || data.Enemy((byte)(id | 0x80)) != enemy,
                $"Somaria mode/type ${id:x2} must strip the collision-enabled high bit.");
        }
        for (int id = 0; id < 0x5a; id++)
        {
            var part = data.Part((byte)id);
            FailIf(part.Swing != (id == 0x17) || part.Block != (id is 0x17 or 0x2a or 0x50) ||
                data.Part((byte)(id | 0x80)) != part,
                $"Somaria partActiveCollisions type ${id:x2} eligibility differs from source.");
        }
        bool rejected = false;
        try { data.Part(0x5a); }
        catch (NotSupportedException error) { rejected = error.Message.Contains("$5a"); }
        FailIf(!rejected, "partActiveCollisions ends at $59; missing $5a must report its source identity.");
        GD.Print("Validated Somaria collision effects and enemy/part masks; live damage dispatch remains separate.");
    }
}
