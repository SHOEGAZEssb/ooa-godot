using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBombchuMasksRom()
    {
        int fixture = 0;
        foreach (var gate in new (int Mask, int Palette)[] { (0, 0), (1, 0), (2, 0), (4, 0),
            (8, 0), (0x10, 0), (0x20, 0), (0x40, 0), (0x80, 0), (0x81, 0), (0x90, 0), (0xff, 0), (0, 1), (0, 0x80) })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            var rom = PrepareSomariaMotionRom(0, primary: true);
            _inventory.GiveTreasure(TreasureId.Bombchus, 0x10); EquipSomariaMotionItem(rom, TreasureId.Bombchus, true);
            rom[0xc6b3] = 0x10; rom.InitializeLinkWalkingAnimation();
            var maskEvent = new PegasusMaskEvent();
            var eventsField = typeof(RoomEventController).GetField("_eventsByPriority", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var events = (IRoomEvent[])eventsField.GetValue(_roomEvents)!;
            eventsField.SetValue(_roomEvents, events.Append(maskEvent).ToArray());
            Func<bool> palette = _entities.PaletteFadeActiveSource, disabled = _entities.InitializedObjectsDisabledSource;
            bool gated = false;
            _entities.PaletteFadeActiveSource = () => gated && gate.Palette != 0;
            _entities.InitializedObjectsDisabledSource = () => gated && (gate.Mask & 0x90) != 0;
            var seed = _random.CaptureState(); var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            void SetGate(bool active)
            {
                gated = active; maskEvent.Mask = active ? gate.Mask : 0;
                rom[0xcc8a] = (byte)maskEvent.Mask; rom[0xc4ab] = active ? (byte)gate.Palette : (byte)0;
            }
            void Compare()
            {
                string context = $"Bombchu mask=${gate.Mask:x2}, palette=${gate.Palette:x2}, active={gated}, batch={batched}, update={++update}";
                var parent = _entities.BombchuParent;
                FailIf(parent.Active != (rom[0xd300] != 0) || parent.Active &&
                    (parent.Counter != rom[0xd320] || parent.Parameter != rom[0xd321]), context + ": parent clock/eligibility differs.");
                var item = _entities.Entities<BombchuItem>().SingleOrDefault();
                FailIf((item is not null) != (rom[0xd700] != 0) || _inventory.Bombchus != rom[0xc6b3],
                    context + ": physical allocation/ammo differs.");
                if (item is not null)
                    FailIf(item.ItemState != rom[0xd704] || item.Counter2 != rom[0xd707] ||
                        item.Position != new Vector2(rom[0xd70d], rom[0xd70b]), context +
                        $": state-zero admission or initialized freeze runtime={item.ItemState}/{item.Counter2}/{item.Position}, " +
                        $"native={rom[0xd704]}/{rom[0xd707]}/({rom[0xd70d]},{rom[0xd70b]}) differs.");
                CompareSomariaMotionRom(rom, context);
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls - seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.SequenceEqual(rom.Sounds), context + ": cue/RNG order differs.");
            }
            void Step(int count = 1, bool press = false) =>
                StepSomariaMotionRom(rom, count, batched, 0xff, press ? 1 : 0, press ? 1 : 0, Compare);
            try
            {
                SetGate(true); Step(3, true); SetGate(false); Step(); Step(1, true);
                SetGate(true); Step(8); SetGate(false); Step(12);
                _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems(); Step();
                Step(1, true); SetGate(true); Step(3); SetGate(false); Step(8);
            }
            finally
            {
                _entities.PaletteFadeActiveSource = palette; _entities.InitializedObjectsDisabledSource = disabled;
                eventsField.SetValue(_roomEvents, events);
            }
        }
        GD.Print("Compared Bombchu allocation, state-zero item admission, independent parent/initialized-child freeze and cancellation/repeat under independent/combined object masks and nonzero palettes at declared owner publications.");
    }
}
