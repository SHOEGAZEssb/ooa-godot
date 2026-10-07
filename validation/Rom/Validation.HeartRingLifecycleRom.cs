using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHeartRingLifecycleRom()
    {
        ValidateHeartRingLifecycleRom(walls: false);
        foreach (int ring in new[] { 0x13, 0x14 })
        {
            ValidateRaftControlRom(false, true, heartRing: ring);
            ValidateMinecartMountGameplayRom(false, heartRing: ring);
        }
        ValidateHeartRingCompanionRom();
    }

    private void ValidateHeartRingWallSlideRom()
        => ValidateHeartRingLifecycleRom(walls: true);

    private void ValidateHeartRingLifecycleRom(bool walls)
    {
        FieldInfo counter = typeof(Player).GetField("_heartRingDistanceFixed", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int hostCase = 0;
        foreach (int ring in new[] { (int)RingId.HeartL1, (int)RingId.HeartL2, (int)RingId.Friendship })
        foreach (int angle in walls ? new[] { 8, 12, 16 } : new[] { 8, 24, 4 })
        foreach (bool airborne in walls ? new[] { false } : new[] { false, true })
        foreach (byte tile in walls ? new byte[] { 0x2c } : new byte[] { 0x2c, 0xf8, 0x8a, 0xfa, 0x54, 0xa0 })
        foreach (byte collision in walls ? new byte[] { 0x03, 0x0c, 0x0f, 0x11, 0x1a } : new byte[] { 0 })
        foreach (bool batched in RomHostSchedules(hostCase++))
        {
            if (tile != 0x2c && (ring != (int)RingId.HeartL2 ||
                (angle != 8 && (tile != 0xf8 || angle != 4)) || airborne)) continue;
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6aa, 0x14); save.WriteWramByte(0xc6ab, 0x30);
            InitializeTransientSession(save);
            LoadValidationRoom(tile == 0xa0 ? 2 : tile is 0x8a or 0x54 ? 4 : 0,
                tile == 0xa0 ? 0x90 : tile == 0x8a ? 0xa8 : tile == 0x54 ? 0x00 : 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.RingBox, 1);
            _inventory.GrantAppraisedRingForDebug(ring);
            FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                $"Could not equip Heart Ring fixture ${ring:x2}.");
            _inventory.GiveTreasure(TreasureId.Feather, 1); _inventory.EquipA(TreasureId.Feather); _inventory.EquipB(0);
            if (tile == 0xfa) _inventory.GiveTreasure(TreasureId.Flippers, 1);
            if (tile == 0xa0) _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            for (int y = 8; y < _currentRoom.Height; y += 16)
            for (int x = 8; x < _currentRoom.Width; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), tile, 0, 0);
            if (walls) _currentRoom.SetPositionTileAndCollision(new(40, 40), 0x2c, collision, 0);
            Vector2 start = walls ? new(28, 24) : new(80.25f, 64.5f);
            _player.WarpTo(start); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(start), "Heart Ring movement must start in reachable open geometry.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 1, (int)start.X, (int)start.Y);
            rom.Word(0xd00a, (int)(start.Y * 256)); rom.Word(0xd00c, (int)(start.X * 256));
            rom[0xd009] = 0xff; rom.InitializeLinkGameplay();
            if (tile == 0xa0)
            {
                // Underwater var2f/velocity must come from the original room
                // initialization, rather than an injected Mermaid state.
                rom[0xd004] = 0; rom[0xd009] = 0;
                rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            }
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, previousHeld = 0;
            void SeedCounter(int value)
            {
                // Declared counter boundary avoids a 512/768-pixel playthrough.
                counter.SetValue(_player, value);
                rom[0xcc53] = (byte)value; rom[0xcc54] = (byte)(value >> 8); rom[0xcc55] = (byte)(value >> 16);
            }
            void Step(int count = 1, int movement = 0xff, bool feather = false)
            {
                Vector2 vector = movement == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(movement);
                int directions = (vector.X > 0 ? 0x10 : vector.X < 0 ? 0x20 : 0) |
                    (vector.Y < 0 ? 0x40 : vector.Y > 0 ? 0x80 : 0);
                int held = directions | (feather ? 1 : 0);
                int edge = held & ~previousHeld; previousHeld = held;
                StepGameplayUpdates(count, vector, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, movement, _entities.FrameCounter); edge = 0;
                    int nativeCounter = rom.Word(0xcc53) | rom[0xcc55] << 16;
                    string context = $"Heart Ring ${ring:x2} tile=${tile:x2} collision=${collision:x2} angle=${angle:x2} air={airborne} batch={batched} update={++update}";
                    FailIf((int)counter.GetValue(_player)! != nativeCounter || _inventory.HealthQuarters != rom[0xc6aa],
                        context + $": distance/heal differs: runtime={(int)counter.GetValue(_player)!:x6}/${_inventory.HealthQuarters:x2} XY={_player.PrecisePosition}, native={nativeCounter:x6}/${rom[0xc6aa]:x2} XY={rom.Word(0xd00c) / 256.0f},{rom.Word(0xd00a) / 256.0f}.");
                    FailIf(_saveData.ReadWramByte(0xc65f) != rom[0xc65f] || _saveData.ReadWramByte(0xc660) != rom[0xc660] ||
                        _inventory.HasTreasure(TreasureId.HeartRefill) != ((rom[0xc69f] & 2) != 0),
                        context + ": Heart refill treasure flag/Gasha maturity differs.");
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f) ||
                        (_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e), context + ": Link fixed motion/height differs.");
                    FailIf(_player.KnockbackFrames != rom[0xd02d] ||
                        _player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]), context + ": recoil/invincibility boundary differs.");
                    // HUD interpolation and the declared contact caller are
                    // separate owners; compare only ensuing movement/item cues.
                    FailIf(!sounds.Requests.Where(id => id is not (SoundId.SndText or SoundId.SndGainHeart or SoundId.SndDamageLink))
                        .SequenceEqual(rom.Sounds.Where(id => id != SoundId.SndDamageLink)),
                        context + $": item cue order differs: runtime={string.Join(',', sounds.Requests)}, native={string.Join(',', rom.Sounds)}.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - seed.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                });
            }
            int threshold = (ring == (int)RingId.HeartL2 ? 3 : 2) << 16;
            if (walls)
            {
                SeedCounter(threshold - 1); Step(24, angle);
                SeedCounter(threshold - 1); Step(6, angle); Step(3);
                Step(8, (angle + 16) & 31); Step(8, angle);
                continue;
            }
            if (tile == 0xa0)
            {
                Step(6); SeedCounter(threshold - 1);
                Step(6, angle); Step(12); // Released Mermaid impulses omit C bit2.
                Step(6, 24); Step(12);
            }
            if (tile == 0x8a)
            {
                FailIf(_currentRoom.GetTerrainInfo(_player.Position).Type != TerrainType.Ice,
                    "Heart Ring coasting fixture must use the native dungeon ice tile $8a.");
                Step(12, angle); SeedCounter(threshold - 1);
                Step(3); // Ice still moves Link, but released input omits C bit2.
            }
            SeedCounter(threshold - 0x100);
            Step(2); // Idle retains the counter, including with a different ring.
            if (airborne) Step(1, angle, feather: true);
            Step(3, angle);
            _dialogue.ShowMessage("Heart Ring pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(3, angle); _dialogue.Close(); rom[0xcba0] = 0;
            Step(airborne ? 32 : 3, angle == 24 ? 8 : 24);
            Step();
            SeedCounter(threshold - 1); Step(1, angle); Step(3);
            SeedCounter(0xffffff); Step(1, angle); Step();
            _inventory.RefillHealth(); rom[0xc6aa] = rom[0xc6ab];
            SeedCounter(threshold - 0x100); Step(1, angle); Step(3);
            if (tile == 0x2c && ring != (int)RingId.Friendship && angle == 8 && !airborne)
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    // Supply the shared accepted-contact boundary. Link's
                    // original handler owns every ensuing recoil update.
                    SeedCounter(threshold - 1);
                    FailIf(!_player.ApplyEnemyContactDamage(_player.Position + Vector2.Left * 16, 1),
                        "Heart Ring repeated contact must pass the normal damage gate.");
                    rom[0xd02a] = 0x80; rom[0xd02b] = 0x22; rom[0xd02c] = 8; rom[0xd02d] = 0x0f;
                    rom.ApplyLinkDamage(0xfe);
                    rom[0xd02a] = 0;
                    Step(4, 24);
                    _dialogue.ShowMessage("Heart Ring recoil pause.", _player.Position.Y); rom[0xcba0] = 1;
                    Step(3, 24); _dialogue.Close(); rom[0xcba0] = 0;
                    Step(11, 24); Step(20); Step(2, 24);
                }
        }
        GD.Print(walls
            ? "Validated clean-US Heart Ring L1/L2 and other-ring cardinal/diagonal partial-wall approach, accepted sliding/blocked axes, distance/healing, treasure/maturity, idle and reapproach through split/batched gameplay."
            : "Validated clean-US top-down Heart Ring L1/L2 and other-ring movement gates, signed/cardinal/diagonal fixed distance, exact refill/reset and 24-bit overflow, full health, treasure flags/Gasha maturity, Feather air/landing, ice coasting, swimming, underwater impulse/coasting, recoil/recovery, conveyor displacement, dialogue and repeat through split/batched gameplay.");
    }
}
