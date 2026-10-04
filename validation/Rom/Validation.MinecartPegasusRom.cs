using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMinecartPegasusRom()
    {
        Vector2I[] directions = [Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left];
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int ring in new[] { 0xff, 0x11 })
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x00); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            _inventory.GiveTreasure(TreasureId.PegasusSeeds, 0x20);
            _inventory.SelectSatchelSeeds(2);
            _inventory.EquipA(primary ? TreasureId.SeedSatchel : 0);
            _inventory.EquipB(primary ? 0 : TreasureId.SeedSatchel);
            if (ring != 0xff)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1);
                _inventory.GrantAppraisedRingForDebug(ring);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                    "Could not equip Pegasus Ring $11 in the minecart fixture.");
            }
            Vector2 center = new(88, 72);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0, 0, 0);
            byte track = (byte)(direction % 2 == 0 ? 0x5e : 0x5d);
            _currentRoom.SetPositionTileAndCollision(center, track, 0, 0);
            _currentRoom.SetPositionTileAndCollision(center + (Vector2)directions[direction] * 16, track, 0, 0);
            _currentRoom.SetPositionTileAndCollision(center - (Vector2)directions[direction] * 16, 0x5f, 0, 0);
            MinecartRuntimeState.Reset(_runtimeState, []);
            MinecartRuntimeState.BeginRide(_runtimeState, 0, 0x00, center, direction);
            var cart = new MinecartRoomEntity(new ActiveMinecart(-1, 0x00, 72, 88, direction, true),
                _currentRoom, new DungeonInteractionDatabase(), _runtimeState,
                new DungeonInteractionVisualDatabase().Visual("minecart"), _sound.PlaySound);
            _entities.AddEntity(cart);
            _player.WarpTo(center + new Vector2(0.25f, 0.5f)); _player.Face(directions[direction]);
            _player.FinishMinecartMount(center, direction, 0);
            var randomStart = _random.CaptureState();
            var rom = new SomariaRom(_saveData, randomStart, _currentRoom, direction, 88, 72)
                { CompanionDispatchEnabled = true };
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            rom[0xd100] = 3; rom[0xd101] = 0x0a;
            rom[0xd108] = (byte)direction; rom[0xd109] = (byte)(direction * 8);
            rom[0xd10b] = 72; rom[0xd10d] = 88;
            // Execute state0 separately: it clears Pegasus before the first
            // Link update. The tested activation occurs on an initialized cart.
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, update = 0, previousHeld = 0;
            void Step(int count = 1, bool satchel = false)
            {
                int held = satchel ? button : 0, pressed = held & ~previousHeld; previousHeld = held;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    rom.UpdateGameplay(pressed, held, 0xff, _entities.FrameCounter - 1); pressed = 0; update++;
                    string context = $"Minecart Pegasus A={primary} ring=${ring:x2} direction={direction} update={update} batch={batched}";
                    FailIf(_inventory.PegasusSeeds != rom[0xc6bb] || _seedSatchel.Pegasus.RawCounter != rom.Word(0xcc6c) ||
                        _player.IsUsingSeedSatchel || Enumerable.Range(0xd2, 4).Any(page =>
                            rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x19), context + ": consumption/timer/parent differs.");
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f) ||
                        _player.MinecartRideActive != (rom[0xcc2c] == 0xd1) ||
                        _player.TopDownAirborne != (rom[0xcc5c] != 0) ||
                        (_player.SwitchHookZFixed & 0xffff) != rom.Word(0xd00e), context + ": rider XY/Z/air/ownership differs.");
                    var dust = _entities.Entities<PegasusDustRoomEntity>();
                    bool nativeDust = rom[0xdf00] != 0 && rom[0xdf01] == 0x1a;
                    FailIf((dust.Count == 1) != nativeDust, context + ": reserved dust allocation/deletion differs.");
                    if (nativeDust)
                        FailIf(dust[0].Substate != rom[0xdf05] || dust[0].Position != new Vector2(rom[0xdf0d], rom[0xdf0b]) ||
                            dust[0].Visible != ((rom[0xdf1a] & 0x80) != 0) || dust[0].TileBase != rom[0xdf1d] ||
                            dust[0].OamFlags != rom[0xdf1c] ||
                            !dust[0].Clouds.ToArray().SequenceEqual(Enumerable.Range(0, 8).Select(index => rom[0xdf30 + index])),
                            context + ": dust state/position/animation/clouds differs.");
                    for (int address = 0xcd80; address < 0xcdc0; address++)
                        FailIf(_runtimeState.ReadWramByte(address) != rom[address], context + $": static byte ${address:x4} differs.");
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds), context + ": sounds differ.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - randomStart.Calls != rom.RandomCalls, context + ": RNG differs.");
                });
            }
            Step(1, true);
            FailIf(_inventory.PegasusSeeds != 0x19 || _seedSatchel.Pegasus.RawCounter != 0x03c0,
                "Initialized minecart must permit Pegasus Satchel activation and one BCD seed consumption.");
            Step(7, true); Step(); Step(1, true);
            FailIf(_inventory.PegasusSeeds != 0x19, "An active minecart timer must reject repeat seed consumption.");
            _dialogue.ShowMessage("Minecart Pegasus pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(6, true); _dialogue.Close(); rom[0xcba0] = 0;
            Step(38, true);
            FailIf(!cart.Dismounting || !_seedSatchel.Pegasus.Active,
                "Minecart must retain the active timer into its airborne platform dismount.");
            Step(48, true);
            FailIf(_player.MinecartJumpActive || !_seedSatchel.Pegasus.Active,
                "Platform landing must preserve the Pegasus timer activated aboard.");
            int decrement = ring == 0x11 ? 1 : 2;
            int remaining = _seedSatchel.Pegasus.RawCounter & 0x7fff;
            Step((remaining + decrement - 1) / decrement - 1);
            FailIf(!_seedSatchel.Pegasus.Active, "Dismounted Pegasus timer must retain its last native decrement.");
            Step(); Step(4);
            FailIf(_seedSatchel.Pegasus.Active, "Dismounted Pegasus timer must expire exactly at zero.");
            Step(1, true); Step(8);
            FailIf(_inventory.PegasusSeeds != 0x18 || !_seedSatchel.Pegasus.Active,
                "Fresh input must permit Pegasus reactivation after the minecart timer expires.");
        }
        GD.Print("Validated clean-US minecart Pegasus Satchel A/B activation, Pegasus Ring, active rejection, retained ride/dismount/landing timer, exact expiry/reactivation, reserved dust/clouds, dialogue, rider/static bytes and sounds/RNG through split/batched gameplay.");
    }
}
