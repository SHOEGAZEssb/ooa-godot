
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateForestControllerScrollRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (int warm in new[] { 1, 3, 0 })
        {
            ReinitializeGameplayForValidation();
            _inventory.SetScriptedEquippedItems(0, 0); _inventory.AssignAnimalCompanion(id);
            _saveData.SetGlobalFlag(0x22); _saveData.SetGlobalFlag(0x42);
            _saveData.SetGlobalFlag(0x23, false); _saveData.SetGlobalFlag(0x24, false);
            _saveData.WriteWramByte(0xc646 + id - 0x0b, 0x60); _saveData.CommitInventoryChange();
            var seed = _random.CaptureState();
            LoadValidationRoom(0, 0x81); _player.WarpTo(new(80, 112), recordSafe: false);
            var parent = _entities.EntityAdapters<CompanionForestControllerRoomEntity>().Single();
            int slot = _entities.NativeInteractionSlot(parent);
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, 80, 112)
                { CompanionDispatchEnabled = true, NativeGameKeysEnabled = true, PostObjectCollisionsEnabled = false,
                    HostileEnemiesEnabled = true, HostilePartsEnabled = true };
            native.InitializeLinkGameplay(); native[0xd000] = 3;
            native[0xcc2e] = 0; native[0xcc05] = 0xff;
            native.InitializeRoomInteractions(); native[0xff70] = 0;
            int updates = 0;
            void Compare()
            {
                string context = $"Forest controller scroll ${id:x2}, warm={warm}, batch={batched}, update{updates}";
                CompareFairyInteractionSlotsRom(native, context);
                foreach (int address in new[] { 0xcc02, 0xcc8a, 0xcc2c, 0xcc6e, 0xcc91, 0xcfd2 })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"{context}: control byte ${address:x4} differs.");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    context + ": original complete room streams consumed different ordered RNG.");
            }
            StepGameplayUpdates(warm, Vector2.Zero, batched: batched, afterUpdate: () =>
            { native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); updates++; Compare(); });
            // Supply an actual clear left-edge point to both boundary callers.
            int edgeY = Enumerable.Range(8, 112).First(y => !_collision.Collides(new(5, y)));
            _player.WarpTo(new(5, edgeY), recordSafe: false);
            native.Word(0xd00a, edgeY * 256); native.Word(0xd00c, 5 * 256);
            OracleRoomData next = _world.LoadRoom(0, 0x80);
            native.ClearRoomVariables(scrolling: true); native.BeginObjectScroll(); native[0xcd00] = 8;
            native[0xcc30] = 0x80; native.CopyRoom(next);
            native.InitializeRoomInteractions(); native[0xff70] = 0;
            _transitions.BeginValidationScroll(_player, Vector2I.Left, 0x80);
            FailIf(parent.Finished || !_entities.IsOutgoingEntity(parent) || _entities.NativeInteractionSlot(parent) != slot,
                "Destination room cancellation must retain the outgoing $71:$09 native allocation until clearObjectsWithEnabled2.");
            bool completed = false;
            while (!completed && updates < warm + 100)
                StepGameplayUpdates(batched ? 3 : 1, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); updates++;
                    if (!completed && !_transitions.ScrollActive)
                    { native.FinishObjectScroll(); native[0xcd00] = 1; completed = true; }
                    Compare();
                });
            FailIf(!completed || _entities.EntityAdapters<CompanionForestControllerRoomEntity>().Contains(parent) ||
                _entities.IsOutgoingEntity(parent) || native[0xd040 + slot * 256] != 0 ||
                _roomEvents.Get<CompanionForestEvent>().HasState,
                "Only completed scrolling may release the unretained forest controller.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () =>
            { native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); updates++; Compare(); });
        }
    }
}
