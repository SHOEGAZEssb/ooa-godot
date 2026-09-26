using Godot;
using System;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullStunMotion()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        // Independent boundary results from bank0.s: signed 16-bit integration,
        // unsigned zh gate, then negate/halve and unsigned comparison to $ff80.
        (int State, int Z, int Speed, int NextZ, int NextSpeed)[] cases = {
            (7, -256, 128, -256, 0),
            (8, 0x0107, 128, 0x0107, 0), (8, 0x08ff, 128, 0x08ff, 0),
            (8, 0x0900, 0, 0, 0), (8, 0, 0, 0, 0),
            (8, -1, 0, -1, 32), (8, -1, 254, 0, 0),
            (8, -1, 255, 0, -128), (8, -1, 256, 0, -128),
            (8, -1, 512, 0, -256), (8, -32760, -32, 0, 16),
            (8, -32768, 32767, -1, -32737)
        };
        foreach (bool fire in new[] { false, true })
        foreach (var row in cases)
        {
            EnemyCharacter enemy;
            var room = _world.LoadRoom(4, fire ? 0x8d : 0x6e);
            if (fire)
            {
                var bat = new FireKeeseCharacter();
                bat.Initialize(new EnemyDatabase().ImportedEnemy(EnemyId.FireKeese), room, new Vector2(124.625f,88), new OracleRandom());
                enemy = bat;
            }
            else
            {
                var gibdo = new GibdoCharacter();
                gibdo.Initialize(new EnemyDatabase().ImportedEnemy(EnemyId.Gibdo), room, new Vector2(124.625f,88), new OracleRandom());
                enemy = gibdo;
            }
            try
            {
                var type = enemy.GetType();
                type.GetField("<State>k__BackingField", flags)!.SetValue(enemy, row.State);
                type.GetField("<ZFixed>k__BackingField", flags)!.SetValue(enemy, row.Z);
                type.GetField("_speedZ", flags)!.SetValue(enemy, row.Speed);
                type.GetField("_stunCounter", flags)!.SetValue(enemy, 30);
                if (fire) ((FireKeeseCharacter)enemy).UpdateFrame(Vector2.Zero, 1);
                else ((GibdoCharacter)enemy).UpdateFrame(1);
                int z = (int)type.GetField("<ZFixed>k__BackingField", flags)!.GetValue(enemy)!;
                int speed = (int)type.GetField("_speedZ", flags)!.GetValue(enemy)!;
                int counter = (int)type.GetField("_stunCounter", flags)!.GetValue(enemy)!;
                FailIf(z != row.NextZ || speed != row.NextSpeed || counter != 29 || enemy.Position != new Vector2(125.625f,88),
                    $"Source stun boundary fire={fire}, state{row.State}, z={row.Z}, speed={row.Speed}: got z={z}, speed={speed}, counter={counter}, XY={enemy.Position}.");
            }
            finally { enemy.Free(); }
        }
    }
}
