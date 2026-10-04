using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkAirProjectileContactRom()
    {
        int hostCase = 0;
        foreach (int jumpUpdates in new[] { 1, 15, 29 })
        foreach (bool primary in new[] { true, false })
        foreach (bool batched in RomHostSchedules(hostCase++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6aa, 0x20); save.WriteWramByte(0xc6ab, 0x30);
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Feather, 1);
            _inventory.EquipA(primary ? TreasureId.Feather : 0); _inventory.EquipB(primary ? 0 : TreasureId.Feather);
            for (int y = 8; y < 128; y += 16)
            for (int x = 8; x < 160; x += 16) _currentRoom.SetPositionTileAndCollision(new(x, y), 0x2c, 0, 0);
            _player.WarpTo(new(80, 64)); _player.Face(Vector2I.Right);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 1, 80, 64) { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            ZoraFireProjectile? shot = null;
            int update = 0;
            void Step(int count = 1, bool feather = false)
            {
                int pressed = feather ? primary ? 1 : 2 : 0;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(pressed), MenuRomActions(pressed), batched, () =>
                {
                    rom.UpdateGameplay(pressed, pressed, 0xff, _entities.FrameCounter); pressed = 0;
                    string context = $"Air PART$19 contact jump={jumpUpdates} A={primary} batch={batched} update={++update}";
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        (_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e) || _player.TopDownAirborne != ((rom[0xcc5c] & 15) != 0) ||
                        _player.KnockbackFrames != rom[0xd02d] || _player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]) ||
                        _player.HealthQuarters != rom[0xc6aa] ||
                        (_player.PendingContactDamageRaw & 0xff) != rom[0xd025] || _player.NativeContactSignal != (rom[0xd02a] != 0),
                        context + $": Link motion/air/contact counters/health/pending damage differ: runtime={_player.PrecisePosition}/{_player.ItemCreationZFixed:x4}/{_player.HealthQuarters}, native={rom.Word(0xd00c):x4},{rom.Word(0xd00a):x4}/{rom.Word(0xd00e):x4}/{rom[0xc6aa]}.");
                    if (shot is not null)
                    {
                        bool alive = rom[0xd0c0] != 0;
                        FailIf(shot.Finished == alive || alive && (shot.State != rom[0xd0c4] || shot.Counter != rom[0xd0c6] ||
                            shot.Position != new Vector2(rom[0xd0cd], rom[0xd0cb])), context + ": PART lifetime/flight state differs.");
                    }
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds), context + ": full cue order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": shared RNG differs.");
                });
            }
            for (int repeat = 0; repeat < (jumpUpdates == 15 ? 1 : 2); repeat++)
            {
                Step(1, feather: true); Step(jumpUpdates - 1);
                Vector2 start = _player.Position + Vector2.Right * 6;
                shot = _entities.Spawn<ZoraFireProjectile>(new ZoraFireSpawn(start));
                rom[0xd0c0] = 1; rom[0xd0c1] = 0x19; rom[0xd0cb] = (byte)start.Y; rom[0xd0cd] = (byte)start.X;
                int health = _player.HealthQuarters;
                Step(4);
                FailIf((_player.HealthQuarters < health) != (jumpUpdates != 15),
                    "Native PART$19 must hit near-ground Feather air and reject its high arc through the actual post-object pass.");
                if (jumpUpdates == 15) continue; // Bounded high-air rejection; flight is covered separately.
                for (int wait = 0; (rom[0xcc5c] != 0 || rom[0xd02d] != 0 || rom[0xd02b] != 0 || rom[0xd0c0] != 0) && wait < 40; wait++) Step();
                FailIf(rom[0xcc5c] != 0 || rom[0xd02d] != 0 || rom[0xd02b] != 0 || rom[0xd0c0] != 0,
                    "Air PART$19 contact must complete before repeated jump/slot reuse.");
                Step();
            }
        }
        GD.Print("Validated clean-US actual PART$19 contact after Feather updates 1/15/29: A/B near-ground damage and high-air rejection, exact post-object pending bytes and next-Link health commit, recoil/gravity/landing, full cues/RNG and repeated use/slot reuse through split/batched gameplay.");
    }
}
