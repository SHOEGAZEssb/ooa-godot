using System;
using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareRoomShopFlagsRom()
    {
        ReadOnlyMemory<byte> image = ValidationRom.LoadCleanUs();
        byte Native(int address) => image.Span[2 * 0x4000 + address - 0x4000];
        var imported = new RoomShopDatabase();
        int records = 0, shops = 0;
        for (int group = 0; group < 8; group++)
        {
            int pointer = 0x7aaa + group * 2;
            int start = Native(pointer) | Native(pointer + 1) << 8;
            for (int cursor = start; Native(cursor) != 0; cursor += 2) records++;
            for (int room = 0; room < 256; room++)
            {
                int handler = -1;
                for (int cursor = start; Native(cursor) != 0; cursor += 2)
                    if (Native(cursor) == room) { handler = Native(cursor + 1); break; }
                byte expected = handler == 4 ? (byte)2 : (byte)0;
                FailIf(imported.InitialFlags(group,room) != expected,
                    $"Native room graphics lookup${group:x}:${room:x2}: imported shop flags differ from bank$02:$7aaa.");
                if (expected == 0) continue;
                shops++;
                // Execute the original room lookup and price-graphics initializer,
                // rather than comparing only against the imported table.
                var rom = new FrontendRom();
                rom[0xcc2d] = (byte)group; rom[0xcc30] = (byte)room;
                rom.Call(0x7a88,2);
                ReinitializeGameplayForValidation();
                _rooms.Load(group,room);
                FailIf(rom[0xccd3] != 2 || _runtimeState.ReadWramByte(WramAddress.wInShop) != rom[0xccd3] ||
                    !_playerWorld.InShop,"Native price initialization must set shared wInShop bit1.");
                foreach (bool scrolling in new[] { false,true })
                {
                    // Both source room clears cover $ccd3, including transient
                    // price-update/chest-game bits written by other owners.
                    rom[0xccd3] = 0x86;
                    rom.Call(scrolling ? 0x49c9 : 0x49af,1);
                    _runtimeState.SetWramByte(WramAddress.wInShop,0x86);
                    if (scrolling) _rooms.SetLoadedRoom(0,_rooms.GetRoom(0,0x2a));
                    else _rooms.LoadCutsceneRoom(0,0x2a);
                    FailIf(_playerWorld.InShop || _runtimeState.ReadWramByte(WramAddress.wInShop) != rom[0xccd3],
                        "Leaving a shop must clear its shared flags on ordinary/cutscene and scrolling room paths.");
                    _rooms.Load(group,room);
                    FailIf(!_playerWorld.InShop || _runtimeState.ReadWramByte(WramAddress.wInShop) != 2,
                        "Repeated shop entry must restore the native initial bit1 flag.");
                }
            }
        }
        FailIf(records != 35 || shops != 4,
            $"Native room graphics table requires35 rows/four shops, got{records}/{shops}.");
        CompareShopCarryGateRom();
    }

    private void CompareShopCarryGateRom()
    {
        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(WramAddress.wBoughtShopItems1,0);
            _saveData.WriteWramByte(WramAddress.wBoughtShopItems2,0);
            SetTreasure(_saveData,TreasureId.RingBox,false);
            LoadValidationRoom(2,0x7e);
            _inventory.EquipA(0); _inventory.EquipB(0);
            var stock = _entities.Entities<LynnaShopItem>().Single(item => item.Record.SubId == 0);
            var keeper = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x46);
            FailIf(stock.Position != new Vector2(64,40) || keeper.Position != new Vector2(24,88),
                "Shop carry gate requires source$2:$7e stock$47:$00 and keeper$46:$01 placements.");
            void Step(int count = 1,int pressed = 0) =>
                StepGameplayUpdates(count,Vector2.Zero,pressed == 0 ? [] : ["attack"],
                    pressed == 0 ? [] : ["attack"],batch);
            void Walk(Vector2 target)
            {
                for (int axis = 0; axis < 2; axis++)
                    for (int wait = 0; Mathf.Abs(axis == 0 ? _player.Position.X-target.X : _player.Position.Y-target.Y) > 1; wait++)
                    {
                        FailIf(wait >= 180,$"Shop carry approach blocked {_player.Position} -> {target}.");
                        Vector2 direction = axis == 0 ? new(Mathf.Sign(target.X-_player.Position.X),0) :
                            new(0,Mathf.Sign(target.Y-_player.Position.Y));
                        StepGameplayUpdates(1,direction,batched:batch);
                    }
                Step();
                FailIf(_currentRoom.IsSolid(_player.Position) || _entities.BlocksLink(_player.Position),
                    $"Shop carry approach must remain on reachable floor at {_player.Position}.");
            }
            _player.WarpTo(new(64,104));
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Walk(new(64,72)); Walk(new(64,58)); _player.Face(Vector2I.Up);
                Step(pressed:1); Step();
                FailIf(!stock.Held || !_player.IsCarryingObject || !_playerWorld.InShop,
                    "Real shop shelf pickup must retain held stock and the authoritative shop flag.");
                Walk(new(64,72)); Walk(new(52,88)); _player.Face(Vector2I.Left);
                int nativeInvincibility = 0;
                foreach (byte flags in new byte[] { 0,2 })
                {
                    // Bounded native A-sensitive selection, before the keeper's
                    // script. Actual runtime pickup/approach supplies Link's
                    // inputs; source keeperState0 supplies its$06/$14 hitbox.
                    // This checks eligibility, not the shop purchase script.
                    var rom = new FrontendRom();
                    rom[0xd000] = 1; rom[0xd004] = 1; rom[0xd024] = 0x80; rom[0xd029] = 1;
                    rom[0xcc2c] = 0xd0; rom[0xcd00] = 1;
                    rom[0xcc2a] = 1; rom[0xccd3] = flags; rom[0xcc5a] = 0x83;
                    rom[0xd008] = 3; rom[0xd00b] = (byte)_player.Position.Y;
                    rom[0xd00d] = (byte)_player.Position.X;
                    rom[0xd02b] = unchecked((byte)(sbyte)_player.InvincibilityFrames);
                    rom[0xccb3] = 0xd2; rom[0xccb4] = 0x71; // Native list stores H then L.
                    rom[0xd24b] = 88; rom[0xd24d] = 24;
                    rom[0xd266] = 6; rom[0xd267] = 20;
                    byte[] caller = [0x16,0xd0,0xcd,0x5d,0x1b,0xc9];
                    for (int index = 0; index < caller.Length; index++) rom[0xc100+index] = caller[index];
                    rom.Call(0xc100,0); // bank0.linkInteractWithAButtonSensitiveObjects
                    FailIf((rom[0xd271] != 0) != (flags != 0),
                        "Native held-stock A-sensitive selection must require nonzero wInShop.");
                    if (flags != 0)
                    {
                        FailIf(rom[0xd02b] != 0xfc,"Native A-sensitive selection must grant four updates before graphics.");
                        byte[] tail = [0x16,0xd0,0x62,0xcd,0x79,0x42,0xc9];
                        for (int index = 0; index < tail.Length; index++) rom[0xc100+index] = tail[index];
                        rom.Call(0xc100,5); // updateSpecialObjects tail: updateLinkInvincibilityCounter.
                        nativeInvincibility = unchecked((sbyte)rom[0xd02b]);
                    }
                }
                Step(pressed:1);
                FailIf(!_dialogue.IsOpen || _roomEvents.Get<LynnaShopEvent>().Stage != LynnaShopEventStage.PurchaseRejected ||
                    !stock.Held || _player.InvincibilityFrames != nativeInvincibility,
                    $"The reachable keeper must accept held stock with native interaction grace before its no-ring-box rejection: modal={_dialogue.IsOpen}, stage={_roomEvents.Get<LynnaShopEvent>().Stage}, held={stock.Held}, invincibility={_player.InvincibilityFrames}/{nativeInvincibility}.");
                _dialogue.Close(); Step(2);
                FailIf(stock.Held || _player.IsCarryingObject,
                    "Closing the rejection must return stock for a repeated actual shelf pickup.");
            }
        }
    }
}
