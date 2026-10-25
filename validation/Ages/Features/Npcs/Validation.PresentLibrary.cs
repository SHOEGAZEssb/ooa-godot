using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoom5d0LibraryReaders()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool clean in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _inventory.EquipA(0); _inventory.EquipB(0);
            _saveData.SetGlobalFlag(0x30, clean);
            // Carpenter $09 bypasses both ordinary carpenter deletion gates.
            _saveData.SetLinkedGame(true);
            _saveData.SetGlobalFlag(0x25); // GLOBALFLAG_SYMMETRY_BRIDGE_BUILT.
            _saveData.SetGlobalFlag(AgesGlobalFlag.GotRingFromZelda, false);
            LoadValidationRoom(5, 0xd0);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            Step(3);
            var actors = _entities.Entities<NpcCharacter>();
            int[] ids = [0x3b, 0xab, 0x3c, 0x2a, 0x9a], subs = [7, 0x0e, 0x0b, 0, 9];
            int[] xs = [0x28, 0x38, 0x48, 0xc0, 0xc8], ys = [0x48, 0x78, 0x18, 0x60, 0x18];
            int[] before = [0x1526, 0x3433, 0x2519, 0x2715], after = [0x1527, 0x3434, 0x251a, 0x2716];
            string[] polluted = ["crinkled old", "filthy seawater", "favorite story", "none remains", "Fairy Powder"];
            string[] cleaned = ["intelligent beauty", "Zora society", "Reading is fun", "be quiet", "romantic poem"];
            FailIf(!actors.Select(n => n.Record.Id).SequenceEqual(ids) || !actors.Select(n => n.Record.SubId).SequenceEqual(subs),
                "Room 5:d0 lost source order $3b:$07,$ab:$0e,$3c:$0b,$2a:$00,$9a:$09.");
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                FailIf(!actor.Active || !actor.Visible || actor.Position != new Vector2(xs[i], ys[i]) ||
                    actor.ObjectCollisionBounds.Size != new Vector2(12, i == 3 ? 24 : 12) ||
                    actor.TimeWarpSolidMarked != (i is 0 or 2 or 4) ||
                    (i < 4 && actor.TextId != (clean ? after[i] : before[i])),
                    $"Room 5:d0 ${ids[i]:x2}:${subs[i]:x2} source initialization/radii/text/solid mark differs.");
                string idle = actor.CurrentScriptAnimationSource;
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    ApproachLibraryActor(actor, batched); Press();
                    // Carpenter's DIR_UP command yields before checkabutton.
                    for (int wait = 0; !_dialogue.IsOpen && wait < 3; wait++) Step();
                    FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Replace("\n", " ").Contains((clean ? cleaned : polluted)[i], StringComparison.OrdinalIgnoreCase),
                        $"Room 5:d0 ${ids[i]:x2}:${subs[i]:x2} source-derived dialogue was not reachable/repeatable.");
                    string facing = actor.CurrentScriptAnimationSource;
                    Step(3);
                    FailIf(actor.CurrentScriptAnimationSource != facing, "Library reading pose changed while text was active.");
                    _dialogue.Close(); Step(2);
                    FailIf(actor.CurrentScriptAnimationSource != idle, $"Room 5:d0 ${ids[i]:x2} did not restore its reading animation.");
                }
            }
            _saveData.SetGlobalFlag(0x30, !clean); Step(2);
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i]; ApproachLibraryActor(actor, batched); Press(); Step();
                bool useClean = i == 4 ? !clean : clean;
                FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Replace("\n", " ").Contains((useClean ? cleaned : polluted)[i], StringComparison.OrdinalIgnoreCase),
                    $"Room 5:d0 ${ids[i]:x2} lost its entry snapshot/live carpenter pollution branch.");
                _dialogue.Close(); Step(2);
            }
            LoadValidationRoom(5, 0xd0); Step(3);
            var reentered = _entities.Entities<NpcCharacter>();
            FailIf(reentered.Take(4).Where((n, i) => n.TextId != (clean ? before[i] : after[i])).Any(),
                "Room 5:d0 re-entry failed to refresh pollution-dependent reading text.");
            ApproachLibraryActor(reentered[0], batched); Press();
            LoadValidationRoom(5, 0xec); Step(3);
            FailIf(_player.CutsceneControlled || _dialogue.IsOpen, "Cancelling a library reader retained dialogue or control.");
        }
        GD.Print("Validated room 5:d0 five ordered readers, source poses/radii/solid marking, both pollution texts, entry snapshots versus live carpenter branch, actual collision approach, repeat, re-entry and cancellation in split/batched gameplay.");
    }

    private void ValidateRoom5c8BookOfSeals()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool talked in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _inventory.EquipA(0); _inventory.EquipB(0);
            _saveData.SetRoomFlag(5, 0xc8, 0x20, false);
            _saveData.SetGlobalFlag(0x20, talked);
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, true); // No postgame deletion gate for $52:$01.
            _inventory.LoseTreasure(TreasureId.BookOfSeals);
            LoadValidationRoom(5, 0xc8);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            Step(2);
            var actors = _entities.Entities<NpcCharacter>();
            FailIf(!actors.Select(n => (n.Record.Id, n.Record.SubId)).SequenceEqual(new[] { (0x80, 8), (0x52, 1) }),
                "Room 5:c8 must allocate decorative $80:$08 before giver $52:$01.");
            var book = actors[0]; var keeper = actors[1];
            FailIf(!keeper.Active || !keeper.Visible || !book.Visible || book.Position != new Vector2(0x78, 0x12) ||
                keeper.Position != new Vector2(0x78, 0x28), "Room 5:c8 source book/giver position or postgame existence differs.");
            ApproachLibraryActor(keeper, batched); Press();
            FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Replace("\n", " ").Contains("Book of Seals", StringComparison.Ordinal) ||
                !_player.CutsceneControlled || _inventory.HasTreasure(TreasureId.BookOfSeals), "TX_3308 did not hold input before the Book of Seals reward.");
            _dialogue.Close(); Step();
            var host = _entities.EntityAdapters<LibraryActorRoomEntity>().Single(e => e.Npc == keeper).Host!;
            if (!talked)
            {
                FailIf(!_player.CutsceneControlled,
                    "Book giver's copied-buffer jump must yield before enabling input.");
                Step();
                FailIf(_player.CutsceneControlled || _saveData.HasRoomFlag(5, 0xc8, 0x20) ||
                    _inventory.HasTreasure(TreasureId.BookOfSeals), "Book giver must loop without a reward before fairy flag $20.");
                ApproachLibraryActor(keeper, batched); Press();
                FailIf(!_dialogue.IsOpen, "Book giver's pre-fairy conversation was not repeatable.");
                _dialogue.Close(); Step();
                _saveData.SetGlobalFlag(0x20); // Live script branch; no room reload.
                ApproachLibraryActor(keeper, batched); Press(); _dialogue.Close(); Step();
            }
            FailIf(host.Counter != 30, "Book giver did not initialize its first wait 30 after the live fairy gate.");
            Step(29); FailIf(_dialogue.IsOpen || host.Counter != 1, "TX_3309 was shown before the first wait's zero update.");
            Step(); FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Replace("\n", " ").Contains("solve the riddle", StringComparison.Ordinal), "Book giver lost TX_3309.");
            _dialogue.Close(); Step();
            FailIf(host.Counter != 30, "Upward animation must initialize the second wait in the same update.");
            Step(29); FailIf(_saveData.HasRoomFlag(5, 0xc8, 0x20), "Book giver set room item flag $20 early.");
            Step(); FailIf(!_saveData.HasRoomFlag(5, 0xc8, 0x20) || !book.Active || _inventory.HasTreasure(TreasureId.BookOfSeals),
                "Book giver flag $20 must yield before the earlier decoration slot deletes and before the reward.");
            Step(); FailIf(book.Active || host.Counter != 30, "Book decoration deletion or third wait initialization missed the next update.");
            Step(29); FailIf(_inventory.HasTreasure(TreasureId.BookOfSeals), "Book reward occurred before the third wait completed.");
            Step();
            for (int i = 0; !_dialogue.IsOpen && i < 35; i++) Step();
            FailIf(!_inventory.HasTreasure(TreasureId.BookOfSeals) || !_dialogue.IsOpen, "Book of Seals $55 failed its native reward/text handoff.");
            _dialogue.Close(); Step(8);
            FailIf(_player.CutsceneControlled || !keeper.Active || _entities.RuntimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0,
                "Book giver did not return to its enabled conversation loop after the reward.");
            ApproachLibraryActor(keeper, batched); Press();
            FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Replace("\n", " ").Contains("solve the riddle", StringComparison.Ordinal), "Completed book giver did not use TX_330a.");
            _dialogue.Close(); Step(2);
            _inventory.LoseTreasure(TreasureId.BookOfSeals); // Placing the book must not let the giver duplicate it.
            LoadValidationRoom(5, 0xc8); Step(2);
            keeper = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x52);
            FailIf(_entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x80).Active, "Book decoration reappeared after persisted room flag $20.");
            ApproachLibraryActor(keeper, batched); Press(); _dialogue.Close(); Step(2);
            FailIf(_inventory.HasTreasure(TreasureId.BookOfSeals) || _player.CutsceneControlled || _rooms.CurrentRoom.Id != 0xc8,
                "Completed book conversation duplicated $55, leaked control, or warped Link.");
        }
        GD.Print("Validated room 5:c8 book/giver source order and existence, live fairy gate, three exact 30-update waits, flag/decorative-book timing, native $55 reward, repeat and completed re-entry in split/batched gameplay.");
    }

    private void ValidatePresentLibraryCancellationAndMasks()
    {
        foreach (bool batched in new[] { false, true })
        {
            foreach (bool committed in new[] { false, true })
            {
                ReinitializeGameplayForValidation(); _inventory.EquipA(0); _inventory.EquipB(0);
                _saveData.SetRoomFlag(5, 0xc8, 0x20, false); _saveData.SetGlobalFlag(0x20);
                _inventory.LoseTreasure(TreasureId.BookOfSeals); LoadValidationRoom(5, 0xc8);
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                var keeper = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x52);
                ApproachLibraryActor(keeper, batched);
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                _dialogue.Close(); StepGameplayUpdates(31, Vector2.Zero, batched: batched);
                FailIf(!_dialogue.IsOpen, "Book cancellation fixture missed TX_3309.");
                _dialogue.Close(); StepGameplayUpdates(committed ? 33 : 12, Vector2.Zero, batched: batched);
                FailIf(_saveData.HasRoomFlag(5, 0xc8, 0x20) != committed, "Book cancellation fixture missed the room flag $20 boundary.");
                LoadValidationRoom(5, 0xd0); StepGameplayUpdates(90, Vector2.Zero, batched: batched);
                FailIf(_player.CutsceneControlled || _inventory.HasTreasure(TreasureId.BookOfSeals) ||
                    _saveData.HasRoomFlag(5, 0xc8, 0x20) != committed ||
                    _entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 ||
                    _entities.RuntimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0,
                    "Cancelled book reward leaked controls, granted $55, or rolled back committed room flag $20.");
            }
            ReinitializeGameplayForValidation(); _inventory.EquipA(0); _inventory.EquipB(0);
            _saveData.SetRoomFlag(5, 0xc8, 0x20, false); _saveData.SetGlobalFlag(0x20);
            _inventory.LoseTreasure(TreasureId.BookOfSeals); LoadValidationRoom(5, 0xc8);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            var giver = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x52);
            ApproachLibraryActor(giver, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            _dialogue.Close(); StepGameplayUpdates(31, Vector2.Zero, batched: batched);
            _dialogue.Close(); StepGameplayUpdates(32, Vector2.Zero, batched: batched);
            FailIf(!_saveData.HasRoomFlag(5, 0xc8, 0x20) || _entities.Entities<NpcCharacter>().Any(n => n.Record.Id == 0x80 && n.Active),
                "Book allocation fixture must commit $20 and free the decoration slot before filling capacity.");
            var add = typeof(RoomEntityManager).GetMethod("AddEntity", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Func<IRoomEntity, IRoomEntity>>(_entities);
            while (_entities.InteractionSlotAvailable)
            {
                var puff = new PuzzlePuffEffect(); puff.Initialize(new(24, 24), SoundId.MusNone);
                add(new InteractionSlotValidationEntity(puff, _ => { }));
            }
            StepGameplayUpdates(30, Vector2.Zero, batched: batched);
            var giverHost = _entities.EntityAdapters<LibraryActorRoomEntity>().Single(e => e.Npc == giver).Host!;
            FailIf(giverHost.Counter != 0 || _dialogue.IsOpen || _inventory.HasTreasure(TreasureId.BookOfSeals),
                "Failed native giveitem $55 must consume its opcode without initializing counter1=$03 or granting a reward.");
            StepGameplayUpdates(4, Vector2.Zero, batched: batched);
            FailIf(_player.CutsceneControlled || !_saveData.HasRoomFlag(5, 0xc8, 0x20) || _inventory.HasTreasure(TreasureId.BookOfSeals),
                "Book giver failed-allocation path must resume its loop and retain committed flag $20.");
            ReinitializeGameplayForValidation(); _inventory.EquipA(0); _inventory.EquipB(0);
            LoadValidationRoom(5, 0xd0); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            var readers = _entities.Entities<NpcCharacter>();
            // Queue A at the real probe, then admit the reduced interaction
            // pass. Only carpenter $09 has the source always-update bit.
            foreach (int id in new[] { 0x3b, 0x9a })
            {
                var reader = readers.Single(n => n.Record.Id == id);
                ApproachLibraryActor(reader, batched);
                var entity = _entities.EntityAdapters<LibraryReaderRoomEntity>().Single(e => e.Npc == reader);
                FailIf(!entity.TryInteract(_player), "Library mask fixture could not queue a reachable A press.");
                FailIf(entity.TryInteract(_player), "Library actor accepted another A press before consuming its pressed-A byte.");
                _entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x02);
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(_dialogue.IsOpen != (id == 0x9a), "Library readers did not honor ordinary versus carpenter interaction-disable eligibility.");
                _dialogue.Close(); _entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(_dialogue.IsOpen != (id == 0x3b), "Library pressed-A byte was lost while frozen or replayed after consumption.");
                _dialogue.Close();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }
            LoadValidationRoom(5, 0xc8);
            _entities.BeginScreenTransition(5, _world.LoadRoom(5, 0xd0), new(240, 0), _player);
            readers = _entities.Entities<NpcCharacter>();
            FailIf(readers.Count != 5 || readers.Any(n => !n.Visible || n.ScriptButtonSensitive), "Room 5:d0 preload ran scripts or exposed unresolved reading poses.");
            FailIf(readers.Take(3).Any(n => n.ObjectCollisionBounds.Size != Vector2.Zero) ||
                readers[3].ObjectCollisionBounds.Size != new Vector2(12, 24) ||
                readers[4].ObjectCollisionBounds.Size != new Vector2(12, 12),
                "Library state zero must leave $3b/$ab/$3c hitboxes clear until script initcollisions, preserving $2a/$9a native radii.");
            _entities.Update(1.0 / 60.0, _player);
            FailIf(readers.Any(n => n.ScriptButtonSensitive != (n.Record.Id == 0x9a)), "Scrolling must run only carpenter $09's always-update script.");
        }
        GD.Print("Validated Book of Seals cancellation before/after committed flag $20, failed native reward allocation and independent reader mask/scroller eligibility in split/batched gameplay.");
    }
}
