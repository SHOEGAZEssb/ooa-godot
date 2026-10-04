using Godot;
using System.Collections;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{

    private void CloseMapMenuOamRom(MenuRom rom, bool batched, string context, bool compareFramePixels = false)
    {
        Texture2D? lastFrameBackground = null;
        byte[]? lastFrameCommands = null;
        void Step(int count, int pressed = 0)
        {
            int edge = pressed;
            StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(pressed), MenuRomActions(pressed), batched, () =>
            {
                rom.AdvancePalette();
                rom.Update(edge, pressed, _saveData.ReadWramByte(0xc622)); edge = 0;
                FailIf(_gameplayPause.IsLeased != (rom[0xcbcb] != 0), context + ": closing pause boundary differs.");
                if (_mapScreen.Visible) CompareMapMenuOamRom(rom, context + " closing");
                if (_mapScreen.Visible && compareFramePixels)
                    CompareMapFramePixelsRom(rom, ref lastFrameBackground, ref lastFrameCommands, context + " closing frame");
            });
        }
        Step(1, 2); Step(22);
        FailIf(_menuLifecycle.IsActive, context + ": native map closing did not complete.");
    }

    private void CompareMapMenuOamRom(MenuRom rom, string context)
    {
        // Exercise the same pure command builders as _Draw. Painter order is
        // the reverse of native OAM; rendering calls remain in the screen node.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var sprites = (IList)typeof(MapScreen).GetField("_sprites", flags)!.GetValue(_mapScreen)!;
        sprites.Clear();
        typeof(MapScreen).GetMethod(_mapScreen.Mode == MapMode.Dungeon ?
            "DrawDungeonMarkers" : "DrawOverworldMarkers", flags)!.Invoke(_mapScreen, null);
        // runMapMenu clears all slots; only ring/inventory reserve HUD OAM.
        int nativeCount = rom[0xff9f] / 4;
        FailIf(sprites.Count != nativeCount,
            context + $": map OAM count differs: runtime={sprites.Count}, native={nativeCount}.");
        for (int index = 0; index < sprites.Count; index++)
        {
            object sprite = sprites[sprites.Count - 1 - index]!;
            object Read(string property) => sprite.GetType().GetProperty(property)!.GetValue(sprite)!;
            var point = (Vector2)Read("Position");
            int tile = (int)Read("Tile");
            int attributes = (int)Read("Palette") | ((bool)Read("FlipX") ? 0x20 : 0) |
                ((bool)Read("FlipY") ? 0x40 : 0);
            int y = ((int)point.Y + 16) & 0xff, x = ((int)point.X + 8) & 0xff;
            int address = 0xcb00 + index * 4;
            FailIf(y != rom[address] || x != rom[address + 1] || tile != rom[address + 2] ||
                attributes != rom[address + 3],
                context + $": map OAM part {index}: runtime=${y:x2}/${x:x2}/${tile:x2}/${attributes:x2}, native=${rom[address]:x2}/${rom[address + 1]:x2}/${rom[address + 2]:x2}/${rom[address + 3]:x2}.");
        }
    }
}
