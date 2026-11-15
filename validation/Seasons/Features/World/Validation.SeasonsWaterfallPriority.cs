using System.Threading.Tasks;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private async Task ValidateSeasonsWaterfallPriority()
    {
        foreach (bool batched in new[] { false, true })
        {
            var viewport = new SubViewport { Size = new(160, 144), Disable3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest };
            AddChild(viewport);
            var root = StartSeasonsAfterImpa(batched, viewport);
            try
            {
                var session = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                session.Inventory.GiveTreasure(TreasureId.Flippers, 1);
                session.DebugTravel(3, 0xac);
                session.Link.WarpTo(new(80, 24));
                var priorityView = root._scene.RoomView.GetNode<RoomPriorityView>("Priority");
                FailIf(priorityView.ZIndex != 14 || priorityView.ZIndex <= Player.AlternateTextboxPaletteZIndex,
                    "Seasons $3:ac BG priority must cover Link's ordinary OBJ queue.");
                Texture2D texture = session.Room.GetPriorityTexture();
                int zeroPixels = 0, coveredPixels = 0;
                app.Step(48, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    // rooms/seasons/small/room04ac.bin: $d6 at $04/$05,
                    // $d7 at $14/$15. tilesetMappings2f: $9b/$9e, flags $8d.
                    using Image vram = session.Room.CaptureLiveGraphics();
                    using Image mask = session.Room.RenderPriorityBackground();
                    for (int y = 0; y < 128; y++)
                    for (int x = 0; x < 160; x++)
                    {
                        bool waterfall = x is >= 64 and < 96 && y < 32;
                        int shade = 0;
                        if (waterfall)
                        {
                            int tile = (y < 24 ? 0x9b : 0x9e) ^ 0x80;
                            shade = OracleGraphicsData.PaletteShade(vram.GetPixel(
                                tile % 16 * 8 + x % 8, tile / 16 * 8 + y % 8), false);
                        }
                        Color actual = mask.GetPixel(x, y);
                        FailIf(actual.A != (shade == 0 ? 0 : 1),
                            $"Seasons $3:ac BG priority/color-zero mismatch at ({x},{y}).");
                        if (!waterfall) continue;
                        if (shade == 0) zeroPixels++;
                        else
                        {
                            coveredPixels++;
                            Color expected = session.Room.BackgroundPalettes.Resolve(5, shade);
                            FailIf(Math.Abs(actual.R8 - expected.R8) > 1 || Math.Abs(actual.G8 - expected.G8) > 1 || Math.Abs(actual.B8 - expected.B8) > 1,
                                $"Seasons $3:ac priority pixels lost their live palette at ({x},{y}): shade={shade}, expected={expected}, actual={actual}.");
                        }
                    }
                });
                FailIf(zeroPixels == 0 || coveredPixels == 0,
                    "The animated waterfall must exercise both OBJ-visible color zero and covering colors.");

                using (Image before = session.Room.RenderPriorityBackground())
                {
                    Color[,] original = session.Room.BackgroundPalettes.Capture();
                    Color[,] sameColors = (Color[,])original.Clone();
                    sameColors[5, 0] = sameColors[5, 1];
                    session.Room.BackgroundPalettes.Restore(sameColors);
                    using Image after = session.Room.RenderPriorityBackground();
                    byte[] beforePixels = before.GetData(), afterPixels = after.GetData();
                    for (int offset = 3; offset < beforePixels.Length; offset += 4)
                        FailIf(beforePixels[offset] != afterPixels[offset],
                            "BG priority must use color indices even when zero and nonzero colors have identical RGB.");
                    session.Room.BackgroundPalettes.Restore(original);
                }

                if (DisplayServer.GetName() != "headless")
                {
                    priorityView.Hide();
                    await FlushDraws();
                    using Image withoutPriority = viewport.GetTexture().GetImage();
                    priorityView.Show();
                    await FlushDraws();
                    using Image screen = viewport.GetTexture().GetImage();
                    using Image mask = session.Room.RenderPriorityBackground();
                    using Image background = session.Room.Texture.GetImage();
                    int linkPixels = 0;
                    for (int y = 0; y < 32; y++)
                    for (int x = 64; x < 96; x++)
                    {
                        Color before = withoutPriority.GetPixel(x, y + 16);
                        if (before != background.GetPixel(x, y)) linkPixels++;
                        Color cover = mask.GetPixel(x, y);
                        Color expected = cover.A == 0 ? before : cover;
                        Color actual = screen.GetPixel(x, y + 16);
                        FailIf(actual.R8 != expected.R8 || actual.G8 != expected.G8 || actual.B8 != expected.B8,
                            $"Seasons $3:ac rendered waterfall did not cover Link at ({x},{y}).");
                    }
                    FailIf(linkPixels == 0, "The rendered fixture must actually overlap Link with the waterfall.");
                    await CaptureSeasonsOpeningScreen("waterfall-priority", viewport);
                }

                int uploads = 0;
                void CountUpload() => uploads++;
                texture.Changed += CountUpload;
                try
                {
                    session.Room.SetPositionTileAndCollision(new(72, 8), 0xfa, null, 0);
                    using (Image cleared = session.Room.RenderPriorityBackground())
                        for (int y = 0; y < 16; y++)
                        for (int x = 64; x < 80; x++)
                            FailIf(cleared.GetPixel(x, y).A != 0, "Replacing $d6 must clear its priority pixels.");
                    session.Room.SetPositionTileAndCollision(new(72, 8), 0xd6, null, 0);
                    FailIf(uploads != 2 || !ReferenceEquals(texture, session.Room.GetPriorityTexture()),
                        "Both tile writes must publish the retained priority texture.");
                }
                finally { texture.Changed -= CountUpload; }
                session.DebugTravel(0, 0xb6);
                app.Step(2, Vector2.Zero, batched: batched);
                session.DebugTravel(3, 0xac);
                session.Link.WarpTo(new(80, 24));
                app.Step(2, Vector2.Zero, batched: batched);
                using Image restored = session.Room.RenderPriorityBackground();
                FailIf(!restored.GetData().Where((_, index) => index % 4 == 3).Any(alpha => alpha == 255),
                    "Re-entering Seasons $3:ac must restore the waterfall priority.");

                async Task FlushDraws()
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                }
            }
            finally { root.Free(); viewport.Free(); ResetValidationInput(); }
        }
        GD.Print("Validated Seasons $3:ac animated waterfall priority, color-zero holes, tile replacement and re-entry through split/batched gameplay updates.");
    }
}
