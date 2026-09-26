using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownWallFollowerMelee()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool whisp in new[] { false, true })
        {
            LoadValidationRoom(4, whisp ? 0x9f : 0xa8);
            _inventory.GiveTreasure(TreasureId.Sword, 0);
            _inventory.SetScriptedEquippedItems(TreasureId.None, TreasureId.Sword);
            for (int row = 2; row <= 5; row++)
            for (int column = 1; column <= 4; column++)
                _currentRoom.SetPositionTileAndCollision(new(column * 16 + 8, row * 16 + 8), 0xa0, 0, 0);
            _player.WarpTo(new(40, 56));
            _player.Face(Vector2I.Right);
            StepGameplayUpdates(1, Vector2.Zero);
            EnemyCharacter actor = whisp ? _entities.Entities<WhispCharacter>().First()
                : _entities.Entities<SparkCharacter>().First();
            IRoomEntity adapter = whisp ? _entities.EntityAdapters<WhispRoomEntity>().First()
                : _entities.EntityAdapters<SparkRoomEntity>().First();
            void Place(Vector2 point)
            {
                actor.Position = point;
                if (!whisp) typeof(SparkCharacter).GetField("_precisePosition", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(actor, point);
            }
            int health = actor.Health;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Place(new(200, 136));
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                StepGameplayUpdates(17, Vector2.Zero, ["attack"], batched: batched);
                FailIf(_player.SwordState != SwordActionState.Held, "Wall-follower melee fixture must reach a held sword.");
                // Hit the sword's outer edge so normal enemy motion cannot
                // immediately damage Link and cancel the resulting poke.
                Place(_player.GetSwordHitbox().GetCenter() + Vector2.Right * 4);
                StepGameplayUpdates(1, Vector2.Zero, ["attack"]);
                FailIf(_player.SwordState != SwordActionState.Held || actor.Health != health ||
                    !((IPostObjectMeleeCollisionRoomEntity)adapter).MeleeReportsContact,
                    "Immune sword contact must publish after objects, retaining held state and enemy health this update.");
                StepGameplayUpdates(1, Vector2.Zero, ["attack"]);
                FailIf(_player.SwordState != SwordActionState.Poke || actor.Health != health ||
                    actor.InvincibilityCounter != 0 || actor.KnockbackCounter != 0,
                    $"Spark/Whisp contact must retract the held sword next update without enemy damage or recoil " +
                    $"(whisp={whisp}, repeat={repeat}, sword={_player.SwordState}, hp={actor.Health}/{health}, " +
                    $"invinc={actor.InvincibilityCounter}, knockback={actor.KnockbackCounter}, enemy={actor.Position}, link={_player.Position}).");
                Place(new(200, 136));
                StepGameplayUpdates(14, Vector2.Zero, batched: batched);
            }
            foreach (var (state, level, expected) in new[]
            {
                (SwordActionState.Swing, 1, true), (SwordActionState.Swing, 2, true),
                // sword.s:@state4 writes $08 at every level; Spark/Whisp exclude it.
                (SwordActionState.Swing, 3, true), (SwordActionState.Spin, 1, false),
                (SwordActionState.Spin, 2, false), (SwordActionState.Held, 1, true)
            })
            {
                ((ILinkSwordStateAwareRoomEntity)adapter).SetLinkSwordState(state, level);
                bool contact = ((ISwordHittableRoomEntity)adapter).ApplySwordHit(actor.CollisionBounds, actor.Position,
                    99, EnemyKnockbackStrength.Normal, []);
                FailIf(contact != expected || ((IPostObjectMeleeCollisionRoomEntity)adapter).MeleeReportsContact != expected ||
                    actor.Health != health, "Spark/Whisp melee masks and effect$1c must preserve health even for high damage.");
            }
            FailIf(((IExpertPunchHittableRoomEntity)adapter).ApplyExpertPunch(actor.CollisionBounds, actor.Position, 99, []),
                "Spark/Whisp mask excludes Expert Punch column$0b.");
            // Exercise the formerly wrong level-1 spin through the complete
            // player -> post-object melee path, twice and with host batching.
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Place(new(200, 136));
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                StepGameplayUpdates(17, Vector2.Zero, ["attack"], batched: batched);
                StepGameplayUpdates(41, Vector2.Zero, ["attack"], batched: batched);
                FailIf(_player.SwordState != SwordActionState.Charged,
                    "Level-1 spin fixture must reach the source $28 charge underflow.");
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(_player.SwordState != SwordActionState.Spin,
                    "Releasing charge must enter the source $08 sword collision state.");
                Place(_player.GetSwordHitbox().GetCenter() + Vector2.Right * 4);
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(_player.SwordState != SwordActionState.Spin || actor.Health != health ||
                    ((IPostObjectMeleeCollisionRoomEntity)adapter).MeleeReportsContact,
                    "Level-1 spin must use excluded collision $08, not $07, against Spark/Whisp.");
                Place(new(200, 136));
                StepGameplayUpdates(23, Vector2.Zero, batched: batched);
                FailIf(_player.SwordState != SwordActionState.None,
                    "Completed spin must retire before the repeat action.");
            }
        }
        LoadValidationRoom(0, 0x60);
    }
}
