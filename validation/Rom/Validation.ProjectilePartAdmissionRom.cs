using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateProjectilePartAdmissionRom()
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        foreach (int id in new[] { 0x09, 0x0a })
        foreach (int free in new[] { 0, 1, 2 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(24, 24)); _player.BeginCutsceneControl();
            Vector2 start = (from y in Enumerable.Range(3, 4) from x in Enumerable.Range(2, 6)
                let point = new Vector2(x * 16 + 8, y * 16 + 8)
                where _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None &&
                    Enumerable.Range(-8, 17).All(dx => Enumerable.Range(-8, 17).All(dy =>
                        !_currentRoom.IsSolid(point + new Vector2(dx, dy)))) select point).First();
            FailIf(!_entities.TrySpawnEnemy(id, 0, start, "PART admission regression", out string error), error);
            var enemy = _entities.Entities<EnemyCharacter>().Single();
            StepGameplayUpdates(1, Vector2.Zero);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 24, 24);
            native[0xd080] = 1; native[0xd081] = (byte)id;
            native[0xd08b] = (byte)start.Y; native[0xd08d] = (byte)start.X;
            native.AdvanceEnemies(_entities.FrameCounter);
            // This fixture starts at an initialized shooting boundary. Common
            // state-zero initialization/RNG fidelity is a separate contract.
            var initial = _random.CaptureState();
            native[0xff94] = initial.Rng1; native[0xff95] = initial.Rng2;
            for (int index = 0; index < 16 - free; index++)
            {
                _entities.Spawn<BlueEnergyBeadRoomEntity>(new BlueEnergyBeadSpawn(index % 8, new(120, 40), 0xff));
                int at = 0xd0c0 + index * 256;
                native[at] = 1; native[at + 1] = 0x53; native[at + 3] = (byte)(index % 8);
                native[at + 6] = 0xff; native[at + 0x30] = 40; native[at + 0x31] = 120;
            }
            void StageThrow()
            {
                // Declare the next terminal decision on the same original
                // clear floor; this is not a whole roaming-cycle comparison.
                enemy.Position = start;
                native.Word(0xd08a, (int)start.Y * 256); native.Word(0xd08c, (int)start.X * 256);
                native[0xd084] = (byte)(id == 9 ? 0x0b : 9);
                if (enemy is OctorokCharacter oct)
                {
                    oct.SetStateForValidation(OctorokState.Shooting, counter1: 2, angle: 8);
                    native[0xd086] = 2; native[0xd089] = 8;
                }
                else
                {
                    typeof(BoomerangMoblinCharacter).GetField("_state", hidden)!.SetValue(enemy, BoomerangMoblinCharacterMoblinState.Deciding);
                    var prediction = new OracleRandom(); prediction.RestoreState(_random.CaptureState()); prediction.Next();
                    int angle = prediction.NextCardinalAngle();
                    Vector2 target = enemy.Position + OracleObjectMath.StrictCardinalVector(angle) * 32;
                    _player.WarpTo(target);
                    native[0xd00b] = (byte)target.Y; native[0xd00d] = (byte)target.X;
                }
            }
            int shotId = id == 9 ? 0x18 : 0x21;
            int observations = 0;
            int soundStart = _sound.PlayRequestsFor(SoundId.SndThrow);
            int nativeSoundStart = native.Sounds.Count(sound => sound == 0x51);
            void Compare()
            {
                // updateAllObjects publishes these before updateEnemies.
                native[0xffb0] = (byte)_player.Position.Y; native[0xffb1] = (byte)_player.Position.X;
                native.AdvanceEnemies(_entities.FrameCounter);
                native.AdvanceParts(_entities.FrameCounter);
                observations++;
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95],
                    $"ENEMY${id:x2} PART admission RNG differs, free={free}, update={observations}, batch={batched}.");
                int state = enemy is OctorokCharacter oct ? (int)oct.State : (int)((BoomerangMoblinCharacter)enemy).State;
                FailIf(state != native[0xd084],
                    $"ENEMY${id:x2} allocation result must preserve native state: runtime=${state:x2}, ROM=${native[0xd084]:x2}, free={free}.");
                if (enemy is OctorokCharacter octorok)
                    FailIf(octorok.Counter1 != native[0xd086], "Octorok failed shots must retain the native $20-update wait.");
                int runtimeShots = id == 9 ? _entities.Entities<OctorokRockProjectile>().Count : _entities.Entities<MoblinBoomerangProjectile>().Count;
                int nativeShots = Enumerable.Range(0, 16).Count(slot => native[0xd0c0 + slot * 256] != 0 && native[0xd0c1 + slot * 256] == shotId);
                FailIf(runtimeShots != nativeShots,
                    $"PART${shotId:x2} admission differs: free={free}, runtime={runtimeShots}, ROM={nativeShots}, update={observations}.");
                for (int slot = 0; slot < 16; slot++)
                {
                    var actor = _entities.ResolvePartSlot(slot);
                    int at = 0xd0c0 + slot * 256;
                    FailIf((actor is null) != (native[at] == 0), "Projectile admission must retain native first-free PART ownership.");
                    if (actor?.Node is OctorokRockProjectile rock)
                        FailIf(native[at + 1] != 0x18 || rock.Position != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f),
                            "Admitted Octorok rock must initialize in its actual PART pass and then move.");
                    if (actor?.Node is MoblinBoomerangProjectile boomerang)
                        FailIf(native[at + 1] != 0x21 || boomerang.Counter != native[at + 6] ||
                            boomerang.Position != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f),
                            "Admitted Moblin boomerang must initialize and advance its native counter/movement.");
                }
                FailIf(_sound.PlayRequestsFor(SoundId.SndThrow) - soundStart != native.Sounds.Count(sound => sound == 0x51) - nativeSoundStart,
                    "Octorok SND_THROW must occur only after successful PART admission.");
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            StageThrow();
            Step(id == 9 ? 2 : 1); // Includes the update before Octorok's terminal counter.
            if (free > 0)
                FailIf(native[0xd0c1 + (16 - free) * 256] != shotId,
                    $"Successful ENEMY${id:x2} throw must allocate the first available PART slot.");
            Step(2);
            if (free == 0)
            {
                _entities.RetireEntity(_entities.ResolvePartSlot(5)!);
                for (int offset = 0; offset < 64; offset++) native[0xd5c0 + offset] = 0;
                // Capacity release alone must not retry either failed request.
                Step(1);
                StageThrow(); Step(id == 9 ? 2 : 1);
                FailIf(native[0xd5c1] != shotId,
                    $"A later ENEMY${id:x2} throw must reuse PART$d5: id=${native[0xd5c1]:x2}, state=${native[0xd084]:x2}, target={_player.Position}, enemy={enemy.Position}, nativeXY={native[0xd08d]}/{native[0xd08b]}, angle={native[0xd089]}, direction={native[0xd088]}.");
                Step(2);
            }
            _entities.Clear();
            FailIf(_entities.Entities<OctorokRockProjectile>().Count != 0 || _entities.Entities<MoblinBoomerangProjectile>().Count != 0,
                "Room cancellation must retire admitted projectiles.");
        }
        ReinitializeGameplayForValidation();
        GD.Print("Validated clean-US Octorok/Moblin PART capacity, failure state, success sound and release/repeat.");
    }
}
