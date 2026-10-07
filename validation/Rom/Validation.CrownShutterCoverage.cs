using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterCoverage()
    {
        // Independent mainData.s transcription supplements the executed
        // parser: assert source content/order as well as runtime allocation.
        var placements = new (int Room,int SubId,int Position)[] {
            (0x9d,6,0xa7),(0xa6,7,0x50),(0xb4,0x0b,0x80),(0xb4,8,0x07),
            (0xb6,8,0x07),(0xbf,8,0x07),(0xbf,0x0b,0x50)
        };
        foreach (int room in placements.Select(row => row.Room).Distinct())
        {
            ReinitializeGameplayForValidation(); EnsureHarpAndSongs(); LoadValidationRoom(4,room);
            var seed = _random.CaptureState();
            var native = new FrontendRom();
            for (int address = 0xc5b0; address < 0xcb00; address++) native[address] = _saveData.ReadWramByte(address);
            native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
            native[0xcc2d] = 4; native[0xcc30] = (byte)room; native[0xcc05] = 0xff;
            native[0xcd00] = 1; native[0xcc4a] = 0xf0;
            native.LoadRoomTileset();
            for (int y = 0; y < _currentRoom.HeightInTiles; y++)
            for (int x = 0; x < _currentRoom.WidthInTiles; x++)
            {
                Vector2 point = new(x*16+8,y*16+8); int packed = y*16+x;
                native[0xcf00+packed] = _currentRoom.GetMetatile(point);
                native[0xce00+packed] = (byte)_currentRoom.GetTerrainInfo(point).Collision;
            }
            native.Call(0x55b7,0x12); // objectData.parseObjectData, including original nested pointers.
            int[] slots = Enumerable.Range(0xd2,14).Select(page => (page<<8)|0x40)
                .Where(address => native[address] != 0 && native[address+1] == 0x1e).ToArray();
            var expected = placements.Where(row => row.Room == room).ToArray();
            var actual = _entities.Entities<DungeonDoorRoomEntity>().OrderBy(_entities.InteractionSlot).ToArray();
            FailIf(slots.Length != expected.Length || actual.Length != expected.Length,
                $"Crown4:{room:x2} original parser and runtime must retain every source shutter.");
            for (int index = 0; index < slots.Length; index++)
            {
                var door = actual[index]; int slot = slots[index];
                FailIf(native[slot+2] != expected[index].SubId || native[slot+0xb] != expected[index].Position ||
                    native[slot+0xd] != 0 || native[slot+4] != 0 ||
                    door.SubId != expected[index].SubId || door.PackedPosition != expected[index].Position ||
                    _entities.InteractionSlot(door) != (slot>>8)-0xd0 || !door.EnemyCompletionSupported ||
                    SomariaPrivate<DoorState>(door,"_state") != DoorState.Initialize,
                    $"Crown4:{room:x2} shutter{index} must retain source order, exact first-free slot, parameters and pending initialization.");
            }
            FailIf(_currentRoom.TilesetFlags != native[0xcc34] || (native[0xcc34]&0x7e) == 0,
                $"Crown4:{room:x2} tileset loader must retain the original dungeon Harp restriction.");
            // Bounded completion-gate comparison. The original parent reaches
            // @harp only after its animation terminal bit; full input/playback,
            // notes and caller cleanup are covered by the Harp ROM scenarios.
            // Supply that boundary here, without repeating 259-update songs.
            for (int song = 1; song <= 3; song++)
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _inventory.SelectHarpSong(song);
                FailIf(_inventory.SelectedHarpSong != song,"Crown completion fixture must select an owned tune.");
                FailIf(_harp.TryStart(_player) <= 0,"Crown completion fixture must begin its selected tune.");
                native[0xc6b7] = (byte)song; native[0xcc8d] = (byte)song;
                native[0xd500] = 1; native[0xd501] = 0x11;
                // Clean-US bank6:4e1a harpFluteParent @harp; HL is the
                // selected-song byte returned by @getSelectedSongAddr.
                byte[] caller = [0x21,0xb7,0xc6,0x16,0xd5,0xcd,0x1a,0x4e,0xc9];
                for (int index = 0; index < caller.Length; index++) native[0xc100+index] = caller[index];
                native.Call(0xc100,6); _harp.Complete(_player,song);
                FailIf(native[0xd500] != 0 || native[0xcc04] != 0 || native[0xc4ab] != 0 || native[0xcba0] != 0 ||
                    _transitions.IsTransitioning || _transitions.PaletteFadeActive || _dialogue.IsOpen || _harp.IsPlaying ||
                    _harp.PlayingInstrument != native[0xcc8d],
                    $"Crown4:{room:x2} tune${song:x2} repeat{repeat} must clear the parent, preserve its completed signal and reject text/time travel before the tune branch.");
            }
        }
    }
}
