using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoomEntryEffectsRom()
    {
        var effects = new RoomEntryEffectDatabase();
        foreach (var room in new (int Group, int Id)[] { (5, 0xd2), (2, 0x0e), (0, 0x39), (0, 0x3a),
            (1, 0x0e), (3, 0x0f), (4, 0xe6) })
        foreach (bool flag in new[] { false, true })
        foreach (int active in new[] { 0, 0xff, 0x28 })
        foreach (int direction in room.Id == 0x39 ? new[] { 0, 1, 2, 3 } : new[] { 0 })
        {
            _saveData.RestoreFrom(OracleSaveData.CreateStandardGame());
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            _saveData.SetGlobalFlag(AgesGlobalFlag.SavedNayru, flag);
            _saveData.SetGlobalFlag(AgesGlobalFlag.RalphEnteredPortal, flag);
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, flag);
            _saveData.SetMinimapLocation(2, 0x77);
            _saveData.WriteWramByte(0xc806, 0xa5);
            int assigned = _sound.Data.RoomMusic(room.Group, room.Id);
            _sound.SetNativeMusicSelection(assigned, active);
            var native = new FrontendRom();
            for (int address = 0xc5b0; address < 0xcb00; address++) native[address] = _saveData.ReadWramByte(address);
            native[0xcc2d] = (byte)room.Group; native[0xcc30] = (byte)room.Id;
            native[0xcc35] = (byte)active; native[0xcc46] = (byte)assigned; native[0xcd02] = (byte)direction;
            native.Call(0x5872, 0x12); // runRoomSpecificCode, including its actual group dispatch.
            Vector2I entry = direction switch { 0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, _ => Vector2I.Left };
            effects.Apply(room.Group, room.Id, _saveData, _sound, entry);
            string context = $"Room entry {room.Group:x1}:{room.Id:x2}, flag={flag}, music=${active:x2}, direction=${direction:x2}";
            for (int address = 0xc5b0; address < 0xcb00; address++)
                FailIf(native[address] != _saveData.ReadWramByte(address),
                    $"{context}: WRAM ${address:x4} differs from the clean-US handler.");
            FailIf(_sound.NativeRoomMusic != native[0xcc46] || _sound.NativeActiveMusic != native[0xcc35] ||
                native.RandomCalls != 0 || native.Sounds.Count != 0,
                context + ": room code must preserve RNG and select music without requesting sound.");

            // Literal expectations independently retain partial minimap writes,
            // exact flag polarity, direction gating and zero-only music reset.
            int expectedMusic = room switch
            {
                (2, 0x0e) when !flag => 0x1f,
                (0, 0x39) when !flag && direction == 1 => 0x35,
                (0, 0x3a) when flag => 0x08,
                _ => assigned
            };
            FailIf(_sound.NativeRoomMusic != expectedMusic ||
                room == (5, 0xd2) && (_saveData.MinimapGroup != 2 || _saveData.MinimapRoom != 6 || _saveData.ReadWramByte(0xc806) != 0xb5) ||
                room == (1, 0x0e) && (_saveData.MinimapGroup != 0 || _saveData.MinimapRoom != 0x3c) ||
                room == (3, 0x0f) && (_saveData.MinimapGroup != 1 || _saveData.MinimapRoom != 0x3c) ||
                _sound.NativeActiveMusic != (room == (4, 0xe6) && active == 0 ? 0xff : active),
                context + ": source-derived write or branch differs.");
            var sounds = _sound.AttachPlayRequestAudit();
            native.Call(0x5e4d, 1); // checkPlayRoomMusic after initializeRoom.
            _sound.CheckPlayRoomMusic();
            FailIf(_sound.NativeActiveMusic != native[0xcc35] || !sounds.Requests.SequenceEqual(native.Sounds),
                context + ": checkPlayRoomMusic must compare the native gate and retain source cue order.");
        }
    }

    private void ValidateRoomEntryEffectsGameplay()
    {
        foreach (bool batched in new[] { false, true })
        foreach (var room in new (int Group, int Id)[] { (5, 0xd2), (2, 0x0e), (0, 0x3a),
            (1, 0x0e), (3, 0x0f), (4, 0xe6) })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame);
            _saveData.SetGlobalFlag(AgesGlobalFlag.SavedNayru, false);
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            int entryMusic = -1, entryActive = -1;
            void ObserveEntry(int group, OracleRoomData data)
            {
                if (group != room.Group || data.Id != room.Id) return;
                entryMusic = _sound.NativeRoomMusic;
                entryActive = _sound.NativeActiveMusic;
            }
            _entities.RoomInitialized += ObserveEntry;
            void Check()
            {
                FailIf(room == (5, 0xd2) && (_saveData.MinimapRoom != 6 || !_saveData.HasRoomFlag(1, 6, 0x10)) ||
                    room == (1, 0x0e) && (_saveData.MinimapGroup != 0 || _saveData.MinimapRoom != 0x3c) ||
                    room == (3, 0x0f) && (_saveData.MinimapGroup != 1 || _saveData.MinimapRoom != 0x3c) ||
                    room == (2, 0x0e) && entryMusic != 0x1f ||
                    room == (0, 0x3a) && entryMusic != 0x08 ||
                    room == (4, 0xe6) && entryActive != 0x28,
                    $"Room {room.Group:x1}:{room.Id:x2} lost its entry side effect through gameplay (batch={batched}).");
            }
            // Resource preload and native background-only loads must not run
            // initializeRoom, mark the palace flag, or replace music selection.
            LoadValidationRoom(0, 0x60);
            _saveData.SetRoomFlag(1, 6, 0x10, false);
            _saveData.SetMinimapLocation(2, 0x77);
            int selected = _sound.NativeRoomMusic;
            _rooms.GetRoom(room.Group, room.Id);
            OracleRoomData background = _rooms.LoadCutsceneRoom(room.Group, room.Id);
            _entities.LoadCutsceneRoom(room.Group, background, includeTimePortals: false);
            FailIf(_saveData.MinimapGroup != 2 || _saveData.MinimapRoom != 0x77 ||
                _saveData.HasRoomFlag(1, 6, 0x10) || _sound.NativeRoomMusic != selected,
                "Preload/background-only load must bypass room-entry side effects.");

            // Use an actual fade warp, then repeat ordinary entry. Vire's
            // audible track already matches, but its zero gate still resets
            // and causes one native request for that same track.
            LoadValidationRoom(room.Group, room.Id);
            int destination = _currentRoom.GetPackedPosition(FindSpawn());
            LoadValidationRoom(0, 0x60);
            _sound.PlaySound(0x28); _sound.SetNativeActiveMusic(0);
            var sounds = _sound.AttachPlayRequestAudit();
            _transitions.ApplyWarp(_player, new Warp(0, 0x60, -1, 0, 0,
                room.Group, room.Id, destination, 0, WarpDestinationTransition.Basic));
            for (int update = 0; IsTransitioning && update < 300; update += 2) Step(2);
            FailIf(IsTransitioning || _activeGroup != room.Group || _currentRoom.Id != room.Id,
                $"Entry regression failed to warp into {room.Group:x1}:{room.Id:x2}.");
            Check();
            if (room == (4, 0xe6)) FailIf(sounds.Requests.Count(cue => cue == 0x28) != 1,
                "Vire room $4:$e6 must request its room music once after resetting a zero gate.");
            int cues = sounds.Requests.Count;
            Step(2); Check();
            FailIf(sounds.Requests.Count != cues, "Completed room entry replayed a sound on idle updates.");
            LoadValidationRoom(room.Group, room.Id); Check();
            _entities.RoomInitialized -= ObserveEntry;
        }

        foreach (bool batched in new[] { false, true })
        foreach (bool completed in new[] { false, true })
        foreach (Vector2I direction in new[] { Vector2I.Right, Vector2I.Up })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            _saveData.SetGlobalFlag(AgesGlobalFlag.RalphEnteredPortal, completed);
            LoadValidationRoom(0, 0x38);
            _sound.SetNativeActiveMusic(0xff);
            _transitions.BeginScroll(_player, direction, 0x39);
            for (int update = 0; IsTransitioning && update < 300; update += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            int expected = !completed && direction == Vector2I.Right ? 0x35 : 0x03;
            FailIf(IsTransitioning || _sound.NativeRoomMusic != expected || _sound.ActiveMusic != expected,
                $"Ralph $0:$39 room-pack entry must select ${expected:x2} (direction={direction}, completed={completed}, batch={batched}).");
            // Cancelling the retained entry event by leaving must let the
            // next room select its own assignment; direct re-entry is not rightward.
            LoadValidationRoom(0, 0x60); LoadValidationRoom(0, 0x39);
            FailIf(_sound.NativeRoomMusic != 0x03,
                "Direct re-entry into Ralph's $0:$39 screen must clear the rightward-only music selection.");
        }
    }
}
