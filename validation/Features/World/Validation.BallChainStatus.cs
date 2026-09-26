using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBallChainStatus()
    {
        // bank0.enemyStandardUpdate: JUST_HIT wins; then counter&$7f wins
        // over health, decrementing the byte only for nonzero low bits.
        // enemyCode4b runs ordinary AI for statuses04/05, without recoil.
        // The high-bit cases exercise the native byte contract directly;
        // ordinary ENEMYDMG tables do not publish high-bit counters.
        foreach (int counter in new[] { 0x00, 0x01, 0x0b, 0x7f, 0x80, 0x81, 0xff })
        foreach (bool lethal in new[] { false, true })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            var soldier = new BallChainSoldierCharacter();
            soldier.Initialize(new EnemyDatabase().ImportedEnemy(EnemyId.BallAndChainSoldier), Room060MovementFixture(), new(64, 64), new());
            Vector2 link = new(64, 100);
            void Step() => soldier.UpdateFrame(link, link, 4, () => { });
            Step();
            Step();
            FailIf(soldier.State != 9 || soldier.Counter1 != 90,
                "Soldier status fixture must enter the source windup state.");
            typeof(EnemyCharacter).GetProperty("KnockbackCounter", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(soldier, counter);
            soldier.Health = lethal ? 0 : 8;
            soldier.MarkContact();
            Step();
            FailIf(soldier.IsDead || soldier.KnockbackCounter != counter || soldier.Counter1 != 89,
                "JUST_HIT must run AI without consuming recoil or handling zero health.");
            int low = counter & 0x7f;
            for (int update = 0; update < low; update++)
            {
                Step();
                FailIf(soldier.IsDead || soldier.KnockbackCounter != counter - update - 1 ||
                    soldier.Position != new Vector2(64, 64),
                    "Each recoil-status update must retain the soldier and consume one low-bit tick without recoil motion.");
            }
            Step();
            FailIf(soldier.IsDead != lethal || !lethal && soldier.KnockbackCounter != (counter & 0x80),
                "The update after low-bit expiry must handle death or preserve the high bit during ordinary AI.");
            soldier.Free();
        }
    }
}
