using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSmasherUnlinkedExpiry()
    {
        var actor = new SmasherCharacter();
        var random = new OracleRandom();
        actor.InitializePending(new EnemyDatabase().ImportedEnemy(EnemyId.Smasher, 0),
            Room060MovementFixture(), new(120, 88), random, 15);
        int initializations = 0, allocations = 0;
        try
        {
            for (int frame = 1; frame <= 359; frame++)
                actor.UpdateInitializationFrame(frame, () => initializations++,
                    () => { allocations++; return null; }, interactionSlotAvailable: false);
            FailIf(actor.State != 0 || actor.ExpirationCounter != 179 ||
                initializations != 359 || allocations != 359 || random.Calls != 359,
                "Smasher state$00 must retain 179 even timer ticks before update360.");
            actor.UpdateInitializationFrame(360, () => initializations++,
                () => { allocations++; return null; }, interactionSlotAvailable: false);
            FailIf(actor.State != 0x0d || actor.ExpirationCounter != 0 ||
                initializations != 359 || allocations != 359 || random.Calls != 360 ||
                actor.Visible || !actor.CollisionEnabled,
                "Smasher expiry must select state$0d before room setup and return on failed puff allocation.");
            int puffs = 0;
            for (int frame = 361; frame <= 364; frame++)
                actor.UpdateNormalFrame(Vector2.Zero, frame, _ => { puffs++; return false; },
                    () => throw new InvalidOperationException("Unlinked ball cannot begin the miniboss."), _ => { });
            FailIf(actor.State != 0x0d || actor.ExpirationCounter != 0 || puffs != 4 || random.Calls != 360,
                "Disappearing Smasher must retry a full interaction pool without property RNG or timer updates.");
            actor.UpdateNormalFrame(Vector2.Zero, 365, _ => true, () => { }, _ => { });
            FailIf(actor.State != 14 || actor.Counter1 != 60 || actor.Visible || actor.CollisionEnabled,
                "Clean-US unlinked stateD must hide the ball, disable collision and start60 updates after successful puff allocation.");
            for (int frame = 366; frame < 425; frame++)
                actor.UpdateNormalFrame(Vector2.Zero, frame, _ => false, () => { }, _ => { });
            FailIf(actor.State != 14 || actor.Counter1 != 1 || actor.Visible,
                "Unlinked ball must remain hidden for the full60-update respawn delay.");
            actor.UpdateNormalFrame(Vector2.Zero, 425, _ => false, () => { }, _ => { });
            FailIf(actor.State != 15 || !actor.Visible || actor.ZFixed >> 8 != -32 || random.Calls != 361,
                "Unlinked ball respawns with one RNG call even if its second puff cannot allocate.");
            for (int update = 0; update < 200 && actor.State != 9; update++)
                actor.UpdateNormalFrame(Vector2.Zero, 1, _ => true, () => { }, _ => { });
            FailIf(actor.State != 9, "Unlinked respawn must bounce and become grabbable again.");
            actor.BeginGrab();
            actor.UpdateNormalFrame(Vector2.Zero, 1, _ => true, () => { }, _ => { }, setLinkGrabState: _ => { });
            // Independent clean-ROM probe: null relatedObj1 reads invincibility
            // at$002b, then Enemy offsets at$008b/$008d/$008f/$00a6/$00a7.
            foreach (var (x, y, z, expectedAngle) in new[]
                { (80,80,-22,-1), (200,100,0,-1), (163,100,-22,-1), (164,100,-22,26), (200,100,-22,29) })
            {
                actor.Position = new(x, y);
                actor.CopyCarriedPosition(new(x, y), z);
                actor.ReleaseGrab(ObjectAngle.Right);
                int sounds = 0, angle = -1;
                for (int repeat = 1; repeat <= 2; repeat++)
                {
                    actor.UpdateNormalFrame(Vector2.Zero, 1, _ => true, () => { }, sound =>
                    {
                        FailIf(sound != SoundId.SndBossDamage, "Unlinked hit must use SND_BOSS_DAMAGE.");
                        sounds++;
                    }, setReservedItemAngle: value => angle = value);
                    FailIf(sounds != (expectedAngle < 0 ? 0 : repeat) ||
                        expectedAngle >= 0 && angle != expectedAngle || actor.Health != 5 || actor.InvincibilityCounter != 0,
                        "Unlinked release must match clean-ROM bounds/Z, reserved-angle reversal and repeated hits without stored enemy damage.");
                }
            }
        }
        finally { actor.QueueFree(); }

        var immediate = new SmasherCharacter();
        immediate.InitializePending(new EnemyDatabase().ImportedEnemy(EnemyId.Smasher, 0),
            Room060MovementFixture(), new(120, 88), new OracleRandom(), 15);
        try
        {
            int puffs = 0, attempts = 0;
            for (int frame = 1; frame <= 360; frame++)
                immediate.UpdateInitializationFrame(frame, () => { },
                    () => { attempts++; return null; }, createInitializationPuff: position =>
                    {
                        FailIf(position != new Vector2(120, 88), "Uninitialized expiry puff must use the original position before ball X offset.");
                        puffs++;
                        return true;
                    });
            FailIf(puffs != 1 || attempts != 359 || immediate.State != 14 ||
                immediate.Counter1 != 60 || immediate.Visible || immediate.CollisionEnabled,
                "Available puff allocation must complete unlinked disappearance on update360 without another parent-allocation attempt.");
        }
        finally { immediate.QueueFree(); }
    }
}
