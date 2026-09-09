using System;
using Godot;

namespace oracleofages;

// bank2.s:loadStatusBarMap / correctAddressForExtraHeart / drawHeartDisplay.
// Shared by the gameplay HUD and the status bar retained in inventory.
internal static class StatusBarLayout
{
    internal static void DrawBiggoronSword(Node2D canvas, Vector2 offset = default, Image? output = null)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/hud/spr_biggoron_sword_icon.png");
        Color[,] palette = ItemIconAtlas.LoadStandardSpritePalettes();
        // updateStatusBar_body@oamData: four bank-1 sprites, palette 3,
        // raw X $18/$20/$28/$30 and raw Y $10.
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 32; x++)
        {
            int shade = ItemIconAtlas.ShadeFromPng(source.GetPixel(x, y), out bool transparent);
            if (!transparent)
            {
                Vector2 position = offset + new Vector2(16 + x, y);
                if (output is null)
                    canvas.DrawRect(new Rect2(position, Vector2.One), palette[3, shade]);
                else
                    output.SetPixel((int)position.X, (int)position.Y, palette[3, shade]);
            }
        }
    }

    internal static int ExtraHeartOffset(int maxHealth) => maxHealth >= 57 ? -1 : 0;

    internal static byte[] ReadMap(int maxHealth, int equippedB, bool attributes = false)
    {
        string layout = equippedB == InventoryState.ItemBiggoronSword
            ? "biggoron_sword" : maxHealth >= 57 ? "extra_hearts" : "normal";
        string path = $"res://assets/oracle/hud/{(attributes ? "flg" : "map")}_hud_{layout}.bin";
        byte[] data = FileAccess.GetFileAsBytes(path);
        if (data.Length != 64)
            throw new InvalidOperationException($"GFXH_HUD_LAYOUT: {path} must contain 64 bytes.");
        return data;
    }

    internal static void WriteHearts(byte[] map, int maxHealth, int health)
    {
        int columns = maxHealth >= 57 ? 8 : 7;
        int start = 0x0d + ExtraHeartOffset(maxHealth);
        int containers = maxHealth / 4;
        int full = health / 4;
        for (int row = 0; row < 2; row++)
        for (int column = 0; column < columns; column++)
        {
            int heart = row * columns + column;
            map[start + row * 32 + column] = heart >= containers ? (byte)0
                : heart < full ? (byte)0x0f
                : heart == full && (health & 3) != 0 ? (byte)(0x0b + (health & 3)) : (byte)0x0b;
        }
    }
}
