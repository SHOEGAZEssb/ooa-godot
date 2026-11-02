
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMooshGoodbyePlacementRom()
    {
        ValidateMooshGoodbyeCapacityRom();
        ValidateMooshGoodbyeCancellation();
        foreach (bool batched in new[] { false, true })
        foreach (bool rope in new[] { false, true })
        foreach (bool remembered in new[] { false, true })
        foreach (int flags in new[] { 0, 0x20, 0x40, 0x80 })
        {
            ReinitializeGameplayForValidation();
            _inventory.SetScriptedEquippedItems(0, 0); _inventory.LoseTreasure(0x52);
            if (rope) _inventory.GiveTreasure(0x52, 0);
            _saveData.WriteWramByte(0xc648, (byte)flags); _saveData.CommitInventoryChange();
            if (remembered) CompanionRuntimeState.Remember(_runtimeState, 0x0d, 0, 0x6b, new(56, 72));
            var seed = _random.CaptureState();
            LoadValidationRoom(0, 0x6b);
            Vector2 link = (from y in Enumerable.Range(1, 6) from x in Enumerable.Range(1, 8)
                let point = new Vector2(x * 16 + 8, y * 16 + 8)
                where !_collision.Collides(point) && _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None &&
                    System.Math.Abs(point.X - 56) + System.Math.Abs(point.Y - 72) > 32
                select point).Last();
            _player.WarpTo(link, recordSafe: false);
            var native = new SomariaRom(_saveData, seed, _currentRoom, 2, (int)link.X, (int)link.Y)
                { CompanionDispatchEnabled = true, NativeGameKeysEnabled = true, PostObjectCollisionsEnabled = false };
            for (int address = 0xcc24; address <= 0xcc28; address++) native[address] = _runtimeState.ReadWramByte(address);
            native.InitializeLinkGameplay(); native[0xcc2e] = 0; native[0xcc05] = 0xff;
            native.InitializeRoomInteractions(); native[0xff70] = 0;
            var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots");
            FailIf(slots.Count != 2 || slots.Single(pair => pair.Value == 2).Key is not CompanionBarrierRoomEntity ||
                slots.Single(pair => pair.Value == 3).Key is not CompanionPresetSpawnerRoomEntity { Id: 0x67, SubId: 1 } ||
                native[0xd240] != 1 || native[0xd241] != 0x71 || native[0xd242] != 1 || native[0xd340] != 1 ||
                native[0xd341] != 0x67 || native[0xd342] != 1 || _runtimeState.ReadWramByte(0xd100) != (remembered ? 1 : 0),
                "Room $0:$6b must retain its original $71:$01/$67:$01 allocations without an eager D1 birth.");
            int updates = 0, textHeld = 0;
            bool born = false, deleted = false, frozen = false, menuRejected = false;
            bool farewell = rope && flags == 0x20;
            void Compare(bool menu = false)
            {
                native.UpdateGameplay(menu ? 8 : 0, menu ? 0x18 : 0, 0xff, _entities.FrameCounter); updates++;
                foreach (int address in new[] { 0xd100, 0xd101, 0xd103, 0xd104, 0xd105, 0xd108, 0xd109,
                    0xd10a, 0xd10b, 0xd10c, 0xd10d, 0xd10e, 0xd10f, 0xd110, 0xd114, 0xd115,
                    0xd11a, 0xd120, 0xd121, 0xd124, 0xd126, 0xd127, 0xd130, 0xd138, 0xd13d, 0xd13e, 0xd13f })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"Moosh farewell update{updates}, flags=${flags:x2}, rope={rope}, remembered={remembered}, batch={batched}: ${address:x4} port=${_runtimeState.ReadWramByte(address):x2}, ROM=${native[address]:x2}.");
                foreach (int address in new[] { 0xcc8a, 0xcc02, 0xcc91, 0xcc6e, 0xcc2c, 0xcc24, 0xcc25, 0xcc26, 0xcc27, 0xcc28 })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"Moosh farewell update{updates}: native control${address:x4} differs.");
                FailIf(_dialogue.IsOpen != (native[0xcba0] != 0) || _saveData.ReadWramByte(0xc648) != native[0xc648] ||
                    _saveData.ReadWramByte(0xc638) != native[0xc638] || _saveData.ReadWramByte(0xc639) != native[0xc639] ||
                    RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    $"Moosh farewell update{updates}: text, save byte, mount point or ordered RNG differs.");
                FailIf(slots.Count != 1 || slots.Single().Value != 2 ||
                    ((CompanionBarrierRoomEntity)slots.Single().Key).State != native[0xd244] || native[0xd340] != 0,
                    "The checked $67:$01 must delete independently while its leading unmounted barrier remains state0.");
                born |= native[0xd100] != 0;
                deleted |= born && native[0xd100] == 0;
                if (updates == 1)
                    FailIf(born != (remembered || rope && (flags & 0x40) == 0) || native[0xd104] != 0 || native[0xd105] != (remembered ? 1 : 0),
                        "The placed $67 visit must birth source state0 after the special pass, using only rope/left/occupied predicates.");
                var body = _entities.EntityAdapters<AgesMooshScriptedCompanionRoomEntity>().SingleOrDefault();
                var presenter = _entities.EntityAdapters<IRoomEntity>().SingleOrDefault(entity =>
                    entity is INativeSpecialObjectRoomEntity nativeOwner && _entities.IsSpecialObjectOwner(nativeOwner));
                FailIf(native[0xd100] != 0 && (presenter?.PresentationNode?.Visible != ((native[0xd11a] & 0x80) != 0)),
                    "Moosh's presenter must preserve the original hidden state0 and visible native initialization/flight boundaries.");
                if (body is not null && native[0xd104] != 0)
                    FailIf(body.Npc.Position != new Vector2(native[0xd10d], native[0xd10b]) ||
                        body.Npc.CurrentAnimationCounter != native[0xd120] || body.Npc.CurrentAnimationParameter != native[0xd121],
                        "The native Moosh presenter must follow the physical slot's animation and position.");
                if (menu)
                {
                    menuRejected = _dialogue.IsOpen && _entities.FrameCounter != 0 && _player.Position == link;
                    FailIf(!menuRejected, "The original $01 menu/object masks must reject inventory and movement through the real application loop.");
                }
                if (_dialogue.IsOpen && ++textHeld == 5)
                {
                    FailIf(native.Word(0xcba2) - 0x400 != 0x2208, "Native farewell must select TX_2208.");
                    _dialogue.Close(); native[0xcba0] = 0;
                }
            }
            var app = new ApplicationValidationFixture(this);
            for (int limit = 0; limit < (farewell ? 384 : 8) && !deleted; limit++)
            {
                if (farewell && !menuRejected && _runtimeState.ReadWramByte(0xd103) == 4)
                    app.Step(1, Vector2.Right, held: ["inventory", "move_right"], pressed: ["inventory"], batched: batched, afterUpdate: () => Compare(true));
                else if (farewell && !frozen && _runtimeState.ReadWramByte(0xd103) == 6)
                {
                    byte mask = _runtimeState.ReadField("wDisabledObjects");
                    _runtimeState.SetField("wDisabledObjects", (byte)(mask | 0x20)); native[0xcc8a] |= 0x20;
                    app.Step(3, Vector2.Zero, batched: batched, afterUpdate: () => Compare());
                    _runtimeState.SetField("wDisabledObjects", mask); native[0xcc8a] = mask; frozen = true;
                }
                else
                {
                    // A remembered body reaches var03=4 one update before a
                    // placed birth. End this host frame at that input boundary.
                    int count = batched ? 3 : 1;
                    if (batched && farewell && remembered && updates == 0) count = 2;
                    app.Step(count, Vector2.Zero, batched: batched, afterUpdate: () => Compare());
                }
            }
            FailIf(farewell && (!deleted || !frozen || !menuRejected || _roomEvents.Get<MooshGoodbyeEvent>().HasState),
                $"Native Moosh farewell must freeze, reject menus and complete: remembered={remembered}, updates={updates}, born={born}, deleted={deleted}, frozen={frozen}, menu={menuRejected}, owner={_roomEvents.Get<MooshGoodbyeEvent>().HasState}.");
            _roomEvents.Get<MooshGoodbyeEvent>().Cancel(); _dialogue.Close();
        }
    }

    private void ValidateMooshGoodbyeCapacityRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool remembered in new[] { false, true })
        foreach (int free in Enumerable.Range(0, 3))
        {
            ReinitializeGameplayForValidation();
            _inventory.SetScriptedEquippedItems(0, 0); _inventory.GiveTreasure(0x52, 0);
            _saveData.SetGlobalFlag(0x15, false); _saveData.SetRoomFlag(0, 0x6a, 0x40);
            _saveData.WriteWramByte(0xc646, 0);
            _saveData.WriteWramByte(0xc648, 0x20); _saveData.CommitInventoryChange();
            LoadValidationRoom(0, 0x6a); StepGameplayUpdates(1, Vector2.Zero); _entities.Clear();
            // The outgoing no-D1 Ricky controller has a live lane. Its entry
            // cancellation must not retire the later remembered Moosh body.
            FailIf(!_roomEvents.Get<RickyGlovesEvent>().HasState, "Entry cancellation fixture requires the actual waiting $71:$03 lane.");
            if (remembered) CompanionRuntimeState.Remember(_runtimeState, 0x0d, 0, 0x6b, new(56, 72));
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, _world.LoadRoom(0, 0x6b), 2, 8, 104)
                { CompanionDispatchEnabled = true, NativeGameKeysEnabled = true, PostObjectCollisionsEnabled = false };
            for (int address = 0xcc24; address <= 0xcc28; address++) native[address] = _runtimeState.ReadWramByte(address);
            native.InitializeLinkGameplay();
            var outgoing = AddFairySlotSparkles(native, 14 - free);
            foreach (var filler in outgoing) native[0xd040 + _entities.NativeInteractionSlot(filler) * 256] = 2;
            native[0xcc2e] = 0; native[0xcc05] = 0xff;
            native.InitializeRoomInteractions(); native[0xff70] = 0; native[0xcd00] = 2;
            _player.WarpTo(new(154, 104), recordSafe: false);
            _transitions.BeginValidationScroll(_player, Vector2I.Right, 0x6b);
            native.AdvanceInteractions();
            void Compare()
            {
                var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots");
                var placed = slots.Where(pair => !outgoing.Contains(pair.Key)).ToArray();
                FailIf(placed.Length != (free == 0 ? 0 : 1) || _runtimeState.ReadWramByte(0xd100) != native[0xd100] ||
                    _runtimeState.ReadWramByte(0xd101) != native[0xd101] || _runtimeState.ReadWramByte(0xd104) != native[0xd104] ||
                    _runtimeState.ReadWramByte(0xd105) != native[0xd105] || _runtimeState.ReadWramByte(0xd103) != native[0xd103] ||
                    _runtimeState.ReadField("wDisabledObjects") != native[0xcc8a] ||
                    _runtimeState.ReadField("wMenuDisabled") != native[0xcc02] || RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    $"Moosh farewell scroll with {free} free slots, remembered={remembered}: source prefix/D1 birth/init/control/RNG differs.");
                if (placed.Length != 0)
                {
                    var (entity, slot) = placed.Single();
                    FailIf(slot != 16 - free || entity is not CompanionBarrierRoomEntity { State: 0 } ||
                        native[0xd040 + slot * 256] != 1 || native[0xd041 + slot * 256] != 0x71 || native[0xd042 + slot * 256] != 1,
                        "The full interaction pool must retain only the original admitted barrier prefix, without retrying a skipped spawner.");
                }
            }
            Compare();
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: () =>
            { native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); Compare(); });
            _roomEvents.Get<MooshGoodbyeEvent>().Cancel(); _entities.FinishScreenTransition();
            FailIf((_saveData.ReadWramByte(0xc648) & 0x40) != 0 || _runtimeState.ReadField("wMenuDisabled") != 0,
                "Cancelling incoming farewell initialization must release its menu without source completion.");
        }
    }

    private void ValidateMooshGoodbyeCancellation()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool laterWriter in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.SetScriptedEquippedItems(0, 0); _inventory.GiveTreasure(0x52, 0);
            _saveData.WriteWramByte(0xc648, 0x20); _saveData.CommitInventoryChange();
            LoadValidationRoom(0, 0x6b); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            var owner = _roomEvents.Get<MooshGoodbyeEvent>();
            var body = owner.Body ?? throw new System.InvalidOperationException("Missing actual farewell body before cancellation.");
            FailIf(_runtimeState.ReadWramByte(0xd103) != 4 || !owner.BlocksGameplay || _dialogue.IsOpen,
                "Cancellation must begin after actual native state0 holds $01/$01 and before TX_2208.");
            if (laterWriter) { _runtimeState.SetField("wDisabledObjects", 0x11); _runtimeState.SetField("wMenuDisabled", 0x80); }
            owner.Cancel(); owner.Cancel();
            FailIf(owner.HasState || _runtimeState.ReadWramByte(0xd100) != 0 || !body.Npc.IsRetired ||
                _runtimeState.ReadField("wDisabledObjects") != (laterWriter ? 0x11 : 0) ||
                _runtimeState.ReadField("wMenuDisabled") != (laterWriter ? 0x80 : 0) || _saveData.ReadWramByte(0xc648) != 0x20,
                "Farewell cancellation must retire its sole native body, preserve completion and release only its own control writes.");
            _runtimeState.SetField("wDisabledObjects", 0); _runtimeState.SetField("wMenuDisabled", 0);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_dialogue.IsOpen || _runtimeState.ReadWramByte(0xd100) != 0, "Cancelled native farewell must not advance after its body is gone.");
            LoadValidationRoom(0, 0x6b); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(owner.Body is not { } replacement || ReferenceEquals(replacement, body) ||
                _runtimeState.ReadWramByte(0xd103) != 4 || _saveData.ReadWramByte(0xc648) != 0x20,
                "Re-entry after cancellation must create one fresh source body without completing or resuming its retired predecessor.");
            owner.Cancel();
        }
    }
}
