using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateChangedTileGraphicsRom()
    {
        int hostCase1 = 0;
        foreach (var location in new[] { (0, 0x33), (4, 0xbf) })
        foreach (bool allBuffers in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(location.Item1, location.Item2); _entities.Clear();
            _player.WarpTo(new(80, 88));
            var rom = new TileQueueRom(); rom.Reset(); rom.SeedRoom(_currentRoom);
            byte edge = (byte)((_currentRoom.HeightInTiles - 1) * 16 + _currentRoom.WidthInTiles - 1);
            byte[] positions = [0, 0x11, edge];
            void Compare()
            {
                FailIf(_rooms.PendingTileGraphics != rom.Count, "ROM/session tile queue count differs.");
                foreach (byte packed in positions)
                {
                    int x = packed & 15, y = packed >> 4;
                    Vector2 point = new(x * 16 + 8, y * 16 + 8);
                    FailIf(_currentRoom.GetMetatile(point) != rom[0xcf00 + packed] ||
                        _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00 + packed] ||
                        _currentRoom.GetUnderlyingMetatile(point) != rom.Bank(3, 0xdf00 + packed),
                        $"ROM logical/underlying tile buffers differ at ${packed:x2}.");
                    for (int q = 0; q < 4; q++)
                    {
                        int sx = x * 2 + q % 2, sy = y * 2 + q / 2;
                        FailIf(_currentRoom.GetBackgroundSubtileForValidation(sx, sy) != rom.Bank(3, 0xd800 + sy * 32 + sx) ||
                            _currentRoom.GetBackgroundAttributeForValidation(sx, sy) != rom.Bank(3, 0xdc00 + sy * 32 + sx),
                            $"ROM queued tile/attribute graphics differ at ${packed:x2}, quarter={q}.");
                    }
                }
            }
            void Write(byte position, byte tile)
            {
                // LeverLavaFillerRoomEntity uses these owners in this order,
                // matching setTileInAllBuffers even when setTile fails.
                if (allBuffers) _currentRoom.SetUnderlyingStorageMetatile(position, tile);
                bool expected = rom.SetTile(position, tile, allBuffers);
                FailIf(_rooms.TrySetTile(position, tile) != expected, "ROM/session tile write acceptance differs.");
                Compare();
            }
            for (int i = 0; i < 33; i++) Write(positions[i % positions.Length], (byte)(i % 2 == 0 ? 0x1d : 0xa3));
            var preloaded = _rooms.GetRoom(location.Item1, location.Item1 == 0 ? 0x34 : 0xbb);
            Compare(); // Preloading must preserve the active queue and buffers.
            void AfterUpdate() { rom.Drain(1); Compare(); }
            StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: AfterUpdate);
            for (int i = 0; i < 9; i++) Write(0x11, (byte)(i % 2 == 0 ? 0xa3 : 0x1d));
            StepGameplayUpdates(9, Vector2.Zero, batched: batched, afterUpdate: AfterUpdate);
            FailIf(rom.Count != 0, "ROM gameplay queue did not fully drain.");
            // Native room-load and scroll-entry routines clear both indices.
            // Old ring entries remain in RAM but must never replay.
            for (int kind = 0; kind < 3; kind++)
            {
                Write(0x11, 0x1d);
                rom.Clear(scrolling: kind == 1);
                if (kind == 0) _rooms.Load(location.Item1, location.Item2);
                else if (kind == 1) _rooms.SetLoadedRoom(location.Item1, preloaded);
                else _rooms.LoadCutsceneRoom(location.Item1, location.Item2);
                FailIf(_rooms.PendingTileGraphics != rom.Count || rom.Count != 0, "Room load did not match native tile queue clearing.");
                rom.SeedRoom(_currentRoom);
                Write(0x11, 0xa3);
                _rooms.UpdateChangedTileGraphics(1); rom.Drain(1); Compare();
            }
        }
        GD.Print("Validated native logical/collision/underlying buffers and queued tile/attribute graphics in small/large rooms, full/wrapped queues, room-load clears and individual/batched gameplay.");
    }

    private void ValidateChangedTileScrollRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33); _entities.Clear();
            _player.WarpTo(new(155, 64));
            var rom = new TileQueueRom(); rom.Reset(); rom.SeedRoom(_currentRoom);
            _rooms.TrySetTile(0x11, 0x1d); rom.SetTile(0x11, 0x1d);
            _transitions.BeginScroll(_player, Vector2I.Right, 0x34); _entities.Clear();
            rom.Clear(scrolling: true);
            FailIf(_rooms.PendingTileGraphics != rom.Count, "Scroll entry did not clear the source tile queue.");
            rom.SeedRoom(_currentRoom);
            for (int i = 0; i < 5; i++) { _rooms.TrySetTile(0x11, (byte)(0x20 + i)); rom.SetTile(0x11, (byte)(0x20 + i)); }
            void Compare(bool frozen)
            {
                rom.Drain(frozen ? (byte)8 : (byte)1);
                FailIf(_rooms.PendingTileGraphics != rom.Count, "Scroll update drained the changed-tile queue at the wrong phase.");
                for (int q = 0; q < 4; q++)
                {
                    int x = 2 + q % 2, y = 2 + q / 2;
                    FailIf(_currentRoom.GetBackgroundSubtileForValidation(x, y) != rom.Bank(3, 0xd800 + y * 32 + x) ||
                        _currentRoom.GetBackgroundAttributeForValidation(x, y) != rom.Bank(3, 0xdc00 + y * 32 + x),
                        "Scroll freeze/release tile graphics differ from the ROM.");
                }
            }
            StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched: batched, afterUpdate: () => Compare(true));
            FailIf(_transitions.ScrollActive || rom.Count != 5, "Final frozen scroll update must retain all five writes.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () => Compare(false));
            FailIf(rom.Count != 0, "Post-scroll updates did not finish draining destination writes.");
        }
        GD.Print("Validated actual scroll-entry queue clearing, frozen destination graphics, final-update gating and post-scroll draining against native queue routines.");
    }
}
