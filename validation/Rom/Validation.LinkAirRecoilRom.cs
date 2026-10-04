using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkAirRecoilRom()
    {
        FieldInfo heartCounter = typeof(Player).GetField("_heartRingDistanceFixed", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int hostCase = 0;
        foreach (int ring in new[] { (int)RingId.HeartL1, (int)RingId.HeartL2, (int)RingId.Steadfast })
        foreach (int jumpUpdates in new[] { 1, 15, 29 })
        foreach (int angle in new[] { 8, 4, 24 })
        foreach (bool batched in RomHostSchedules(hostCase++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6aa, 0x14); save.WriteWramByte(0xc6ab, 0x30);
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.RingBox, 1); _inventory.GrantAppraisedRingForDebug(ring);
            FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                $"Could not equip airborne recoil ring ${ring:x2}.");
            _inventory.GiveTreasure(TreasureId.Feather, 1);
            bool primary = jumpUpdates != 15;
            _inventory.EquipA(primary ? TreasureId.Feather : 0); _inventory.EquipB(primary ? 0 : TreasureId.Feather);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0x2c,
                    x is 0 or 9 || y is 0 or 7 ? (byte)0x0f : (byte)0, 0);
            _player.WarpTo(new(80.25f, 64.5f)); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(_player.Position), "Air recoil must begin in reachable open floor geometry.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 1, 80, 64);
            rom.Word(0xd00a, 0x4080); rom.Word(0xd00c, 0x5040); rom[0xd009] = 0xff;
            rom.InitializeLinkGameplay();
            var geometry = new ObjectCollisionRom();
            var sounds = _sound.AttachPlayRequestAudit();
            int previousHeld = 0, update = 0;
            void Step(int count = 1, int direction = 0xff, bool feather = false)
            {
                Vector2 movement = direction == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(direction);
                int held = (movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0) |
                    (movement.Y < 0 ? 0x40 : movement.Y > 0 ? 0x80 : 0) | (feather ? primary ? 1 : 2 : 0);
                int edge = held & ~previousHeld; previousHeld = held;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, direction, _entities.FrameCounter); edge = 0;
                    string context = $"Air recoil ring=${ring:x2} jump={jumpUpdates} angle=${angle:x2} batch={batched} update={++update}";
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        (_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e) ||
                        (_player.TopDownAirSpeedZ & 0xffff) != rom.Word(0xd014) ||
                        _player.TopDownAirborne != ((rom[0xcc5c] & 15) != 0),
                        context + $": fixed XY/Z/gravity/air differs: runtime={_player.PrecisePosition}/{_player.ItemCreationZFixed:x4}/{_player.TopDownAirSpeedZ:x4}, native={rom.Word(0xd00c):x4},{rom.Word(0xd00a):x4}/{rom.Word(0xd00e):x4}/{rom.Word(0xd014):x4}.");
                    FailIf(_player.KnockbackFrames != rom[0xd02d] || _player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]) ||
                        _player.HealthQuarters != rom[0xc6aa] || (int)heartCounter.GetValue(_player)! != (rom.Word(0xcc53) | rom[0xcc55] << 16),
                        context + ": recoil/invincibility, health or Heart Ring movement eligibility differs.");
                    FailIf(_saveData.ReadWramByte(0xc65f) != rom[0xc65f] || _saveData.ReadWramByte(0xc660) != rom[0xc660] ||
                        _inventory.HasTreasure(TreasureId.HeartRefill) != ((rom[0xc69f] & 2) != 0), context + ": refill treasure/maturity differs.");
                    // The declared contact caller and HUD interpolation have
                    // separate fixtures. All ensuing jump/landing cues execute.
                    FailIf(!sounds.Requests.Where(id => id is not (SoundId.SndText or SoundId.SndGainHeart or SoundId.SndDamageLink))
                        .SequenceEqual(rom.Sounds.Where(id => id != SoundId.SndDamageLink)), context + ": movement/item cue order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": shared RNG differs.");
                });
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                int travel = (angle + repeat * 16) & 31, recoil = (travel + 16) & 31;
                Step(1, travel, feather: true); Step(jumpUpdates - 1, travel);
                FailIf(!_player.TopDownAirborne, "Declared air-contact boundary must still be in its actual Feather arc.");
                foreach (int difference in new[] { -8, -7, 6, 7 })
                {
                    geometry.ClearObjects();
                    for (int offset = 0; offset < 0x40; offset++) geometry[0xd000 + offset] = rom[0xd000 + offset];
                    geometry[0xcc5c] = rom[0xcc5c];
                    geometry[0xd0e4] = 0x99; geometry[0xd0e5] = 0; // PART $19, original no-op collision effect.
                    geometry[0xd0cb] = rom[0xd00b]; geometry[0xd0cd] = rom[0xd00d];
                    geometry[0xd0cf] = unchecked((byte)(rom[0xd00f] + difference));
                    geometry[0xd0e6] = geometry[0xd0e7] = 2;
                    geometry.Call(ObjectCollisionRom.Scan);
                    Rect2 bounds = new(_player.Position - Vector2.One * 2, Vector2.One * 4);
                    FailIf(_player.OverlapsEnemyCollision(bounds, unchecked((sbyte)geometry[0xd0cf])) != (geometry.Dispatches.Count != 0),
                        $"Air contact jump={jumpUpdates} ring=${ring:x2}: native Z boundary {difference} differs.");
                }
                int boundary = ((ring == (int)RingId.HeartL2 ? 3 : 2) << 16) - 1;
                heartCounter.SetValue(_player, boundary);
                rom[0xcc53] = (byte)boundary; rom[0xcc54] = (byte)(boundary >> 8); rom[0xcc55] = (byte)(boundary >> 16);
                FailIf(!_player.ApplyEnemyContactDamage(_player.Position - OracleObjectMovement.Shared.Direction(recoil) * 16, 1),
                    "Air recoil must enter through the shared accepted-contact gate.");
                rom[0xd02a] = 0x80; rom[0xd02b] = 0x22; rom[0xd02c] = (byte)recoil; rom[0xd02d] = 0x0f;
                rom.ApplyLinkDamage(0xfe); rom[0xd02a] = 0;
                Step(2, recoil);
                _dialogue.ShowMessage("Air recoil pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(3, recoil); _dialogue.Close(); rom[0xcba0] = 0;
                for (int wait = 0; ((rom[0xcc5c] & 15) != 0 || rom[0xd02d] != 0 || rom[0xd02b] != 0) && wait < 40; wait++) Step(direction: recoil);
                FailIf((rom[0xcc5c] & 15) != 0 || rom[0xd02d] != 0 || rom[0xd02b] != 0,
                    "Air recoil/landing/invincibility must finish within the bounded native window.");
                Step(2, travel); Step();
            }
        }
        GD.Print("Validated clean-US Feather contact after updates 1/15/29, A/B, cardinal/diagonal retained air momentum plus recoil, gravity and exact decrement/underflow/landing recovery, Heart L1/L2 and Steadfast, dialogue, treasure effects/RNG/cues and repeated split/batched gameplay.");
    }
}
