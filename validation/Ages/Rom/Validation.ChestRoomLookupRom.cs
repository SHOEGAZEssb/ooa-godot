using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateChestRoomLookupRom()
    {
        foreach (bool batched in new[] { false, true })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x49);
            _interactions.ResetChestForTesting(0, 0x49, 0x51);
            _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            // Stage an additional chest through the real changed-tile owner.
            // Native getChestData still selects room0:49's $28:$04 contents.
            FailIf(!_rooms.TrySetTile(0x44, ChestDatabase.ClosedTile),
                "The moved-chest fixture must publish tile $44 through the shared tile queue.");
            _player.WarpTo(new(72, 88)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),
                "The moved-chest approach must begin on real room0:49 floor.");
            int rupees = _inventory.Rupees;
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 72, 88);
            native.InitializeLinkGameplay(); native.InitializeLinkWalkingAnimation();
            var nativeHud = native.CreateMenuView();
            nativeHud[0xcbe4] = nativeHud[0xc6aa];
            nativeHud[0xcbe5] = nativeHud[0xc6ad]; nativeHud[0xcbe6] = nativeHud[0xc6ae];
            nativeHud[0xcbe9] = 0xff;
            var sounds = _sound.AttachPlayRequestAudit();
            int heldBefore = 0, update = 0;
            void Step(int count = 1, bool move = false, bool press = false)
            {
                int held = (move ? 0x40 : 0) | (press ? 1 : 0);
                int edge = held & ~heldBefore; heldBefore = held;
                StepGameplayUpdates(count, move ? Vector2.Up : Vector2.Zero,
                    MenuRomActions(held), MenuRomActions(edge), batched, () =>
                    {
                        native.UpdateGameplay(edge, held, move ? 0 : 0xff, _entities.FrameCounter);
                        edge = 0; native.AdvanceTileGraphics();
                        nativeHud.UpdateHud(_entities.FrameCounter);
                        string context = $"Room0:49 chest$44, batch={batched}, repeat={repeat}, update={++update}";
                        CompareSomariaMotionRom(native, context);
                        for (int address = 0xc672; address < 0xc6cc; address++)
                            FailIf(_saveData.ReadWramByte(address) != native[address],
                                $"{context}: inventory byte ${address:x4} differs.");
                        FailIf(_saveData.GetRoomFlags(0, 0x49) != native[0xc749] ||
                            _runtimeState.ReadField("wDisabledObjects") != native[0xcc8a] ||
                            _runtimeState.ReadField("wDisableLinkCollisionsAndMenu") != native[0xcbca],
                            context + ": chest flags or input/collision masks differ.");
                        if (_interactions.ChestReward is { } reward)
                            FailIf(reward.Position != new Vector2(native[0xd04d], native[0xd04b]) ||
                                reward.VisualGraphic != native[0xd042] ||
                                reward.Visible != ((native[0xd05a] & 0x80) != 0),
                                context + ": reserved treasure position, graphic or visibility differs.");
                        int displayed = (nativeHud[0xcbe6] & 15) * 100 +
                            (nativeHud[0xcbe5] >> 4) * 10 + (nativeHud[0xcbe5] & 15);
                        FailIf(_statusBar.DisplayedRupees != displayed || _statusBar.DisplayedHealth != nativeHud[0xcbe4],
                            context + ": status-bar display counters differ.");
                        FailIf(!sounds.Requests.SequenceEqual(native.Sounds),
                            $"{context}: sound order differs: runtime=[{string.Join(',', sounds.Requests)}], native=[{string.Join(',', native.Sounds)}].");
                    });
            }
            Step(20, move: true); Step();
            FailIf(!_player.FacesTileWallForInteraction || _collision.Collides(_player.Position),
                "Link must approach chest$44 through the actual room collision geometry.");
            Step(press: true);
            FailIf(!_interactions.ChestRewardActive || _interactions.ChestReward is not { VisualGraphic: 0x2b } ||
                _currentRoom.GetPackedStorageMetatile(0x44) != 0xf0 || _currentRoom.GetPackedStorageMetatile(0x51) != 0xf1,
                "The moved chest must open its live tile and use room0:49's 30-rupee graphic, retaining the original chest tile.");
            Step(32);
            FailIf(_dialogue.IsOpen || _inventory.Rupees != rupees,
                "The moved chest must not grant its reward before the native rise completes.");
            Step();
            FailIf(!_dialogue.IsOpen || _inventory.Rupees != rupees + 30 || native[0xcba0] == 0,
                "Chest$44 must grant the native room0:49 reward, rather than the no-position-match 1-rupee fallback.");
            Step(3);
            _dialogue.Close(); native[0xcba0] = 0;
            Step(); Step(press: true);
            FailIf(_interactions.ChestRewardActive || _inventory.Rupees != rupees + 30 ||
                sounds.Requests.Count(sound => sound == SoundId.SndOpenChest) != 1,
                "The opened moved chest must release control and reject a second reward.");
            LoadValidationRoom(0, 0x60); LoadValidationRoom(0, 0x49);
            FailIf(_currentRoom.GetPackedStorageMetatile(0x51) != 0xf0 ||
                _currentRoom.GetPackedStorageMetatile(0x44) == 0xf1,
                "Re-entry must restore the native stored chest position, rather than the transient interacted position.");
        }
    }
}
