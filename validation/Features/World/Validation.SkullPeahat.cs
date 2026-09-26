using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullPeahatCycle()
    {
        var profile = EnemyBehaviorTables.Shared.Peahat;
        FailIf(!profile.Speeds.Select(v => v.Value).SequenceEqual(new[] { 30,30,30,20,20,10,10,5,5 }) ||
            !profile.AnimationFrequencies.Select(v => v.Value).SequenceEqual(new[] { 255,255,255,0,0,1,3,7 }),
            "Peahat motion must retain the source SPEED_c0..SPEED_20 and ff/00/01/03/07 tables.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var frameField = typeof(RoomEntityManager).GetField("_enemyFrameCounter", flags)!;
        var animationField = typeof(EnemyCharacter).GetField("_animation", flags)!;
        var animationCounter = typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!;
        void Step(int count = 1, Vector2 movement = default) =>
            StepGameplayUpdates(count, movement, [], [], batched: true);
        int AnimationRemaining(PeahatCharacter p) => (int)animationCounter.GetValue(animationField.GetValue(p))!;
        var random = CaptureOracleRandomForValidation();
        var checkpoints = new List<(Vector2, int, int, int, int, int, int)>();
        foreach (bool batch in new[] { false, true })
        {
            RestoreOracleRandomForValidation(random);
            LoadValidationRoom(4, 0x7b);
            frameField.SetValue(_entities, 0);
            var peahats = _entities.Entities<PeahatCharacter>().ToArray();
            FailIf(peahats.Length != 2 || peahats[0].Position != new Vector2(200,72) ||
                peahats[1].Position != new Vector2(56,88), "Room 4:7b lost its two source-ordered Peahats.");
            Vector2? approach = null;
            for (int y = 32; y < _currentRoom.Height - 32 && approach is null; y += 8)
            for (int x = 32; x < _currentRoom.Width - 40 && approach is null; x += 8)
                if (Enumerable.Range(-6, 21).All(dx => Enumerable.Range(-6, 13).All(dy =>
                    !_currentRoom.IsSolid(new Vector2(x + dx, y + dy)) &&
                    _currentRoom.GetTerrainInfo(new Vector2(x + dx, y + dy)).Hazard == HazardType.None)))
                    approach = new Vector2(x,y);
            FailIf(approach is null, "Room 4:7b needs a clear physical Peahat observation approach.");
            _player.WarpTo(approach!.Value);
            _player.SetBraceletLiftCollisionsDisabled(true);
            int[] animationTicks = [0,0];
            int saved = 0;
            for (int tick = 0; tick < 720;)
            {
                int count = batch && tick >= 8 ? Math.Min(16, 720 - tick) : 1;
                var before = peahats.Select(p => (p.State, p.Counter, p.ZHigh)).ToArray();
                Step(count, tick < 8 ? Vector2.Right : Vector2.Zero);
                tick += count;
                if (!batch)
                {
                    int globalFrame = (int)frameField.GetValue(_entities)!;
                    for (int index = 0; index < peahats.Length; index++)
                    {
                        var p = peahats[index];
                        var old = before[index];
                        // enemyAnimation37457 alternates two four-update frames.
                        // peahat_updatePosition runs twice for ff, once when the
                        // global frame passes its mask, and never on state8 waits.
                        int calls = old.State switch
                        {
                            PeahatState.Uninitialized => 0,
                            PeahatState.Stationary => old.Counter == 1 ? 1 : 0,
                            PeahatState.Flying => 1,
                            PeahatState.Accelerating when old.Counter == 1 => 1,
                            PeahatState.Slowing when old.Counter == 127 => 1,
                            _ => AnimationCalls(p.Counter, globalFrame)
                        };
                        animationTicks[index] += calls;
                        FailIf(p.AnimationFrame != (animationTicks[index] / 4 & 1) ||
                            AnimationRemaining(p) != 4 - animationTicks[index] % 4 ||
                            p.CollisionMode != (old.ZHigh == 0 ? EnemyCollisionMode.PeahatVulnerable : EnemyCollisionMode.Peahat),
                            $"Room 4:7b Peahat {index} lost source animation/mode timing at update {tick}, state {old.State}->{p.State}, counter {old.Counter}->{p.Counter}, frame {globalFrame}, animation {p.AnimationFrame}/{AnimationRemaining(p)}, calls {animationTicks[index]}, mode {p.CollisionMode:x2}.");
                    }
                }
                if (tick >= 8 && ((tick - 8) % 16 == 0 || tick == 720))
                foreach (var p in peahats)
                {
                    var value = (p.Position, (int)p.State, p.Counter, p.ZHigh, p.CollisionMode, p.AnimationFrame, AnimationRemaining(p));
                    if (!batch) checkpoints.Add(value);
                    else FailIf(checkpoints[saved++] != value, $"Peahat diverged between single and batched updates at {tick}.");
                }
            }
            FailIf(_player.Position.X <= approach.Value.X, "Peahat observation must include actual movement through room geometry.");
        }

        // The source has no ecom_checkHazards call: even ground-height
        // acceleration ignores lava/hole tiles while its counter remains live.
        OracleRoomData room = _world.LoadRoom(4, 0x7b);
        Vector2? hazard = null;
        for (int y = 8; y < room.Height && hazard is null; y += 16)
        for (int x = 8; x < room.Width && hazard is null; x += 16)
            if (room.GetTerrainInfo(new Vector2(x,y)).Hazard != HazardType.None) hazard = new Vector2(x,y);
        FailIf(hazard is null, "Peahat hazard fixture needs a source room hazard.");
        var overHazard = new PeahatCharacter();
        overHazard.Initialize(new EnemyDatabase().ImportedEnemy(EnemyId.Peahat), room, hazard!.Value, new OracleRandom());
        for (int i = 0; i < 60; i++) overHazard.UpdateFrame(i);
        FailIf(overHazard.IsDead || overHazard.DiedInHazard || overHazard.State != PeahatState.Accelerating ||
            overHazard.Counter != 69 || overHazard.ZHigh != 0, "Peahat incorrectly entered a hazard handler while grounded.");
        overHazard.Free();

        static int AnimationCalls(int counter, int frame)
        {
            int band = counter >> 4;
            if (band <= 2) return 2;
            int mask = band switch { 3 or 4 => 0, 5 => 1, 6 => 3, 7 => 7, _ => throw new InvalidOperationException() };
            return (frame & mask) == 0 ? 1 : 0;
        }
    }
}
