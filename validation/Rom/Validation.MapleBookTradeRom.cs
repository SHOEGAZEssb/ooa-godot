using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMapleBookTradeRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int linkX in new[] { 80, 112 })
        {
            var (maple, native) = MapleBodyBoundary();
            _player.EndCutsceneControl(); _player.WarpTo(new(linkX, 80)); _player.Face(Vector2I.Down);
            _inventory.GiveTreasure(TreasureId.TradeItem, 8); _saveData.SetMapleState(0x18);
            var encounter = SomariaPrivate<MapleEncounterState>(maple, "_encounter"); encounter.ObjectsDisabled = true;
            F1Set(maple, "<Stage>k__BackingField", MapleEncounterStage.BookExchange);
            F1Set(maple, "_mainFlight", true); F1Set(maple, "_vehicle", 1);
            F1Set(maple, "_precisePosition", new Vector2(80.25f, 56.5f)); maple.Position = new(80, 56);
            F1Set(maple, "_zFixed", -0x1800); F1Set(maple, "_speedZ", 0x40); F1Set(maple, "_oscillationCounter", 22);
            F1Set(maple, "_angle", 0xff); F1Set(maple, "_speedRaw", 0x28);
            _runtimeState.SetWramByte(WramAddress.wDisableScreenTransitions, 1);
            var book = (MapleTouchingBookRoomEntity?)_entities.TryCreateInteraction(new MapleTouchingBookSpawn(
                maple, new(80, 56), 0, new MapleEventDatabase().BookVisual))
                ?? throw new InvalidOperationException("Book trade fixture could not reserve INTERAC$a5.");
            F1Set(maple, "_book", book); F1Set(book, "<State>k__BackingField", 2);
            book.PrecisePosition = new(80.75f, 56.5f); book.Position = new(80, 56); book.Visible = true;
            _runtimeState.SetWramByte(WramAddress.wMenuDisabled, 1);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 1);
            for (int address = 0xc5b0; address < 0xcb00; address++) native[address] = _saveData.ReadWramByte(address);
            native[0xd00b] = 80; native[0xd00d] = (byte)linkX; native[0xd008] = 2;
            native[0xcc02] = native[0xcc8a] = native[0xcc91] = 1;
            native[0xd104] = 0x0b; native[0xd108] = 0x0b; native[0xd109] = 0xff; native[0xd128] = 1; native[0xd13c] = 22; native[0xd13e] = 1;
            native[0xd110] = 0x28; native.Word(0xd10a, 0x3880); native.Word(0xd10c, 0x5040);
            native.Word(0xd10e, -0x1800); native.Word(0xd114, 0x40);
            int at = 0xd040 + _entities.InteractionSlot(book) * 256;
            native[at] = 0x81; native[at + 1] = 0xa5; native[at + 4] = 2; native[at + 26] = 0x80;
            native.Word(at + 10, 0x3880); native.Word(at + 12, 0x50c0);
            var texts = new List<int>();
            void Text(int id, string message, Player player) => texts.Add(id);
            _entities.MapleDialogueRequested += Text;
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, textGeneration = native.TextGeneration, heldText = 0, sourceTexts = 0;
            // bank0.showText adds $04 to the semantic text-group byte.
            int[] expectedTexts = [0x0b0e, 0x0b0f, 0x0b10, 0x0463, 0x0b11];
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); update++;
                foreach (int address in new[] { 0xcc02, 0xcc8a, 0xcc91 })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"Book trade update={update}: shared control byte ${address:x4} differs.");
                if (native.TextGeneration != textGeneration)
                {
                    FailIf(sourceTexts >= expectedTexts.Length || native.Word(0xcba2) != expectedTexts[sourceTexts] || !_dialogue.IsOpen,
                        $"Book trade update={update}: source text=${native.Word(0xcba2):x4}, expected index={sourceTexts}, port dialogue={_dialogue.IsOpen}.");
                    sourceTexts++; textGeneration = native.TextGeneration; heldText = 0;
                }
                bool bookExists = _entities.EntityAdapters<MapleTouchingBookRoomEntity>().Any();
                FailIf(bookExists != (native[at] != 0) || _saveData.MapleState != native[0xc644] ||
                    _inventory.TradeItem != native[0xc6c0] || maple.Finished != (native[0xd100] == 0),
                    $"Book trade update={update}, batch={batched}: book={bookExists}/{native[at]}, " +
                    $"MapleState=${_saveData.MapleState:x2}/${native[0xc644]:x2}, item=${_inventory.TradeItem:x2}/${native[0xc6c0]:x2}.");
                if (bookExists)
                    FailIf(book.State != native[at + 4] || book.PrecisePosition != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                        book.Visible != ((native[at + 26] & 0x80) != 0),
                        $"Book trade state update={update}, batch={batched}: " +
                        $"port={book.State}/{book.PrecisePosition}/{book.Visible}, source={native[at + 4]}/({native.Word(at + 12):x4},{native.Word(at + 10):x4})/{native[at + 26]:x2}.");
                foreach (int cue in new[] { SoundId.SndGetSeed, SoundId.SndGetItem })
                    FailIf(sounds.Requests.Count(id => id == cue) != native.Sounds.Count(id => id == cue),
                        $"Book trade update={update}: source/runtime sound ${cue:x2} counts differ.");
                FailIf(_saveData.GetRoomFlags(1, 2) != native[0xc802],
                    "The shared Magic Oar owner must retain the source treasure's room-item flag policy.");
                if (!maple.Finished)
                    FailIf((int)maple.Stage != native[0xd104] || maple.Substate != native[0xd105] || maple.Angle != native[0xd109] ||
                        maple.BookDirection != native[0xd108] || maple.AnimationIndex != native[0xd130] ||
                        SomariaPrivate<Vector2>(maple, "_precisePosition") != new Vector2(native.Word(0xd10c) / 256f, native.Word(0xd10a) / 256f) ||
                        (SomariaPrivate<int>(maple, "_zFixed") & 0xffff) != native.Word(0xd10e),
                        $"Book companion handoff update={update}: port={maple.Stage}/{maple.Substate}/{maple.Angle:x2}/{SomariaPrivate<Vector2>(maple, "_precisePosition")}; " +
                        $"source={native[0xd104]:x2}/{native[0xd105]}/{native[0xd109]:x2}/({native.Word(0xd10c):x4},{native.Word(0xd10a):x4}).");
            });
            try
            {
                Step(1);
                for (int wait = 0; !maple.Finished && wait < 200; wait++)
                {
                    if (native[0xcba0] != 0 && ++heldText >= 3) { _dialogue.Close(); native[0xcba0] = 0; }
                    Step(batched ? 2 : 1);
                }
                FailIf(!maple.Finished || sourceTexts != expectedTexts.Length ||
                    !texts.SequenceEqual(new[] { 0x070e, 0x070f, 0x0710, 0x0711 }) || _inventory.TradeItem != 9,
                    "Book interaction must finish its native text/treasure/companion handoffs and grant the Magic Oar once.");
            }
            finally { _entities.MapleDialogueRequested -= Text; _entities.Clear(); }
        }
    }

    private void ValidateMapleBookOarAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool lowerHole in new[] { false, true })
        foreach (bool cancel in new[] { false, true })
        {
            var (maple, native) = MapleBodyBoundary();
            _player.EndCutsceneControl(); _player.WarpTo(new(80, 80));
            _inventory.GiveTreasure(TreasureId.TradeItem, 8); _saveData.SetMapleState(0x38);
            SomariaPrivate<MapleEncounterState>(maple, "_encounter").ObjectsDisabled = true;
            // Declare a stationary companion while comparing state7's checked
            // allocation, source slot cursor and the independent $60 owner.
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x21);
            _runtimeState.SetWramByte(WramAddress.wMenuDisabled, 1);
            native.CompanionDispatchEnabled = false; native[0xcc8a] = 0x21; native[0xcc02] = 1;
            native[0xd00b] = native[0xd00d] = 80;
            for (int address = 0xc5b0; address < 0xcb00; address++) native[address] = _saveData.ReadWramByte(address);
            if (lowerHole)
            {
                var puff = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(144, 104), SoundId.MusNone));
                int slot = 0xd040 + _entities.InteractionSlot(puff) * 256;
                native[slot] = 1; native[slot + 1] = 5; native[slot + 2] = 0x80;
            }
            var book = (MapleTouchingBookRoomEntity?)_entities.TryCreateInteraction(new MapleTouchingBookSpawn(
                maple, new(96, 80), 0, new MapleEventDatabase().BookVisual))
                ?? throw new InvalidOperationException("Book oar fixture could not allocate its parent interaction.");
            F1Set(maple, "_book", book); F1Set(book, "<State>k__BackingField", 7);
            int at = 0xd040 + _entities.InteractionSlot(book) * 256;
            native[at] = 0x81; native[at + 1] = 0xa5; native[at + 4] = 7;
            FillInteractionAdmissionPool(native, 0);
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                FailIf(book.State != native[at + 4] || SomariaPrivate<int>(book, "_counter") != native[at + 6] ||
                    _inventory.TradeItem != native[0xc6c0],
                    "Book state7/state8 must retain failed admission and exact success counter without granting inventory directly.");
                var treasures = _entities.Entities<GroundTreasurePickup>();
                int[] source = Enumerable.Range(2, 14).Select(slot => 0xd040 + slot * 256)
                    .Where(slot => native[slot] != 0 && native[slot + 1] == 0x60).ToArray();
                FailIf(treasures.Count != source.Length || InteractionAdmissionPool.Count > 14,
                    "Magic Oar creation must reserve exactly one authoritative dynamic interaction slot.");
                if (treasures.Count != 0)
                {
                    var treasure = treasures.Single(); int sourceAt = source.Single();
                    FailIf(_entities.InteractionSlot(treasure) != (sourceAt >> 8) - 0xd0 ||
                        (int)treasure.State != native[sourceAt + 4] || treasure.Visible != ((native[sourceAt + 26] & 0x80) != 0),
                        $"Oar live cursor lower={lowerHole}: port slot={_entities.InteractionSlot(treasure):x2}/state{treasure.State}/visible{treasure.Visible}; " +
                        $"source slot=${sourceAt:x4}/state{native[sourceAt + 4]}/visible${native[sourceAt + 26]:x2}.");
                }
            });
            Step(3);
            FailIf(book.State != 7 || _inventory.TradeItem != 8 || _dialogue.IsOpen,
                "A full pool must leave the book retrying state7 without oar inventory, presentation or dialogue.");
            if (!cancel)
            {
                int releasedSlot = lowerHole ? 2 : _entities.InteractionSlot(book) + 1;
                var released = InteractionAdmissionPool.Single(pair => pair.Value == releasedSlot).Key;
                _entities.RetireEntity(released);
                // interactionDelete clears all $40 bytes at this declared hole.
                for (int offset = 0; offset < 0x40; offset++) native[0xd040 + releasedSlot * 256 + offset] = 0;
                Step(1);
                FailIf(book.State != 8 || SomariaPrivate<int>(book, "_counter") != 2,
                    "The next book visit must retry successfully, set counter2 and yield.");
                Step(2);
                FailIf(_inventory.TradeItem != 9 || !_dialogue.IsOpen,
                    "Only the newly admitted shared $60 owner must grant/present the Magic Oar.");
            }
            _entities.Clear();
            FailIf(InteractionAdmissionPool.Count != 0 || cancel && _inventory.TradeItem != 8,
                "Declared room cancellation must release book/filler/treasure slots without granting a rejected oar.");
        }
        ReinitializeGameplayForValidation();
    }
}
