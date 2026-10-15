using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareInteractionSpritePixelsRom(SomariaRom rom, Texture2D texture,
        Vector2 drawOffset, int slot, int expectedCells, string context)
    {
        // Decode executed native VRAM/OAM/palettes independently of the
        // imported PNG, animation definitions and grayscale metadata.
        var native = rom.CreateMenuView();
        var source = ValidationRom.LoadCleanUs();
        int address = 0x14 * 0x4000 + rom.Word(slot + 0x1e) - 0x4000;
        FailIf(source.Span[address] != expectedCells, context + $": expected {expectedCells} native OBJ cells.");
        using Image actual = texture.GetImage();
        int width = actual.GetWidth(), height = actual.GetHeight();
        var expected = new Color[width * height];
        for (int cell = expectedCells - 1; cell >= 0; cell--)
        {
            int row = address + 1 + cell * 4;
            int flags = source.Span[row + 3] ^ rom[slot + 0x1c];
            int tile = (source.Span[row + 2] + rom[slot + 0x1d]) & 0xfe;
            int originX = unchecked((sbyte)source.Span[row + 1]) - 8 - (int)drawOffset.X;
            int originY = unchecked((sbyte)source.Span[row]) - 16 - (int)drawOffset.Y;
            FailIf(originX < 0 || originX + 8 > width || originY < 0 || originY + 16 > height,
                context + ": runtime texture bounds/offset clip native OAM.");
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int sourceY = (flags & 0x40) == 0 ? y : 15 - y;
                int bit = (flags & 0x20) == 0 ? 7 - x : x;
                int gfx = 0x8000 + tile * 16 + sourceY * 2;
                int bank = flags >> 3 & 1;
                int shade = (native.MapGfx(bank, gfx) >> bit & 1) |
                    (native.MapGfx(bank, gfx + 1) >> bit & 1) << 1;
                if (shade == 0) continue;
                int rgb = native.HudColor(true, flags & 7, shade);
                expected[(originY + y) * width + originX + x] =
                    new Color((rgb & 31) / 31f, (rgb >> 5 & 31) / 31f, (rgb >> 10 & 31) / 31f);
            }
        }
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Color pixel = actual.GetPixel(x, y), wanted = expected[y * width + x];
            FailIf(Math.Abs(pixel.A - wanted.A) > 1f / 255 || wanted.A != 0 &&
                (Math.Abs(pixel.R - wanted.R) > 1f / 255 || Math.Abs(pixel.G - wanted.G) > 1f / 255 ||
                 Math.Abs(pixel.B - wanted.B) > 1f / 255),
                context + $": pixel ({x},{y})={pixel}, clean-US OBJ={wanted}.");
        }
    }

    private void CompareInteractionPalettePixelsRom(SomariaRom rom, NpcCharacter actor, int paletteIndex, string context)
    {
        var palette = rom.CreateMenuView();
        var colors = Enumerable.Range(1, 3).Select(shade =>
        {
            int rgb = palette.HudColor(true, paletteIndex, shade);
            return new Color((rgb & 31) / 31f, (rgb >> 5 & 31) / 31f, (rgb >> 10 & 31) / 31f);
        }).ToArray();
        var frames = SomariaPrivate<List<NpcCharacterAnimationFrame>>(actor, "_scriptAnimation");
        if (frames.Count == 0)
            frames = SomariaPrivate<List<NpcCharacterAnimationFrame>[]>(actor, "_facingAnimations")
                [(int)SomariaPrivate<NpcCharacterFacing>(actor, "_facing")];
        using Image image = frames[actor.CurrentAnimationFrame].Texture.GetImage();
        for (int y = 0; y < image.GetHeight(); y++)
        for (int x = 0; x < image.GetWidth(); x++)
        {
            Color pixel = image.GetPixel(x, y);
            if (pixel.A == 0) continue;
            FailIf(!colors.Any(color => Math.Abs(pixel.R - color.R) <= 1f / 255 &&
                Math.Abs(pixel.G - color.G) <= 1f / 255 && Math.Abs(pixel.B - color.B) <= 1f / 255),
                $"{context}: pixel ({x},{y})={pixel} is outside native OBJ palette ${paletteIndex:x2}.");
        }
    }

    private void CompareInteractionOamRom(SomariaRom rom, NpcCharacter actor, int slot, string context)
    {
        string encoded = SomariaPrivate<string>(actor, "_scriptAnimationSource");
        if (encoded.Length == 0) encoded = actor.Record.DownAnimation;
        var frame = OracleGraphicsCache.GetAnimationDefinition(encoded).Frames[actor.CurrentAnimationFrame];
        int counter = frame.Duration - (int)SomariaPrivate<double>(actor, "_animationTicks");
        var image = ValidationRom.LoadCleanUs();
        int address = rom.Word(slot + 0x1e);
        byte ReadOam(int offset) => image.Span[0x14 * 0x4000 + address - 0x4000 + offset];
        int count = ReadOam(0);
        FailIf(count is < 1 or > 40, $"{context}: invalid native interaction OAM count {count} at $14:${address:x4}.");
        string oam = string.Join(';', Enumerable.Range(0, count).Select(cell =>
            string.Join(',', Enumerable.Range(0, 4).Select(b => ReadOam(1 + cell * 4 + b)))));
        // Derive bounds from executed ROM OAM, including negative offsets and
        // sprites extending beyond the old 32x32 canvas.
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int cell = 0; cell < count; cell++)
        {
            int y = unchecked((sbyte)ReadOam(1 + cell * 4));
            int x = unchecked((sbyte)ReadOam(2 + cell * 4));
            minX = Math.Min(minX, x); minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x + 8); maxY = Math.Max(maxY, y + 16);
        }
        Vector2I size = new(maxX - minX, maxY - minY);
        Vector2 offset = new(minX - 8, minY - 16);
        FailIf(actor.CurrentAnimationTextureSize != size || actor.CurrentAnimationOffset != offset,
            $"{context}: complete ROM OAM requires size={size}/offset={offset}, rendered={actor.CurrentAnimationTextureSize}/{actor.CurrentAnimationOffset}.");
        FailIf(counter != rom[slot + 0x20] || actor.CurrentAnimationParameter != rom[slot + 0x21] ||
            frame.EncodedOam != oam || actor.Visible != ((rom[slot + 0x1a] & 0x80) != 0) ||
            actor.Record.Palette != (rom[slot + 0x1c] & 7),
            $"{context}: animation counter={counter}/{rom[slot + 0x20]}, parameter={actor.CurrentAnimationParameter}/{rom[slot + 0x21]}, visible={actor.Visible}/{rom[slot + 0x1a]:x2}, palette={actor.Record.Palette}/{rom[slot + 0x1c]:x2}, OAM={frame.EncodedOam}/{oam}.");
    }
}
