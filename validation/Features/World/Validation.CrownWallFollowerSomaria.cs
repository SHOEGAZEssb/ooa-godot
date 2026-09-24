using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownWallFollowerSomaria()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool whisp in new[] { false, true })
        {
            LoadValidationRoom(4, whisp ? 0x9f : 0xa8);
            _player.WarpTo(new(24, 24));
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            Step();
            EnemyCharacter actor = whisp ? _entities.Entities<WhispCharacter>().First()
                : _entities.Entities<SparkCharacter>().First();
            ISomariaBlockCollisionRoomEntity adapter = whisp ? _entities.EntityAdapters<WhispRoomEntity>().First()
                : _entities.EntityAdapters<SparkRoomEntity>().First();
            void Place(Vector2 point)
            {
                actor.Position = point;
                if (actor is SparkCharacter)
                    typeof(SparkCharacter).GetField("_precisePosition", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .SetValue(actor, point);
            }
            Vector2 point = new(120, 88);
            for (int row = 4; row <= 6; row++)
            for (int column = 6; column <= 8; column++)
                _currentRoom.SetPositionTileAndCollision(new(column * 16 + 8, row * 16 + 8), 0xa0, 0, 0);
            int health = actor.Health;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Place(new(200, 136));
                FailIf(!_entities.TryCreateSomariaBlock(_player, 4, point, 0), "Wall-follower fixture must allocate ITEM$18.");
                var block = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
                Step(9);
                Place(point);
                FailIf(adapter.ApplySomariaBlockCollision(block, []), "Somaria's first nine phase-in updates must reject contact.");
                Step();
                FailIf((block.Flags & 0x20) == 0 || block.Finished || block.Health != 9 || block.DamageToApply != 0 ||
                    actor.Health != health || actor.InvincibilityCounter != 0 || actor.KnockbackCounter != 0,
                    "Spark/Whisp effect$2d must mark ITEM$18 after phase-in without exchanging damage or recoil.");
                var text = _entities.TextActiveSource;
                try
                {
                    _entities.TextActiveSource = () => true;
                    Step(2);
                    FailIf(block.Finished || _currentRoom.GetMetatile(point) != 0xda,
                        "Text freeze must retain the marked Somaria block and its solid tile.");
                }
                finally { _entities.TextActiveSource = text; }
                Step();
                FailIf(!block.Finished || _currentRoom.GetMetatile(point) != 0xa0 || actor.Health != health,
                    "The next eligible item update must remove the block and restore its original floor.");
            }
        }
        LoadValidationRoom(0, 0x60);
    }
}
