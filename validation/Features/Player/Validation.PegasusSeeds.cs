using Godot;
using System;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePegasusSatchel()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var pegasus = _seedSatchel.Pegasus;
        OracleSaveData.TryDeserialize(_saveData.Serialize(), out var initialSave);
        foreach (bool ring in new[] { false, true })
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default, bool attack = false) =>
                StepGameplayUpdates(count, movement, attack ? ["attack"] : [], attack ? ["attack"] : [], batched: batch);
            pegasus.Clear();
            _saveData.RestoreFrom(initialSave!);
            _saveData.WriteWramByte(WramAddress.wRingBoxLevel, 1);
            _saveData.WriteWramByte(WramAddress.wRingBoxContents, ring ? (byte)RingId.Pegasus : (byte)0xff);
            _saveData.WriteWramByte(WramAddress.wShooterSelectedSeeds, 0xff);
            typeof(InventoryState).GetMethod("LoadFromSaveData", flags)!.Invoke(_inventory, null);
            if (ring) FailIf(!_inventory.EquipRingAt(0), "Could not equip the Pegasus Ring fixture.");
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            _inventory.GiveTreasure(TreasureId.PegasusSeeds, 0x20);
            _inventory.SelectSatchelSeeds(2);
            _inventory.EquipA(TreasureId.SeedSatchel);
            LoadValidationRoom(4, 0x91);
            _player.WarpTo(new Vector2(120, 144));
            Step(32, Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112), "Pegasus fixture must walk into the actual Skull entrance.");
            Step(attack: true); Step(6);
            _player.BeginForcedRoomEntryMovement(Vector2I.Up);
            FailIf(pegasus.Active, "LINK_STATE_FORCE_MOVEMENT initialization must clear Pegasus.");
            _player.EndForcedRoomEntryMovement();
            _entities.RuntimeState.SetWramByte(WramAddress.wPegasusSeedCounter, 0x11);
            Step();
            FailIf(pegasus.RawCounter != (ring ? 0x8010 : 0x800f),
                "An odd Pegasus counter must retain the dust pulse from either decrement, including the first of two.");
            pegasus.Clear();
        }
        GD.Print("Validated Pegasus forced-walk clearing and odd-counter pulse arithmetic; lifecycle/scrolling belong to executed ROM scenarios.");
    }
}
