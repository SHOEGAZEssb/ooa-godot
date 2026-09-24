using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoomRasterization()
    {
        // Independent, pixel-by-pixel GBC BG reference: signed tile addressing,
        // attribute palette/flips, and the live VRAM bytes at this boundary.
        void Verify(OracleRoomData room)
        {
            using Image vram = room.CaptureLiveGraphics();
            using Image expected = Image.CreateEmpty(room.Width, room.Height, false, Image.Format.Rgba8);
            for (int y = 0; y < room.Height; y++)
            for (int x = 0; x < room.Width; x++)
            {
                int tile = room.GetBackgroundSubtileForValidation(x / 8, y / 8) ^ 0x80;
                int attribute = room.GetBackgroundAttributeForValidation(x / 8, y / 8);
                int px = (attribute & 0x20) != 0 ? 7 - x % 8 : x % 8;
                int py = (attribute & 0x40) != 0 ? 7 - y % 8 : y % 8;
                float red = vram.GetPixel(tile % 16 * 8 + px, tile / 16 * 8 + py).R;
                int shade = Mathf.Clamp(Mathf.RoundToInt((1 - red) * 3), 0, 3);
                expected.SetPixel(x, y, room.BackgroundPalettes.Resolve(attribute & 7, shade));
            }
            // Inspect the CPU result directly: the dummy rendering backend
            // may retain a prior readback within one synchronous host frame.
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var headers = (int[])typeof(OracleRoomData).GetField("_activeAnimationHeaders", flags)!.GetValue(room)!;
            using Image actual = (Image)typeof(OracleRoomData).GetMethod("RenderRoom", flags)!.Invoke(room, [headers])!;
            FailIf(!expected.GetData().SequenceEqual(actual.GetData()),
                $"Room {room.Group:x1}:{room.Id:x2} raster differs from signed BG tile/attribute reference.");
            using Image clear = (Image)typeof(OracleRoomData).GetMethod("RenderClearedTilemap", flags)!.Invoke(room, null)!;
            Image hud = OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/gfx_hud.png");
            using Image expectedClear = Image.CreateEmpty(room.Width, room.Height, false, Image.Format.Rgba8);
            for (int y = 0; y < room.Height; y++)
            for (int x = 0; x < room.Width; x++)
            {
                int shade = Mathf.RoundToInt((1 - hud.GetPixel(x % 8, y % 8).R) * 3);
                expectedClear.SetPixel(x, y, room.BackgroundPalettes.Resolve(0, shade));
            }
            FailIf(!expectedClear.GetData().SequenceEqual(clear.GetData()),
                $"Room {room.Group:x1}:{room.Id:x2} cleared BG tilemap changed.");
        }

        foreach (var id in new[] { (0, 0x6a), (0, 0x6b), (4, 0x07) })
        {
            LoadValidationRoom(id.Item1, id.Item2);
            Verify(_currentRoom);
            for (int tick = 1; tick <= 32; tick++) _currentRoom.UpdateAnimation(tick);
            Verify(_currentRoom);
        }
        foreach (bool entering in new[] { true, false })
        {
            LoadValidationRoom(0, entering ? 0x6a : 0x6b);
            OracleRoomData source = _currentRoom;
            _transitions.BeginScroll(_player, entering ? Vector2I.Right : Vector2I.Left, entering ? 0x6b : 0x6a);
            OracleRoomData target = _currentRoom;
            int total = _transitions.ScrollTotalFrames;
            for (int tick = 1; tick <= total; tick++)
            {
                UpdateScrollingTransition(1.0 / 60);
                if (tick is 1 or 3 or 8 or 16 or 32 || tick == total)
                {
                    if (tick != total) Verify(source);
                    Verify(target);
                }
            }
            GD.Print($"Yoll {(entering ? "entry" : "exit")}: room and cleared-tilemap reference pixels match.");
        }
    }
}
