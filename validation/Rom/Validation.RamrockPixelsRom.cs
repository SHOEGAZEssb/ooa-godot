using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRamrockHandPixelsRom()
    {
        var graphics = new FrontendRom { VramDmaTransfersEnabled = true };
        graphics.LoadHudGraphics();
        graphics[0xd081] = 0x07;
        graphics[0xd181] = 0x05;
        // Execute enemyLoadGraphicsAndProperties with the body followed by
        // its hands. Header $ce chains $cf; the hands resolve the second slot.
        byte[] caller = [0x16, 0xd0, 0xcd, 0x68, 0x43,
            0x16, 0xd1, 0xcd, 0x68, 0x43,
            0x3e, 0x83, 0xcd, 0x0b, 0x05, 0xc9];
        for (int i = 0; i < caller.Length; i++) graphics[0xc100 + i] = caller[i];
        graphics.Call(0xc100, 0x3f);
        FailIf(graphics[0xd19d] - graphics[0xd09d] != 0x20 || graphics[0xd19c] != 6,
            "ENEMY$05 must resolve header $cf one $20-tile slot after ENEMY$07/$ce, with OBJ palette$06.");

        var database = new DungeonBossDatabase();
        for (int subid = 0; subid < 6; subid++)
            FailIf(database.Enemy(0x05, subid).Sprites is not ["spr_ramrock_2"],
                $"ENEMY$05:${subid:x2} must address spr_ramrock_2 from header $cf.");
        var record = database.Enemy(0x05, 0);
        var animation = new EnemyAnimationPlayer(_player, record.Animations.Length);
        animation.Load(EnemyVisualSource.LoadComposite(record.Sprites), record.Animations,
            record.TileBase, record.Palette, paletteOverrides: RamrockProfile.Shared.Palette(0x83),
            sourceGrayscaleInverted: record.SourceGrayscaleInverted, positionedOam: true);
        var native = new ObjectAnimationRom(1, 0x05);
        for (int index = 0; index < 8; index++)
        {
            native.Set(index); animation.SetAnimation(index);
            var definition = OracleGraphicsCache.GetAnimationDefinition(record.Animations[index]);
            // Grabber/shield poses are held by ramrockArms.s without calling
            // enemyAnimate. Compare their imported frames without executing
            // beyond those unterminated streams into the following label.
            for (int frame = 0; frame < definition.Frames.Length; frame++)
            {
                FailIf(definition.Frames[animation.FrameIndex].EncodedOam != native.Oam,
                    $"ENEMY$05 animation${index:x2}/frame{frame}: OAM differs from clean US.");
                {
                    using Image actual = animation.CurrentTexture.GetImage();
                    var expected = new Color[actual.GetWidth() * actual.GetHeight()];
                    int[][] cells = native.Oam.Split(';').Select(cell => cell.Split(',').Select(int.Parse).ToArray()).ToArray();
                    foreach (int[] cell in cells.Reverse())
                    {
                        int flags = cell[3] ^ graphics[0xd19c];
                        int tile = (cell[2] + graphics[0xd19d]) & 0xfe;
                        int left = unchecked((sbyte)cell[1]) - 8 - (int)animation.CurrentOffset.X;
                        int top = unchecked((sbyte)cell[0]) - 16 - (int)animation.CurrentOffset.Y;
                        FailIf(left < 0 || top < 0 || left + 8 > actual.GetWidth() || top + 16 > actual.GetHeight(),
                            $"ENEMY$05 animation${index:x2}: positioned bounds clip native OAM.");
                        for (int y = 0; y < 16; y++)
                        for (int x = 0; x < 8; x++)
                        {
                            int row = (flags & 0x40) == 0 ? y : 15 - y;
                            int bit = (flags & 0x20) == 0 ? 7 - x : x;
                            int address = 0x8000 + tile * 16 + row * 2;
                            int bank = flags >> 3 & 1;
                            int shade = (graphics.VramByte(bank, address) >> bit & 1) |
                                (graphics.VramByte(bank, address + 1) >> bit & 1) << 1;
                            if (shade == 0) continue;
                            int palette = 0xdec0 + (flags & 7) * 8 + shade * 2;
                            int rgb = graphics.BankByte(2, palette) | graphics.BankByte(2, palette + 1) << 8;
                            expected[(top + y) * actual.GetWidth() + left + x] =
                                new Color((rgb & 31) / 31f, (rgb >> 5 & 31) / 31f, (rgb >> 10 & 31) / 31f);
                        }
                    }
                    for (int y = 0; y < actual.GetHeight(); y++)
                    for (int x = 0; x < actual.GetWidth(); x++)
                    {
                        Color pixel = actual.GetPixel(x, y), wanted = expected[y * actual.GetWidth() + x];
                        FailIf(Math.Abs(pixel.A - wanted.A) > 1f / 255 || wanted.A != 0 &&
                            (Math.Abs(pixel.R - wanted.R) > 1f / 255 || Math.Abs(pixel.G - wanted.G) > 1f / 255 ||
                             Math.Abs(pixel.B - wanted.B) > 1f / 255),
                            $"ENEMY$05 animation${index:x2}/frame{animation.FrameIndex}: pixel({x},{y})={pixel}, clean-US OBJ={wanted}.");
                    }
                }
                if (frame + 1 < definition.Frames.Length)
                {
                    native.Counter = 1; animation.SetFrameCounter(1);
                    native.Advance(); animation.Advance();
                }
            }
        }
    }
}
