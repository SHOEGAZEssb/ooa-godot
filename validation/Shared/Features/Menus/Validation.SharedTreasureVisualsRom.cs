
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedTreasureVisualsRom()
    {
        foreach (var game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            bool ages = game == OracleGame.Ages;
            var rom = ValidationRom.LoadCleanUs(game);
            var treasures = TreasureDatabase.For(OracleAssetPack.ForGame(game));
            // Literal clean-US interaction60 tables; independent of importer symbols.
            int data = ages ? 0xfe6db : 0xfe6dc;
            int animations = ages ? 0x59e41 : 0x51719;
            int pointers = ages ? 0x5af04 : 0x5284f;
            int oamBank = ages ? 0x50000 : 0x4c000;
            int[] graphics = treasures.Objects.Select(row => row.Graphic).Distinct().Order().ToArray();
            FailIf(graphics.Length != (ages ? 84 : 73),
                $"{game}: the native treasure catalog lost a graphics identity.");
            int Word(int address) => rom.Span[address] | rom.Span[address + 1] << 8;
            foreach (int graphic in graphics)
            {
                var visual = treasures.GetObjectVisual(graphic);
                int row = data + graphic * 3;
                int animation = rom.Span[row + 2] & 15;
                int frame = (animations & ~0x3fff) + (Word(animations + animation * 2) & 0x3fff);
                int oam = oamBank + (Word(pointers + rom.Span[frame + 1]) & 0x3fff);
                int count = rom.Span[oam];
                FailIf(count is < 1 or > 40 || rom.Span[frame] != 0x7f,
                    $"{game}: INTERAC_TREASURE $60 graphic${graphic:x2} has an untraced native frame.");
                string cells = string.Join(';', Enumerable.Range(0, count).Select(cell =>
                    string.Join(',', Enumerable.Range(0, 4).Select(offset => rom.Span[oam + 1 + cell * 4 + offset]))));
                var definition = OracleGraphicsCache.GetAnimationDefinition(visual.Animation);
                FailIf(visual.AssetRoot != treasures.AssetRoot || visual.DefaultAnimation != animation ||
                    visual.TileBase != (rom.Span[row + 1] & 0x7f) || visual.Palette != (rom.Span[row + 2] >> 4 & 7) ||
                    definition.Frames.Length != 1 || definition.Frames[0].Duration != 0x7f ||
                    definition.Frames[0].Parameter != rom.Span[frame + 2] || definition.Frames[0].EncodedOam != cells,
                    $"{game}: INTERAC_TREASURE $60 graphic${graphic:x2} differs from native palette, tile, animation or OAM.");
                _ = OracleGraphicsCache.LoadImage(visual.AssetRoot + $"gfx/{visual.Sprite}.png");
            }
        }
    }
}
