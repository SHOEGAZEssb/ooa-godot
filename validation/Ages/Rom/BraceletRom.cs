
namespace Oracles;

// Clean-US shared Bracelet parent, reserved $dc child, native wall probes and
// Link movement, late grabbed-position attachment, and item post pass.
internal sealed class BraceletRom
{
    private readonly LinkCollisionRom _rom;
    private readonly OracleGame _game;
    internal IReadOnlyList<int> Sounds => _rom.SoundRequests;
    internal void ClearSounds() => _rom.SoundRequests.Clear();
    private int Address(int address) => _game == OracleGame.Ages ? address : address switch
    {
        0xc641 => 0xc63e,
        0xc688 => 0xc680, 0xc689 => 0xc681, 0xc6cb => 0xc6c5,
        0xcc08 => 0xcc07, 0xcc09 => 0xcc08,
        0xcc0a => 0xcc09, 0xcc0b => 0xcc0a,
        >= 0xcc21 and <= 0xcc28 => address + 0x1c,
        0xcc29 => 0xcc45, 0xcc2a => 0xcc46, 0xcc2b => 0xcc47,
        0xcc2c => 0xcc48, 0xcc2d => 0xcc49, 0xcc2e => 0xcc4a, 0xcc30 => 0xcc4c,
        0xcc4a => 0xcc66,
        0xcc32 => 0xcc4e, 0xcc33 => 0xcc4f, 0xcc34 => 0xcc50, 0xcc39 => 0xcc55,
        0xcc4f => 0xcc6a, 0xcc50 => 0xcc6b, 0xcc51 => 0xcc6c, 0xcc52 => 0xcc6d,
        0xcc56 => 0xcc71, 0xcc57 => 0xcc72, 0xcc59 => 0xcc74,
        0xcc5a => 0xcc75, 0xcc5b => 0xcc76, 0xcc5c => 0xcc77, 0xcc5d => 0xcc78, 0xcc61 => 0xcc7c,
        0xcc63 => 0xcc7e, 0xcc6c => 0xcc86, 0xcc6d => 0xcc87,
        0xcc86 => 0xcca0, 0xcc87 => 0xcca1,
        0xcc85 => 0xcc9f,
        0xcc99 => 0xccb3, 0xcc9a => 0xccb4, 0xcc9b => 0xccb5,
        0xcc9c => 0xccb6, 0xcc9d => 0xccb7, 0xcc9e => 0xccb8, 0xccaa => 0xccc5,
        0xcc8a => 0xcca4,
        0xcc91 => 0xccab,
        0xccd3 => 0xccea,
        0xccde => 0xccf4,
        0xccdf => 0xccf5, 0xcce0 => 0xccf6,
        0xcdd1 => 0xcc30,
        0xcdda => 0xcc3a,
        >=0xcdc0 and <=0xcdcf => address-0xcdc0+0xcc1f,
        0xcdd0 => 0xcc2f,
        _ => address
    };
    internal byte this[int address] { get => _rom[Address(address)]; set => _rom[Address(address)] = value; }
    internal byte ReadNativeByte(int address) => _rom[address];
    internal byte UniqueGraphicsByte(int offset) => _rom.BankByte(7,0xd800+offset);
    internal void LoadUniqueGraphics(int index) => _rom.Call(_game==OracleGame.Ages?0x380b:0x379c,bank:0,accumulator:index);
    internal int InitializeBossRoom()
    {
        _rom.Call(_game == OracleGame.Ages ? 0x4552 : 0x4571,
            bank:_game == OracleGame.Ages ? 0x0f : 0x0e);
        // LinkCollisionRom.Call already captures the returned AF at $c200/$c201.
        return _rom[0xc200];
    }
    internal void SetNativeByte(int address, byte value) => _rom[address] = value;
    internal int Word(int address) => this[address] | this[address + 1] << 8;
    internal void Word(int address, int value) { this[address] = unchecked((byte)value); this[address + 1] = unchecked((byte)(value >> 8)); }
    internal byte Underlying(int offset) => _rom.BankByte(3, 0xdf00 + offset);
    internal int RandomCalls => _rom.RandomUpdates;
    internal bool ChildActive => this[0xdc00] != 0;
    internal void LoadSave(OracleSaveData save)
    {
        if (save.Game != _game) throw new System.ArgumentException("Link item ROM requires the selected native save layout.");
        for (int address = 0xc5b0; address < 0xcb00; address++) _rom[address] = save.ReadWramByte(address);
    }

    internal BraceletRom(OracleRoomData room, Vector2 link, int direction, int ring, int level = 1, OracleGame game = OracleGame.Ages)
    {
        _game = game;
        _rom = new(bankedStack: true, game: game);
        this[0xcc2c] = 0xd0;
        this[0xc6cb] = (byte)ring;
        if (game == OracleGame.Ages) this[0xc6b8] = (byte)level;
        this[0xcc08] = (byte)(game == OracleGame.Ages ? 0x78 : 0x5c);
        this[0xcc09] = 1;
        this[0xcc30] = (byte)room.Id;
        this[0xcc33] = (byte)room.ActiveCollisions;
        this[0xcc34] = (byte)room.TilesetFlags;
        this[0xcc86] = (byte)room.Height;
        this[0xcc87] = (byte)room.Width;
        this[0xcc39] = this[0xccaa] = 0xff;
        this[0xd000] = 3; // bank1.initializeGame: Link survives setObjectsEnabledTo2/cleanup.
        this[0xd004] = 1;
        this[0xd008] = (byte)direction;
        this[0xd009] = (byte)(direction * 8);
        this[0xd024] = 0x80;
        this[0xd026] = this[0xd027] = 6;
        Word(0xd00c, (int)(link.X * 256));
        Word(0xd00a, (int)(link.Y * 256));
        for (int tile = 0; tile < 256; tile++) _rom.Bank(3, 0xdb00 + tile, room.GetCollision((byte)tile));
        CopyRoom(room);
        Probe();
    }

    internal void CopyRoom(OracleRoomData room)
    {
        // loadRoomCollisions.@blankDataAroundCollisions installs wrapped
        // top/left edges and the small/large bottom/right padding.
        for (int x = 0; x < 16; x++)
            this[0xcef0 + x] = this[0xce00 + room.HeightInTiles * 16 + x] = 0xff;
        for (int y = 0; y < 11; y++)
            this[0xce0f + y * 16] = this[0xce00 + room.WidthInTiles + y * 16] = 0xff;
        for (int y = 0; y < room.HeightInTiles; y++)
        for (int x = 0; x < room.WidthInTiles; x++)
        {
            Vector2 point = new(x * 16 + 8, y * 16 + 8);
            int offset = y * 16 + x;
            this[0xcf00 + offset] = room.GetMetatile(point);
            this[0xce00 + offset] = (byte)room.GetTerrainInfo(point).Collision;
            _rom.Bank(3, 0xdf00 + offset, room.GetUnderlyingMetatile(point));
        }
    }

    internal void Probe() => Call(5, LinkCollisionRom.Probe);
    internal void InitializeWarpArrival(int mode, int parameter)
    {
        this[0xd004] = 0x0a;
        _rom[_game == OracleGame.Ages ? 0xcc49 : 0xcc65] = (byte)(0x80 | mode);
        _rom[_game == OracleGame.Ages ? 0xcc50 : 0xcc6b] = (byte)parameter;
        _rom.Call(_game == OracleGame.Ages ? 0x4a26 : 0x49df,bank:5);
    }
    internal void InitializeWalkingAnimation() => SetLinkAnimation(0x10);
    internal void LoadDungeon(int index)
    {
        this[0xcc39]=(byte)index;
        _rom.Call(_game==OracleGame.Ages?0x564e:0x54c1,bank:1);
        this[0xff70]=0;
    }
    internal void SetLinkAnimation(int mode) =>
        _rom.Call(_game == OracleGame.Ages ? 0x2b0a : 0x2a51, bank: 0, accumulator: mode);
    internal void ClearItemParents() => _rom.Call(_game == OracleGame.Ages ? 0x2c10 : 0x2b57, bank: 0);
    internal void ReassignParentButtons() => _rom.Call(_game == OracleGame.Ages ? 0x4890 : 0x4842, bank: 6);
    internal void ClearScreenVariables() => _rom.Call(_game == OracleGame.Ages ? 0x35a6 : 0x3528, bank: 0);
    internal void ClearPhysicalItems() => _rom.Call(_game == OracleGame.Ages ? 0x19ad : 0x1986, bank: 0); // clearAllItemsAndPutLinkOnGround.
    internal void CheckAndSpawnMaple() => _rom.Call(_game == OracleGame.Ages ? 0x76d3 : 0x5ed0,
        bank: _game == OracleGame.Ages ? 2 : 0x10);
    internal void LoadRememberedCompanion() => _rom.Call(_game == OracleGame.Ages ? 0x768a : 0x5ea0,
        bank: _game == OracleGame.Ages ? 2 : 0x10);
    internal void DismountCompanion()=>_rom.Call(_game==OracleGame.Ages?0x460c:0x45bf,bank:5,objectPage:0xd1);
    internal void RespawnCompanion()=>_rom.Call(_game==OracleGame.Ages?0x46e6:0x46ab,bank:5,objectPage:0xd1);
    internal void ApplyOpenedChest() => _rom.Call(_game == OracleGame.Ages ? 0x626f : 0x5f53, bank: 4);
    internal void SetScriptInputEnabled(bool enabled) => CallScriptInputCommand(enabled ? 0x415e : 0x4147);
    internal void SetScriptMenuEnabled(bool enabled) => CallScriptInputCommand(enabled ? 0x4162 : 0x414c);
    internal void RequestState08() => _rom.Call(_game == OracleGame.Ages ? 0x2aad : 0x29f4, bank: 0);
    internal void AdvanceScreenShake() => _rom.Call(_game == OracleGame.Ages ? 0x427d : 0x423b, bank: 1);
    internal bool LinkVulnerable()
    {
        _rom.Call(_game == OracleGame.Ages ? 0x1d28 : 0x1ce6, bank: 0);
        return (_rom[0xc201] & 0x10) != 0;
    }
    internal void RequestGetItemState(byte parameter) { this[0xcc4f] = 4; this[0xcc50] = parameter; }
    private void CallScriptInputCommand(int entry)
    {
        // Script handlers pop the saved bytecode cursor before returning.
        byte[] caller = [0x21, 0x00, 0xc4, 0xe5, 0xc3, (byte)entry, (byte)(entry >> 8)];
        for (int index = 0; index < caller.Length; index++) _rom[0xc300 + index] = caller[index];
        _rom.Call(0xc300, bank: _game == OracleGame.Ages ? 0x0c : 0x0b);
    }
    internal (int Position, int TreasureId, int SubId) GetChestData()
    {
        int entry = _game == OracleGame.Ages ? 0x10cc : 0x109a;
        byte[] capture = [0xcd, (byte)entry, (byte)(entry >> 8),
            0x78, 0xea, 0x10, 0xc2, 0x79, 0xea, 0x11, 0xc2,
            0x7b, 0xea, 0x12, 0xc2, 0xc9];
        for (int index = 0; index < capture.Length; index++) _rom[0xc300 + index] = capture[index];
        _rom.Call(0xc300, bank: 0);
        return (_rom[0xc212], _rom[0xc210], _rom[0xc211]);
    }
    internal void BeginScroll()
    {
        _rom.Call(_game == OracleGame.Ages ? 0x49d7 : 0x4964,bank:1);
        this[0xcd00] = 8;
    }
    internal void FinishScroll() => _rom.Call(_game == OracleGame.Ages ? 0x4a17 : 0x49a4,bank:1);
    internal void AdvanceScreenBoundary() => _rom.Call(0x4105,bank:1);
    internal void AdvanceEnemies(int frameCounter)
    {
        this[0xcc00] = (byte)frameCounter;
        _rom.Call(_game == OracleGame.Ages ? 0x2ea5 : 0x2e86,bank:0,restoresBank:false);
    }
    internal void AdvanceInteractions(int frameCounter)
    {
        this[0xcc00] = (byte)frameCounter;
        _rom.Call(_game == OracleGame.Ages ? 0x3b36 : 0x3b0a,bank:0,restoresBank:false);
    }
    internal void AdvanceParts() => _rom.Call(_game == OracleGame.Ages ? 0x5e58 : 0x61dc,
        bank:_game == OracleGame.Ages ? 0x11 : 0x10);
    internal void GeneratePlacementBuffer() => _rom.Call(_game == OracleGame.Ages ? 0x7823 : 0x602b,
        bank:_game == OracleGame.Ages ? 2 : 0x10);

    internal void ParseObjectStream(byte[] stream)
    {
        if (stream.Length is < 1 or > 256 || stream[^1] != 0xff)
            throw new System.ArgumentException("Native declared object stream must end with obj_End and fit $100 bytes.",nameof(stream));
        for (int index=0; index<stream.Length; index++) this[0xc400+index]=stream[index];
        ParseObjectAddress(0xc400);
    }

    internal void ParseObjectAddress(int address)
    {
        if (address is <0x4000 or >0xdfff) throw new System.ArgumentOutOfRangeException(nameof(address));
        int entry=_game == OracleGame.Ages ? 0x55d4 : 0x58df;
        // The wrapper supplies DE only. Original parseGivenObjectData owns
        // opcode dispatch, physical allocation, placement and banked calls.
        byte[] caller=[0x11,(byte)address,(byte)(address>>8),0xcd,(byte)entry,(byte)(entry>>8),0xc9];
        for (int index=0; index<caller.Length; index++) this[0xc300+index]=caller[index];
        _rom.Call(0xc300,bank:_game == OracleGame.Ages ? 0x12 : 0x11);
    }

    internal bool TileAllowsEnemyPlacement(int mode, byte tile, byte collision)
    {
        this[0xcc33]=(byte)mode; this[0xce33]=collision; this[0xcf33]=tile;
        _rom.Call(_game == OracleGame.Ages ? 0x7882 : 0x608a,
            bank:_game == OracleGame.Ages ? 2 : 0x10,angle:0x33);
        return (_rom[0xc201]&0x10)==0;
    }
    internal void SetResidentObjectGraphics(int slot, int gfx)
    {
        if ((uint)slot >= 8 || (uint)gfx > 0xff) throw new System.ArgumentOutOfRangeException(nameof(slot));
        // include/wram.s:wLoadedObjectGfx: staged VRAM residency, not a graphics-loader stub.
        int address = (_game == OracleGame.Ages ? 0xcc08 : 0xcc07) + slot * 2;
        _rom[address] = (byte)gfx; _rom[address + 1] = 1;
    }
    internal void SetTile(byte position, byte tile) =>
        _rom.Call(_game == OracleGame.Ages ? 0x3a9c : 0x3a52,bank:0,angle:position,accumulator:tile);
    internal void AdvanceChangedTiles()
    {
        // The preceding VBlank consumed commands; retain the gameplay tile queue.
        _rom[_game == OracleGame.Ages ? 0xffa5 : 0xffa3] = 0;
        _rom.Call(_game == OracleGame.Ages ? 0x6c32 : 0x6b25,bank:4);
    }
    internal void InitializeHud(int health,int rupees)
    {
        this[0xcbe4]=(byte)health;
        // loadCommonGraphics_body selects wNumRupees for this fixture's rupee display.
        if(_game==OracleGame.Seasons)this[0xcbe5]=0xa5;
        int bcd=rupees/100*256+rupees%100/10*16+rupees%10;
        Word(_game==OracleGame.Ages?0xcbe5:0xcbe6,bcd);
    }
    internal void LoadCommonGraphics() => _rom.Call(_game == OracleGame.Ages ? 0x515c : 0x5127, bank: 2);
    internal void UpdateHud(int frame)
    {
        this[0xff70] = 0;
        this[0xcc00] = (byte)frame;
        _rom.Call(_game == OracleGame.Ages ? 0x518d : 0x5173, bank: 2);
    }
    internal byte HudTile(int offset) => _rom.BankByte(4, 0xd240 + offset);
    internal void HideStatusBar() => _rom.Call(_game == OracleGame.Ages ? 0x4f2c : 0x4eed,bank:2);
    internal void ShowStatusBar() => _rom.Call(_game == OracleGame.Ages ? 0x4f64 : 0x4f25,bank:2);
    internal bool HeartDisplayUpdated()
    {
        CallScriptInputCommand(_game == OracleGame.Ages ? 0x453f : 0x452e);
        return (_rom[0xc201] & 0x10) != 0;
    }
    internal void UpdateLinkItems(int frameCounter, int keys, bool interactions = false, bool parts = false, Action? beforeInteractions = null,bool statusBar=false)
    {
        // Original dispatcher, including physical key edges, native Link gates,
        // timer, item parents, companion and movement; hostiles use separate fixtures.
        this[0xcc00] = (byte)frameCounter;
        this[0xc481] = (byte)keys;
        _rom.Call(0x4000, bank: 5);
        Call(7, 0x4872);
        if (parts) AdvanceParts();
        beforeInteractions?.Invoke();
        if (interactions) _rom.Call(_game == OracleGame.Ages ? 0x3b36 : 0x3b0a,bank:0,restoresBank:false);
        if((this[0xcc2c]&1)!=0)_rom.Call(_game==OracleGame.Ages?0x410d:0x40f1,bank:5,objectPage:0xd1);
        if ((this[0xcc5a] & 0x80) != 0) Call(6,0x54df);
        _rom.Call(_game == OracleGame.Ages ? 0x2b25 : 0x2a6c, bank: 0);
        Call(7, 0x491a);
        if(statusBar)_rom.Call(_game==OracleGame.Ages?0x518d:0x5173,bank:2);
        if(parts)_rom.Call(_game==OracleGame.Ages?0x1de7:0x1da5,bank:0);
    }
    internal void Walk(int angle)
    {
        _rom.Call(Entry(5, LinkCollisionRom.Probe), angle: angle);
        _rom.Call(Entry(5, LinkCollisionRom.Move), angle: angle);
    }
    private int Entry(int bank, int address) => _game == OracleGame.Ages ? address : (bank, address) switch
    {
        (5, 0x5e62) => 0x5d53, (5, 0x5d9f) => 0x5c90,
        (6, 0x48b3) => 0x4865, (7, 0x4872) => 0x485a,
        (6, 0x54df) => 0x5429, (7, 0x491a) => 0x4902,
        _ => throw new System.NotSupportedException($"Seasons Bracelet ROM entry ${bank:x2}:${address:x4} requires its native symbol.")
    };
    internal void Call(int bank, int address) => _rom.Call(Entry(bank, address), bank: bank);
    internal void BeginSmasherBallThrow(Vector2 position, int z, int angle)
    {
        // Declared handoff from parent state3: the related native enemy has
        // just entered grabbed substate2, and the new physical child starts
        // with zero low position bytes (objectCopyPosition_rawAddress).
        this[0xd080] = 3;
        this[0xd081] = 0x74;
        this[0xd084] = 2;
        this[0xd085] = 2;
        this[0xd0a6] = this[0xd0a7] = 6;
        this[0xd0a9] = 5;
        this[0xd0b0] = 0xff;
        Word(0xd098, 0xd180);
        this[0xd1ab] = 0x40; // Declared invulnerable parent, no boss hit branch.
        this[0xdc00] = 1;
        this[0xdc01] = 0x16;
        this[0xdc09] = (byte)angle;
        this[0xdc38] = 0x20;
        Word(0xdc18, 0xd080);
        Word(0xdc0a, (int)position.Y * 256);
        Word(0xdc0c, (int)position.X * 256);
        Word(0xdc0e, z * 256);
    }
    internal void Update(int angle = 0xff, bool held = false, bool pressed = false,
        bool primary = true, bool moveLink = false, bool otherPressed = false, bool updateEnemies = false,
        bool updateInteractions = false, int item = ItemId.Bracelet, bool updateParts = false)
    {
        this[primary ? 0xc689 : 0xc688] = (byte)item;
        this[primary ? 0xc688 : 0xc689] = 0;
        byte button = (byte)(primary ? 1 : 2);
        int directionKey = angle switch { 0 => 0x40, 8 => 0x10, 16 => 0x80, 24 => 0x20, _ => 0 };
        this[0xcc29] = (byte)(directionKey | (held ? button : 0));
        this[0xcc2a] = (byte)((pressed ? button : 0) | (otherPressed ? primary ? 2 : 1 : 0));
        this[0xcc2b] = (byte)angle;
        if (this[0xcba0] == 0)
        {
            Call(6, 0x48b3); // checkUseItems, including original parent dispatch.
            if (moveLink && this[0xcc61] == 0 && angle < 0x20)
            {
                Walk(angle);
            }
        }
        Call(7, 0x4872); // updateItems includes the reserved Bracelet child.
        // bank0.updateEnemies intentionally leaves the last enemy code bank
        // selected, unlike the bank-restoring bounded geometry calls.
        if (updateEnemies) AdvanceEnemies(this[0xcc00]);
        if (updateParts) _rom.Call(_game == OracleGame.Ages ? 0x5e58 : 0x61dc, bank: _game == OracleGame.Ages ? 0x11 : 0x10);
        if (updateInteractions) _rom.Call(_game == OracleGame.Ages ? 0x3b36 : 0x3b0a, bank: 0, restoresBank: false);
        if ((this[0xcc5a] & 0x80) != 0) Call(6, 0x54df);
        Call(7, 0x491a);
    }
}
