using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOctorokMovementMemoryRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int angle in new[] { 0x00, 0x08, 0x10, 0x18 })
        foreach (string gate in new[] { "text", "mask08", "mask80" })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(24, 24)); _player.BeginCutsceneControl();
            for (int y = 8; y < 128; y += 16)
            for (int x = 8; x < 160; x += 16) _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 24, 24);
            const int part = 0xd0c0;
            var start = new Vector2(80.75f, 72.5f);
            var rock = _entities.Spawn<OctorokRockProjectile>(new OctorokRockSpawn(start, angle));
            void NativeBirth()
            {
                native[part] = 1; native[part + 1] = 0x18; native[part + 9] = (byte)angle;
                native.Word(part + 0x0a, 0x4880); native.Word(part + 0x0c, 0x50c0);
            }
            NativeBirth();
            byte[] sentinel = [0xa5, 0x5a, 0xc3, 0x3c];
            byte[] observed = new byte[4];
            byte[] expected = sentinel;
            // Literal cardinal words from bank3.objectSpeedTable:
            // SPEED_200=$50, SPEED_40=$05. Bounce reverses the angle.
            int yFlight = angle == 0 ? -0x200 : angle == 0x10 ? 0x200 : 0;
            int xFlight = angle == 8 ? 0x200 : angle == 0x18 ? -0x200 : 0;
            byte[] Words(int y, int x) => [unchecked((byte)y), unchecked((byte)(y >> 8)),
                unchecked((byte)x), unchecked((byte)(x >> 8))];
            byte[] flight = Words(yFlight, xFlight), bounce = Words(-yFlight / 8, -xFlight / 8);
            int staged = 0, observations = 0, updates = 0;
            void AddProbes()
            {
                var stage = new PartMovementScratchProbe(() =>
                {
                    for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, sentinel[i]);
                    staged++;
                });
                _entities.RegisterEnemySlot(stage, 15); _entities.AddEntity(stage);
                _entities.AddEntity(new PartMovementScratchProbe(() =>
                {
                    for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i);
                    observations++;
                }));
            }
            AddProbes();
            var previousText = _entities.TextActiveSource;
            bool held = false, cancelled = false;
            _entities.TextActiveSource = () => held && gate == "text";
            void Gates(bool value)
            {
                held = value;
                native[0xcba0] = held && gate == "text" ? (byte)1 : (byte)0;
                native[0xcc8a] = held && gate == "mask08" ? (byte)8 : held && gate == "mask80" ? (byte)0x80 : (byte)0;
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, native[0xcc8a]);
            }
            void Step(int count)
            {
                StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    for (int i = 0; i < 4; i++) native[0xcec0 + i] = sentinel[i];
                    native.AdvanceParts(_entities.FrameCounter);
                    string context = $"PART $18 angle=${angle:x2}, {gate}, batch={batched}, update={++updates}";
                    FailIf(staged != updates || observations != updates,
                        context + ": scratch probes must bracket every actual PART pass, including frozen updates.");
                    FailIf(!Enumerable.Range(0, 4).All(i => native[0xcec0 + i] == expected[i]),
                        context + ": native scratch disagrees with the source-derived movement/no-movement boundary.");
                    FailIf(!Enumerable.Range(0, 4).All(i => observed[i] == native[0xcec0 + i]),
                        context + $": live movement memory differs: runtime={string.Join(' ', observed.Select(b => b.ToString("x2")))}, native={string.Join(' ', expected.Select(b => b.ToString("x2")))}.");
                    bool finished = native[part] == 0;
                    FailIf(cancelled ? _entities.Entities<OctorokRockProjectile>().Count != 0 : rock.Finished != finished,
                        context + ": deletion/cancellation boundary differs.");
                    if (finished) return;
                    int state = rock.State switch
                    {
                        HostileProjectileState.Flying => 1, HostileProjectileState.CollisionPending => 2,
                        HostileProjectileState.Bouncing => 3, _ => 0
                    };
                    FailIf(state != native[part + 4] || rock.Angle != native[part + 9] ||
                        rock.Counter != native[part + 6] || (rock.ZFixed & 0xffff) != native.Word(part + 0x0e) ||
                        rock.Position != new Vector2(native.Word(part + 0x0c) / 256f, native.Word(part + 0x0a) / 256f),
                        context + ": projectile pose, counter, direction or state differs.");
                });
            }
            try
            {
                Gates(false); Step(1); // State zero does not apply speed.
                expected = flight; Step(3);
                Gates(true); expected = sentinel; Step(2);
                Gates(false); expected = flight; Step(2);
                // Declared pending-collision boundary isolates the movement
                // connection from the separate tile/item collision findings.
                var lifecycle = SomariaPrivate<HostileProjectileLifecycle>(rock, "_lifecycle");
                F1Set(lifecycle, "<State>k__BackingField", HostileProjectileState.CollisionPending);
                native[part + 4] = 2;
                Gates(true); expected = sentinel; Step(2);
                Gates(false); Step(1);
                FailIf(rock.Counter != 0x20, "PART $18 pending bounce must install counter $20 without movement.");
                Gates(true); Step(2);
                Gates(false); expected = bounce; Step(31);
                FailIf(rock.Counter != 1 || rock.Finished, "PART $18 must retain the final live bounce update.");
                Gates(true); expected = sentinel; Step(2);
                Gates(false); Step(2); // Deletion, then no actor and no write.
                rock = _entities.Spawn<OctorokRockProjectile>(new OctorokRockSpawn(start, angle));
                NativeBirth(); Step(1);
                expected = flight; Step(2); // Reused allocation is bound too.
                _entities.Clear(); native[part] = 0; cancelled = true; AddProbes();
                expected = sentinel; Step(2);
            }
            finally { _entities.TextActiveSource = previousText; _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); }
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared Octorok PART $18 movement memory against original PART dispatch for 24 direction/gate/host cases: init, flight, freeze, declared pending collision, bounce, deletion, slot reuse and cancellation.");
    }
}
