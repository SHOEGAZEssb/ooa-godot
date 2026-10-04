using System.Collections.Generic;

namespace oracleofages;

// Bounded bank2 menu/status dispatch with private CGB memory. LCD/DMA timing
// is outside this comparison; graphics loaders and menu instructions execute
// from the verified ROM through FrontendRom's declared memory boundary.
internal sealed class MenuRom
{
    private readonly FrontendRom _rom;
    private int _textGeneration;
    internal byte this[int address] { get => _rom[address]; set => _rom[address] = value; }
    internal byte Tile(int offset) => _rom.BankByte(4, 0xd240 + offset);
    internal byte HudAttribute(int offset) => _rom.BankByte(4, 0xd640 + offset);
    internal byte HudItemGfx(int offset) => _rom.BankByte(4, 0xd680 + offset);
    internal byte HudBgGfx(int offset) => _rom.VramByte(0, 0x9000 + offset);
    internal int HudColor(bool sprite, int palette, int shade)
    {
        int address = (sprite ? 0xdec0 : 0xde80) + palette * 8 + shade * 2;
        return _rom.BankByte(2, address) | _rom.BankByte(2, address + 1) << 8;
    }
    internal void LoadHudGraphics() => _rom.LoadHudGraphics();
    internal void LoadRoomGraphics() => _rom.Call(0x3796, 0); // loadTilesetGraphics, including source room palettes.
    internal void SeedBackgroundPalette(int palette, int[] colors)
    {
        if (palette is < 0 or > 7 || colors.Length != 4)
            throw new System.ArgumentException("Background palette requires a BG slot and four RGB555 colors.");
        for (int shade = 0; shade < 4; shade++)
        {
            int address = 0xde80 + palette * 8 + shade * 2;
            _rom.SetBankByte(2, address, (byte)colors[shade]);
            _rom.SetBankByte(2, address + 1, (byte)(colors[shade] >> 8));
        }
    }
    internal int FadingColor(bool sprite, int palette, int shade)
    {
        int address = (sprite ? 0xdfc0 : 0xdf80) + palette * 8 + shade * 2;
        return _rom.BankByte(2, address) | _rom.BankByte(2, address + 1) << 8;
    }
    internal void SeedRetainedSpritePalette(int palette, int[] colors)
    {
        if (palette is < 6 or > 7 || colors.Length != 4)
            throw new System.ArgumentException("Retained sprite palette requires OBJ6/7 and four RGB555 colors.");
        for (int shade = 0; shade < 4; shade++)
        {
            int address = 0xdec0 + palette * 8 + shade * 2;
            _rom.SetBankByte(2, address, (byte)colors[shade]);
            _rom.SetBankByte(2, address + 1, (byte)(colors[shade] >> 8));
        }
    }
    internal void EnableVramDmaTransfers() => _rom.VramDmaTransfersEnabled = true;
    internal int VramDmaTransfers => _rom.VramDmaTransfers;
    internal void PublishHudGraphics() => _rom.Call(0x1a71, 0); // mainThread tail: checkReloadStatusBarGraphics.
    internal void HideHud() => _rom.Call(0x4f2c, 2); // hideStatusBar_body, including native graphics and OAM clearing.
    internal void ShowHud() => _rom.Call(0x4f64, 2); // showStatusBar_body publishes the deferred refresh request.
    internal void SaveGraphicsBeforeMenu() => _rom.Call(0x5077, 2); // saveGraphicsOnEnterMenu_body, including native palette/OAM capture.
    internal void AdvanceToggleCutscene() => _rom.AdvanceToggleCutscene();
    internal void CopyToggleRoom(OracleRoomData room)
    {
        for (int offset = 0; offset < 0xb0; offset++)
        {
            this[0xcf00 + offset] = room.Layout[offset];
            this[0xce00 + offset] = room.GetCollision(room.Layout[offset]);
            _rom.SetBankByte(3, 0xdf00 + offset, room.GetUnderlyingStorageMetatile(offset));
        }
    }
    internal byte MapTile(int x, int y) => _rom.BankByte(4, 0xd000 + y * 32 + x);
    internal byte MapAttribute(int x, int y) => _rom.BankByte(4, 0xd400 + y * 32 + x);
    internal byte MapGfx(int bank, int address) => _rom.VramByte(bank, address);
    internal IReadOnlyList<int> Sounds => _rom.Sounds;
    internal int RandomCalls => _rom.RandomCalls;
    internal void ClearSounds() => _rom.Sounds.Clear();
    internal bool ResetHandoff => _rom.ResetHandoff;
    internal bool RestartHandoff => _rom.FileSelectHandoff;
    internal byte SavedByte(int slot, int address, bool backup = false) => _rom.SavedByte(slot, address, backup);

    internal MenuRom(OracleSaveData save, OracleRoomData room)
    {
        _rom = new();
        _rom.StopAtReset = true;
        CopySave(save);
        this[0xcc2d] = (byte)room.Group;
        this[0xcc30] = (byte)room.Id;
        this[0xcc34] = room.TilesetFlags;
        this[0xcc39] = 0xff;
        this[0xcd00] = 1;
        this[0xcbe4] = this[0xc6aa];
        this[0xcbe5] = this[0xc6ad];
        this[0xcbe6] = this[0xc6ae];
        this[0xcbe9] = 0xff;
    }

    // Preserve the gameplay ROM's memory at a real item-to-menu handoff.
    internal MenuRom(FrontendRom rom) => _rom = rom;

    internal void CopySave(OracleSaveData save)
    {
        for (int address = 0xc5b0; address < 0xcb00; address++)
            this[address] = save.ReadWramByte(address);
    }

    internal void OpenRingFromVasu(int mode)
    {
        // scripts/common/scriptHelper.s:vasu_openRingMenu at $15:$426e
        // publishes mode, wDisabledObjects=$01 and calls openMenu($04).
        byte[] caller = [0x3e, (byte)mode, 0xcd, 0x6e, 0x42, 0xc9];
        for (int offset = 0; offset < caller.Length; offset++) this[0xc100 + offset] = caller[offset];
        _rom.Call(0xc100, 0x15);
    }

    internal void OpenImmediately(int menu, int ringMode = 0, bool gameOver = false)
    {
        // Explicit initial condition: enter menuSpecificCode at full white.
        // saveGraphicsOnEnterMenu_body clears wMenuUnionStart..wMenuUnionEnd
        // before this dispatch, including the dungeon flicker on reopening.
        for (int address = 0xcbb3; address < 0xcbc3; address++) this[address] = 0;
        this[0xcbcb] = (byte)menu;
        this[0xcbcc] = 1;
        this[0xcbcd] = this[0xcbce] = 0;
        this[0xcbd3] = (byte)ringMode;
        if (menu == 3) this[0xcbb4] = gameOver ? (byte)1 : (byte)0;
        Update(0, 0, 0);
        if (!gameOver) this[0xc4ab] = 0; // Game Over begins its real fade at white.
        ClearSounds();
    }

    internal void Update(int pressed, int held, int frame)
    {
        this[0xff70] = 0;
        this[0xc482] = (byte)pressed;
        this[0xc481] = (byte)held;
        this[0xcc00] = (byte)frame;
        _rom.Call(0x4fcf, 2); // bank2.b2_updateMenus, including native callers.
    }

    internal void AdvancePalette() => _rom.AdvancePalette();
    internal void AdvanceElectricShock() => _rom.Call(0x4a81, 1); // cutscene01.updateLinkBeingShocked, before updateMenus.
    internal void UpdateScreenShake() => _rom.Call(0x427d, 1); // updateAllObjects tail, including ordered shared RNG.
    internal void LoadRoomTileset() => _rom.LoadRoomTileset();
    internal int TextState => _rom.BankByte(7, 0xd0c0);
    internal byte Text(int address) => _rom.BankByte(7, address);
    internal byte RingNameGfx(int offset) => _rom.BankByte(7, 0xd200 + offset);
    internal void AdvanceText()
    {
        if (this[0xcba0] == 0) return;
        if (_textGeneration != _rom.TextGeneration)
        {
            _rom.Call(0x4af7, 0x3f); // initTextbox at the native thread restart.
            _textGeneration = _rom.TextGeneration;
        }
        _rom.Call(0x4b1f, 0x3f); // updateTextbox after menus, as in mainThread.
        this[0xff70] = 0;
    }

    internal void LoadDungeon(int index)
    {
        this[0xcc39] = (byte)index;
        _rom.Call(0x564e, 1); // loadDungeonLayout_b01: native data and room search.
        this[0xff70] = 0;
    }
    internal void UpdateHud(int frame)
    {
        this[0xff70] = 0;
        this[0xcc00] = (byte)frame;
        _rom.Call(0x518d, 2); // bank2.updateStatusBar_body
    }
}
