using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{

    private void ValidateMapBackgroundSubstitutionsRom()
    {
        int hostCase1 = 0;
        foreach (int group in new[] { 0, 1 })
        foreach (int companion in new[] { 0x0b, 0x0c, 0x0d })
        foreach (bool city in new[] { false, true })
        foreach (bool stone in new[] { false, true })
        foreach (bool allVisited in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetGlobalFlag(GlobalFlag.IntroDone);
            save.WriteWramByte(0xc610, (byte)companion);
            save.SetRoomFlag(0, 0x13, 1, city); save.SetRoomFlag(1, 0x41, 1, stone);
            for (int era = 0; era < 2; era++)
            for (int room = 0; room < 256; room++)
                save.SetRoomFlag(era, room, 0x10, allVisited || (room & 3) == 0);
            InitializeTransientSession(save); LoadValidationRoom(group, 0x45); _entities.Clear();
            var rom = new MenuRom(_saveData, _currentRoom);
            Texture2D? background = null;
            var sounds = _sound.AttachPlayRequestAudit();
            string context = $"Map substitution era={group} companion=${companion:x2} city={city} stone={stone} visits={allVisited} batch={batched}";
            for (int reopen = 0; reopen < 2; reopen++)
            {
                _mapMenu.OpenImmediatelyForValidation(); rom.OpenImmediately(2); sounds.Clear();
                CompareMapBackgroundPixelsRom(rom, ref background, context + $" reopen={reopen}");
                int edge = 0x10;
                StepGameplayUpdates(4, Vector2.Zero, MenuRomActions(0x10), MenuRomActions(0x10), batched, () =>
                {
                    rom.Update(edge, 0x10, _saveData.ReadWramByte(0xc622)); edge = 0;
                    FailIf(_mapScreen.CursorRoom != rom[0xcbb6] || !_gameplayPause.IsLeased ||
                        !System.Linq.Enumerable.SequenceEqual(sounds.Requests, rom.Sounds),
                        context + ": substituted background changed input, sound or pause ownership.");
                    CompareMapBackgroundPixelsRom(rom, ref background, context);
                });
                _mapMenu.CloseImmediatelyForValidation();
                // Reverse source flags between performances to catch retained
                // texture state. Visited masking is applied after substitutions.
                _saveData.SetRoomFlag(0, 0x13, 1, !city);
                _saveData.SetRoomFlag(1, 0x41, 1, !stone);
                _inventory.AssignAnimalCompanion(companion == 0x0b ? 0x0c : 0x0b);
                rom.CopySave(_saveData);
            }
        }
        GD.Print("Validated clean-US map background pixels for Ricky/Dimitri/Moosh terrain substitutions, Symmetry City and Talus stone flags in both eras, sparse/all visited masking, reverse flags/companion on reopening and split/batched gameplay.");
    }

    private void CompareMapBackgroundPixelsRom(MenuRom rom, ref Texture2D? previous, string context)
    {
        // Compare the screen's actual composed background with independent CGB
        // decoding of executed native loaders. Sprites and hardware timing are
        // separate boundaries. Recheck whenever the production texture changes.
        var texture = (Texture2D)typeof(MapScreen).GetField("_background",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_mapScreen)!;
        if (ReferenceEquals(texture, previous)) return;
        previous = texture;
        using Image actual = texture.GetImage();
        byte[] pixels = actual.GetData();
        FailIf(actual.GetWidth() != 160 || actual.GetHeight() != 144 || actual.GetFormat() != Image.Format.Rgba8,
            context + ": composed background must be a 160-by-144 RGBA image.");
        for (int row = 0; row < 18; row++)
        for (int column = 0; column < 20; column++)
        {
            int tile = rom.MapTile(column, row), attribute = rom.MapAttribute(column, row);
            // Map menus use signed $8800/$9000 tile addressing; bit 3 selects
            // the actual CGB VRAM bank, independently of the imported atlas.
            int source = 0x9000 + unchecked((sbyte)tile) * 16;
            int bank = (attribute >> 3) & 1;
            for (int y = 0; y < 8; y++)
            {
                int sourceY = (attribute & 0x40) == 0 ? y : 7 - y;
                int low = rom.MapGfx(bank, source + sourceY * 2);
                int high = rom.MapGfx(bank, source + sourceY * 2 + 1);
                for (int x = 0; x < 8; x++)
                {
                    int sourceX = (attribute & 0x20) == 0 ? x : 7 - x;
                    int shade = (low >> (7 - sourceX) & 1) | (high >> (7 - sourceX) & 1) << 1;
                    int expected = rom.HudColor(false, attribute & 7, shade);
                    int pixel = ((row * 8 + y) * 160 + column * 8 + x) * 4;
                    int observed = (pixels[pixel] * 31 + 127) / 255 |
                        ((pixels[pixel + 1] * 31 + 127) / 255) << 5 |
                        ((pixels[pixel + 2] * 31 + 127) / 255) << 10;
                    if (observed != expected || pixels[pixel + 3] != 255) FailIf(true,
                        context + $": background pixel ({column * 8 + x},{row * 8 + y}) runtime=${observed:x4}, native=${expected:x4}, tile=${tile:x2}/attr=${attribute:x2}/shade={shade}.");
                }
            }
        }
    }
}
