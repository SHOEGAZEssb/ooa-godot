using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateCompanionWeaponUpdateGatesRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (var gate in new (int Mask, bool Palette)[]
            { (0, false), (2, false), (4, false), (8, false), (0x10, false), (0x80, false), (0, true) })
        {
            var (actor, native) = PrepareMountedCompanionRom(id);
            IRoomEntity? Weapon() => _entities.EntityAdapters<IRoomEntity>().SingleOrDefault(entity => entity is
                RickyPunchAttackRoomEntity or MooshStompAttackRoomEntity or DimitriMouthRoomEntity);
            for (int wait = 0; Weapon() is null && wait < 160; wait++)
                StepCompanionRom(actor, native, 1, Vector2.Zero, batched, attack: true, edge: wait == 0);
            var weapon = Weapon() ?? throw new InvalidOperationException($"Companion ${id:x2} did not create its real weapon.");
            int Counter() => weapon is RickyPunchAttackRoomEntity punch ? punch.Counter :
                CompanionField<int>(weapon, "_counter");
            const int at = 0xd600; // companionCreateWeaponItem / w1WeaponItem.
            int initial = id == 0x0c ? 11 : 20;
            FailIf(Counter() != initial || native[at + 6] != initial,
                $"Companion ${id:x2} weapon setup must preserve source counter {initial}.");
            var cpu = CompanionField<LinkCollisionRom>(native, "_rom");
            var previousPalette = _entities.PaletteFadeActiveSource;
            try
            {
                _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, (byte)gate.Mask);
                native[0xcc8a] = (byte)gate.Mask; native[0xc4ab] = gate.Palette ? (byte)1 : (byte)0;
                _entities.PaletteFadeActiveSource = () => gate.Palette;
                int updates = 0;
                StepGameplayUpdates(4, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    cpu.Call(0x4872, bank: 7); // Actual updateItems, including $90/text/palette gates.
                    updates++;
                    int expected = (gate.Mask & 0x90) != 0 || gate.Palette ? initial : initial - updates;
                    FailIf(native[at + 6] != expected || Counter() != expected,
                        $"Companion ${id:x2} ITEM $d6 mask=${gate.Mask:x2}, palette={gate.Palette}, update={updates}: counter {Counter()}/{native[at + 6]}, expected {expected}.");
                });
                _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
                native[0xcc8a] = native[0xc4ab] = 0;
                _entities.PaletteFadeActiveSource = previousPalette;
                int before = Counter();
                StepGameplayUpdates(1, Vector2.Zero, batched: batched); cpu.Call(0x4872, bank: 7);
                FailIf(Counter() != before - 1 || native[at + 6] != before - 1,
                    "Reserved companion weapons must resume on the first eligible item pass.");
                _entities.ClearPhysicalPlayerItems();
                FailIf(Weapon() is not null, "Clearing physical items must retire the reserved companion weapon.");
            }
            finally
            {
                _entities.PaletteFadeActiveSource = previousPalette;
                _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
            }
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared 42 real companion weapon cases with original updateItems: $02/$04/$08 continue; $10/$80/palette freeze; release and cancellation checked.");
    }
}
