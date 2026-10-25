using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoomTileBatches()
    {
        foreach (var (group, id) in new[] { (0, 0x11), (5, 0xa7), (2, 0x90) })
        {
            var sequential = new OracleWorldData().LoadRoom(group, id);
            var batched = new OracleWorldData().LoadRoom(group, id);
            foreach (OracleRoomData room in new[] { sequential, batched })
            {
                _ = room.UnderwaterBackgroundTexture;
                room.SetInterleavedMetatile(new(8, 8), 0xa0, 0x20, 0, 0);
                room.SetPositionTileAndCollision(new(24, 8), 0xa1, 15, 0, preserveRenderedTile: true);
            }
            RoomTileWrite[] writes = [new(new(8, 8), 0xa0, 3), new(new(24, 8), 0xa2, null),
                new(new(40, 8), 0xa3, 15), new(new(8, 8), 0xa4, null),
                new(new(sequential.Width - 8, sequential.Height - 8), 0xa5, 5)];
            foreach (RoomTileWrite write in writes)
                sequential.SetPositionTileAndCollision(write.Position, write.Tile, write.Collision, 7);
            batched.SetTilesAndCollisions(writes, 7);
            void Compare()
            {
                FailIf(!sequential.Layout.SequenceEqual(batched.Layout) ||
                    !sequential.CaptureStorageCollisions().SequenceEqual(batched.CaptureStorageCollisions()) ||
                    !sequential.CaptureBackgroundMappings(nativeStorage: true).SequenceEqual(batched.CaptureBackgroundMappings(nativeStorage: true)),
                    $"Room ${group:x1}:${id:x2}: batched ordered writes changed layout, padding, collision or visual overrides.");
                foreach (bool underwater in new[] { false, true })
                {
                    using Image expected = (underwater ? sequential.UnderwaterBackgroundTexture : sequential.Texture).GetImage();
                    using Image actual = (underwater ? batched.UnderwaterBackgroundTexture : batched.Texture).GetImage();
                    FailIf(!expected.GetData().SequenceEqual(actual.GetData()),
                        $"Room ${group:x1}:${id:x2}: final batched pixels differ, underwater={underwater}.");
                }
            }
            Compare();
            batched.SetTilesAndCollisions([], 99);
            // An empty batch must not advance animation or publish an image.
            Compare();
            // Independent pins: last write wins and null removes the explicit
            // collision override, including the original sixteen-byte stride.
            int last = (sequential.HeightInTiles - 1) * (sequential.Layout.Length / sequential.HeightInTiles) + sequential.WidthInTiles - 1;
            FailIf(batched.Layout[0] != 0xa4 || batched.CaptureStorageCollisions()[0] != batched.GetCollision(0xa4) ||
                batched.Layout[last] != 0xa5 || batched.CaptureStorageCollisions()[last] != 5,
                $"Room ${group:x1}:${id:x2}: batch lost ordered overwrite, collision removal or last playable tile.");

            sequential.SetPositionTileAndCollision(new(8, 8), 0xa6, 7, 8);
            bool rejected = false;
            try { batched.SetTilesAndCollisions([new(new(8, 8), 0xa6, 7), new(new(batched.Width, 8), 0, 0)], 8); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            FailIf(!rejected, "A room tile batch must reject the first padding/out-of-room column.");
            Compare();
            // The ordinary immediate and native queued APIs must still work
            // after a batch; their intermediate display is intentionally kept.
            foreach (OracleRoomData room in new[] { sequential, batched })
                room.SetPositionTileAndCollision(new(40, 8), 0xa7, 1, 9, preserveRenderedTile: true);
            Compare();
            foreach (OracleRoomData room in new[] { sequential, batched })
                room.ApplyQueuedTileGraphics(new(0x02, 0xa7), 9);
            Compare();
        }
    }
}
