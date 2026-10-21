using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateUnderwaterItemsRom()
    {
        // Native parent state0 gates, independently traced in itemParents:
        // isLinkUnderwater: Feather/Bomb/Bombchu/Boomerang/Biggoron/Satchel;
        // checkLinkOnGround: Bracelet/Shovel/Harp/Flute; Shield also checks swim.
        int fixture = 0;
        foreach (int item in new[] { TreasureId.Feather, TreasureId.Harp, TreasureId.Flute,
            TreasureId.Bombs, TreasureId.Bombchus, TreasureId.Boomerang,
            TreasureId.BiggoronSword, TreasureId.Bracelet, TreasureId.Shovel,
            TreasureId.Shield, TreasureId.SeedSatchel })
        foreach (int selection in item == TreasureId.SeedSatchel ? Enumerable.Range(0, 5) : new[] { 0 })
        foreach (bool primary in new[] { false, true })
        foreach (int terrain in new[] { 0, 1, 2 })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            // Flippers swimming is not isLinkUnderwater. Biggoron's parent
            // checks only var2f bit7, so unlike the others it can run there.
            if (item == TreasureId.BiggoronSword && terrain == 1) continue;
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            if (terrain != 1) _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.GiveTreasure(item, item is TreasureId.Bombs or TreasureId.Bombchus ? 0x10 : 1);
            _inventory.GiveTreasure(TreasureId.TuneOfEchoes, 0);
            for (int seed = 0; seed < 5; seed++) _inventory.GiveTreasure(0x20 + seed, 0x10);
            _inventory.SelectSatchelSeeds(selection);
            _inventory.EquipA(primary ? item : 0); _inventory.EquipB(primary ? 0 : item);
            LoadValidationRoom(terrain == 0 ? 2 : 7, terrain == 0 ? 0x90 : 0x05);
            _entities.Clear();
            Vector2 point = terrain == 0 ? new(88, 72) : new(40, 56);
            if (terrain == 0) _currentRoom.SetPositionTileAndCollision(point, 0xa0, 0, 0);
            _player.WarpTo(point); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(point), "Underwater item fixture starts in solid room geometry.");
            var rom = new SideViewRom(_saveData, _random.CaptureState(), _currentRoom, point, _entities.FrameCounter);
            rom[0xd008] = 1;
            var sounds = _sound.AttachPlayRequestAudit();
            byte[] ammo = Enumerable.Range(0xc6b0, 14).Select(_saveData.ReadWramByte).ToArray();
            int button = primary ? 1 : 2;
            void Step(int count = 1, int held = 0, int pressed = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(pressed: edge, held: held); edge = 0;
                    string context = $"Underwater ITEM ${item:x2}/seed={selection} terrain={terrain} A={primary} batch={batched}";
                    FailIf(Enumerable.Range(0xd2, 10).Any(page => rom[page << 8] != 0) ||
                        _player.NativeItemUseActive || _player.IsUsingHarp || _player.IsUsingShield ||
                        _player.TopDownAirborne || _entities.BombchuParent.Active ||
                        _bracelet.State != BraceletState.Idle || _bomb.Active,
                        context + ": rejected item retained a parent/child/action.");
                    FailIf(!Enumerable.Range(0xc6b0, 14).Select(_saveData.ReadWramByte).SequenceEqual(ammo) ||
                        !Enumerable.Range(0xc6b0, 14).Select(address => rom[address]).SequenceEqual(ammo),
                        context + ": rejected item spent ammunition.");
                    CompareSideViewRom(rom, context);
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), context + ": rejected item changed sound order.");
                });
            }
            Step();
            if (terrain != 0)
                FailIf(!_player.SideScrollSwimming || rom[0xcc5d] == 0,
                    "Room 7:05 must enter actual swimming before item input.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(1, button, button); Step(8, button);
                _dialogue.ShowMessage("Underwater item pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(3, button); _dialogue.Close(); rom[0xcba0] = 0;
                Step(3, button); Step();
            }
        }
        GD.Print("Compared all 11 rejected Ages items, all five Satchel seeds, both buttons, underwater floors and side-view Flippers/Mermaid water: native parent/child rejection, movement, counters, ammo, sounds, RNG, text and repeats in split/batched gameplay.");
    }

    private void ValidateUnderwaterAllowedItemsRom()
    {
        // These handlers have underwater animations, not a rejection gate.
        // Punch is selected by checkItemUsed only when both buttons are empty.
        int fixture = 0;
        foreach ((int item, int ring) in new[] { (TreasureId.Sword, 0xff),
            (TreasureId.SwitchHook, 0xff), (TreasureId.Shooter, 0xff),
            (TreasureId.CaneOfSomaria, 0xff), (0, 0x0b), (0, 0x3d),
            (TreasureId.BiggoronSword, 0xff) })
        foreach (int terrain in item == TreasureId.BiggoronSword ? new[] { 1 } : new[] { 0, 2 })
        foreach (bool primary in terrain == 0 ? new[] { true } : new[] { false, true })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            if (terrain != 1) _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            if (item != 0) _inventory.GiveTreasure(item, 1);
            _inventory.GiveTreasure(TreasureId.EmberSeeds, 0x20);
            if (ring != 0xff)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1);
                _inventory.GrantAppraisedRingForDebug(ring);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                    $"Underwater punch fixture could not equip ring ${ring:x2}.");
            }
            _inventory.EquipA(primary ? item : 0); _inventory.EquipB(primary ? 0 : item);
            LoadValidationRoom(terrain == 0 ? 2 : 7, terrain == 0 ? 0x90 : 0x05);
            _entities.Clear();
            Vector2 point = terrain == 0 ? new(88, 72) : new(40, 56);
            if (terrain == 0)
                for (int y = 1; y < _currentRoom.HeightInTiles - 1; y++)
                for (int x = 1; x < _currentRoom.WidthInTiles - 1; x++)
                    _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0, 0, 0);
            _player.WarpTo(point); _player.Face(Vector2I.Right);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 1, (int)point.X, (int)point.Y);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xd009] = 0xff;
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, update = 0;
            void Step(int count = 1, int held = 0, int pressed = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, 0xff, _entities.FrameCounter); edge = 0;
                    string context = $"Allowed water ITEM ${item:x2}/ring=${ring:x2} terrain={terrain} A={primary} batch={batched} update={++update}";
                    FailIf(_player.NativeItemUseActive != (rom[0xcc5f] != 0) ||
                        _player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f),
                        context + ": native parent lifetime or fixed Link position differs.");
                    if (terrain != 0)
                        FailIf(SomariaPrivate<int>(_player, "_sideScrollBubbleCounter") != rom[0xd007],
                            context + ": swimming bubble timer must keep updating while a parent locks movement.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), context + ": sound order differs.");
                    FailIf(RandomDiffers(initialRandom, rom[0xff94], rom[0xff95], rom.RandomCalls), context + ": shared RNG differs.");
                    FailIf(!Enumerable.Range(0xc6b0, 14).Select(_saveData.ReadWramByte).SequenceEqual(
                        Enumerable.Range(0xc6b0, 14).Select(address => rom[address])), context + ": item ammo differs.");
                });
            }
            Step(6);
            FailIf(terrain != 0 && (!_player.SideScrollSwimming || (rom[0xd02f] & 0x80) != (terrain == 2 ? 0x80 : 0)),
                "Allowed item fixture must establish native Mermaid underwater or Flippers swimming first.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(1, button, button);
                // Side-view Flippers reserve A for their swim burst. B can
                // still use Biggoron's sword because var2f bit7 is clear.
                bool shouldStart = terrain != 1 || !primary;
                bool active = item == TreasureId.BiggoronSword ? _player.IsUsingBiggoron : _player.NativeItemUseActive;
                bool nativeActive = item == TreasureId.BiggoronSword ? rom[0xd200] != 0 : rom[0xcc5f] != 0;
                FailIf(active != shouldStart || nativeActive != shouldStart,
                    $"ITEM ${item:x2}/ring=${ring:x2} water terrain={terrain}, A={primary}: admission runtime={active}/native={nativeActive}, expected={shouldStart}.");
                Step(8, button);
                _dialogue.ShowMessage("Water item pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(3, button); _dialogue.Close(); rom[0xcba0] = 0;
                Step(60);
                FailIf(_player.NativeItemUseActive || _player.IsUsingBiggoron || rom[0xcc5f] != 0 || rom[0xd200] != 0,
                    $"ITEM ${item:x2}/ring=${ring:x2} must finish after release in water terrain={terrain}.");
            }
        }
        GD.Print("Compared underwater Sword/Switch Hook/Shooter/Somaria and both punch rings, plus Flippers Biggoron allowance: native parent lifetimes, completion/repeat, Link XY, ammo, sound and RNG in split/batched gameplay.");
    }
}
