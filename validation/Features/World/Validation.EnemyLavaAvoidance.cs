using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEnemyLavaAvoidance()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var random = CaptureOracleRandomForValidation();
        foreach (bool moldorm in new[] { false, true })
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count)
            {
                input.CaptureForValidation([], [], Vector2.Zero);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60, update);
            }
            RestoreOracleRandomForValidation(random);
            // Locate a real floor/lava edge, retaining the room's collision
            // data. The right-facing source probes are (x+6,y-1/y+5).
            Vector2? start = null;
            int roomId = -1;
            foreach (int id in moldorm ? new[] { 0x6d, 0x6f, 0x86, 0x92 }
                : new[] { 0x72, 0x76, 0x7c, 0x7e, 0x86, 0x88, 0x8c, 0x90 })
            {
                var room = _world.LoadRoom(4, id);
                for (int y = 24; y < room.Height - 16 && start is null; y++)
                for (int x = 32; x < room.Width - 16 && start is null; x += 16)
                {
                    bool Lava(int px, int py) => room.GetTerrainInfo(new(px, py)).Hazard == HazardType.Lava;
                    bool Floor(int px, int py) => !room.IsSolid(new(px, py)) &&
                        room.GetTerrainInfo(new(px, py)).Hazard == HazardType.None;
                    if (Lava(x, y - 1) && Lava(x, y + 5) &&
                        Enumerable.Range(x - 20, 20).All(px => Enumerable.Range(y - 4, 12).All(py => Floor(px, py))))
                    { start = new Vector2(x - 7, y); roomId = id; }
                }
                if (start is not null) break;
            }
            FailIf(start is null, $"D4 {(moldorm ? "Moldorm" : "Stalfos")} rooms must provide an actual clear approach to lava.");
            LoadValidationRoom(4, roomId);
            Step(4);
            EnemyCharacter enemy = moldorm ? _entities.Entities<MoldormCharacter>()[0]
                : _entities.Entities<StalfosCharacter>()[0];
            enemy.Position = start!.Value;
            if (moldorm)
            {
                typeof(MoldormCharacter).GetField("_preciseHeadPosition", flags)!.SetValue(enemy, start.Value);
                typeof(MoldormCharacter).GetField("_angle", flags)!.SetValue(enemy, 0x08);
                typeof(MoldormCharacter).GetField("_turnCounter", flags)!.SetValue(enemy, 8);
            }
            else
            {
                typeof(StalfosCharacter).GetField("_state", flags)!.SetValue(enemy, StalfosState.Walking);
                typeof(StalfosCharacter).GetField("_angle", flags)!.SetValue(enemy, 0x08);
                typeof(StalfosCharacter).GetField("_counter1", flags)!.SetValue(enemy, 8);
            }
            int calls = _random.Calls;
            Step(4);
            int angle = moldorm ? ((MoldormCharacter)enemy).Angle : ((StalfosCharacter)enemy).Angle;
            // Source SPEED_100 ($28) moves Moldorm 1px; SPEED_80 ($14)
            // moves Stalfos 0.5px. Reflection $08->$18 happens before speed.
            Vector2 expected = start.Value - new Vector2(moldorm ? 2 : 0, 0);
            FailIf(angle != 0x18 || enemy.Position != expected || enemy.IsDead || enemy.DiedInHazard,
                $"4:{roomId:x2} {(moldorm ? "Moldorm" : "Stalfos")} must bounce before lava: angle=${angle:x2}, position={enemy.Position}, expected={expected}, batch={batch}.");
            FailIf(_random.Calls != calls,
                "The lava reflection must not add RNG consumption or restart the walking state.");
            LoadValidationRoom(4, 0x91);
            FailIf(_entities.Entities<MoldormCharacter>().Count != 0 || _entities.Entities<StalfosCharacter>().Count != 0,
                "Leaving the lava room retained the outgoing enemies.");
        }
    }
}
