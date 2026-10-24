using Godot;
using System.Threading.Tasks;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private async Task ValidateNativeFullscreenPresentation()
    {
        using Image pattern = Image.CreateEmpty(160, 256, false, Image.Format.Rgba8);
        for (int y = 0; y < 256; y++)
        for (int x = 0; x < 160; x++) pattern.SetPixel(x, y, Color.Color8((byte)x, (byte)y, 0));
        using ImageTexture texture = ImageTexture.CreateFromImage(pattern);
        using Image small = Image.CreateEmpty(160, 128, false, Image.Format.Rgba8);
        small.Fill(Colors.White);
        using ImageTexture smallTexture = ImageTexture.CreateFromImage(small);
        var viewport = new SubViewport { Size = new(160, 144), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest };
        AddChild(viewport);
        // bank0._getObjectPositionOnScreen retains the $10 OBJ origin;
        // gfx-register state $04 displays BG row $00 at display line $00.
        var view = new RoomView { Position = new(0, 16) };
        viewport.AddChild(view);
        try
        {
            foreach (var mode in new (bool Full, bool Tilemap)[] { (false, false), (true, false), (true, true) })
            {
                view.SetRoom(texture);
                if (mode.Tilemap) view.SetNativeTilemap(texture);
                view.SetNativeFullScreenPresentation(mode.Full);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using Image actual = viewport.GetTexture().GetImage();
                for (int y = mode.Full ? 0 : 16; y < 144; y++)
                for (int x = 0; x < 160; x++)
                    FailIf(actual.GetPixel(x, y) != pattern.GetPixel(x, mode.Full ? y : y - 16),
                        $"Native full-screen BG origin differs at ({x},{y}), full={mode.Full}, tilemap={mode.Tilemap}.");
            }
            // func_6fb0's black BG must cover all 144 lines even when the
            // current room has only 128 texture rows and the HUD is absent.
            view.SetRoom(smallTexture);
            view.SetBackgroundFade(Colors.Black, 1);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image faded = viewport.GetTexture().GetImage();
            for (int y = 0; y < 144; y++)
                FailIf(faded.GetPixel(80, y) != Colors.Black, $"Native black backdrop missed display line {y}.");
        }
        finally { viewport.Free(); }
    }
}
