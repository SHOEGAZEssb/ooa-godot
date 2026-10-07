using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBiggoronSwordTilesRom()
    {
        ReinitializeGameplayForValidation();
        int signRoom = -1;
        Vector2 sign = default;
        for (int id = 0; id < 256 && signRoom < 0; id++)
        {
            if (!_rooms.World.HasRoom(0, id)) continue;
            var room = _rooms.GetRoom(0, id);
            if (room.ActiveCollisions != 0) continue;
            for (int y = 24; y < room.Height - 16 && signRoom < 0; y += 16)
            for (int x = 40; x < room.Width - 16; x += 16)
            {
                Vector2 point = new(x, y);
                if (room.GetMetatile(point) != 0xf2 || room.IsSolid(point + Vector2.Left * 16) ||
                    room.IsSolid(point + Vector2.Left * 32)) continue;
                signRoom = id; sign = point; break;
            }
        }
        FailIf(signRoom < 0, "No source overworld sign with a west floor approach was found.");
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, signRoom);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(TreasureId.BiggoronSword, 1);
            _inventory.GiveTreasure(TreasureId.Sword, 1);
            // B reaches the sign's weapon path; A is its conversation input.
            _inventory.EquipA(0);
            _inventory.EquipB(TreasureId.BiggoronSword);
            _player.WarpTo(sign + Vector2.Left * 32);
            StepGameplayUpdates(16, Vector2.Right, batched: batched);
            FailIf(_player.Position != sign + Vector2.Left * 16 || _collision.Collides(_player.Position),
                "Biggoron sign fixture must approach through actual source floor collision.");
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 1,
                (int)_player.Position.X, (int)_player.Position.Y);
            rom.InitializeLinkGameplay();
            // This interior attack uses the room's actual edge collision
            // bytes, rather than the helper's declared dungeon edge fences.
            rom.CopyRoom(_currentRoom);
            rom.InitializeLinkWalkingAnimation();
            var rng = _random.CaptureState(); var sounds = _sound.AttachPlayRequestAudit();
            int signs = _saveData.ReadWramByte(0xc626);
            StepSomariaMotionRom(rom, 18, batched, held: 2, pressed: 2, afterUpdate: () =>
            {
                FailIf(_saveData.ReadWramByte(0xc626) != rom[0xc626] ||
                    !sounds.Requests.SequenceEqual(rom.Sounds) || _random.Calls - rng.Calls != rom.RandomCalls,
                    $"Biggoron sign room$0:${signRoom:x2}: count={_saveData.ReadWramByte(0xc626)}/{rom[0xc626]}, " +
                    $"cues=[{string.Join(',',sounds.Requests)}]/[{string.Join(',',rom.Sounds)}], RNG={_random.Calls-rng.Calls}/{rom.RandomCalls}; " +
                    $"biggoron={_player.IsUsingBiggoron}, event={_roomEvents.Active}, swordDisabled={_playerWorld.SwordDisabled}, itemsDisabled={_playerWorld.ItemUsageDisabled}, text={_dialogue.IsOpen}.");
            });
            // Source mode$0d permits L2 ($02), but excludes L1 ($01).
            // Biggoron cuts the source $f2 sign with ordinary Sword level 1.
            FailIf(_inventory.SwordLevel != 1 || _currentRoom.GetMetatile(sign) != 0x3a ||
                _saveData.ReadWramByte(0xc626) != signs + 1,
                "Biggoron must independently choose BREAKABLETILESOURCE_SWORD_L2 and increment signs once.");
            StepSomariaMotionRom(rom, 16, batched);
            StepSomariaMotionRom(rom, 1, batched, held: 2, pressed: 2);
            StepSomariaMotionRom(rom, 34, batched);
            FailIf(_saveData.ReadWramByte(0xc626) != signs + 1 || _entities.Biggoron!.Active,
                "A completed/repeated Biggoron swing must preserve the destroyed sign's persistent side effect.");
        }
        ReinitializeGameplayForValidation();
    }
}
