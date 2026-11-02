
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateFairyControllerSlotsRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool pressure in new[] { false, true })
        {
            InitializeCompletedIntroRoom(0, 0x11);
            _saveData.WriteWramByte(0xc6a2, 1);
            _runtimeState.SetWramByte(0xcfd0, pressure ? (byte)1 : (byte)0);
            _runtimeState.SetWramByte(0xcfd1, pressure ? (byte)1 : (byte)0);
            LoadValidationRoom(0, 0x82);
            var parent = _entities.EntityAdapters<FairyHidingMinigameRoomEntity>().Single();
            F1Retain(parent);
            FailIf(_entities.NativeInteractionSlot(parent) != 2 || parent.State != 0,
                "mainData.s: room $0:$82 must allocate its leading $6c:$00 in INTERACTION$d2 before initialization.");
            _player.WarpTo(new(80, 112));
            FailIf(_collision.Collides(_player.Position), "Fairy controller fixture must place Link on actual forest floor.");
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 80, 112);
            native.InitializeLinkGameplay(); native[0xd240] = 1; native[0xd241] = 0x6c;
            native[0xcfd0] = _runtimeState.ReadWramByte(0xcfd0); native[0xcfd1] = _runtimeState.ReadWramByte(0xcfd1);
            int updates = 0;
            void Compare()
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                CompareFairyInteractionSlotsRom(native, $"fairy slots update{++updates}, pressure={pressure}, batch={batched}");
                FailIf(_runtimeState.ReadField("wDisabledObjects") != native[0xcc8a] ||
                    _runtimeState.ReadField("wMenuDisabled") != native[0xcc02] ||
                    _runtimeState.ReadWramByte(0xcfd0) != native[0xcfd0] ||
                    _runtimeState.ReadWramByte(0xcfd2) != native[0xcfd2],
                    "The controller's slot must publish native controls and signals before its children visit later slots.");
            }
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            if (pressure)
            {
                // Retain twelve actual $84:$02 slots, leaving only $df free.
                // Children with var03=2/1 delete in state0 because found=$01.
                var fillers = AddFairySlotSparkles(native, 12);
                Step(2);
                FailIf(parent.Finished || parent.State != 0 || _entities.EntityAdapters<DiscoveredForestFairyRoomEntity>().Any(),
                    "One free slot must leave $6c state0 alive after its var03=2 child deletes later in that pass.");
                ReleaseFairySlotFiller(native, fillers[0]); Step();
                FailIf(parent.Finished || parent.State != 0,
                    "Two free slots must preserve the parent through another partial three-child attempt.");
                ReleaseFairySlotFiller(native, fillers[1]); Step();
                var survivor = _entities.EntityAdapters<DiscoveredForestFairyRoomEntity>().Single();
                FailIf(!parent.Finished || _entities.NativeInteractionSlot(survivor) != 15 || !survivor.Actor.Visible,
                    "With three free slots, the found var03=0 child must initialize at $df before the next update.");
            }
            else
            {
                Step();
                FailIf(parent.State != 1 || _runtimeState.ReadField("wDisabledObjects") != 0,
                    "$6c state0 must only select state1 on an untouched minigame.");
                Step();
                FailIf(parent.State != 2 || _entities.EntityAdapters<ForestFairyFlight>().Any(),
                    "$6c state1 installs its script before any ASM15 spawn.");
                Step();
                var flight = _entities.EntityAdapters<ForestFairyFlight>().Single();
                FailIf(_entities.NativeInteractionSlot(flight) != 3 || flight.UpdatesDuringDialogue,
                    "The first ASM15 child must occupy $d3 and receive state0 in its parent's actual interaction pass.");
                Step(3);
                _dialogue.ShowMessage("Native fairy slot hold.", 112); native[0xcba0] = 1;
                Step(3); _dialogue.Close(); native[0xcba0] = 0;
                Step(20);
            }
            _roomEvents.Get<FairiesWoodsEvent>().Cancel();
        }
    }

    private void ValidateFairyRevealSlotsRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool pressure in new[] { false, true })
        {
            InitializeCompletedIntroRoom(0, 0x11); _saveData.WriteWramByte(0xc6a2, 1);
            _runtimeState.SetWramByte(0xcfd0, 1); _runtimeState.SetWramByte(0xcfd1, 3);
            LoadValidationRoom(0, 0x91);
            var parent = _entities.EntityAdapters<FairyHidingMinigameRoomEntity>().Single();
            F1Retain(parent); _player.WarpTo(FindSpawn());
            FailIf(_collision.Collides(_player.Position), "The hidden-fairy fixture must keep Link on actual forest floor.");
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, (int)_player.Position.X, (int)_player.Position.Y);
            native.InitializeLinkGameplay(); native[0xd240] = 1; native[0xd241] = 0x6c; native[0xd242] = 1;
            native[0xd24b] = 0x38; native[0xd24d] = 0x28; native[0xcfd0] = 1; native[0xcfd1] = 3;
            int updates = 0, generation = 0, textHold = 0;
            bool requested = false;
            void Compare()
            {
                if (requested) return;
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                CompareFairyInteractionSlotsRom(native, $"reveal slots update{++updates}, pressure={pressure}, batch={batched}");
                FailIf(_runtimeState.ReadField("wDisabledObjects") != native[0xcc8a] ||
                    _runtimeState.ReadField("wMenuDisabled") != native[0xcc02] ||
                    _runtimeState.ReadField("wDisableScreenTransitions") != native[0xcc91] ||
                    _runtimeState.ReadWramByte(0xcfd1) != native[0xcfd1] || _runtimeState.ReadWramByte(0xcfd2) != native[0xcfd2],
                    "Hidden fairy controls, found bits and signals must follow the original slot walk.");
                // This bounded fixture omits the native text thread; hold each
                // source request for three object updates before closing both.
                if (native.TextGeneration != generation)
                {
                    generation = native.TextGeneration; textHold = 3;
                    FailIf(!_dialogue.IsOpen, "Native found-fairy text must open through the actual controller script.");
                }
                if (textHold != 0 && --textHold == 0) { _dialogue.Close(); native[0xcba0] = 0; }
                if (native[0xcc4b] == 0) return;
                requested = true;
                var warp = (Warp)typeof(RoomTransitionController).GetField("_pendingWarp", F1Hidden)!.GetValue(_transitions)!;
                FailIf(native[0xcc47] != 0x80 || native[0xcc48] != 0x82 || native[0xcc49] != 0 ||
                    native[0xcc4a] != 0x64 || native[0xcc4b] != 3 ||
                    warp.DestinationGroup != 0 || warp.DestinationRoom != 0x82 || warp.DestinationPosition != 0x64 ||
                    warp.DestinationTransition != 0 || !warp.DirectFadeOut || _transitions.PhaseName != "FadeOut",
                    "m_HardcodedWarpA's request $03 must start the shared white fade after objects, retaining destination transition $00.");
                native.ApplyRequestedWarp();
                FailIf(native[0xc4ab] != 1 || native[0xc2ef] != 3 || !parent.Finished,
                    "The native final-found handoff must retire $6c and select CUTSCENE $03 with a white palette thread.");
            }
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            Step();
            byte original = _currentRoom.GetMetatile(new(40, 56));
            FailIf(!_currentRoom.ReplaceMetatile(new(40, 56), original, (byte)(original ^ 1), (long)_animationTicks),
                "The reveal fixture requires the actual source hiding metatile $32.");
            native[0xcf32] = (byte)(original ^ 1);
            Step(11);
            var fillers = pressure ? AddFairySlotSparkles(native, 13) : new List<IRoomEntity>();
            _sound.ClearPlayRequestAudit();
            Step();
            FailIf(parent.State != (pressure ? 2 : 3) || parent.Counter != 0,
                "Hidden delay $0c must attempt state2 allocation on the same update and retain state2 only on failure.");
            if (pressure)
            {
                Step();
                FailIf(parent.State != 2 || _entities.EntityAdapters<ForestFairyFlight>().Any(),
                    "A full reveal pool must retry state2 without advancing the script or creating effects.");
                ReleaseFairySlotFiller(native, fillers[0]);
                Step();
            }
            FailIf(parent.State != 3 || _entities.EntityAdapters<ForestFairyFlight>().Count() != 1 ||
                _sound.PlayRequestsFor(SoundId.SndPoof) != (pressure ? 0 : 1),
                "One available slot must admit the fairy while the checked puff fails silently; two slots admit both in source order.");
            while (!requested && updates < 600) Step(batched ? 3 : 1);
            FailIf(!requested || generation != 1, "The bounded native reveal must reach its single text and final-found warp request.");
        }
    }

    private List<IRoomEntity> AddFairySlotSparkles(SomariaRom native, int count)
    {
        var visual = _roomEvents.Get<FairiesWoodsEvent>().Database.Event;
        var fillers = new List<IRoomEntity>();
        for (int index = 0; index < count; index++)
        {
            var node = _entities.TryCreateInteraction(new InteractionSparkleSpawn(new(24 + index * 8, 112), 0,
                new(AgesGameContent.AssetRoot, visual.SparkleSprite, visual.SparkleTileBase, visual.SparklePalette, visual.SparkleAnimation), Subid: 2))!;
            var filler = _entities.EntityAdapters<IRoomEntity>().Single(entity => ReferenceEquals(entity.PresentationNode, node));
            fillers.Add(filler);
            int at = 0xd040 + _entities.NativeInteractionSlot(filler) * 256;
            native[at] = 1; native[at + 1] = 0x84; native[at + 2] = 2;
            native[at + 11] = 112; native[at + 13] = (byte)(24 + index * 8);
        }
        return fillers;
    }

    private void ValidateFairyExitSlotsRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            InitializeCompletedIntroRoom(0, 0x11);
            _runtimeState.SetWramByte(0xcfd0, 1);
            _inventory.EquipA(0); _inventory.EquipB(0);
            LoadValidationRoom(0, 0x92);
            var parent = _entities.EntityAdapters<FairyHidingMinigameRoomEntity>().Single();
            F1Retain(parent);
            _player.WarpTo(new(149, 40)); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(_player.Position), "Invisible exit interaction must be tested from real floor outside its collision trigger.");
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 149, 40);
            native.InitializeLinkGameplay(); native[0xcfd0] = 1;
            native[0xd240] = 1; native[0xd241] = 0x6c; native[0xd242] = 2;
            native[0xd24b] = 40; native[0xd24d] = 159; native[0xd008] = 1; native[0xd009] = 8;
            int updates = 0;
            void Step(int count = 1, bool press = false) => StepGameplayUpdates(count, Vector2.Zero,
                held: press ? ["attack"] : [], pressed: press ? ["attack"] : [], batched: batched, afterUpdate: () =>
                {
                    native.UpdateGameplay(press ? 1 : 0, press ? 1 : 0, 0xff, _entities.FrameCounter);
                    CompareFairyInteractionSlotsRom(native, $"exit A-list update{++updates}, batch={batched}");
                    FailIf(_dialogue.IsOpen || _player.InvincibilityFrames != unchecked((sbyte)native[0xd02b]) ||
                        _player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f),
                        "The invisible A-sensitive exit controller must consume its first press and preserve native Link grace without triggering collision dialogue.");
                });
            // State0 installs the script; setcollisionradii yields, then
            // makeabuttonsensitive registers after Link's third visit.
            Step(3); Step(press: true);
            FailIf(!parent.PressedAButton || native[0xd271] != 1,
                "makeabuttonsensitive must publish to invisible controllers too; $6c's unused pressed-A byte remains set.");
            Step(6); Step(press: true);
            FailIf(_player.InvincibilityFrames != 0 || !parent.PressedAButton,
                "A second press must skip the already-pressed invisible object and grant no new interaction grace.");
            _roomEvents.Get<FairiesWoodsEvent>().Cancel();
        }
    }
    private void ReleaseFairySlotFiller(SomariaRom native, IRoomEntity entity)
    {
        int at = 0xd040 + _entities.NativeInteractionSlot(entity) * 256;
        _entities.RetireEntity(entity);
        for (int offset = 0; offset < 0x40; offset++) native[at + offset] = 0;
    }

    private void CompareFairyInteractionSlotsRom(SomariaRom native, string context)
    {
        var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots");
        for (int slot = 2; slot < 16; slot++)
        {
            int at = 0xd040 + slot * 256;
            var entity = slots.FirstOrDefault(pair => pair.Value == slot && pair.Key is not IRoomEntityLifetime { Finished: true }).Key;
            FailIf((entity is null) != (native[at] == 0), $"{context}: INTERACTION${0xd0 + slot:x2} occupancy differs, runtime={entity?.GetType().Name}/{entity?.PresentationNode?.Name}, native ID=${native[at + 1]:x2}, state=${native[at + 4]:x2}.");
            if (entity is null) continue;
            int id, subid;
            if (entity is FairyHidingMinigameRoomEntity parent)
            {
                var record = SomariaPrivate<FairyHidingControllerRecord>(parent, "_record");
                id = 0x6c; subid = record.SubId;
                FailIf(parent.State != native[at + 4] || parent.Counter != native[at + 6] || parent.PressedAButton != ((native[at + 0x31] & 1) != 0),
                    $"{context}: INTERACTION${0xd0 + slot:x2} parent state/counter {parent.State}/${parent.Counter:x2}, native {native[at + 4]}/${native[at + 6]:x2}.");
            }
            else if (entity is CompanionForestControllerRoomEntity controller)
            {
                var record = SomariaPrivate<ForestNativeObjectRecord>(controller, "_record");
                id = 0x71; subid = record.SubId;
                FailIf(controller.State != native[at + 4] || controller.Counter != native[at + 6],
                    $"{context}: $71 slot${0xd0 + slot:x2} state/counter {controller.State}/${controller.Counter:x2}, native {native[at + 4]}/${native[at + 6]:x2}.");
            }
            else if (entity is CompanionPresetSpawnerRoomEntity spawner)
            {
                id = spawner.Id; subid = spawner.SubId;
            }
            else if (entity.PresentationNode is NpcCharacter npc) { id = npc.Record.Id; subid = npc.Record.SubId; }
            else if (entity.PresentationNode is InteractionSparkleEffect sparkle) { id = 0x84; subid = sparkle.Subid; }
            else if (entity.PresentationNode is StationaryDebrisEffect puff) { id = puff.InteractionId; subid = 0; }
            else throw new InvalidOperationException($"{context}: undeclared interaction {entity.GetType().Name}.");
            FailIf(id != native[at + 1] || subid != native[at + 2],
                $"{context}: INTERACTION${0xd0 + slot:x2} identity ${id:x2}:${subid:x2} differs from ROM ${native[at + 1]:x2}:${native[at + 2]:x2}.");
        }
    }
}
