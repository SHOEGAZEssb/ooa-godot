using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownWallFollowers()
    {
        foreach (byte collision in new byte[] { 0x10, 0x1c, 0x1f })
        {
            LoadValidationRoom(4, 0xa8);
            var spark = _entities.Entities<SparkCharacter>().First();
            spark.Initialize(spark.Record, _currentRoom, new(120, 89));
            for (int y = 4; y <= 6; y++)
            for (int x = 6; x <= 8; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    0xa0, collision, (long)_animationTicks);
            // whisp.s spark_checkWallInDirection's first north probe is
            // (116,80). The disallow-holes table has $ff/$c1/$ff for these
            // values; all have bit0 set, so state0 chooses angle$08.
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!spark.Initialized || spark.Angle != 8 || spark.Position != new Vector2(120, 89),
                $"Spark state0 must use the source disallow-holes mask for collision${collision:x2}.");
        }
        foreach (bool batched in new[] { false, true })
        {
            LoadValidationRoom(4, 0x9f);
            _player.WarpTo(new(24, 24));
            StepGameplayUpdates(1, Vector2.Zero);
            var whisp = _entities.Entities<WhispCharacter>().First();
            whisp.Position = new(120, 88);
            typeof(WhispCharacter).GetField("_angle", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(whisp, 4);
            for (int y = 4; y <= 6; y++)
            for (int x = 6; x <= 8; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    0xf7, 0x10, (long)_animationTicks);
            // whisp_state8 calls ecom_bounceOffWalls (A=0), not the
            // walls-and-holes variant. Both axes must remain unblocked.
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(whisp.Angle != 4 || whisp.Position.X <= 120 || whisp.Position.Y >= 88,
                "Whisps must retain their northeast angle and cross pit collision$10.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
