using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMooshInteractionPlacementRom()
    {
        ValidateMooshPlacementCapacityRom();
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.SetScriptedEquippedItems(0, 0); _inventory.LoseTreasure(0x52);
            _saveData.WriteWramByte(0xc6bf, 2); _saveData.SetRoomFlag(1, 0x79, 0x40);
            _saveData.WriteWramByte(0xc648, 0); _saveData.CommitInventoryChange();
            var seed = _random.CaptureState();
            LoadValidationRoom(0, 0x6c);
            Vector2 link = new(120, 104);
            FailIf(_collision.Collides(link), "Native Moosh placement fixture requires the actual $0:$6c floor.");
            _player.WarpTo(link, recordSafe: false);
            var owner = _roomEvents.Get<MooshRescueEvent>();
            var controller = _entities.EntityAdapters<MooshRescueControllerRoomEntity>().Single();
            var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots");
            var placed = slots.OrderBy(pair => pair.Value).ToArray();
            var expected = new[] { (0x73, 0), (0x73, 1), (0x73, 2), (0x71, 0), (0x71, 2), (0x67, 0) };
            FailIf(placed.Length != 6 || _runtimeState.ReadWramByte(0xd100) != 0 || owner.MooshActor is not null,
                "Room $0:$6c must retain six fresh source interactions without creating D1 before the $67 visit.");
            var native = new SomariaRom(_saveData, seed, _currentRoom, 2, 120, 104)
                { CompanionDispatchEnabled = true, NativeGameKeysEnabled = true, PostObjectCollisionsEnabled = false };
            native.InitializeLinkGameplay();
            native[0xcc2e] = 0; // wRoomIsLarge: this source room is 10x8.
            native[0xcc05] = 0xff; // bank1.loadingRoomState0 selects parsing/remembered-companion/Maple work.
            // Execute the original room's whole placed stream. No synthetic $67,
            // $71 or $73 headers, special-object birth or script cursor is staged.
            native.InitializeRoomInteractions();
            native[0xff70] = 0; // The following object pass addresses the ordinary WRAM1 object pages.
            for (int index = 0; index < placed.Length; index++)
            {
                var (entity, slot) = placed[index];
                var identity = entity switch
                {
                    MooshRescueGhiniRoomEntity => (0x73, ((NpcCharacter)entity.PresentationNode!).Record.SubId),
                    MooshRescueControllerRoomEntity => (0x71, 0),
                    CompanionBarrierRoomEntity barrier => (barrier.Record.Id, barrier.Record.SubId),
                    CompanionPresetSpawnerRoomEntity spawner => (spawner.Id, spawner.SubId),
                    _ => (-1, -1)
                };
                int at = 0xd040 + slot * 256;
                FailIf(slot != index + 2 || identity != expected[index] || native[at] != 1 ||
                    native[at + 1] != identity.Item1 || native[at + 2] != identity.Item2,
                    $"Moosh source stream row{index}: port slot$d{slot:x1} ${identity.Item1:x2}:${identity.Item2:x2}; " +
                    $"ROM=${native[at]:x2}/${native[at + 1]:x2}:${native[at + 2]:x2}, wcc05=${native[0xcc05]:x2}, group/room={native[0xcc2d]:x2}/{native[0xcc30]:x2}; " +
                    $"slots=[{string.Join(';', Enumerable.Range(0,16).Select(i => $"{i:x}:{native[0xd040 + i * 256]:x2}/{native[0xd041 + i * 256]:x2}:{native[0xd042 + i * 256]:x2}"))}].");
            }

            int updates = 0, textHold = 0, masked = 0;
            bool sawBirth = false, sawShake = false, done = false;
            byte precedingMask = 0;
            var positions = SomariaPrivate<Dictionary<string, OracleObjectPosition>>(owner, "_positions");
            void Compare()
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                updates++;
                FailIf(_dialogue.IsOpen != (native[0xcba0] != 0) || controller.State != native[0xd544] ||
                    controller.ShakeCounter != native[0xd57a] ||
                    _runtimeState.ReadField("wDisabledObjects") != native[0xcc8a] ||
                    _runtimeState.ReadField("wMenuDisabled") != native[0xcc02] ||
                    _runtimeState.ReadField("wDisableScreenTransitions") != native[0xcc91] ||
                    _runtimeState.ReadField("wDiggingUpEnemiesForbidden") != native[0xccde],
                    $"Moosh placed controllers update{updates}, batch={batched}: state={controller.State}/{native[0xd544]}, var3a={controller.ShakeCounter}/{native[0xd57a]}, " +
                    $"text={_dialogue.IsOpen}/{native[0xcba0]:x2}, masks=${_runtimeState.ReadField("wDisabledObjects"):x2}/${native[0xcc8a]:x2}, " +
                    $"SVBK=${native[0xff70]:x2}, scroll=${native[0xcd00]:x2}, Ghini0=${native[0xd240]:x2}/{native[0xd244]}, ctrl=${native[0xd540]:x2}, " +
                    $"prerequisites=${native[0xc6bf]:x2}/${native[0xc879]:x2}/${native[0xc648]:x2}, death=${native[0xcdd5]:x2}.");
                foreach (int address in new[] { 0xd100, 0xd101, 0xd103, 0xd104, 0xd105, 0xd10a, 0xd10b,
                    0xd10c, 0xd10d, 0xd10e, 0xd10f, 0xd11a, 0xd120, 0xd121, 0xd130, 0xd13d, 0xd13e })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"Moosh placed D1 update{updates}: ${address:x4} port=${_runtimeState.ReadWramByte(address):x2}, ROM=${native[address]:x2}.");
                for (int index = 0; index < 3; index++)
                {
                    var ghini = (MooshRescueGhiniRoomEntity)placed[index].Key;
                    var actor = (NpcCharacter)ghini.Node;
                    int at = 0xd240 + index * 256;
                    OracleObjectPosition position = positions[$"Ghini{index}"];
                    FailIf(ghini.State != native[at + 4] || position.YFixed != native.Word(at + 10) ||
                        position.XFixed != native.Word(at + 12) || actor.Visible != ((native[at + 0x1a] & 0x80) != 0) ||
                        actor.CurrentAnimationCounter != native[at + 0x20] || actor.CurrentAnimationParameter != native[at + 0x21],
                        $"Moosh placed Ghini${index:x2} update{updates}: state={ghini.State}/{native[at + 4]}, XY={position.PrecisePosition}/" +
                        $"{native.Word(at + 12) / 256f},{native.Word(at + 10) / 256f}, anim={actor.CurrentAnimationCounter}/{native[at + 0x20]}.");
                }
                FailIf(_saveData.ReadWramByte(0xc638) != native[0xc638] || _saveData.ReadWramByte(0xc639) != native[0xc639] ||
                    RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    $"Moosh placed update{updates}: native preset mount point or ordered RNG changed.");
                if (updates == 1)
                {
                    sawBirth = native[0xd100] == 1 && native[0xd104] == 0 && native[0xd740] == 0;
                    FailIf(!sawBirth || _entities.EntityAdapters<CompanionPresetSpawnerRoomEntity>().Any(),
                        "The first placed $67 visit must create state0 D1 and release its own $d7 allocation.");
                }
                sawShake |= controller.ShakeCounter != 0;
                done = sawShake && controller.ShakeCounter == 0 && _dialogue.IsOpen;
                if (_dialogue.IsOpen && ++textHold == 5 && !done)
                { _dialogue.Close(); native[0xcba0] = 0; textHold = 0; }
                else if (!_dialogue.IsOpen) textHold = 0;
            }
            var app = new ApplicationValidationFixture(this);
            for (int limit = 0; !done && limit < 256; limit++)
            {
                if (sawShake && masked == 0)
                {
                    precedingMask = _runtimeState.ReadField("wDisabledObjects");
                    _runtimeState.SetField("wDisabledObjects", 2); native[0xcc8a] = 2;
                    byte counter = controller.ShakeCounter;
                    app.Step(5, Vector2.Zero, batched: batched, afterUpdate: Compare);
                    FailIf(controller.ShakeCounter != counter, "The enabled1 $71 var3a body must freeze under DISABLE_INTERACTIONS.");
                    _runtimeState.SetField("wDisabledObjects", precedingMask); native[0xcc8a] = precedingMask; masked = 1;
                }
                app.Step(batched ? 3 : 1, Vector2.Zero, batched: batched, afterUpdate: Compare);
            }
            FailIf(!sawBirth || !sawShake || !done || masked != 1,
                "Native placed actors must reach the first var3a delay, retain it through masks and finish at TX_2200.");
            owner.Cancel(); _dialogue.Close(); app.Step(1, Vector2.Zero, batched: batched);
            FailIf(owner.HasState || _runtimeState.ReadField("wDisableScreenTransitions") != 0,
                "Cancelling native Moosh lanes must release their own transition control without completing the rescue.");
        }
    }

    private void ValidateMooshPlacementCapacityRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int free in Enumerable.Range(0, 7))
        {
            ReinitializeGameplayForValidation();
            _inventory.SetScriptedEquippedItems(0, 0); _inventory.LoseTreasure(0x52);
            _saveData.WriteWramByte(0xc6bf, 2); _saveData.SetRoomFlag(1, 0x79, 0x40);
            _saveData.WriteWramByte(0xc648, 0); _saveData.CommitInventoryChange();
            LoadValidationRoom(0, 0x7c); _entities.Clear();
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, _world.LoadRoom(0, 0x6c), 2, 88, 6)
                { CompanionDispatchEnabled = true, NativeGameKeysEnabled = true, PostObjectCollisionsEnabled = false };
            native.InitializeLinkGameplay();
            var outgoing = AddFairySlotSparkles(native, 14 - free);
            // setObjectsEnabledTo2 retains these real outgoing allocations while
            // the original parser attempts the incoming stream in source order.
            foreach (var filler in outgoing) native[0xd040 + _entities.NativeInteractionSlot(filler) * 256] = 2;
            native[0xcc2e] = 0; native[0xcc05] = 0xff;
            native.InitializeRoomInteractions(); native[0xff70] = 0;
            native[0xcd00] = 2;
            _player.WarpTo(new(88, 6), recordSafe: false);
            _transitions.BeginValidationScroll(_player, Vector2I.Up, 0x6c);
            SomariaPrivate<FrontendRom>(native, "_rom").Call(0x3b36, 0);
            void Compare()
            {
                var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots");
                var placed = slots.Where(pair => !outgoing.Contains(pair.Key)).OrderBy(pair => pair.Value).ToArray();
                int expectedCount = free == 6 ? 5 : free; // $67 deletes after its state0 birth.
                FailIf(placed.Length != expectedCount || _runtimeState.ReadWramByte(0xd100) != native[0xd100] ||
                    _runtimeState.ReadField("wDisableScreenTransitions") != native[0xcc91] ||
                    _runtimeState.ReadField("wDiggingUpEnemiesForbidden") != native[0xccde] ||
                    RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    $"Moosh scroll stream with {free} free slots changed admission, controls, D1 birth or ordered RNG.");
                foreach (var (entity, slot) in placed)
                {
                    var identity = entity switch
                    {
                        MooshRescueGhiniRoomEntity ghini => (0x73, ((NpcCharacter)ghini.Node).Record.SubId, ghini.State),
                        MooshRescueControllerRoomEntity controller => (0x71, 0, controller.State),
                        CompanionBarrierRoomEntity barrier => (barrier.Record.Id, barrier.Record.SubId, barrier.State),
                        _ => (-1, -1, -1)
                    };
                    int at = 0xd040 + slot * 256;
                    FailIf(native[at] != (entity is MooshRescueGhiniRoomEntity ? 0x81 : 1) ||
                        native[at + 1] != identity.Item1 || native[at + 2] != identity.Item2 || native[at + 4] != identity.Item3,
                        $"Moosh scroll capacity{free}: interaction slot$d{slot:x1} port=${identity.Item1:x2}:${identity.Item2:x2}/state{identity.Item3}, " +
                        $"ROM=${native[at]:x2}/${native[at + 1]:x2}:${native[at + 2]:x2}/state{native[at + 4]}.");
                }
            }
            Compare();
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: () =>
            { native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); Compare(); });
            _roomEvents.Get<MooshRescueEvent>().Cancel(); _entities.FinishScreenTransition();
        }
    }
}
