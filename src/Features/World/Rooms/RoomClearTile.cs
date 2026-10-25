using Godot;

namespace Oracles;

// One world-owned presentation of initializeVramMaps' tile $00 / attribute $80.
// Signed addressing selects gfx_hud tile 0 at $9000, with live BG palette 0.
internal sealed class RoomClearTile(Image hud, BackgroundPaletteState palettes)
{
    private ImageTexture? _texture;
    private (Color, Color, Color, Color) _palette;
    private (Color, Color, Color, Color) Palette =>
        (palettes.Resolve(0, 0), palettes.Resolve(0, 1), palettes.Resolve(0, 2), palettes.Resolve(0, 3));

    internal Texture2D Texture
    {
        get
        {
            if (_texture is null)
            {
                using Image image = Render();
                _texture = ImageTexture.CreateFromImage(image);
                _palette = Palette;
            }
            else Refresh();
            return _texture;
        }
    }

    internal void Refresh()
    {
        if (_texture is null || _palette == Palette) return;
        using Image image = Render();
        _texture.SetImage(image);
        _palette = Palette;
    }

    internal Image Render()
    {
        Image image = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            int shade = Mathf.RoundToInt((1 - hud.GetPixel(x, y).R) * 3);
            image.SetPixel(x, y, palettes.Resolve(0, shade));
        }
        return image;
    }
}
