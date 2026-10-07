using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePegasusGatesRom()
    {
        int fixture = 0;
        // Independent bits, combined native masks, and nonzero palette bytes;
        // no Cartesian product with equipment, directions or host schedules.
        foreach (var gate in new (int Mask, int Palette, int Ring)[] { (0, 0, 0xff), (1, 0, 0xff),
            (2, 0, 0xff), (4, 0, 0xff), (8, 0, 0xff), (0x10, 0, 0xff), (0x20, 0, 0xff),
            (0x40, 0, 0xff), (0x80, 0, 0xff), (0x81, 0, 0xff), (0x90, 0, 0xff), (0xff, 0, 0xff),
            (0, 1, 0xff), (0, 0x80, 0xff), (0, 0, 0x11) })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x91); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            _inventory.GiveTreasure(TreasureId.PegasusSeeds, 0x20); _inventory.SelectSatchelSeeds(2);
            if (gate.Ring == 0x11)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1); _inventory.GrantAppraisedRingForDebug(0x11);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, 0x11) || !_inventory.EquipRingAt(0),
                    "Could not equip Pegasus Ring $11 for the odd-counter boundary.");
            }
            int button = (fixture & 1) == 0 ? 1 : 2;
            bool instrumentGate = gate.Mask is 1 or 0x80 || gate.Palette != 0;
            if (instrumentGate) { EnsureHarpAndSongs(); _inventory.SelectHarpSong(1); }
            _inventory.EquipA(button == 1 ? TreasureId.SeedSatchel : instrumentGate ? TreasureId.Harp : 0);
            _inventory.EquipB(button == 2 ? TreasureId.SeedSatchel : instrumentGate ? TreasureId.Harp : 0);
            _player.WarpTo(new(120, 128)); StepGameplayUpdates(16, Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112) || _currentRoom.IsSolid(_player.Position),
                "Pegasus mask fixture must approach through room $4:$91's entrance.");
            _player.Face(Vector2I.Up);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 120, 112);
            rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            var maskEvent = new PegasusMaskEvent();
            var eventsField = typeof(RoomEventController).GetField("_eventsByPriority", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var events = (IRoomEvent[])eventsField.GetValue(_roomEvents)!;
            eventsField.SetValue(_roomEvents, events.Append(maskEvent).ToArray());
            Func<bool> palette = _entities.PaletteFadeActiveSource;
            Func<bool> disabled = _entities.InitializedObjectsDisabledSource;
            bool gated = false, forced = false;
            // Existing owner inputs stand for the declared mask publication.
            // Link state01 reads $81; updateItems reads $90 and palette mode.
            // ITEM $df keeps state zero and bypasses all initialized-item gates.
            _entities.PaletteFadeActiveSource = () => gated && gate.Palette != 0;
            _entities.InitializedObjectsDisabledSource = () => gated && (gate.Mask & 0x90) != 0;
            int update = 0;
            void SetGate(bool active)
            {
                gated = active; maskEvent.Mask = active ? gate.Mask : 0;
                rom[0xcc8a] = (byte)(maskEvent.Mask | (rom[0xd500] != 0 ? 0x7e : 0));
                rom[0xc4ab] = active ? (byte)gate.Palette : (byte)0;
            }
            void Step(int count = 1, bool press = false, bool instrument = false)
            {
                int held = (press ? button : 0) | (instrument ? button ^ 3 : 0), edge = held;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, 0xff, _entities.FrameCounter); edge = 0;
                    string context = $"Pegasus mask=${gate.Mask:x2} palette=${gate.Palette:x2} gated={gated} update={++update}";
                    FailIf(_seedSatchel.Pegasus.RawCounter != rom.Word(0xcc6c) || _inventory.PegasusSeeds != rom[0xc6bb],
                        context + ": timer/pulse/ammo differs.");
                    FailIf(!forced && _player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f),
                        context + ": retained Link position differs.");
                    ComparePegasusDustRom(rom, false, context);
                    FailIf(_player.IsUsingHarp != (rom[0xd500] != 0) || _harp.PlayingInstrument != rom[0xcc8d],
                        context + ": retained instrument parent/publication differs.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), context + ": dust sound order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": shared RNG differs.");
                });
            }
            try
            {
                SetGate(true); Step(3, true); SetGate(false); Step(); Step(1, true);
                FailIf(!_seedSatchel.Pegasus.Active, "Fresh eligible Pegasus input must activate after mask release.");
                // Freeze both initial animation and established cloud slots.
                SetGate(true); Step(8, true); SetGate(false); Step(20);
                if (instrumentGate) Step(2, instrument: true);
                SetGate(true); Step(8, true); SetGate(false); Step(3);
                int remaining = 280;
                while (rom[0xd500] != 0 && remaining-- > 0) Step();
                FailIf(rom[0xd500] != 0, "Released Pegasus mask retained the Harp beyond its native completion.");
                // Source decPegasusSeedCounter retains a pulse from either
                // decrement, then clears on the exact zero-counter update.
                _entities.RuntimeState.SetWramByte(WramAddress.wPegasusSeedCounter, 0x11);
                _entities.RuntimeState.SetWramByte(WramAddress.wPegasusSeedCounter + 1, 0);
                rom.Word(0xcc6c, 0x11); Step();
                FailIf(_seedSatchel.Pegasus.RawCounter != (gate.Ring == 0x11 ? 0x8010 : 0x800f),
                    "Native odd Pegasus decrement must retain the first decrement's dust pulse.");
                Step(gate.Ring == 0x11 ? 16 : 8);
                FailIf(_seedSatchel.Pegasus.Active, "Pegasus must expire on its capped zero decrement.");
                Step(4); Step(1, true); Step(4);
                // A published state $0b request returns from normal Link.
                // Initialization clears Pegasus on the following update;
                // the source caller owns subsequent forced movement itself.
                forced = true; _player.BeginForcedRoomEntryMovement(Vector2I.Up, deferInitialization: true);
                rom[0xcc4f] = 0x0b; rom[0xcc51] = 4;
                int retained = _seedSatchel.Pegasus.RawCounter;
                Step(); FailIf(_seedSatchel.Pegasus.RawCounter != retained, "Forced-walk request cleared Pegasus before state $0b initialization.");
                Step(); FailIf(_seedSatchel.Pegasus.Active, "State $0b initialization must clear Pegasus.");
                Step(3); _player.EndForcedRoomEntryMovement();
            }
            finally
            {
                _entities.PaletteFadeActiveSource = palette;
                _entities.InitializedObjectsDisabledSource = disabled;
                eventsField.SetValue(_roomEvents, events);
            }
        }
        GD.Print("Validated native Pegasus independent/combined object masks, nonzero palette modes, rejected/fresh activation, continuing startup/cloud dust, odd pulse, exact expiry and repeat through split/batched gameplay.");
    }
}
