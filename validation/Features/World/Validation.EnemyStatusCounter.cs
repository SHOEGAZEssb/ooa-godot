using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEnemyStatusCounter()
    {
        var counter = typeof(EnemyCharacter).GetProperty("KnockbackCounter",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var database = new EnemyDatabase();
        // Native-byte fixtures: enemyStandardUpdate tests counter&7f after
        // JUST_HIT, before health/stun. Ordinary damage tables write low bits.
        foreach (int value in new[] { 0x00, 0x80 })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            var gel = new GelCharacter();
            gel.Initialize(database.Gel, Room060MovementFixture(), new(64, 64), new());
            counter.SetValue(gel, value);
            gel.UpdateFrame(new(96, 64), Vector2I.Right, false);
            FailIf(gel.KnockbackCounter != value || gel.Counter1 != 15,
                "Gel must run ordinary AI without decrementing a zero-low-bit recoil byte.");
            gel.TakeSwordHit(99);
            gel.ApplySwordKnockback(new(48, 64), EnemyKnockbackStrength.Low);
            counter.SetValue(gel, value);
            gel.DeferNativeHitStatus();
            gel.UpdateFrame(new(96, 64), Vector2I.Right, false);
            FailIf(gel.IsDead || gel.KnockbackCounter != value || !gel.PendingKnockbackDeath,
                "JUST_HIT must retain a lethally hit Gel before its zero-low-bit death dispatch.");
            gel.UpdateFrame(new(96, 64), Vector2I.Right, false);
            FailIf(!gel.IsDead || gel.PendingKnockbackDeath,
                "Counter80 must allow deferred death on the next update, just like counter00.");
            gel.Free();

            var moblin = new ArrowMoblinCharacter();
            moblin.Initialize(database.ImportedEnemy(EnemyId.ArrowMoblin, 0), Room060MovementFixture(), new(64, 64), new());
            moblin.UpdateFrame(new(96, 64));
            counter.SetValue(moblin, value);
            moblin.ApplyBoomerangStun(2);
            Vector2 before = moblin.Position;
            moblin.UpdateFrame(new(96, 64), frameCounter: 1);
            FailIf(moblin.KnockbackCounter != value || moblin.StunCounter != 1 ||
                moblin.Position != new Vector2(((int)before.X ^ 1) + before.X % 1, before.Y),
                "Counter80 must allow the source odd-update stun decrement and X-bit shake without recoil motion.");
            moblin.Free();
        }
    }
}
