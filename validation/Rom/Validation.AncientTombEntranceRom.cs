using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareAncientTombEntranceRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(1, 0x8c, 0x80, false);
            LoadValidationRoom(1, 0x8c);
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(88, 40)); _player.Face(Vector2I.Down);
            var entrance = _entities.Entities<AncientTombEntranceRoomEntity>().Single();
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 2, 88, 40);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            int slot = (0xd0 + _entities.InteractionSlot(entrance)) * 256 + 0x40;
            rom[slot] = 1; rom[slot + 1] = 0xdc; rom[slot + 2] = 0x0e;
            rom[slot + 11] = 0x38; rom[slot + 13] = 0x48;
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            void Step(int count = 1, int angle = 0xff) =>
                StepSomariaMotionRom(rom, count, batch, angle, afterUpdate: () =>
                {
                    string context = $"1:8c $dc:$0e batch={batch} update={++update}";
                    FailIf(entrance.Finished != (rom[slot] == 0) || !entrance.Finished &&
                        (entrance.State != rom[slot + 4] || entrance.Counter != rom[slot + 6]) ||
                        _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                        _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != rom[0xcc02] ||
                        _saveData.GetRoomFlags(1, 0x8c) != rom[0xc88c],
                        $"{context}: state/counter/flag/masks differ: runtime={entrance.State}/{entrance.Counter}, native={rom[slot + 4]}/{rom[slot + 6]}.");
                    foreach (int position in new[] { 0x04, 0x05, 0x14, 0x15, 0x34, 0x35 })
                    {
                        Vector2 point = new((position & 15) * 16 + 8, (position >> 4) * 16 + 8);
                        FailIf(_currentRoom.GetMetatile(point) != rom[0xcf00 + position] ||
                            _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00 + position],
                            $"{context}: tile/collision at ${position:x2} differs from clean ROM.");
                    }
                    var puffs = _entities.Entities<PuzzlePuffEffect>();
                    var nativePuffs = Enumerable.Range(2, 14).Select(index => (0xd0 + index) * 256 + 0x40)
                        .Where(address => rom[address] != 0 && rom[address + 1] == 0x05).ToArray();
                    FailIf(puffs.Count != nativePuffs.Length || puffs.Where((puff, index) =>
                        puff.Position != new Vector2(rom[nativePuffs[index] + 13], rom[nativePuffs[index] + 11]) ||
                        puff.Visible != ((rom[nativePuffs[index] + 0x1a] & 0x80) != 0)).Any(),
                        context + ": live puff order/position/visibility differs from clean ROM.");
                    FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls) || !sounds.Requests.SequenceEqual(rom.Sounds),
                        context + ": ordered sounds or shared RNG differ from clean ROM.");
                });
            Step(); Step(10, 8); Step(13, 16);
            for (int i = 0; i < 100 && entrance.State != 2; i++) Step(1, 24);
            FailIf(entrance.State != 2 || entrance.Counter != 45, "Clean-ROM grave approach did not reach $dc:$0e state2/counter45.");
            _dialogue.ShowMessage("Entrance pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            Step(44, 16); Step(1, 16); Step(4, 16);
            FailIf(!entrance.Finished || !_saveData.HasRoomFlag(1, 0x8c, 0x80),
                "Clean-ROM $dc:$0e comparison must include completion and the following player updates.");
        }
        // Supplement the gameplay route with bounded original updateInteractions
        // execution for checkLinkVulnerable's independent gate inputs. These
        // fixtures deliberately avoid Link's earlier pass clearing var2a.
        foreach (var gate in new (string Field, object Value, int Address, byte Native, bool Blocked)[]
        {
            ("_nativeContactSignal", true, 0xd02a, 1, true),
            ("_enemyInvincibilityFrames", 1f, 0xd02b, 1, true),
            ("_enemyInvincibilityFrames", -1f, 0xd02b, 0xff, true),
            ("_enemyKnockbackFrames", 1f, 0xd02d, 1, true),
            ("_instantRespawnCollisionDisabled", true, 0xd024, 0, true),
            ("menu", (byte)1, 0xcc02, 1, true),
            ("collision-menu", true, 0xcbca, 1, true),
            ("_deathPending", true, 0xcdd5, 1, true),
            ("_spinnerControlled", true, 0xcc95, 0x80, true),
            ("_ledgeJumpState", 1, 0xcc5c, 0x80, true),
            ("_topDownAirborne", true, 0xcc5c, 1, false)
        })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(1, 0x8c);
            var entrance = _entities.Entities<AncientTombEntranceRoomEntity>().Single();
            var frame = new RoomEntityFrame(_player, 0, false);
            var spawns = new List<RoomEntitySpawn>();
            entrance.UpdateFrame(frame, spawns);
            _rooms.TrySetTile(0x34, 0x02);
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 2, 88, 40);
            int slot = (0xd0 + _entities.InteractionSlot(entrance)) * 256 + 0x40;
            rom[slot] = 1; rom[slot + 1] = 0xdc; rom[slot + 2] = 0x0e;
            rom[slot + 4] = 1; rom[slot + 11] = 0x38; rom[slot + 13] = 0x48;
            FieldInfo? field = gate.Field is "menu" or "collision-menu" ? null :
                typeof(Player).GetField(gate.Field, BindingFlags.Instance | BindingFlags.NonPublic)!;
            void SetGate(bool active)
            {
                if (gate.Field == "menu") _runtimeState.SetWramByte(WramAddress.wMenuDisabled, active ? (byte)1 : (byte)0);
                else if (gate.Field == "collision-menu")
                {
                    if (active) _entities.DisableLinkCollisionsAndMenu();
                    else _entities.EnableLinkCollisionsAndMenu();
                }
                else if (field!.FieldType.IsEnum) field.SetValue(_player, Enum.ToObject(field.FieldType, active ? 1 : 0));
                else field.SetValue(_player, active ? gate.Value : field.FieldType == typeof(float) ? (object)0f : false);
                rom[gate.Address] = active ? gate.Native : gate.Address == 0xd024 ? (byte)0x80 : (byte)0;
            }
            SetGate(true);
            entrance.UpdateFrame(frame, spawns); rom.AdvanceInteractions(0);
            FailIf(entrance.State != (gate.Blocked ? 1 : 2) || entrance.State != rom[slot + 4] ||
                entrance.Counter != rom[slot + 6] ||
                _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a],
                $"1:8c $dc:$0e checkLinkVulnerable gate {gate.Field} differs from clean-ROM dispatch.");
            if (gate.Blocked)
            {
                SetGate(false);
                entrance.UpdateFrame(frame, spawns); rom.AdvanceInteractions(1);
                FailIf(entrance.State != 2 || entrance.Counter != 45 || rom[slot + 4] != 2 || rom[slot + 6] != 45,
                    $"1:8c $dc:$0e did not retry immediately after clearing {gate.Field}.");
            }
            _entities.Clear();
        }
    }
}
