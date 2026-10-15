using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateAncientTombSlateRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0x8a);
            _entities.Clear(); _inventory.EquipA(0); _inventory.EquipB(0);
            var data = new AncientTombDatabase();
            var record = data.GetRoomRecords(5, 0x8a).Single(r => r.Kind == DungeonObjectKind.SlateSlot && r.SubId == 0);
            var socket = new SlateSlotRoomEntity(record, data, _saveData, _inventory, _runtimeState,
                (pos, tile) => { if (_rooms.TrySetTile(pos, tile)) _entities.OnRoomTileChanged(); },
                _entities.OnNativeSoundRequested, () => _entities.OnRoomEntityDialogueRequested(0x5111, data.Text(0x5111), record.Position));
            _entities.AddEntity(socket);
            _player.WarpTo(new(120, 152)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "The south slate socket approach must start outside its solid tile.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 120, 152);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            int s = 0xd040 + _entities.InteractionSlot(socket) * 256;
            rom[s] = 1; rom[s + 1] = 0xdb; rom[s + 11] = 0x7a; rom[s + 13] = 0x78;
            int update = 0; var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1, bool move = false) => StepGameplayUpdates(count, move ? Vector2.Up : Vector2.Zero,
                batched: batch, afterUpdate: () => {
                    rom.UpdateGameplay(0, move ? 0x40 : 0, move ? 0 : 0xff, _entities.FrameCounter);
                    rom.AdvanceTileGraphics(); update++;
                    CompareSomariaMotionRom(rom, $"D8 slate update{update}");
                    FailIf(socket.Finished != (rom[s] == 0) || !socket.Finished &&
                        (socket.State != rom[s + 4] || socket.PushCounter != rom[s + 0x3f]),
                        $"D8 slate update{update}: state/counter {socket.State}/{socket.PushCounter}, ROM {rom[s+4]}/{rom[s+0x3f]}, finished {socket.Finished}/{rom[s]}.");
                    FailIf(_inventory.Slates != rom[WramAddress.wNumSlates] || _saveData.GetRoomFlags(5, 0x8a) != rom[0xca8a] ||
                        _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                        _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != rom[0xcc02],
                        $"D8 slate update{update}: inventory, socket flag or input mask differs.");
                    foreach (int pos in new[] { 0x77, 0x86, 0x88 })
                        FailIf(_currentRoom.GetMetatile(new((pos & 15) * 16 + 8, (pos >> 4) * 16 + 8)) != rom[0xcf00 + pos],
                            $"D8 slate update{update}: source socket/torch tile${pos:x2} differs.");
                    FailIf(!sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        $"D8 slate update{update}: source door-close cue differs.");
                });
            Step(4);
            for (int walk = 0; !_dialogue.IsOpen && walk < 60; walk++) Step(move: true);
            FailIf(!_dialogue.IsOpen || rom[0xcba0] == 0 ||
                _dialogue.CurrentMessage != "It looks like\nsomething fits\nin this hole." || _inventory.Slates != 0 || socket.Finished,
                $"Pushing source socket without slate must show TX_5111; Link={_player.Position}, counter={socket.PushCounter}, native radii={rom[s+0x26]}/{rom[s+0x27]}, text={_dialogue.SourceTextId:x4}, tiles={string.Join(',', Enumerable.Range(6,4).Select(y => _currentRoom.GetTerrainInfo(new(120,y*16+8)).Collision))}.");
            _dialogue.Close(); rom[0xcba0] = 0;
            Step(2);
            _inventory.GiveTreasure(TreasureId.Slate, 0); rom[WramAddress.wNumSlates] = 1;
            for (int push = 0; !socket.Finished && push < 20; push++) Step(move: true);
            FailIf(!socket.Finished || _inventory.Slates != 0 || !_saveData.HasRoomFlag(5, 0x8a, 1) ||
                _currentRoom.GetMetatile(new(120, 120)) != 0xd4 || _currentRoom.GetMetatile(new(104, 136)) != 9 ||
                _currentRoom.GetMetatile(new(136, 136)) != 9,
                "One slate must fill socket$00, set ROOMFLAG$01 and light the original torch pair$86/$88.");
            Step(12, true);
            LoadValidationRoom(5, 0x8a); StepGameplayUpdates(3, Vector2.Zero);
            FailIf(_entities.Entities<SlateSlotRoomEntity>().Count != 3 || _inventory.Slates != 0,
                "Re-entry must remove the filled slate socket and preserve consumed inventory.");
        }
        GD.Print("Validated clean-US D8 slate socket approach, missing-slate text, repeated push, tile/torch writes, inventory and re-entry.");
    }
}
