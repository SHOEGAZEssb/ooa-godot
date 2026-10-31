using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTuniNutSlotsRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int gate in new[] { 0, 1, 2, 3 })
        foreach (bool pressure in gate == 1 ? new[] { false, true } : new[] { false })
        {
            InitializeCompletedIntroRoom(0, 0x11);
            _saveData.SetGlobalFlag(AgesGlobalFlag.TuniNutPlaced, gate == 2);
            if (gate is 1 or 3) _inventory.GiveTreasure(TreasureId.TuniNut, gate == 1 ? 2 : 0); else _inventory.LoseTreasure(TreasureId.TuniNut);
            LoadValidationRoom(5, 0xf6);
            var nut = _entities.EntityAdapters<TuniNutRoomEntity>().Single();
            FailIf(_entities.NativeInteractionSlot(nut) != 2 || nut.State != 0,
                "Placed $b1 reserves $d2 before its live inventory/flag gate and before both sisters.");
            F1Retain(nut); _player.WarpTo(new(120, 80));
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 120, 80);
            native.InitializeLinkGameplay(); native[0xd240] = 1; native[0xd241] = 0xb1;
            native[0xd24b] = 40; native[0xd24d] = 120;
            StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                FailIf(nut.Finished != (native[0xd240] == 0) || !nut.Finished &&
                    (nut.State != native[0xd244] || nut.Visible != ((native[0xd25a] & 0x80) != 0) ||
                    nut.Position != new Vector2(native[0xd24d], native[0xd24b])), "Tuni Nut state0 gate/visibility/position differs.");
            });
            if (gate != 1) continue;
            // Declare the native centering completion boundary on both sides;
            // the following actual update must allocate its own later child.
            F1Set(nut, "<State>k__BackingField", 2); F1Set(nut, "_counter", 1);
            native[0xd244] = 2; native[0xd246] = 1;
            _runtimeState.SetField("wDisabledObjects", 1); _runtimeState.SetField("wMenuDisabled", 1);
            native[0xcc8a] = 1; native[0xcc02] = 1;
            if (pressure) AddFairySlotSparkles(native, 13);
            var aliases = new List<(int, byte)>();
            void Alias(int address, byte value) => aliases.Add((address, value));
            _entities.NativeSoundAllocationAliasWritten += Alias;
            int updates = 0;
            void Compare()
            {
                native.AdvancePaletteThread(); native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); updates++;
                FailIf(nut.State != native[0xd244] || nut.Substate != native[0xd245] || nut.Counter != native[0xd246] ||
                    nut.Position != new Vector2(native[0xd24d], native[0xd24b]) || nut.Height != unchecked((short)native.Word(0xd24e)),
                    $"Tuni Nut native update{updates}, pressure={pressure}: state{nut.State}/{native[0xd244]}, sub{nut.Substate}/{native[0xd245]}, counter{nut.Counter}/{native[0xd246]}, XY{nut.Position}/{native[0xd24d]},{native[0xd24b]}, Z{nut.Height}/{unchecked((short)native.Word(0xd24e))}, palette mode{_transitions.NativePaletteThreadMode}/{native[0xc4ab]} differs.");
                FailIf(_transitions.NativePaletteThreadMode != native[0xc4ab], $"Tuni Nut palette mode differs on update{updates}.");
                if (updates == 1)
                {
                    FailIf(_entities.EntityAdapters<TuniNutSparkleRoomEntity>().Count() != (pressure ? 0 : 1), "Tuni Nut child allocation differs.");
                    if (!pressure)
                    {
                        var child = _entities.EntityAdapters<TuniNutSparkleRoomEntity>().Single();
                        FailIf(_entities.NativeInteractionSlot(child) != 3 || child.State != native[0xd344] || !child.Visible,
                            "Later-slot $84:$07 must receive state0 in the birth update.");
                    }
                }
            }
            StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: Compare);
            if (pressure)
                FailIf(!aliases.SequenceEqual(new[] { (0xc056, (byte)0x46), (0xc057, (byte)0xd2) }) ||
                    native[0xc056] != 0x46 || native[0xc057] != 0xd2, "Unchecked $84:$07 failure must retain DE=$d246 and write through audio aliases$c056/$c057.");
            else
            {
                while (nut.State != 4 && updates < 400) StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: Compare);
                FailIf(nut.State != 4 || _inventory.HasTreasure(TreasureId.TuniNut) || !_saveData.HasGlobalFlag(AgesGlobalFlag.TuniNutPlaced) ||
                    _entities.EntityAdapters<TuniNutSparkleRoomEntity>().Any() || _runtimeState.ReadField("wDisabledObjects") != 0 ||
                    _runtimeState.ReadField("wMenuDisabled") != 0, "Completed Tuni Nut must remove its child, consume the treasure and return control.");
            }
            _entities.NativeSoundAllocationAliasWritten -= Alias;
        }
    }
}
