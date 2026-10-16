using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBlackTowerFlamesRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0xd2);
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new(0x68, 0x68));
            FailIf(_collision.Collides(_player.Position), "PART $59 fixture requires the central floor of room $4:d2.");
            var flames = _entities.EntityAdapters<BlackTowerFlameRoomEntity>().OrderBy(flame => flame.SubId).ToArray();
            FailIf(flames.Length != 4 || !flames.Select(flame => _currentRoom.GetPackedPosition(flame.Position)).SequenceEqual(new[] { 0x56, 0x47, 0x58, 0x67 }),
                "Room $4:d2 must allocate PART $59:$00-$03 at source tiles $56/$47/$58/$67 in order.");
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 0x68, 0x68)
                { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay();
            // Isolate the PART dispatcher from this room's wrong-route tile
            // warp. wWarpsDisabled rejects tile warps without gating contacts.
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 1); rom[WramAddress.wWarpsDisabled] = 1;
            for (int index = 0; index < 4; index++)
            {
                int address = 0xd0c0 + index * 256;
                rom[address] = 1; rom[address + 1] = 0x59; rom[address + 2] = (byte)index;
                rom[address + 11] = (byte)flames[index].Position.Y; rom[address + 13] = (byte)flames[index].Position.X;
            }
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            bool frozen = true;
            _entities.TextActiveSource = () => frozen;
            void Compare()
            {
                rom[0xcba0] = (byte)(frozen ? 1 : 0);
                rom.UpdateGameplay(0, 0, 0xff, ++update);
                for (int index = 0; index < 4; index++)
                {
                    int address = 0xd0c0 + index * 256; var flame = flames[index];
                    FailIf(flame.Finished != (rom[address] == 0), $"PART $59:${index:x2} lifetime differs update {update}.");
                    if (flame.Finished) continue;
                    FailIf(flame.State != rom[address + 4] || flame.Counter != rom[address + 6] || flame.RouteIndex != rom[address + 7] ||
                        flame.Angle != rom[address + 9] || flame.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        flame.Visible != ((rom[address + 0x1a] & 0x80) != 0) ||
                        (flame.State >= 4 && flame.Target != new Vector2I(rom[address + 0x31], rom[address + 0x30])),
                        $"PART $59:${index:x2} update {update}, batch={batched}: port={flame.State}/{flame.Counter}/{flame.RouteIndex}/{flame.Position}/{flame.Angle}/{flame.Target}, " +
                        $"ROM={rom[address + 4]}/{rom[address + 6]}/{rom[address + 7]}/{rom.Word(address + 12) / 256f},{rom.Word(address + 10) / 256f}/{rom[address + 9]}/{rom[address + 0x31]},{rom[address + 0x30]}.");
                }
                FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), $"PART $59 ignition/puff sound order differs update {update}: port={string.Join(',', sounds.Requests)}, ROM={string.Join(',', rom.Sounds)}.");
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            Step(3);
            FailIf(flames.Any(flame => flame.State != 1), "PART $59 must initialize under text, then retain its ignition delay.");
            frozen = false; Step(100);
            FailIf(flames.Any(flame => !flame.Visible), "All four PART $59 ignition delays must have elapsed before movement.");
            frozen = true; Step(5); frozen = false;
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 8); rom[WramAddress.wDisabledObjects] = 8;
            Step(5); _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); rom[WramAddress.wDisabledObjects] = 0;
            for (int bound = 0; bound < 3600 && flames.Any(flame => !flame.Finished); bound += 8) Step(8);
            Step(32);
            FailIf(flames.Any(flame => !flame.Finished) || !flames.Select(flame => flame.RouteIndex).SequenceEqual(new[] { 12, 10, 11, 17 }) ||
                !flames.Select(flame => _currentRoom.GetPackedPosition(flame.Position)).SequenceEqual(new[] { 0x8a, 0x62, 0x26, 0x88 }) ||
                sounds.Requests.Count(cue => cue == SoundId.SndLightTorch) != 4 || sounds.Requests.Count(cue => cue == SoundId.SndPoof) != 4,
                "PART $59 must traverse all four independently source-derived routes and puff once at each terminator.");
            LoadValidationRoom(4, 0xd3); LoadValidationRoom(4, 0xd2);
            FailIf(_entities.EntityAdapters<BlackTowerFlameRoomEntity>().Count() != 4, "PART $59 route completion must not persist across room re-entry.");
        }
        ValidateBlackTowerFlameContact();
    }

    private void ValidateBlackTowerFlameContact()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0xd2);
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new(0x78, 0x78)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Invisible PART $59 contact must approach through room $4:d2 floor.");
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 0x78, 0x78) { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay();
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 1); rom[WramAddress.wWarpsDisabled] = 1;
            var flames = _entities.EntityAdapters<BlackTowerFlameRoomEntity>().OrderBy(flame => flame.SubId).ToArray();
            for (int index = 0; index < 4; index++)
            {
                int address = 0xd0c0 + index * 256;
                rom[address] = 1; rom[address + 1] = 0x59; rom[address + 2] = (byte)index;
                rom[address + 11] = (byte)flames[index].Position.Y; rom[address + 13] = (byte)flames[index].Position.X;
            }
            int update = 0; bool contact = false;
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count, Vector2 movement) => StepGameplayUpdates(count, movement, batched: batched, afterUpdate: () =>
            {
                rom.UpdateGameplay(0, movement == Vector2.Up ? 0x40 : 0, movement == Vector2.Up ? 0 : 0xff, ++update);
                FailIf(_player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]) || _player.KnockbackFrames != rom[0xd02d] ||
                    _player.HealthQuarters != rom[0xc6aa] ||
                    _player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                    !sounds.Requests.SequenceEqual(rom.Sounds),
                    $"PART $59 contact update {update}, batch={batched}: Link={_player.PrecisePosition}/{rom.Word(0xd00c) / 256f},{rom.Word(0xd00a) / 256f}, " +
                    $"invincibility={_player.InvincibilityFrames}/{unchecked((sbyte)rom[0xd02b])}, knockback={_player.KnockbackFrames}/{rom[0xd02d]}, health={_player.HealthQuarters}/{rom[0xc6aa]}.");
                if (_player.InvincibilityFrames != 0)
                {
                    contact = true;
                    FailIf(flames[3].Visible || flames[3].State != 1, "PART $59:$03 must damage Link during its invisible ignition delay.");
                }
            });
            Step(8, Vector2.Up); Step(24, Vector2.Zero);
            FailIf(!contact || sounds.Requests.Count(cue => cue == SoundId.SndDamageLink) != 1,
                "Invisible PART $59 contact must apply one hit, then respect Link's native collision lockout.");
        }
    }
}
