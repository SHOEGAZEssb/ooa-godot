using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateAncientTombSarcophagusRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Bracelet, 1);
            _inventory.SetScriptedEquippedItems(0, TreasureId.Bracelet);
            LoadValidationRoom(5, 0x8f);
            var active = SomariaPrivate<List<IRoomEntity>>(_entities, "_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var entity in active.Where(e => e is not SarcophagusRoomEntity).ToArray())
            { active.Remove(entity); free.Invoke(_entities, [entity]); }
            var actors = _entities.Entities<SarcophagusRoomEntity>();
            FailIf(actors.Count != 4, "Source room$5:$8f has four ordered INTERAC$82 placements.");
            var puzzle = actors.Single(e => e.Node.Position == new Vector2(104, 144));
            _player.WarpTo(new(80, 144)); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(_player.Position), "Sarcophagus approach must start on original floor beside its solid tiles.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 1, 80, 144);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 8;
            var slots = actors.ToDictionary(actor => actor, actor => 0xd040 + _entities.InteractionSlot(actor) * 256);
            foreach (var actor in actors)
            {
                int slot = slots[actor];
                rom[slot] = 1; rom[slot + 1] = 0x82;
                rom[slot + 2] = (byte)(actor == puzzle ? 1 : 0);
                rom[slot + 11] = (byte)actor.Position.Y; rom[slot + 13] = (byte)actor.Position.X;
            }
            int update = 0; var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1, int angle = 0xff, bool pressed = false, bool held = false) =>
                StepGameplayUpdates(count, angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                    held || pressed ? ["attack"] : [], pressed ? ["attack"] : [], batch, () => {
                        int buttons = (held || pressed ? 1 : 0) | (angle switch { 0 => 0x40, 8 => 0x10, 16 => 0x80, 24 => 0x20, _ => 0 });
                        rom.UpdateGameplay(pressed ? 1 : 0, buttons, angle, _entities.FrameCounter);
                        pressed = false; rom.AdvanceTileGraphics(); update++;
                        string context = $"D8 sarcophagus update{update}, batch={batch}";
                        CompareSomariaMotionRom(rom, context, allowReservedBracelet: true);
                        foreach (var actor in actors)
                        {
                            int slot = slots[actor];
                            FailIf(actor.Finished != (rom[slot] == 0) || !actor.Finished &&
                                (actor.State != rom[slot + 4] || actor.Substate != rom[slot + 5] ||
                                actor.Position != new Vector2(rom.Word(slot + 12) / 256f, rom.Word(slot + 10) / 256f) ||
                                actor.ZHigh != unchecked((sbyte)rom[slot + 15]) || actor.State == 3 && actor.Counter != rom[slot + 6]),
                                $"{context}: INTERAC$82 slot${slot:x4} state/position/height/break timer differs; runtime {actor.State}:{actor.Substate}:{actor.Position}:{actor.ZHigh}, ROM {rom[slot+4]}:{rom[slot+5]}:{rom[slot+13]},{rom[slot+11]}:{unchecked((sbyte)rom[slot+15])}.");
                        }
                        foreach (int tile in new[] { 0x86, 0x96 })
                            FailIf(_currentRoom.GetPackedStorageMetatile((byte)tile) != rom[0xcf00 + tile] ||
                                _currentRoom.CaptureStorageCollisions()[tile] != rom[0xce00 + tile],
                                $"{context}: source restored layout/collision at${tile:x2} differs.");
                        FailIf(_saveData.GetRoomFlags(5, 0x8f) != rom[0xca8f] ||
                            !sounds.Requests.SequenceEqual(rom.Sounds), $"{context}: puzzle flag or sound sequence differs.");
                        var motion = puzzle.ReservedThrow;
                        FailIf(motion.Active != (rom[0xdc00] != 0), $"{context}: reserved ITEM$16 occupancy differs.");
                        if (motion.Active)
                            FailIf(motion.Position != new Vector2(rom.Word(0xdc0c) / 256f, rom.Word(0xdc0a) / 256f) ||
                                unchecked((ushort)motion.ZFixed) != rom.Word(0xdc0e) ||
                                motion.SpeedZ != unchecked((short)rom.Word(0xdc14)) || motion.Speed != rom[0xdc10] ||
                                motion.Angle != rom[0xdc09], $"{context}: native weight0 reserved-item motion differs.");
                    });
            Step(2); Step(10, angle: 8); Step(1, pressed: true, held: true); Step(12, held: true); Step();
            FailIf(_player.IsCarryingObject || puzzle.State != 1 || _saveData.HasRoomFlag(5, 0x8f, 0x40),
                "Level1 bracelet must leave the heavy sarcophagus and puzzle flag intact.");
            _inventory.GiveTreasure(TreasureId.Bracelet, 2); rom[0xc6b8] = 2;
            Step(2); Step(1, pressed: true, held: true); Step(13);
            FailIf(!_player.IsCarryingObject || puzzle.State != 2 || puzzle.Substate != 1 ||
                !_saveData.HasRoomFlag(5, 0x8f, 0x40), $"The Power Glove must lift the source puzzle sarcophagus and set ROOMFLAG$40. Level={_inventory.BraceletLevel}, Link={_player.Position}/{_player.FacingVector}, state={puzzle.State}/{puzzle.Substate}, bracelet={_bracelet.State}, flag=${_saveData.GetRoomFlags(5,0x8f):x2}, nativeLevel={rom[0xc6b8]}, nativeGrab={rom[0xcc5a]:x2}.");
            Step(1, angle: 0, pressed: true); Step(90);
            FailIf(!puzzle.Finished || _player.IsCarryingObject || puzzle.ReservedThrow.Active,
                "The native object throw must settle, break once, release its reserved item and delete the sarcophagus.");
            LoadValidationRoom(5, 0x8f); StepGameplayUpdates(2, Vector2.Zero);
            FailIf(_entities.Entities<SarcophagusRoomEntity>().Count != 3,
                "Re-entry must suppress only the nonzero-subid puzzle sarcophagus.");
        }
        GD.Print("Validated clean-US D8 sarcophagus approach, Power Glove gate, tile restoration, native weight0 throw/break and puzzle persistence.");
    }
}
