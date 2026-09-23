using Godot;
using System.Collections.Generic;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogLargeCloud()
    {
        var record = new EnemyDatabase().ImportedEnemy(0x7c,0);
        SmogCharacter Create(int phase, int random, System.Func<int,byte> collision)
        {
            var actor = new SmogCharacter();
            actor.InitializeMergedCloud(record,3,phase,new(72,72),0,collision);
            for (int i = 0; i < 5; i++) actor.UpdateMergedInitialization(2,_ => FailIf(true,"Large promotion cannot write interaction counter."), (_,_) => { }, () => random);
            return actor;
        }
        var large = Create(0,3,_ => 0);
        var shots = new List<Vector2>();
        int draws = 0;
        try
        {
            for (int tick = 1; tick <= 242; tick++)
            {
                var before = large.NativePosition;
                int counter = large.Counter1;
                large.UpdateLargeCloud(new(200,72),shots.Add,() => { draws++; return 3; });
                if (tick == 1) FailIf(large.Speed != 0 || large.Counter1 != 19 || large.Angle != 8 || large.NativePosition != before,
                    "Large Smog first update must aim once, clear speed and fall through to the first of20 speed-counter updates.");
                if (tick == 20) FailIf(large.Speed != 5 || large.Counter1 != 20 || large.Position != new Vector2(72,72),
                    "Large Smog's first speed increase occurs after20 zero-speed movements.");
                if (tick == 120) FailIf(large.Speed != 0x1e || large.LargeSubstate != 2 || large.Position != new Vector2(109,72),
                    "Large Smog reaches SPEED_c0 after six20-update acceleration stages, retaining its fractional position.");
                if (tick is 195 or 206) FailIf(large.NativePosition != before || large.Counter1 != counter,
                    "Large Smog projectile and terminal-animation branches must skip movement and speed countdown.");
                if (tick == 195) FailIf(shots.Count != 1 || shots[0] != before.PixelPosition + new Vector2(0,8) || draws != 0 || large.AnimationParameter != 0x80,
                    "Large Smog timer150 plus45 animation updates must emit one large projectile at Y+8 without resetting RNG yet.");
                if (tick == 206) FailIf(large.AnimationIndex != 4 || large.ProjectileCounter != 150 || draws != 1,
                    "Large terminal parameter1 resets idle and timer without decrementing the newly drawn timer.");
            }
            FailIf(large.NativePosition != OracleObjectPosition.FromPixels(new(162,72)) || large.Speed != 0 || large.LargeSubstate != 0 || shots.Count != 1,
                "Large Smog's240 movement updates plus two animation-branch pauses must finish its speed cycle at exactly X162.");
            large.UpdateLargeCloud(new(0,72),shots.Add,() => 3);
            FailIf(large.Angle != 24 || large.Speed != 0 || large.Counter1 != 19,
                "Large Smog must reacquire its target only when starting the next speed cycle.");
        }
        finally { large.Free(); }
        foreach (byte collision in new byte[] { 0,1,0x0f,0x10,0xff })
        {
            large = Create(0,3,_ => collision);
            try
            {
                large.UpdateLargeCloud(new(200,72),_ => { },() => 3);
                FailIf(large.Angle != (collision == 0xff ? 24 : 8),
                    $"Large Smog boundary probes must accept only raw$ff, not collision${collision:x2}.");
            }
            finally { large.Free(); }
        }
        large = Create(3,0,_ => 0);
        shots.Clear(); draws = 0;
        try
        {
            for (int tick = 0; tick < 55; tick++) large.UpdateLargeCloud(new(200,72),shots.Add,() => { draws++; return 0; });
            var stopped = large.NativePosition;
            int counter = large.Counter1;
            large.DisableCollision(); large.PublishCollision(0xa0);
            for (int tick = 1; tick <= 70; tick++)
            {
                large.UpdateLargeCloud(new(0,72),shots.Add,() => { draws++; return 0; });
                FailIf(large.Counter2 != 70-tick || large.NativePosition != stopped || large.Counter1 != counter ||
                    large.ProjectileCounter != 0 || shots.Count != 0 || draws != 0 || large.ContactFlags != 0x20 ||
                    large.LargeSubstate != (tick == 70 ? 0 : 3) || large.CollisionEnabled != (tick == 70),
                    "Smog shock handler must animate but suspend motion/shooting/timers for70 updates and re-enable collision only at completion.");
            }
            FailIf(large.AnimationParameter != 1, "Shock must allow the firing animation to pass its projectile frame and reach its held terminal parameter.");
            large.UpdateLargeCloud(new(0,72),shots.Add,() => { draws++; return 0; });
            FailIf(large.Speed != 0 || large.Counter1 != 20 || large.Angle != 24 || large.AnimationIndex != 4 ||
                large.ProjectileCounter != 20 || draws != 1 || shots.Count != 0,
                "Post-shock resume must re-aim/reset speed then consume terminal parameter without replaying the skipped projectile frame.");
        }
        finally { large.Free(); }
        GD.Print("Validated isolated large Smog acceleration/deceleration, animation-branch pauses, projectile offset, raw boundary bounce and70-update shock behavior.");
    }
}
