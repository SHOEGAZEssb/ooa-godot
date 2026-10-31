using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateForestPlacementCapacityRom()
    {
        ValidateForestPendingNpcHeadersRom();
        foreach (bool batched in new[] { false, true })
        foreach (int room in new[] { 0x63, 0x81 })
        foreach (bool finished in new[] { false, true })
        foreach (bool remembered in new[] { false, true })
        foreach (int free in Enumerable.Range(0, room == 0x63 ? 3 : 5))
        {
            ReinitializeGameplayForValidation();
            _inventory.SetScriptedEquippedItems(0, 0); _inventory.AssignAnimalCompanion(0x0d);
            _saveData.SetGlobalFlag(0x22); _saveData.SetGlobalFlag(0x42);
            _saveData.SetGlobalFlag(0x23, false); _saveData.SetGlobalFlag(0x24, room == 0x63);
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, finished);
            _saveData.WriteWramByte(0xc648, 0x60); _saveData.CommitInventoryChange();
            LoadValidationRoom(0, room - 1); _entities.Clear();
            Vector2 animal = room == 0x63 ? new(104, 72) : new(80, 88);
            if (remembered) CompanionRuntimeState.Remember(_runtimeState, 0x0d, 0, room, animal);
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, _world.LoadRoom(0, room), 0, 8, 104)
                { CompanionDispatchEnabled = true, NativeGameKeysEnabled = true, PostObjectCollisionsEnabled = false,
                    HostileEnemiesEnabled = true, HostilePartsEnabled = true };
            for (int address = 0xcc24; address <= 0xcc28; address++) native[address] = _runtimeState.ReadWramByte(address);
            native.InitializeLinkGameplay();
            var outgoing = AddFairySlotSparkles(native, 14 - free);
            foreach (var filler in outgoing) native[0xd040 + _entities.NativeInteractionSlot(filler) * 256] = 2;
            native[0xcc2e] = 0; native[0xcc05] = 0xff;
            native.InitializeRoomInteractions(); native[0xff70] = 0; native[0xcd00] = 8;
            _player.WarpTo(new(154, 104), recordSafe: false);
            _transitions.BeginValidationScroll(_player, Vector2I.Right, room);
            native.AdvanceEnemies(_entities.FrameCounter); native.AdvanceParts(_entities.FrameCounter); native.AdvanceInteractions();
            int updates = 0;
            void Compare()
            {
                string context = $"Forest${room:x2}, free={free}, finished={finished}, remembered={remembered}, batch={batched}, update{updates}";
                CompareFairyInteractionSlotsRom(native, context);
                foreach (int address in new[] { 0xd100, 0xd101, 0xd103, 0xd104, 0xd105, 0xd108, 0xd109,
                    0xd10a, 0xd10b, 0xd10c, 0xd10d, 0xd11a, 0xd120, 0xd121, 0xd124, 0xd126, 0xd127, 0xd130,
                    0xd13d, 0xd13f, 0xcc02, 0xcc8a, 0xcc2c, 0xcc6e, 0xcc91, 0xcc24, 0xcc25, 0xcc26, 0xcc27, 0xcc28 })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"{context}: ${address:x4} port=${_runtimeState.ReadWramByte(address):x2}, ROM=${native[address]:x2}.");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    context + ": complete room parse/initialization consumed different ordered RNG.");
            }
            Compare();
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: () =>
            { native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); updates++; Compare(); });
        }
    }

    private void ValidateForestPendingNpcHeadersRom()
    {
        foreach (bool finished in new[] { false, true })
        foreach (bool flipBeforeInitialization in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.SetScriptedEquippedItems(0, 0); _inventory.AssignAnimalCompanion(0x0d);
            _saveData.SetGlobalFlag(0x22); _saveData.SetGlobalFlag(0x42);
            _saveData.SetGlobalFlag(0x23, false); _saveData.SetGlobalFlag(0x24, false);
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, finished);
            _saveData.WriteWramByte(0xc648, 0x60); _saveData.CommitInventoryChange();
            var seed = _random.CaptureState();
            LoadValidationRoom(0, 0x81); _player.WarpTo(new(120, 112), recordSafe: false);
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, 120, 112)
                { CompanionDispatchEnabled = true, NativeGameKeysEnabled = true, PostObjectCollisionsEnabled = false,
                    HostileEnemiesEnabled = true, HostilePartsEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc2e] = 0; native[0xcc05] = 0xff;
            native.InitializeRoomInteractions(); native[0xff70] = 0;
            // The original parser reserves all four rows before state0 can
            // delete $49:$0d, even when its visibility predicate is false.
            var npc = _entities.EntityAdapters<ForestNpcRoomEntity>().Single(entity => entity.Npc.Record.SubId == 0x0d);
            FailIf(_entities.NativeInteractionSlot(npc) != 4 || !npc.NativeInitializationPending || npc.Npc.Visible,
                "Forest $49:$0d must reserve $d4, hidden and pending, before its native visibility/deletion visit.");
            foreach (var row in new (int Slot, int Id, int Sub)[] { (2, 0x6c, 1), (3, 0x71, 9), (4, 0x49, 0x0d), (5, 0x67, 4) })
                FailIf(native[0xd040 + row.Slot * 256] != 1 || native[0xd041 + row.Slot * 256] != row.Id ||
                    native[0xd042 + row.Slot * 256] != row.Sub,
                    "Native forest parser must retain its complete $6c/$71/$49/$67 prefix before dispatch.");
            CompareFairyInteractionSlotsRom(native, "forest pending headers");
            bool retained = finished ^ flipBeforeInitialization;
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, retained);
            // include/wram.s:$c6d0 and GLOBALFLAG_FINISHEDGAME=$14.
            native[0xc6d2] = (byte)((native[0xc6d2] & ~0x10) | (retained ? 0x10 : 0));
            StepGameplayUpdates(1, Vector2.Zero);
            native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            CompareFairyInteractionSlotsRom(native, "forest first native state0 visit");
            FailIf(npc.NativeInitializationPending || npc.Finished == retained ||
                retained && !npc.Npc.Visible || !retained && !npc.Npc.IsRetired,
                "Only the original state0 visibility gate may retain/show or delete the forest NPC's physical allocation.");
        }
    }
}
