using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

// Clean-US parent dispatch, item update and unconditional post pass in original
// object order. Room bytes and Link's ground state are explicit inputs; graphics,
// block allocation, tile writes/restoration and puff creation execute native code.
internal sealed class SomariaRom
{
    private readonly FrontendRom _rom = new();
    internal byte this[int address] { get => _rom[address]; set => _rom[address] = value; }
    internal int RandomCalls => _rom.RandomCalls;
    internal int TextGeneration => _rom.TextGeneration;
    internal bool HostilePartsEnabled { get; set; }
    internal bool PostObjectCollisionsEnabled { get; set; } = true;
    internal bool CompanionDispatchEnabled { get; set; }
    internal IReadOnlyList<int> Sounds => _rom.Sounds;
    internal int[] Blocks => Enumerable.Range(0xd7, 5).Select(page => page << 8)
        .Where(slot => this[slot] != 0 && this[slot + 1] == 0x18).ToArray();
    internal int Word(int address) => _rom.Word(address);
    internal void Word(int address, int value)
    {
        this[address] = (byte)value; this[address + 1] = (byte)(value >> 8);
    }
    internal byte Underlying(int packed) => _rom.BankByte(3, 0xdf00 + packed);
    internal byte FloorPermutation(int index) => _rom.BankByte(4,0xd000+index);
    internal byte BackgroundTile(int x, int y) => _rom.BankByte(3, 0xd800 + y * 32 + x);
    internal byte BackgroundAttribute(int x, int y) => _rom.BankByte(3, 0xdc00 + y * 32 + x);
    internal MenuRom CreateMenuView() => new(_rom);
    internal void AdvanceDeathPrelude() => _rom.UpdateDeathPrelude();
    internal void SelectToggleCutscene()
    {
        _rom.Call(0x7c6c,1); // cutscene01.checkUpdateToggleBlocks, after objects.
        if (this[0xcc04] != 0) _rom.Call(0x5e3d,1); // setCutsceneIndexIfCutsceneTriggerSet.
    }
    internal void AdvanceEnemyAndPartCollisions() => _rom.Call(0x41d1,7);
    internal void CheckTileWarps() => _rom.Call(0x60e9,1); // cutscene01, after toggle selection.
    internal void SelectNextActiveRoom() => _rom.Call(0x5f45,1); // getNextActiveRoom, after warp lookup.
    internal void InitializeRoomSpecificInteractions() => _rom.Call(0x5872,0x12); // Original runRoomSpecificCode/group dispatch.
    internal void ApplyLinkDamage(byte rawDamage)
    {
        this[0xd025] = rawDamage;
        CallLink(0x46bb, 6); // linkApplyDamage, including native death publication.
    }
    internal void AdvanceWarpPalette() => _rom.AdvancePalette();
    internal void ApplyRequestedWarp() => _rom.Call(0x5e0e, 1); // applyWarpTransition2, after objects.
    internal void UpdateFadeOutWarp() => _rom.Call(0x5bd8, 1); // cutscene03, including native room load.
    internal void CompleteGaleMenuHandoff() => _rom.Call(0x5de8, 1); // cutscene16 after updateMenus/ret nz.
    internal void AdvanceArrivalRoomControl()
    {
        // updateAllObjects' native screen-transition pass, followed by
        // cutscene00's continuation when that caller still owns the room.
        _rom.Call(0x4000, 1);
        if (this[0xc2ef] == 0) _rom.Call(0x5b2c, 1);
    }
    internal void UpdateGaleCancelGameplay(int pressed, int held, int frameCounter)
    {
        // cutscene16 consumes wWarpTransition2 before updateAllObjects.
        // This fixture supplies that object pass via UpdateGameplay, then
        // executes its native CUTSCENE_INGAME/lock-clearing epilogue.
        this[0xcc4b] = 0;
        UpdateGameplay(pressed, held, 0xff, frameCounter);
        _rom.Call(0x5df6, 1);
    }

    internal SomariaRom(OracleSaveData save, OracleRandomState random, OracleRoomData room,
        int direction, int x, int y)
    {
        for (int address = 0xc5b0; address < 0xcb00; address++) this[address] = save.ReadWramByte(address);
        this[0xff94] = random.Rng1; this[0xff95] = random.Rng2;
        this[0xcc2b] = 0xff;
        this[0xcc2c] = 0xd0;
        this[0xcc2d] = (byte)room.Group; this[0xcc30] = (byte)room.Id; this[0xcc2e] = 1;
        this[0xcc33] = (byte)room.ActiveCollisions; this[0xcc34] = room.TilesetFlags;
        this[0xcc86] = (byte)room.Height; this[0xcc87] = (byte)room.Width;
        this[0xcd00] = 1;
        this[0xd000] = 1; this[0xd004] = 1; this[0xd024] = 0x80;
        this[0xd029] = 1;
        this[0xd026] = this[0xd027] = 6;
        this[0xd008] = (byte)direction; this[0xd009] = (byte)(direction * 8);
        this[0xd00b] = (byte)y; this[0xd00d] = (byte)x;
        for (int tile = 0; tile < 256; tile++)
            _rom.SetBankByte(3, 0xdb00 + tile, room.GetCollision((byte)tile));
        CopyRoom(room);
    }

    internal void Update(int pressed, int held, int frameCounter)
    {
        this[0xcc00] = (byte)frameCounter;
        this[0xcc29] = (byte)held; this[0xcc2a] = (byte)pressed;
        this[0xcc99] = (byte)((this[0xd00b] & 0xf0) | (this[0xd00d] >> 4));
        if (this[0xcba0] == 0 && (this[0xcc8a] & 0x81) == 0)
            _rom.Call(0x48b3, 6); // checkUseItems, including parent pass.
        _rom.Call(0x4872, 7); // updateItems: initialized children obey native freeze gates.
        _rom.Call(0x3616, 0); // bank0.setEnemyTargetToLinkPosition, before later object phases.
        _rom.Call(0x3b36, 0); // updateInteractions, including block-deletion puffs.
        _rom.Call(0x491a, 7); // updateItemsPost: runs even while parents/items are frozen.
    }

    internal void ClearPhysicalItems() => _rom.Call(0x19ad, 0);
    internal void ClearOutgoingInteractions() => _rom.Call(0x4a31,1); // clearInteractionsWithEnabled2.
    internal void SetOutgoingInteractions() => _rom.Call(0x49f1,1); // setInteractionsEnabledTo2.
    internal void SetOutgoingParts() => _rom.Call(0x49ff,1); // setPartsEnabledTo2.
    internal void ClearOutgoingParts() => _rom.Call(0x4a3f,1); // clearPartsWithEnabled2.
    internal void AdvanceInteractions(int frameCounter)
    {
        this[0xcc00] = (byte)frameCounter;
        _rom.Call(0x3b36,0); // Original object pass, before scroll control.
    }

    internal void AdvanceParts(int frameCounter)
    {
        this[0xcc00] = (byte)frameCounter;
        _rom.Call(0x5e58,0x11); // Original updateParts, including modal/scroll masks.
    }

    internal void InteractWithFrontTile() => CallLink(0x4000,6);

    // Clean-US bank1 func_49c9 / clearMemoryOnScreenReload. The former
    // clears $cc8a..$cce0; the latter starts at $cc5c and also clears items.
    internal void ClearRoomVariables(bool scrolling) => _rom.Call(scrolling ? 0x49c9 : 0x49af,1);
    internal void AdvanceTileGraphics()
    {
        // updateAllObjects tail, after the previous VBlank consumed commands.
        this[0xffa5] = 0;
        _rom.Call(0x6c32, 4); // tilesets.updateChangedTileQueue, at most four entries.
    }
    internal void SetTile(byte position, byte tile)
    {
        // Original setTile: supplied A/C, including full-queue rejection.
        byte[] caller = [0x3e,tile,0x0e,position,0xcd,0x9c,0x3a,0xc9];
        for (int index = 0; index < caller.Length; index++) this[0xc100+index] = caller[index];
        _rom.Call(0xc100,0);
    }
    internal void ClearItemParents() => _rom.Call(0x4878, 6); // clearAllParentItems_body.
    internal void DeleteDynamicItem(int slot)
    {
        if (slot < 0xd700 || slot > 0xdb00 || (slot & 0xff) != 0)
            throw new ArgumentOutOfRangeException(nameof(slot));
        // Native itemDelete with the caller's D register pointing at its slot.
        byte[] caller = [0x16, (byte)(slot >> 8), 0xcd, 0xe2, 0x2c, 0xc9];
        for (int index = 0; index < caller.Length; index++) this[0xc100 + index] = caller[index];
        _rom.Call(0xc100, 7);
    }

    internal void ScanBombchuTarget(int slot)
    {
        if (slot < 0xd700 || slot > 0xdb00 || (slot & 0xff) != 0)
            throw new ArgumentOutOfRangeException(nameof(slot));
        // bombchuCheckForEnemyTarget, original bank $07:$5426. The caller
        // supplies only D; the routine reads the actual enemy-slot bytes.
        byte[] caller = [0x16, (byte)(slot >> 8), 0xcd, 0x26, 0x54, 0xc9];
        for (int index = 0; index < caller.Length; index++) this[0xc100 + index] = caller[index];
        _rom.Call(0xc100, 7);
    }

    internal void HomeBombchu(int slot, bool sidescroll) =>
        CallBombchuHelper(slot, sidescroll ? 0x539e : 0x5361);
    internal void CheckBombchuWalls(int slot) => CallBombchuHelper(slot, 0x52ea);
    internal void TurnBombchuFromHole(int slot) => CallBombchuHelper(slot, 0x52b0);
    internal void PlaceBombchuInFrontOfLink(int slot) => CallBombchuHelper(slot, 0x53ce);
    internal int ReadBombchuFrontCollision(int slot)
    {
        // bombchuGetTileCollisions takes E=Item.angle. Preserve its actual
        // front-offset lookup and room-collision reader, then publish A.
        byte[] caller = [0x16, (byte)(slot >> 8), 0x1e, 9, 0xcd, 0xbd, 0x52,
            0xea, 0x20, 0xc1, 0xc9];
        for (int index = 0; index < caller.Length; index++) this[0xc100 + index] = caller[index];
        _rom.Call(0xc100, 7);
        return this[0xc120];
    }
    private void CallBombchuHelper(int slot, int address)
    {
        if (slot < 0xd700 || slot > 0xdb00 || (slot & 0xff) != 0)
            throw new ArgumentOutOfRangeException(nameof(slot));
        byte[] caller = [0x16, (byte)(slot >> 8), 0xcd, (byte)address, (byte)(address >> 8), 0xc9];
        for (int index = 0; index < caller.Length; index++) this[0xc100 + index] = caller[index];
        _rom.Call(0xc100, 7);
    }

    internal void CopyRoom(OracleRoomData room)
    {
        for (int row = 0; row < room.HeightInTiles; row++)
        for (int column = 0; column < room.WidthInTiles; column++)
        {
            int packed = row * 16 + column;
            Godot.Vector2 point = new(column * 16 + 8, row * 16 + 8);
            this[0xcf00 + packed] = room.GetMetatile(point);
            this[0xce00 + packed] = (byte)room.GetTerrainInfo(point).Collision;
            // Runtime small-room storage has ten columns; live native WRAM
            // tile buffers always use the sixteen-byte row stride.
            _rom.SetBankByte(3, 0xdf00 + packed, room.GetUnderlyingMetatile(point));
        }
    }

    internal void InitializeLinkGameplay()
    {
        this[0xcc39] = this[0xccaa] = 0xff;
        this[0xcc6a] = 20;
        for (int column = 0; column < 16; column++)
            this[0xcef0 + column] = this[0xce00 + this[0xcc86] + column] = 0xff;
        for (int row = 0; row < 11; row++)
            this[0xce0f + row * 16] = this[0xce00 + (this[0xcc87] >> 4) + row * 16] = 0xff;
    }

    internal void InitializeLinkWalkingAnimation()
    {
        this[0xd01a] = 0x80;
        CallLink(0x2b0a, 0, 0x10); // specialObjectSetAnimation WALK, as linkState00.
    }

    internal void PublishLinkGraphics() => _rom.Call(0x2b25,0);

    internal void UpdateGameplay(int pressed, int held, int angle, int frameCounter,
        Action? beforeInteractions = null, Action? afterInteractions = null)
    {
        this[0xcc00] = (byte)frameCounter;
        this[0xcc29] = (byte)held; this[0xcc2a] = (byte)pressed; this[0xcc2b] = (byte)angle;
        // updateSpecialObjects preparation/tail, with no companion or physical
        // controller input. The actual Link dispatch owns tile interaction,
        // item parents, wall probes, movement, facing and grab eligibility.
        _rom.UpdateSpecialObjectPrelude();
        if (CompanionDispatchEnabled)
        {
            CallSpecialObject(0xd1);
            this[0xcc68] = this[0xcc98] = 0;
            CallSpecialObject(0xd0);
        }
        else CallLink(0x49b6);
        CallLink(0x4279); // updateLinkInvincibilityCounter.
        if (CompanionDispatchEnabled) this[0xcc96] = this[0xcc8d];
        this[0xcc61] &= 0x0f;
        this[0xd02a] = this[0xcc67] = this[0xccd8] = 0;
        if (this[0xcc6b] != 0) this[0xcc6b]--;
        // Clear only after Link has read the preceding publication. ITEM$18
        // state3 republishes later in this update, including after a push.
        for (int address = 0xcc74; address < 0xcc84; address++) this[address] = 0;
        _rom.Call(0x4872, 7);
        _rom.Call(0x3616, 0); // updateAllObjects publishes the post-Link target before interactions.
        if (HostilePartsEnabled) _rom.Call(0x5e58, 0x11); // updateParts, before interactions.
        beforeInteractions?.Invoke();
        _rom.Call(0x3b36, 0);
        // updateAllObjects calls bank1.func_4000 here, before its rider copy.
        // A scroll fixture supplies that native phase; ordinary fixtures
        // retain their declared room boundary without a transition pass.
        afterInteractions?.Invoke();
        if ((this[0xcc5a] & 0x80) != 0) _rom.Call(0x54df, 6);
        if (CompanionDispatchEnabled && (this[0xcc2c] & 1) != 0)
            CallLink(0x410d); // Native bank0.updateAllObjects post-object rider copy.
        // This late graphics pass publishes wLinkPushingDirection for the
        // following Link update, using its retained adjacent-wall probes.
        _rom.Call(0x2b25, 0);
        _rom.Call(0x491a, 7);
        if (HostilePartsEnabled && PostObjectCollisionsEnabled && this[0xcba0] == 0)
            _rom.Call(0x41d1, 7); // Original post-object collision publication.
    }

    private void CallLink(int address, int bank = 5, int accumulator = 0)
    {
        this[0xffae] = 0; this[0xffaf] = 0xd0;
        // Supply D/H as updateSpecialObjects does; no native instruction is
        // replaced. FrontendRom keeps the stack in unbanked WRAM.
        byte[] caller = [0x3e, (byte)accumulator, 0x16, 0xd0, 0x62, 0xcd, (byte)address, (byte)(address >> 8), 0xc9];
        for (int index = 0; index < caller.Length; index++) this[0xc100 + index] = caller[index];
        _rom.Call(0xc100, bank);
    }

    private void CallSpecialObject(int page)
    {
        // updateSpecialObjects @updateSpecialObject ($05:$407d), including
        // enabled gates, original ID dispatch and its banked vehicle handler.
        byte[] caller = [0x21, 0, (byte)page, 0xcd, 0x7d, 0x40, 0xc9];
        for (int index = 0; index < caller.Length; index++) this[0xc100 + index] = caller[index];
        _rom.Call(0xc100, 5);
    }
}
