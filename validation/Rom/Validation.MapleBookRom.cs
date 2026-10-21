using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMapleBookAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.TuniNutPlaced); _saveData.SetMapleKillCounter(30);
            _inventory.GiveTreasure(TreasureId.TradeItem, 0x08);
            LoadValidationRoom(1, 2); _inventory.SetScriptedEquippedItems(0, 0);
            _player.WarpTo(new(80, 80)); _player.BeginCutsceneControl();
            var maple = _entities.Entities<MapleEncounter>().Single();
            F1Retain(_entities.EntityAdapters<MapleEncounterRoomEntity>().Single());
            for (int wait = 0; wait < 1600 && !(maple.Stage == MapleEncounterStage.Flying && maple.MainFlight &&
                SomariaPrivate<int>(maple, "_counter") == 0 && maple.Position.X is > 24 and < 136 && maple.Position.Y is > 24 and < 104); wait++)
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(maple.Stage != MapleEncounterStage.Flying || !maple.MainFlight,
                "Maple $0e must reach its imported main flight before the book allocation comparison.");
            // Declared collision overlap: compare this producer's allocation
            // boundary, separately from a complete native Maple encounter.
            _player.WarpTo(maple.Position);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, (int)_player.Position.X, (int)_player.Position.Y);
            FillInteractionAdmissionPool(native, free);
            int occupied = InteractionAdmissionPool.Count;
            native[0xd100] = 1; native[0xd101] = 0x0e;
            native[0xd10b] = (byte)maple.Position.Y; native[0xd10d] = (byte)maple.Position.X;
            native[0xffae] = 0; native[0xffaf] = 0xd1;
            byte[] caller = [0x16, 0xd1, 0xcd, 0x93, 0x69, 0xc9]; // mapleSpawnItemDrops $05:$6993.
            for (int index = 0; index < caller.Length; index++) native[0xc100 + index] = caller[index];
            SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, 5);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(maple.Stage != MapleEncounterStage.Recoiling ||
                ((_saveData.MapleState & 0x10) != 0) != (NativeAdmissionChildren(native, 0xa5, 0).Length != 0) ||
                InteractionAdmissionPool.Count != occupied + free,
                $"Maple INTERAC $a5 checked admission/flag differs: free={free}, slots={InteractionAdmissionPool.Count}/{occupied + free}, MapleState=${_saveData.MapleState:x2}, native=${native[0xc644]:x2}.");
            _entities.Clear();
            FailIf(InteractionAdmissionPool.Count != 0, "Maple cancellation must release the book and every declared interaction reservation.");
        }
    }

    private void ValidateMapleBookFlightRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.TuniNutPlaced); _saveData.SetMapleKillCounter(30);
            _inventory.GiveTreasure(TreasureId.TradeItem, 0x08); LoadValidationRoom(1, 2);
            _inventory.SetScriptedEquippedItems(0, 0);
            var maple = _entities.Entities<MapleEncounter>().Single();
            F1Retain(_entities.EntityAdapters<MapleEncounterRoomEntity>().Single());
            // Isolate the book flight from Maple's later private exchange.
            // The declared companion boundary remains stationary in XY;
            // Link and the book use the real application/native object passes.
            F1Set(maple, "<Stage>k__BackingField", MapleEncounterStage.BookExchange);
            F1Set(maple, "_precisePosition", new Vector2(24.75f, 80.5f));
            F1Set(maple, "_zFixed", -0x1880); F1Set(maple, "_angle", 0xff);
            maple.Position = new(24, 80);
            FillValidationRoom(0x2c, width: 10, height: 8);
            _player.WarpTo(new(80.25f, 80.5f)); _player.Face(Vector2I.Right);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 1, 80, 80);
            native[0xd00a] = 0x80; native[0xd00c] = 0x40; native.InitializeLinkGameplay();
            native[0xd100] = 1; native[0xd101] = 0x0e;
            native.Word(0xd10a, 0x5080); native.Word(0xd10c, 0x18c0); native.Word(0xd10e, -0x1880);
            // This flight comparison declares a stationary native companion.
            // Keep the real parent under its common companion mask as well;
            // the independent always-updating book remains admitted.
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x20); native[0xcc8a] = 0x20;
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                var book = (MapleTouchingBookRoomEntity?)_entities.TryCreateInteraction(new MapleTouchingBookSpawn(
                    maple, new(24.75f, 80.5f), -0x1880, new MapleEventDatabase().BookVisual))
                    ?? throw new System.InvalidOperationException("INTERAC$a5 test admission unexpectedly failed.");
                byte[] caller = [0x16, 0xd1, 0xcd, 0x93, 0x69, 0xc9];
                for (int index = 0; index < caller.Length; index++) native[0xc100 + index] = caller[index];
                native[0xffae] = 0; native[0xffaf] = 0xd1;
                SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, 5);
                int at = NativeAdmissionChildren(native, 0xa5, 0).Single();
                FailIf(_entities.InteractionSlot(book) != (at >> 8) - 0xd0,
                    "Book birth/reuse must take the same lowest physical interaction slot as the source.");
                _player.ApplyMapleKnockback(new(24, _player.Position.Y));
                native[0xd02d] = 0x18; native[0xd02c] = 8;
                void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); update++;
                    FailIf(book.State != native[at + 4] || book.PrecisePosition != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                        (book.ZFixed & 0xffff) != native.Word(at + 14) || (book.SpeedZ & 0xffff) != native.Word(at + 20) ||
                        book.Angle != native[at + 9] || book.Visible != ((native[at + 26] & 0x80) != 0) ||
                        _player.KnockbackFrames != native[0xd02d],
                        $"INTERAC$a5 flight update={update} repeat={repeat} batch={batched}: runtime={book.State}/{book.PrecisePosition}/Z{book.ZFixed & 0xffff:x4}/v{book.SpeedZ & 0xffff:x4}/angle{book.Angle:x2}/Link recoil{_player.KnockbackFrames}; " +
                        $"native={native[at + 4]}/({native.Word(at + 12):x4},{native.Word(at + 10):x4})/Z{native.Word(at + 14):x4}/v{native.Word(at + 20):x4}/angle{native[at + 9]:x2}/Link recoil{native[0xd02d]}.");
                    FailIf(sounds.Requests.Count(id => id == 0x57) != native.Sounds.Count(id => id == 0x57),
                        "Book SND_GAINHEART must be requested once at setup, never on allocation or the first movement visit.");
                });
                Step(3);
                FailIf(book.State != 0 || book.Visible, "Book must remain hidden and unmoved while Link's knockback is active.");
                for (int wait = 0; book.State == 0 && wait < 30; wait++) Step();
                FailIf(book.State != 1 || book.PrecisePosition != _player.Position.Floor() || book.ZFixed != 0 || book.SpeedZ != -0x100,
                    "Book setup must copy Link high XYZ and yield without applying gravity or movement.");
                _dialogue.ShowGameplayMessage("Book flight text", 100); native[0xcba0] = 1;
                Step(3); _dialogue.Close(); native[0xcba0] = 0;
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x22); native[0xcc8a] = 0x22;
                Step(3); _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x20); native[0xcc8a] = 0x20;
                for (int wait = 0; !book.Landed && wait < 30; wait++) Step();
                FailIf(!book.Landed, "Book must reach state2 after native gravity, omitting the landing update's planar step.");
                Step(2);
                _entities.RetireEntity(book);
                for (int offset = 0; offset < 0x40; offset++) native[at + offset] = 0;
            }
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0);
            _entities.Clear();
        }
    }
}
