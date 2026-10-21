using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDialoguePaging()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x47);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                _dialogue.MessageSpeed = 3;
                _dialogue.ShowMessage("A\nBB\nCCC\nDDDD\\stop()EE", _player.Position.Y);
                Vector2 start = _player.Position;
                int sounds = _sound.PlayRequestsFor(SoundId.SndText2);
                // Source states 0,1,2,3,4,5 -> 6,7,8,9,a,b,c,d,e,3,4.
                // Clean-US TX_0102 executes the same two-line continuation
                // at updates $0671-$067c. A single press starts both lines.
                // The stop press returns to state 0 while retaining the old
                // mapping; state 0 clears it on the following update.
                int[] glyphs = [0, 0, 1, 1, 3, 3, 3, 3, 2, 2, 5, 5, 5, 5, 3, 3, 7, 7, 0, 0, 2, 2, 2];
                int[] scroll = [0, 0, 0, 0, 0, 8, 8, 16, 0, 0, 0, 8, 8, 16, 0, 0, 0, 0, 0, 0, 0, 0, 0];
                int update = 0;
                void Step(int count, string? button = null)
                {
                    StepGameplayUpdates(count, Vector2.Right,
                        button is null ? [] : [button], button is null ? [] : [button], batched,
                        afterUpdate: () =>
                        {
                            FailIf(_dialogue.VisibleGlyphCount != glyphs[update] ||
                                _dialogue.TextScrollOffset != scroll[update] ||
                                _dialogue.IsOpen != (update < 22) || _player.Position != start,
                                $"Standard dialogue update {update + 1}: glyphs={_dialogue.VisibleGlyphCount}, " +
                                $"scroll={_dialogue.TextScrollOffset}, open={_dialogue.IsOpen}.");
                            update++;
                        });
                }
                Step(2, "attack"); // Opening A cannot reveal or dismiss text.
                Step(1, "item");
                Step(1, "attack");
                Step(1, "item");
                Step(1, "attack"); // One SND_TEXT_2 for both new lines.
                Step(3);
                Step(1, "item");
                Step(1, "attack");
                Step(4); // Automatic shift, without an A/B edge or another cue.
                Step(1, "item");
                Step(1, "attack");
                Step(1, "item"); // Stop returns to state 0, then 1.
                Step(2, "attack");
                Step(1, "item"); // Revealing EE cannot close on the same press.
                Step(1, "attack");
                Step(1); // $10 releases text after the object pass.
                FailIf(_sound.PlayRequestsFor(SoundId.SndText2) != sounds + 2,
                    "Automatic second-line scrolling must not replay SND_TEXT_2.");
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(_dialogue.BlocksPlayerInput, "Dialogue retained modal input after release.");
                _dialogue.ShowMessage("Cancelled\ntext\nMore", _player.Position.Y);
                _dialogue.Close();
                StepGameplayUpdates(3, Vector2.Zero, batched: batched);
                FailIf(_dialogue.IsOpen || _dialogue.IsScrollingText,
                    "Cancelling dialogue retained a pending scroll or reinitialization.");
            }
        }
        GD.Print("Validated standard two-line paging, stop initialization, input ownership, cancellation and repeat.");
        ValidateDialogueComposition();
    }

    private void ValidateDialogueComposition()
    {
        ValidateDialogueCanvasPixels();
        _dialogue.Close();
        const string first = "ABCDEFGHIJKLMNOP", second = "QRSTUVWXYZabcdef";
        _dialogue.ShowMessage(first + "\n" + second, _player.Position.Y);
        _dialogue.RevealCurrentPageForValidation();
        // Independent source atlas addressing: copyTextCharacterGfx copies
        // each ASCII character's consecutive upper/lower 8x8 font cells.
        Image font = OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/gfx_font.png");
        using Image expected = Image.CreateEmpty(160, 40, false, Image.Format.Rgba8);
        expected.Fill(Colors.Transparent);
        expected.FillRect(new Rect2I(8, 0, 144, 40), Colors.Black);
        Color ink = _rooms.World.BackgroundPalettes.Resolve(0, 2);
        for (int row = 0; row < 2; row++)
        for (int column = 0; column < 16; column++)
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int code = (row == 0 ? first : second)[column];
            if (font.GetPixel((code & 15) * 8 + x, (code >> 4) * 16 + y).R > 0.5f)
                expected.SetPixel(16 + column * 8 + x, row * 16 + y, ink);
        }
        using (Image actual = _dialogue.ComposeImage())
            FailIf(!System.Linq.Enumerable.SequenceEqual(expected.GetData(), actual.GetData()),
                "Full dialogue composition differs from the source font, live palette, or textbox boundaries.");
        // Diagnostic only: no machine-dependent timing assertion. Keep this
        // workload identical when measuring changes to dialogue composition.
        var timer = System.Diagnostics.Stopwatch.StartNew();
        for (int frame = 0; frame < 256; frame++)
        {
            using Image image = _dialogue.ComposeImage();
        }
        timer.Stop();
        GD.Print($"Dialogue composition: 256 full two-line pages in {timer.Elapsed.TotalMilliseconds:F3} ms.");
        _dialogue.Close();
    }

    private void ValidateDialogueCanvasPixels()
    {
        using Image source = Image.CreateEmpty(8, 16, false, Image.Format.Rgba8);
        byte[] levels = [0, 1, 63, 127, 128, 192, 254, 255];
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
            source.SetPixel(x, y, new Color(levels[x] / 255f,
                levels[(x + 3) % 8] / 255f, levels[y % 8] / 255f, levels[(x + y) % 8] / 255f));
        using ImageTexture texture = ImageTexture.CreateFromImage(source);
        foreach (Color background in new[] { Colors.Transparent, Colors.Black, new Color(0.2f, 0.7f, 0.4f, 0.5f) })
        foreach (Color modulation in new[] { Colors.White, new Color(0.13f, 0.91f, 0.57f, 0.73f) })
        {
            var canvas = new OracleImageCanvas(12, 20);
            canvas.Fill(background);
            using Image expected = Image.CreateEmpty(12, 20, false, Image.Format.Rgba8);
            expected.Fill(background);
            // Compare with the original native Image operations, including
            // clipped glyphs, subrectangles, overlap, alpha and RGBA8 rounding.
            foreach (Vector2I destination in new[] { new Vector2I(-2, -3), new Vector2I(8, 14), new Vector2I(2, 3) })
            {
                canvas.BlitTexture(texture, new Rect2(destination, new Vector2(6, 12)),
                    new Rect2(1, 2, 6, 12), modulation);
                for (int y = 0; y < 12; y++)
                for (int x = 0; x < 6; x++)
                {
                    int targetX = destination.X + x, targetY = destination.Y + y;
                    if ((uint)targetX >= 12 || (uint)targetY >= 20) continue;
                    Color ink = source.GetPixel(1 + x, 2 + y) * modulation;
                    if (ink.A == 0) continue;
                    Color under = expected.GetPixel(targetX, targetY);
                    float alpha = ink.A + under.A * (1 - ink.A);
                    expected.SetPixel(targetX, targetY, new Color(
                        (ink.R * ink.A + under.R * under.A * (1 - ink.A)) / alpha,
                        (ink.G * ink.A + under.G * under.A * (1 - ink.A)) / alpha,
                        (ink.B * ink.A + under.B * under.A * (1 - ink.A)) / alpha, alpha));
                }
            }
            using Image actual = canvas.ToImage();
            FailIf(!System.Linq.Enumerable.SequenceEqual(expected.GetData(), actual.GetData()),
                "Dialogue RGBA8 composition changed clipping, modulation, alpha or pixel quantization.");
        }
        // Trade-item textures may be rebuilt after palette changes. A later
        // redraw must capture the current image rather than keep stale pixels.
        source.Fill(Colors.Red);
        using ImageTexture replacement = ImageTexture.CreateFromImage(source);
        var refreshed = new OracleImageCanvas(8, 16);
        refreshed.BlitTexture(replacement, new Rect2(0, 0, 8, 16), new Rect2(0, 0, 8, 16), Colors.White);
        using Image result = refreshed.ToImage();
        FailIf(!System.Linq.Enumerable.SequenceEqual(result.GetData(), source.GetData()),
            "Dialogue redraw retained a previous texture snapshot.");
    }
}
