using Godot;
using System;
using System.Linq;
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
            _saveData.WriteWramByte(0xc6cc, 1);
            _saveData.WriteWramByte(0xc6c6, ring ? (byte)RingId.Pegasus : (byte)0xff);
            _saveData.WriteWramByte(0xc6c5, 0xff);
            typeof(InventoryState).GetMethod("LoadFromSaveData", flags)!.Invoke(_inventory, null);
            if (ring) FailIf(!_inventory.EquipRingAt(0), "Could not equip the Pegasus Ring fixture.");
            _inventory.GiveTreasure(0x19, 1);
            _inventory.GiveTreasure(0x22, 0x20);
            _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
            _inventory.SelectSatchelSeeds(2);
            _inventory.EquipA(InventoryState.ItemSeedSatchel);
            LoadValidationRoom(4, 0x91);
            _player.WarpTo(new Vector2(120, 144));
            Step(32, Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112), "Pegasus fixture must walk into the actual Skull entrance.");
            int seeds = _inventory.PegasusSeeds;
            Step(attack: true);
            var dust = _entities.Entities<PegasusDustRoomEntity>().Single();
            FailIf(pegasus.RawCounter != 0x03c0 || _inventory.PegasusSeeds != 0x19 || seeds != 0x20 ||
                _player.IsUsingSeedSatchel || dust.Substate != 1 || dust.TileBase != 0x16 || dust.OamFlags != 0x0b,
                $"Satchel $22 activation: ring={ring}, batch={batch}, raw=${pegasus.RawCounter:x4}, seeds=${seeds:x2}->${_inventory.PegasusSeeds:x2}, parent={_player.IsUsingSeedSatchel}, dust={dust.Substate}/${dust.TileBase:x2}/${dust.OamFlags:x2}.");
            Step(attack: true);
            int decrement = ring ? 1 : 2;
            FailIf(pegasus.RawCounter != 960 - decrement || _inventory.PegasusSeeds != 0x19 ||
                dust.TileBase != 0x48 || dust.OamFlags != 0x68,
                "Active Pegasus must reject refresh/consumption while dust starts its source palette/flip cycle.");
            Step(5);
            FailIf(dust.Substate != 2 || dust.Visible || dust.OamFlags != 0x0b,
                "Dust animation0 must fall through into animation1's $98 terminator on update6.");
            Step(2);
            FailIf(pegasus.RawCounter != (0x8000 | (960 - 8 * decrement)) ||
                dust.Clouds[0] != 0x81 || dust.Clouds[1] != 0 || dust.Clouds[2] != 117 || dust.Clouds[3] != 120 ||
                !dust.Visible || dust.TileBase != 0x18,
                "Update8 must publish the dust bit and use cloud slot0 at Link Y+5, even while standing.");
            _dialogue.ShowMessage("Pegasus timer.", _player.Position.Y);
            int frozen = pegasus.RawCounter;
            byte cloudCounter = dust.Clouds[0];
            Step(20);
            FailIf(pegasus.RawCounter != frozen || dust.Clouds[0] != cloudCounter,
                "Text must freeze initialized dust and Link's Pegasus decrement.");
            for (int i = 0; _dialogue.IsOpen && i < 180; i++) Step(attack: i % 12 == 0);
            FailIf(_dialogue.IsOpen, "Pegasus text fixture did not close through A input.");
            Step(2);
            // Closing text can resume Link in the same host update; finish
            // from the live source counter, without assuming a UI phase.
            int remaining = (pegasus.RawCounter & 0x7fff) / decrement;
            Step(remaining - 1);
            FailIf(pegasus.RawCounter != decrement || !pegasus.Active || dust.Finished,
                $"Pegasus and dust must survive the penultimate countdown update: ring={ring}, batch={batch}, remaining={remaining}, raw=${pegasus.RawCounter:x4}, dustFinished={dust.Finished}.");
            Step();
            FailIf(pegasus.Active || !dust.Finished, "The zero-counter update must end Pegasus and delete ITEM_DUST.");
            Step(attack: true);
            FailIf(!pegasus.Active || _inventory.PegasusSeeds != 0x18,
                "A completed Pegasus use must allow a second real Satchel activation.");
            Step(960 / decrement - 5);
            _inventory.EquipA(InventoryState.ItemFeather);
            Step(movement: Vector2.Right, attack: true);
            FailIf(!_player.TopDownAirborne || (int)typeof(Player).GetField("_topDownAirSpeedRaw", flags)!.GetValue(_player)! != 0x3c,
                "A live Pegasus Feather jump must snapshot SPEED_180 at takeoff.");
            Step(14, Vector2.Right);
            FailIf(pegasus.Active || (int)typeof(Player).GetField("_topDownAirSpeedRaw", flags)!.GetValue(_player)! != 0x3c,
                "Expired Pegasus must retain the takeoff target during descending steering.");
            Step(20);
            FailIf(_player.TopDownAirborne || _player.IsFallingInHole || _player.IsDying,
                "The Pegasus jump must finish on the entrance floor.");
            for (int i = 0; _player.Position.X > 120 && i < 70; i++) Step(movement: Vector2.Left);
            _inventory.EquipA(InventoryState.ItemSeedSatchel);
            Step(attack: true);
            dust = _entities.Entities<PegasusDustRoomEntity>().Single(d => !d.Finished);
            for (int i = 0; !IsTransitioning && i < 130; i++) Step(movement: Vector2.Up);
            FailIf(!IsTransitioning, "Pegasus scroll fixture did not walk through 4:91's north exit.");
            int beforeScroll = pegasus.RawCounter;
            byte[] cloudsBeforeScroll = dust.Clouds.ToArray();
            for (int i = 0; IsTransitioning && i < 200; i++)
            {
                Step();
                FailIf(pegasus.RawCounter != beforeScroll || !dust.Clouds.SequenceEqual(cloudsBeforeScroll),
                    "Ordinary scrolling must freeze the Pegasus timer and dust cloud slots.");
            }
            FailIf(IsTransitioning || _currentRoom.Id != 0x8d ||
                !_entities.Entities<PegasusDustRoomEntity>().Contains(dust),
                "enabled=$03 must retain the same reserved ITEM_DUST across 4:91->4:8d.");
            Step();
            FailIf((pegasus.RawCounter & 0x7fff) != (beforeScroll & 0x7fff) - decrement,
                "The first resumed Link update must continue the retained Pegasus counter.");
            _player.BeginForcedRoomEntryMovement(Vector2I.Up);
            FailIf(pegasus.Active, "LINK_STATE_FORCE_MOVEMENT initialization must clear Pegasus.");
            _player.EndForcedRoomEntryMovement();
            _entities.RuntimeState.SetWramByte(0xcc6c, 0x11);
            Step();
            FailIf(pegasus.RawCounter != (ring ? 0x8010 : 0x800f),
                "An odd Pegasus counter must retain the dust pulse from either decrement, including the first of two.");
            pegasus.Clear();
        }
    }
}
