using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullZolCycles()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var animation = typeof(EnemyCharacter).GetField("_animation", flags)!;
        var remaining = typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!;
        int AnimationRemaining(ZolCharacter zol) => (int)remaining.GetValue(animation.GetValue(zol))!;
        var originalRandom = CaptureOracleRandomForValidation();
        foreach (bool batch in new[] { false, true })
        foreach (int subid in new[] { 0, 1 })
        {
            void Step(int count = 1, Vector2 movement = default)
            {
                input.CaptureForValidation([], [], movement);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60, update);
            }
            RestoreOracleRandomForValidation(originalRandom);
            LoadValidationRoom(4, 0x91);
            _player.WarpTo(new Vector2(120, 128));
            Step(16, Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112) || Enumerable.Range(74, 45).Any(y =>
                Enumerable.Range(114, 13).Any(x => _currentRoom.IsSolid(new Vector2(x, y)))),
                "Zol observation approach must walk through the real 4:91 floor.");
            _player.SetBraceletLiftCollisionsDisabled(true);
            FailIf(!_entities.TrySpawnEnemy(0x34, subid, new Vector2(120, 80), "Skull Zol lifecycle", out string error), error);
            var zol = _entities.Entities<ZolCharacter>().Single();
            int randomBefore = _random.Calls;
            FailIf(zol.State != ZolState.Uninitialized || zol.CollisionEnabled || zol.Visible,
                "Zol creation must leave native state0 pending until its ordered ENEMY update.");
            _sound.ClearPlayRequestAudit();
            Step();
            FailIf(_random.Calls != randomBefore + 1 || zol.State != (subid == 0 ? ZolState.GreenHidden : ZolState.RedWaiting) ||
                zol.Counter1 != (subid == 0 ? 0 : 24),
                "Zol state0 must consume one enemyStandardUpdate RNG call without dispatching state8 in the same update.");
            if (subid == 0)
            {
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    int sounds = cycle * 5;
                    Step();
                    FailIf(zol.State != ZolState.GreenEmerging || zol.Counter2 != 4 || AnimationRemaining(zol) != 16,
                        "Green Zol proximity must start emergence with four hops and its untouched first animation frame.");
                    Step(32);
                    FailIf(zol.AnimationParameter != 1 || zol.ZFixed != 0 || _sound.PlayRequestsFor(OracleSoundEngine.SndEnemyJump) != sounds,
                        "Zol's 16+16 emergence animation must publish parameter1 before movement or jump sound.");
                    Step();
                    FailIf(zol.ZFixed != -512 || _sound.PlayRequestsFor(OracleSoundEngine.SndEnemyJump) != sounds + 1,
                        "Green Zol must play its emergence sound once on the first native Z update.");
                    Step(26);
                    FailIf(zol.State != ZolState.GreenWaiting || zol.Counter1 != 48 || !zol.CollisionEnabled ||
                        _sound.PlayRequestsFor(OracleSoundEngine.SndEnemyJump) != sounds + 1,
                        "Green Zol emergence must land after27 gravity updates without replaying its sound.");
                    for (int hop = 0; hop < 4; hop++)
                    {
                        Step(47);
                        FailIf(zol.State != ZolState.GreenWaiting || zol.Counter1 != 1, "Green Zol wait ended before48 updates.");
                        Step();
                        FailIf(zol.State != ZolState.GreenHopping || AnimationRemaining(zol) != 126 ||
                            _sound.PlayRequestsFor(OracleSoundEngine.SndEnemyJump) != sounds + hop + 2,
                            $"Green Zol hop must play SND_ENEMY_JUMP and fall through to one Animate call after selecting animation2: state={zol.State}, timer={AnimationRemaining(zol)}, sounds={_sound.PlayRequestsFor(OracleSoundEngine.SndEnemyJump)}, cycle={cycle}, hop={hop}.");
                        Step(27);
                        FailIf(zol.Counter2 != 3 - hop || zol.ZFixed != 0 ||
                            zol.State != (hop == 3 ? ZolState.GreenDisappearing : ZolState.GreenWaiting),
                            "Green Zol must consume one hop on its27th native gravity update.");
                    }
                    Step(40);
                    FailIf(zol.State != ZolState.GreenDisappearing || zol.AnimationParameter != 1 || zol.CollisionEnabled,
                        "Green Zol disappearance must await its8+16+16 animation before underground recovery.");
                    Step(); Step(39);
                    FailIf(zol.State != ZolState.GreenGone || zol.Counter1 != 1 || zol.Visible,
                        "Green Zol must spend40 complete counter updates underground.");
                    Step();
                    FailIf(zol.State != ZolState.GreenHidden || AnimationRemaining(zol) != 16 || _random.Calls != randomBefore + 1,
                        "Green Zol recovery must restart animation0 and preserve the common RNG count across repeated cycles.");
                }
            }
            else
            {
                int hops = 0, slides = 0;
                for (int decision = 0; decision < 40; decision++)
                {
                    Step(23);
                    FailIf(zol.State != ZolState.RedWaiting || zol.Counter1 != 1, "Red Zol idle must last24 updates.");
                    var rng = _random.CaptureState();
                    int next = (rng.Rng1 + ((((rng.Rng2 << 8) | rng.Rng1) * 3 & 0xffff) >> 8)) & 255;
                    Step();
                    bool hop = (next & 7) == 0;
                    FailIf(_random.Calls != rng.Calls + 1 || _random.LastResult.Value != next ||
                        zol.State != (hop ? ZolState.RedShaking : ZolState.RedSliding) || zol.Counter1 != (hop ? 32 : 16),
                        "Red Zol must consume exactly one global RNG result and select the source1/8 hop branch.");
                    if (hop)
                    {
                        hops++;
                        FailIf(zol.AnimationIndex != 5 || zol.AnimationFrame != 0 || AnimationRemaining(zol) != 4,
                            "Red Zol must select its shake animation without advancing it on the decision update.");
                        Step(31); Step();
                        FailIf(zol.State != ZolState.RedHopping || AnimationRemaining(zol) != 127 ||
                            _sound.PlayRequestsFor(OracleSoundEngine.SndEnemyJump) != hops,
                            "Red Zol's32nd shake update must start its hop/sound without the green animation fallthrough.");
                        Step(27);
                    }
                    else { slides++; Step(16); }
                    FailIf(zol.State != ZolState.RedWaiting || zol.Counter1 != 24,
                        "Red Zol slide/hop must return to its full24-update idle.");
                }
                FailIf(hops == 0 || slides == 0, "Repeated red Zol cycles failed to exercise both native RNG branches.");
            }
        }
    }
}
