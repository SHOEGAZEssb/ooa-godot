
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRickyInteractionPlacementRom()
    {
        ValidateRickyPlacementCapacityRom();
        foreach (bool batched in new[] { false, true })
        foreach (bool prerequisite in new[] { false, true })
        foreach (int flags in new[] { 0, 0x20, 0x40, 0x80 })
        {
            ReinitializeGameplayForValidation();
            _inventory.SetScriptedEquippedItems(0, 0); _inventory.LoseTreasure(0x48);
            _saveData.SetGlobalFlag(0x15, prerequisite); _saveData.SetRoomFlag(0, 0x6a, 0x40);
            _saveData.WriteWramByte(0xc646, (byte)flags); _saveData.CommitInventoryChange();
            var seed = _random.CaptureState();
            LoadValidationRoom(0, 0x6a);
            _player.WarpTo(new(112, 88), recordSafe: false);
            FailIf(_collision.Collides(_player.Position), "Ricky source placement fixture requires actual room $0:$6a floor.");
            var native = new SomariaRom(_saveData, seed, _currentRoom, 2, 112, 88)
                { CompanionDispatchEnabled = true, NativeGameKeysEnabled = true, PostObjectCollisionsEnabled = false };
            native.InitializeLinkGameplay(); native[0xcc2e] = 0; native[0xcc05] = 0xff;
            native.InitializeRoomInteractions(); native[0xff70] = 0;
            var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots");
            var expected = new[] { (0x31, 0), (0xdc, 8), (0x71, 4), (0x71, 5), (0x67, 2), (0x71, 3) };
            FailIf(slots.Count != 6 || _runtimeState.ReadWramByte(0xd100) != 0,
                "Room $0:$6a must allocate its six source interactions before $67:$02 creates D1.");
            for (int index = 0; index < expected.Length; index++)
            {
                int at = 0xd240 + index * 256;
                var actual = RickyPlacementIdentity(slots.Single(pair => pair.Value == index + 2).Key);
                FailIf((actual.Id, actual.SubId) != expected[index] || native[at] != 1 ||
                    native[at + 1] != actual.Id || native[at + 2] != actual.SubId,
                    $"Ricky source stream row{index} must retain its original allocation before state0.");
            }
            int updates = 0;
            void Compare(int pressed = 0, int held = 0)
            {
                native.UpdateGameplay(pressed, held, 0xff, _entities.FrameCounter); updates++;
                for (int slot = 2; slot < 16; slot++)
                {
                    var entity = slots.SingleOrDefault(pair => pair.Value == slot).Key;
                    int at = 0xd040 + slot * 256;
                    FailIf((entity is not null) != (native[at] != 0),
                        $"Ricky placed slot$d{slot:x1} lifetime differs at update{updates}, flags=${flags:x2}, prerequisite={prerequisite}.");
                    if (entity is null) continue;
                    var actual = RickyPlacementIdentity(entity);
                    FailIf(native[at] != 1 || native[at + 1] != actual.Id || native[at + 2] != actual.SubId || native[at + 4] != actual.State,
                        $"Ricky slot$d{slot:x1} source identity/state differs at update{updates}: ${actual.Id:x2}:${actual.SubId:x2}/{actual.State} vs ${native[at + 1]:x2}:${native[at + 2]:x2}/{native[at + 4]}.");
                }
                foreach (int address in new[] { 0xd100, 0xd101, 0xd103, 0xd104, 0xd105, 0xd10a, 0xd10b, 0xd10c,
                    0xd10d, 0xd10e, 0xd10f, 0xd11a, 0xd120, 0xd121, 0xd130, 0xd13d, 0xd13e })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"Ricky placed update{updates}, flags=${flags:x2}, prerequisite={prerequisite}: D1${address:x4}=${_runtimeState.ReadWramByte(address):x2}, ROM=${native[address]:x2}.");
                foreach (int address in new[] { 0xcc8a, 0xcc02, 0xcc91, 0xcc2c, 0xcc6e, 0xcfc1 })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"Ricky source controller update{updates}: WRAM${address:x4}=${_runtimeState.ReadWramByte(address):x2}, ROM=${native[address]:x2}.");
                FailIf(_dialogue.IsOpen != (native[0xcba0] != 0) || _saveData.ReadWramByte(0xc646) != native[0xc646] ||
                    _saveData.ReadWramByte(0xc638) != native[0xc638] || _saveData.ReadWramByte(0xc639) != native[0xc639] ||
                    RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    $"Ricky placed update{updates}, batch={batched}: text={_dialogue.IsOpen}/${native[0xcba0]:x2}, state=${_saveData.ReadWramByte(0xc646):x2}/${native[0xc646]:x2}, " +
                    $"mount={_saveData.ReadWramByte(0xc638):x2},{_saveData.ReadWramByte(0xc639):x2}/{native[0xc638]:x2},{native[0xc639]:x2}, " +
                    $"RNG={_random.CaptureState()}/{native[0xff94]:x2},{native[0xff95]:x2},{native.RandomCalls}, nativeText=${native.Word(0xcba2):x4}, " +
                    $"Link={_player.Position}/{native.Word(0xd00c) / 256f},{native.Word(0xd00a) / 256f}, nativeButton={native[0xd13d]}.");
            }
            StepGameplayUpdates(8, Vector2.Zero, batched: batched, afterUpdate: () => Compare());
            if (prerequisite && flags == 0)
            {
                // Approach from real floor outside Ricky's A-button reach.
                _player.WarpTo(new(80, 82), recordSafe: false); _player.Face(Vector2I.Up);
                native.Word(0xd00a, 82 * 256); native.Word(0xd00c, 80 * 256); native[0xd008] = native[0xd009] = 0;
                FailIf(_collision.Collides(_player.Position) || _entities.FindTalkTarget(_player) is not null,
                    "Ricky conversation must begin on real floor outside his A-button range.");
                StepGameplayUpdates(6, Vector2.Up, ["move_up"], batched: batched, afterUpdate: () => Compare(0, 0x40));
                StepGameplayUpdates(1, Vector2.Zero, held: ["attack"], pressed: ["attack"], batched: batched, afterUpdate: () => Compare(1, 1));
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () => Compare());
                FailIf(!_dialogue.IsOpen || native.Word(0xcba2) - 0x400 != 0x2000,
                    "The original placed Ricky controller must open TX_2000 through actual movement and A-button routing.");
                _dialogue.Close(); native[0xcba0] = 0;
                StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: () => Compare());
                FailIf(!_dialogue.IsOpen || native.Word(0xcba2) - 0x400 != 0x2003,
                    "Ricky's actual source lane must reach TX_2003 after the first meeting.");
                _dialogue.Close(); native[0xcba0] = 0;
                StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: () => Compare());
                FailIf(_runtimeState.ReadWramByte(0xd13d) != 0 || _roomEvents.Get<RickyGlovesEvent>().BlocksGameplay,
                    "The missing-gloves loop must clear its native pending button and release input for another conversation.");
                _inventory.GiveTreasure(0x48, 0); native[0xc6a3] |= 1;
                StepGameplayUpdates(1, Vector2.Zero, held: ["attack"], pressed: ["attack"], batched: batched, afterUpdate: () => Compare(1, 1));
                StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: () => Compare());
                FailIf(!_dialogue.IsOpen || native.Word(0xcba2) - 0x400 != 0x2004,
                    "Repeating the actual interaction with gloves must yield through the source item query before TX_2004.");
                _dialogue.Close(); native[0xcba0] = 0;
                for (int limit = 0; !_dialogue.IsOpen && limit < 64; limit++)
                    StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: () => Compare());
                FailIf(!_dialogue.IsOpen || native.Word(0xcba2) - 0x400 != 0x2005 || _inventory.HasTreasure(0x48),
                    "Returning the gloves must consume the original treasure and reach TX_2005 through the real force-mount handoff.");
                _dialogue.Close(); native[0xcba0] = 0;
                StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: () => Compare());
                FailIf(_roomEvents.Get<RickyGlovesEvent>().HasState || (_saveData.ReadWramByte(0xc646) & 0x20) == 0,
                    "The placed Ricky controller must set completion, enable menus and delete after TX_2005.");
            }
            _roomEvents.Get<RickyGlovesEvent>().Cancel(); _dialogue.Close();
        }
    }

    private void ValidateRickyPlacementCapacityRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int free in Enumerable.Range(0, 7))
        {
            ReinitializeGameplayForValidation();
            _inventory.SetScriptedEquippedItems(0, 0); _inventory.LoseTreasure(0x48);
            _saveData.SetGlobalFlag(0x15); _saveData.SetRoomFlag(0, 0x6a, 0x40);
            _saveData.WriteWramByte(0xc646, 0); _saveData.CommitInventoryChange();
            LoadValidationRoom(0, 0x69); _entities.Clear();
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, _world.LoadRoom(0, 0x6a), 2, 8, 88)
                { CompanionDispatchEnabled = true, NativeGameKeysEnabled = true, PostObjectCollisionsEnabled = false };
            native.InitializeLinkGameplay();
            var outgoing = AddFairySlotSparkles(native, 14 - free);
            foreach (var filler in outgoing) native[0xd040 + _entities.NativeInteractionSlot(filler) * 256] = 2;
            native[0xcc2e] = 0; native[0xcc05] = 0xff;
            native.InitializeRoomInteractions(); native[0xff70] = 0; native[0xcd00] = 2;
            _player.WarpTo(new(154, 88), recordSafe: false);
            _transitions.BeginValidationScroll(_player, Vector2I.Right, 0x6a);
            native.AdvanceInteractions();
            void Compare()
            {
                var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots");
                var placed = slots.Where(pair => !outgoing.Contains(pair.Key)).ToArray();
                int expectedCount = free - (free > 0 ? 1 : 0) - (free >= 5 ? 1 : 0);
                FailIf(placed.Length != expectedCount || _runtimeState.ReadWramByte(0xd100) != native[0xd100] ||
                    _runtimeState.ReadField("wDisabledObjects") != native[0xcc8a] ||
                    _runtimeState.ReadField("wMenuDisabled") != native[0xcc02] ||
                    RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    $"Ricky scroll with {free} free slots changed the source admitted prefix, D1 birth, controls or RNG.");
                for (int slot = 16 - free; slot < 16; slot++)
                {
                    var entity = placed.SingleOrDefault(pair => pair.Value == slot).Key;
                    int at = 0xd040 + slot * 256;
                    FailIf((entity is not null) != (native[at] != 0),
                        $"Ricky capacity{free}: source slot$d{slot:x1} lifetime differs.");
                    if (entity is null) continue;
                    var actual = RickyPlacementIdentity(entity);
                    FailIf(native[at] != 1 || native[at + 1] != actual.Id || native[at + 2] != actual.SubId || native[at + 4] != actual.State,
                        $"Ricky capacity{free}: source slot$d{slot:x1} identity/state differs.");
                }
            }
            Compare();
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: () =>
            { native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); Compare(); });
            _roomEvents.Get<RickyGlovesEvent>().Cancel(); _entities.FinishScreenTransition();
        }
    }

    private static (int Id, int SubId, int State) RickyPlacementIdentity(IRoomEntity entity) => entity switch
    {
        ImpaEncounterRoomEntity impa => (0x31, 0, impa.State),
        RoomTileChangeWatcherRoomEntity watcher => (0xdc, 8, watcher.Initialized ? 1 : 0),
        CompanionBarrierRoomEntity barrier => (barrier.Record.Id, barrier.Record.SubId, barrier.State),
        CompanionPresetSpawnerRoomEntity spawner => (spawner.Id, spawner.SubId, 0),
        RickyGlovesControllerRoomEntity controller => (0x71, 3, controller.State),
        _ => throw new System.InvalidOperationException($"Unexpected room $0:$6a interaction {entity.GetType().Name}.")
    };
}
