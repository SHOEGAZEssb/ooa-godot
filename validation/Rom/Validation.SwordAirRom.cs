using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwordAirRom()
    {
        int hostCase = 0;
        foreach (int jumpUpdates in new[] { 1, 15 })
        foreach (int angle in new[] { 4, 8 })
        foreach (bool primary in new[] { true, false })
        foreach (bool batched in RomHostSchedules(hostCase++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6aa, 0x20); save.WriteWramByte(0xc6ab, 0x30);
            InitializeTransientSession(save); LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Feather, 1); _inventory.GiveTreasure(TreasureId.Sword, 1);
            _inventory.EquipA(primary ? TreasureId.Sword : TreasureId.Feather);
            _inventory.EquipB(primary ? TreasureId.Feather : TreasureId.Sword);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0x2c,
                    x is 0 or 9 || y is 0 or 7 ? (byte)0x0f : (byte)0, 0);
            _player.WarpTo(new(80.25f, 64.5f)); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(_player.Position), "Sword air fixture must begin on reachable open floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 1, 80, 64);
            rom.Word(0xd00c, 0x5040); rom.Word(0xd00a, 0x4080); rom[0xd009] = 0xff;
            rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            int previousHeld = 0, update = 0;
            void Step(int count = 1, int direction = 0xff, bool feather = false, bool sword = false)
            {
                Vector2 movement = direction == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(direction);
                int held = (movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0) |
                    (movement.Y < 0 ? 0x40 : movement.Y > 0 ? 0x80 : 0) |
                    (feather ? primary ? 2 : 1 : 0) | (sword ? primary ? 1 : 2 : 0);
                int edge = held & ~previousHeld; previousHeld = held;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, direction, _entities.FrameCounter); edge = 0;
                    string context = $"Sword air jump={jumpUpdates} angle=${angle:x2} A={primary} batch={batched} update={++update}";
                    // linkState01 updates parents before linkUpdateInAir. Its
                    // rising momentum and gravity survive Sword immobilization;
                    // descending steering still belongs to linkUpdateVelocity.
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        (_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e) ||
                        (_player.TopDownAirSpeedZ & 0xffff) != rom.Word(0xd014) ||
                        _player.TopDownAirborne != ((rom[0xcc5c] & 15) != 0), context + ": fixed XY/Z/gravity/air differs.");
                    int parent = Enumerable.Range(0xd2, 3).Select(page => page << 8)
                        .FirstOrDefault(slot => rom[slot] != 0 && rom[slot + 1] == 5);
                    SwordActionState expected = parent == 0 ? SwordActionState.None : rom[parent + 4] switch
                    {
                        1 => SwordActionState.Swing, 2 => SwordActionState.Held,
                        _ => throw new InvalidOperationException(context + $": unsupported Sword parent state ${rom[parent + 4]:x2}.")
                    };
                    FailIf(_player.SwordState != expected, context + ": Sword parent state differs.");
                    Rect2 hitbox = _player.GetSwordHitbox();
                    bool collision = (rom[0xd624] & 0x80) != 0;
                    FailIf((hitbox.Size != Vector2.Zero) != collision, context + ": Sword child collision eligibility differs.");
                    if (collision)
                        FailIf(hitbox != new Rect2(new(rom[0xd60d] - rom[0xd627], rom[0xd60b] - rom[0xd626]),
                            new(2 * rom[0xd627], 2 * rom[0xd626])) || _player.SwordDamage != -unchecked((sbyte)rom[0xd628]),
                            context + ": Sword child geometry/damage differs.");
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds), context + ": cue order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": shared RNG differs.");
                });
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                int takeoff = (angle + repeat * 16) & 31, steering = (takeoff + 16) & 31;
                Step(1, takeoff, feather: true); Step(jumpUpdates - 1, takeoff);
                Step(1, steering, sword: true);
                FailIf(_player.SwordState != SwordActionState.Swing || !_player.TopDownAirborne,
                    "Ordinary Feather air must admit a fresh Sword parent through real A/B input.");
                _dialogue.ShowMessage("Sword air pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(3, steering, sword: true); _dialogue.Close(); rom[0xcba0] = 0;
                Step(4, steering);
                for (int wait = 0; (rom[0xcc5c] != 0 || Enumerable.Range(0xd2, 3).Any(page => rom[page << 8] != 0)) && wait < 40; wait++)
                    Step(direction: steering);
                FailIf(rom[0xcc5c] != 0 || _player.IsAttacking,
                    "Released Sword and Feather air must finish within their bounded native lifetime.");
                Step(2, takeoff); Step();
            }
        }
        GD.Print("Validated clean-US Sword A/B during rising/apex Feather air: cardinal/diagonal retained momentum, descending steering, full fixed motion/gravity, child collision/damage, dialogue, landing/reuse and cues/RNG through split/batched gameplay.");
    }
}
