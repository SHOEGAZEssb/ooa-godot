using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownWallFollowerBeam()
    {
        var beamData = new SwordBeamDatabase().Get(0);
        foreach (bool batched in new[] { false, true })
        foreach (bool whisp in new[] { false, true })
        foreach (bool exhausted in new[] { false, true })
        {
            LoadValidationRoom(4, whisp ? 0x9f : 0xa8);
            _player.WarpTo(new(24, 24));
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            Step();
            EnemyCharacter actor = whisp ? _entities.Entities<WhispCharacter>().First()
                : _entities.Entities<SparkCharacter>().First();
            IPostObjectItemCollisionRoomEntity adapter = whisp ? _entities.EntityAdapters<WhispRoomEntity>().First()
                : _entities.EntityAdapters<SparkRoomEntity>().First();
            for (int row = 4; row <= 6; row++)
            for (int column = 6; column <= 8; column++)
                _currentRoom.SetPositionTileAndCollision(new(column * 16 + 8, row * 16 + 8), 0xa0, 0, 0);
            int health = actor.Health;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                actor.Position = new(120, 88);
                if (!whisp) typeof(SparkCharacter).GetField("_precisePosition", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(actor, actor.Position);
                var beam = _entities.Spawn<SwordBeamEffect>(new SwordBeamSpawn(
                    actor.Position - new Vector2(beamData.OffsetX, beamData.OffsetY), ObjectDirection.Up));
                int clinks = _entities.EntityAdapters<SwordBeamClinkRoomEntity>().Count();
                Step();
                FailIf(!beam.PendingNativeCollision || beam.CollisionEnabled || beam.Finished || actor.Health != health,
                    "Spark/Whisp must mark a sword beam after object updates without deleting it or damaging the enemy yet.");
                Vector2 impact = beam.Position;
                if (exhausted)
                    while (_entities.InteractionSlotAvailable)
                        _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24, 24), SoundId.MusNone));
                var text = _entities.TextActiveSource;
                try
                {
                    _entities.TextActiveSource = () => true;
                    Step(2);
                    FailIf(beam.Finished || !beam.PendingNativeCollision || beam.Position != impact,
                        "Text freeze must retain the pending beam hit without movement or deletion.");
                }
                finally { _entities.TextActiveSource = text; }
                Step();
                FailIf(!beam.Finished || beam.Position != impact || actor.Health != health ||
                    _entities.EntityAdapters<SwordBeamClinkRoomEntity>().Count() != clinks + (exhausted ? 0 : 1),
                    "The next eligible beam update must delete before movement, creating a clink only if its native slot allocation succeeds.");
                Step(10);
            }
            if (exhausted)
            {
                Step(20);
                FailIf(!_entities.InteractionSlotAvailable || _entities.EntityAdapters<SwordBeamClinkRoomEntity>().Any(),
                    "Releasing the exhausted interaction pool must not retry a deleted beam's failed clink.");
            }
            FailIf(adapter.ApplyItemCollision(RoomEntityItemCollision.Bomb, actor.CollisionBounds, actor.Position, 99, []),
                "Spark/Whisp active-collision masks exclude bomb column$18.");
            FailIf(!adapter.ApplyItemCollision(RoomEntityItemCollision.ThrownObject, actor.CollisionBounds, actor.Position, 99, []) ||
                actor.Health != health || actor.InvincibilityCounter != 0 || actor.KnockbackCounter != 0,
                "Thrown-object column$16 must report contact with no Spark/Whisp damage or recoil.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
