using Godot;
using System;

namespace oracleofages;

public sealed class InventoryState
{
    public const int InventoryCapacity = 16;
    public const int NumInventoryItems = 0x20;
    public const int ItemNone = 0x00;
    public const int ItemSword = 0x05;
    public const int ItemBracelet = 0x16;
    public const int TreasurePunch = 0x02;

    private static readonly int[] RupeeValues =
    {
        0, 1, 2, 5, 10, 20, 40, 30, 60, 70,
        25, 50, 100, 200, 400
    };

    private readonly TreasureDatabase _treasures;
    private readonly OracleSaveData? _saveData;
    private readonly byte[] _obtainedTreasureFlags = new byte[16];
    private readonly byte[] _inventoryStorage = new byte[InventoryCapacity];
    private readonly byte[] _dungeonSmallKeys = new byte[16];
    private readonly byte[] _dungeonBossKeys = new byte[2];
    private readonly byte[] _dungeonCompasses = new byte[2];
    private readonly byte[] _dungeonMaps = new byte[2];
    private readonly byte[] _ringBoxContents = new byte[5];
    private byte _upgradesObtained;

    public event Action? Changed;
    public event Action? HealthChanged;
    public event Action? RupeesChanged;

    public int EquippedB { get; private set; }
    public int EquippedA { get; private set; }
    public ReadOnlySpan<byte> InventoryStorage => _inventoryStorage;
    public int HealthQuarters { get; private set; }
    public int MaxHealthQuarters { get; private set; }
    public int Rupees { get; private set; }
    public int MaxBombs { get; private set; }
    public int Bombs { get; private set; }
    public int SwordLevel { get; private set; }
    public int ShieldLevel { get; private set; }
    public int BraceletLevel { get; private set; }
    public int SwitchHookLevel { get; private set; }
    public int BoomerangLevel { get; private set; }
    public int FeatherLevel { get; private set; }
    public int SlingshotLevel { get; private set; }
    public int SeedSatchelLevel { get; private set; }
    public int SatchelSelectedSeeds { get; private set; }
    public int ShooterSelectedSeeds { get; private set; }
    public int SlingshotSelectedSeeds { get; private set; }
    public int SelectedHarpSong { get; private set; }
    public int GashaSeeds { get; private set; }
    public int HeartPieces { get; private set; }
    public int Slates { get; private set; }
    public int Essences { get; private set; }
    public int TradeItem { get; private set; }
    public int TuniNutState { get; private set; }
    public int ActiveRing { get; private set; }
    public int RingBoxLevel { get; private set; }
    public int RingBoxCapacity => RingBoxLevel switch { 1 => 1, 2 => 3, >= 3 => 5, _ => 0 };

    public int GetDungeonSmallKeys(int dungeon) =>
        dungeon is >= 0 and < 16 ? _dungeonSmallKeys[dungeon] : 0;

    public bool HasDungeonBossKey(int dungeon) => HasDungeonBit(_dungeonBossKeys, dungeon);
    public bool HasDungeonCompass(int dungeon) => HasDungeonBit(_dungeonCompasses, dungeon);
    public bool HasDungeonMap(int dungeon) => HasDungeonBit(_dungeonMaps, dungeon);

    public InventoryState(TreasureDatabase treasures, OracleSaveData? saveData = null)
    {
        _treasures = treasures;
        _saveData = saveData;
        if (_saveData is null)
            ApplyStandardGameInitialVariables();
        else
            LoadFromSaveData();
    }

    public bool HasTreasure(int treasure) =>
        treasure >= 0 && treasure < 0x80 &&
        (_obtainedTreasureFlags[treasure >> 3] & (1 << (treasure & 7))) != 0;

    public int StorageItemAt(int index) =>
        index >= 0 && index < InventoryCapacity ? _inventoryStorage[index] : ItemNone;

    public int RingAt(int index) =>
        index >= 0 && index < RingBoxCapacity ? _ringBoxContents[index] : 0xff;

    public bool EquipRingAt(int index)
    {
        int ring = RingAt(index);
        if (ring == 0xff)
            return false;
        ActiveRing = ActiveRing == ring ? 0xff : ring;
        NotifyChanged();
        return true;
    }

    public void EquipA(int item) => EquipButton(item, isA: true);
    public void EquipB(int item) => EquipButton(item, isA: false);

    public void SwapStorageSlotWithButton(int storageIndex, bool isA)
    {
        if (storageIndex < 0 || storageIndex >= InventoryCapacity)
            return;

        int buttonSlot = isA ? 1 : 0;
        int oldButtonItem = GetInventorySlot(buttonSlot);
        SetInventorySlot(buttonSlot, _inventoryStorage[storageIndex]);
        _inventoryStorage[storageIndex] = (byte)oldButtonItem;
        NotifyChanged();
    }

    public void GiveTreasure(TreasureDatabase.TreasureObjectRecord treasureObject)
    {
        GiveTreasure(treasureObject.TreasureId, treasureObject.Parameter);
    }

    public bool ApplyDamage(int quarters)
    {
        if (quarters <= 0 || HealthQuarters <= 0)
            return false;

        int previous = HealthQuarters;
        HealthQuarters = Math.Max(0, HealthQuarters - quarters);
        if (previous == HealthQuarters)
            return false;

        HealthChanged?.Invoke();
        NotifyChanged();
        return true;
    }

    public bool Heal(int quarters)
    {
        if (quarters <= 0)
            return false;

        int previous = HealthQuarters;
        HealthQuarters = Math.Min(MaxHealthQuarters, HealthQuarters + quarters);
        if (previous == HealthQuarters)
            return false;

        HealthChanged?.Invoke();
        NotifyChanged();
        return true;
    }

    public void RefillHealth()
    {
        if (HealthQuarters == MaxHealthQuarters)
            return;

        HealthQuarters = MaxHealthQuarters;
        HealthChanged?.Invoke();
        NotifyChanged();
    }

    public void AddRupees(int amount)
    {
        int previous = Rupees;
        Rupees = Mathf.Clamp(Rupees + amount, 0, 999);
        if (previous == Rupees)
            return;

        RupeesChanged?.Invoke();
        NotifyChanged();
    }

    private void ApplyStandardGameInitialVariables()
    {
        Array.Fill(_ringBoxContents, (byte)0xff);
        ActiveRing = 0xff;
        MaxBombs = 0x10;
        HealthQuarters = 0x0c;
        MaxHealthQuarters = 0x0c;
        SetTreasureFlag(TreasurePunch);
    }

    private void LoadFromSaveData()
    {
        EquippedB = _saveData!.ReadWramByte(0xc688);
        EquippedA = _saveData.ReadWramByte(0xc689);
        _saveData.ReadWramBytes(0xc68a, _inventoryStorage);
        _saveData.ReadWramBytes(0xc69a, _obtainedTreasureFlags);
        _saveData.ReadWramBytes(0xc672, _dungeonSmallKeys);
        _saveData.ReadWramBytes(0xc682, _dungeonBossKeys);
        _saveData.ReadWramBytes(0xc684, _dungeonCompasses);
        _saveData.ReadWramBytes(0xc686, _dungeonMaps);
        _saveData.ReadWramBytes(0xc6c6, _ringBoxContents);
        HealthQuarters = _saveData.ReadWramByte(0xc6aa);
        MaxHealthQuarters = _saveData.ReadWramByte(0xc6ab);
        HeartPieces = _saveData.ReadWramByte(0xc6ac);
        Rupees = FromBcdWord(
            _saveData.ReadWramByte(0xc6ad) | _saveData.ReadWramByte(0xc6ae) << 8);
        ShieldLevel = _saveData.ReadWramByte(0xc6af);
        Bombs = _saveData.ReadWramByte(0xc6b0);
        MaxBombs = _saveData.ReadWramByte(0xc6b1);
        SwordLevel = _saveData.ReadWramByte(0xc6b2);
        SeedSatchelLevel = _saveData.ReadWramByte(0xc6b4);
        SwitchHookLevel = _saveData.ReadWramByte(0xc6b6);
        SelectedHarpSong = _saveData.ReadWramByte(0xc6b7);
        BraceletLevel = _saveData.ReadWramByte(0xc6b8);
        GashaSeeds = _saveData.ReadWramByte(0xc6be);
        Essences = _saveData.ReadWramByte(0xc6bf);
        TradeItem = _saveData.ReadWramByte(0xc6c0);
        TuniNutState = _saveData.ReadWramByte(0xc6c2);
        Slates = _saveData.ReadWramByte(0xc6c3);
        ActiveRing = _saveData.ReadWramByte(0xc6cb);
        RingBoxLevel = _saveData.ReadWramByte(0xc6cc);
    }

    private void NotifyChanged()
    {
        if (_saveData is not null)
        {
            _saveData.WriteWramByte(0xc688, (byte)EquippedB);
            _saveData.WriteWramByte(0xc689, (byte)EquippedA);
            _saveData.WriteWramBytes(0xc68a, _inventoryStorage);
            _saveData.WriteWramBytes(0xc69a, _obtainedTreasureFlags);
            _saveData.WriteWramBytes(0xc672, _dungeonSmallKeys);
            _saveData.WriteWramBytes(0xc682, _dungeonBossKeys);
            _saveData.WriteWramBytes(0xc684, _dungeonCompasses);
            _saveData.WriteWramBytes(0xc686, _dungeonMaps);
            _saveData.WriteWramBytes(0xc6c6, _ringBoxContents);
            _saveData.WriteWramByte(0xc6aa, (byte)HealthQuarters);
            _saveData.WriteWramByte(0xc6ab, (byte)MaxHealthQuarters);
            _saveData.WriteWramByte(0xc6ac, (byte)HeartPieces);
            int rupeesBcd = ToBcdWord(Rupees);
            _saveData.WriteWramByte(0xc6ad, (byte)rupeesBcd);
            _saveData.WriteWramByte(0xc6ae, (byte)(rupeesBcd >> 8));
            _saveData.WriteWramByte(0xc6af, (byte)ShieldLevel);
            _saveData.WriteWramByte(0xc6b0, (byte)Bombs);
            _saveData.WriteWramByte(0xc6b1, (byte)MaxBombs);
            _saveData.WriteWramByte(0xc6b2, (byte)SwordLevel);
            _saveData.WriteWramByte(0xc6b4, (byte)SeedSatchelLevel);
            _saveData.WriteWramByte(0xc6b6, (byte)SwitchHookLevel);
            _saveData.WriteWramByte(0xc6b7, (byte)SelectedHarpSong);
            _saveData.WriteWramByte(0xc6b8, (byte)BraceletLevel);
            _saveData.WriteWramByte(0xc6be, (byte)GashaSeeds);
            _saveData.WriteWramByte(0xc6bf, (byte)Essences);
            _saveData.WriteWramByte(0xc6c0, (byte)TradeItem);
            _saveData.WriteWramByte(0xc6c2, (byte)TuniNutState);
            _saveData.WriteWramByte(0xc6c3, (byte)Slates);
            _saveData.WriteWramByte(0xc6cb, (byte)ActiveRing);
            _saveData.WriteWramByte(0xc6cc, (byte)RingBoxLevel);
            _saveData.CommitInventoryChange();
        }
        Changed?.Invoke();
    }

    private void GiveTreasure(int treasure, int parameter)
    {
        if (treasure == TreasureDatabase.TreasureSeedSatchel)
        {
            GiveTreasureCore(treasure, parameter);
            GiveTreasureCore(TreasureDatabase.TreasureBombs + 0x1d, 0x20);
            return;
        }

        if (treasure == TreasureDatabase.TreasureHeartContainer)
        {
            GiveTreasureCore(treasure, parameter);
            GiveTreasureCore(TreasureDatabase.TreasureHeartRefill, 0x40);
            return;
        }

        GiveTreasureCore(treasure, parameter);
    }

    private void GiveTreasureCore(int treasure, int parameter)
    {
        if (treasure == TreasureDatabase.TreasureRingBox && RingBoxLevel == 0)
        {
            Array.Fill(_ringBoxContents, (byte)0xff);
            ActiveRing = 0xff;
        }
        AddTreasureToInventory(treasure);
        SetTreasureFlag(treasure);

        TreasureDatabase.BehaviourRecord behaviour = _treasures.GetBehaviour(treasure);
        ApplyParameter(behaviour.Variable, behaviour.Mode & 0x0f, parameter);
        NotifyChanged();
    }

    private void ApplyParameter(string variable, int mode, int parameter)
    {
        switch (mode)
        {
            case 0:
                return;
            case 1:
                SetBitVariable(variable, parameter);
                return;
            case 2:
                SetVariable(variable, GetVariable(variable) + 1);
                return;
            case 3:
                SetVariable(variable, AddBcd(GetVariable(variable), 1));
                return;
            case 4:
                SetVariable(variable, AddBcd(GetVariable(variable), parameter));
                return;
            case 5:
                SetVariable(variable, parameter);
                return;
            case 6:
                SetBitVariable(variable, 0);
                return;
            case 7:
                _dungeonSmallKeys[0]++;
                return;
            case 8:
                if (GetVariable(variable) < parameter)
                    SetVariable(variable, parameter);
                return;
            case 10:
                SetVariable(variable, GetVariable(variable) + parameter);
                return;
            case 11:
                _upgradesObtained |= (byte)(1 << (parameter & 7));
                return;
            case 12:
                AddCapped(variable, parameter, bcd: false);
                return;
            case 13:
                AddCapped(variable, parameter, bcd: true);
                return;
            case 14:
                AddRupees(RupeeValues[Math.Min(parameter, RupeeValues.Length - 1)]);
                return;
            case 15:
                SetVariable(variable, Math.Min(
                    AddBcd(GetVariable(variable), parameter),
                    SeedSatchelLevel switch { 2 => 0x50, >= 3 => 0x99, _ => 0x20 }));
                return;
            default:
                return;
        }
    }

    private void AddCapped(string variable, int parameter, bool bcd)
    {
        int value = bcd ? AddBcd(GetVariable(variable), parameter) : GetVariable(variable) + parameter;
        int cap = variable switch
        {
            "wLinkHealth" => MaxHealthQuarters,
            "wNumBombs" => MaxBombs,
            _ => 0xff
        };
        int previous = GetVariable(variable);
        SetVariable(variable, Math.Min(value, cap));
        if (variable == "wLinkHealth" && previous != HealthQuarters)
            HealthChanged?.Invoke();
    }

    private void AddTreasureToInventory(int treasure)
    {
        if (treasure < 0 || treasure >= NumInventoryItems)
            return;

        int item = treasure;
        int existing = FindInventoryItem(item);
        if (existing >= 0)
        {
            SetInventorySlot(existing, item);
            return;
        }

        int empty = FindInventoryItem(ItemNone);
        if (empty >= 0)
            SetInventorySlot(empty, item);
    }

    private int FindInventoryItem(int item)
    {
        for (int slot = 0; slot < InventoryCapacity + 2; slot++)
        {
            if (GetInventorySlot(slot) == item)
                return slot;
        }
        return -1;
    }

    private int GetInventorySlot(int slot) => slot switch
    {
        0 => EquippedB,
        1 => EquippedA,
        _ => _inventoryStorage[slot - 2]
    };

    private void SetInventorySlot(int slot, int item)
    {
        if (slot == 0)
            EquippedB = item;
        else if (slot == 1)
            EquippedA = item;
        else
            _inventoryStorage[slot - 2] = (byte)item;
    }

    private void EquipButton(int item, bool isA)
    {
        int slot = FindInventoryItem(item);
        if (slot < 0)
            return;

        int buttonSlot = isA ? 1 : 0;
        int oldButtonItem = GetInventorySlot(buttonSlot);
        SetInventorySlot(buttonSlot, item);
        SetInventorySlot(slot, oldButtonItem);
        NotifyChanged();
    }

    private void SetTreasureFlag(int treasure)
    {
        if (treasure < 0 || treasure >= 0x80)
            return;
        _obtainedTreasureFlags[treasure >> 3] |= (byte)(1 << (treasure & 7));
    }

    private void SetBitVariable(string variable, int bit)
    {
        byte mask = (byte)(1 << (bit & 7));
        switch (variable)
        {
            case "wDungeonBossKeys":
                _dungeonBossKeys[0] |= mask;
                break;
            case "wDungeonCompasses":
                _dungeonCompasses[0] |= mask;
                break;
            case "wDungeonMaps":
                _dungeonMaps[0] |= mask;
                break;
            default:
                SetVariable(variable, GetVariable(variable) | mask);
                break;
        }
    }

    private static bool HasDungeonBit(byte[] values, int dungeon) =>
        dungeon >= 0 && dungeon < values.Length * 8 &&
        (values[dungeon >> 3] & (1 << (dungeon & 7))) != 0;

    private int GetVariable(string variable) => variable switch
    {
        "wLinkHealth" => HealthQuarters,
        "wLinkMaxHealth" => MaxHealthQuarters,
        "wNumRupees" => Rupees,
        "wNumBombs" => Bombs,
        "wMaxBombs" => MaxBombs,
        "wSwordLevel" => SwordLevel,
        "wShieldLevel" => ShieldLevel,
        "wBraceletLevel" => BraceletLevel,
        "wSwitchHookLevel" => SwitchHookLevel,
        "wBoomerangLevel" => BoomerangLevel,
        "wFeatherLevel" => FeatherLevel,
        "wSlingshotLevel" => SlingshotLevel,
        "wSeedSatchelLevel" => SeedSatchelLevel,
        "wNumGashaSeeds" => GashaSeeds,
        "wNumHeartPieces" => HeartPieces,
        "wNumSlates" => Slates,
        "wSelectedHarpSong" => SelectedHarpSong,
        "wEssencesObtained" => Essences,
        "wTradeItem" => TradeItem,
        "wTuniNutState" => TuniNutState,
        "wRingBoxLevel" => RingBoxLevel,
        _ => 0
    };

    private void SetVariable(string variable, int value)
    {
        switch (variable)
        {
            case "wLinkHealth":
                HealthQuarters = Math.Min(value, MaxHealthQuarters);
                HealthChanged?.Invoke();
                break;
            case "wLinkMaxHealth":
                MaxHealthQuarters = value;
                HealthQuarters = Math.Min(HealthQuarters, MaxHealthQuarters);
                HealthChanged?.Invoke();
                break;
            case "wNumRupees":
                AddRupees(value - Rupees);
                break;
            case "wNumBombs":
                Bombs = Math.Min(value, MaxBombs);
                break;
            case "wMaxBombs":
                MaxBombs = value;
                Bombs = Math.Min(Bombs, MaxBombs);
                break;
            case "wSwordLevel":
                SwordLevel = value;
                break;
            case "wShieldLevel":
                ShieldLevel = value;
                break;
            case "wBraceletLevel":
                BraceletLevel = value;
                break;
            case "wSwitchHookLevel":
                SwitchHookLevel = value;
                break;
            case "wBoomerangLevel":
                BoomerangLevel = value;
                break;
            case "wFeatherLevel":
                FeatherLevel = value;
                break;
            case "wSlingshotLevel":
                SlingshotLevel = value;
                break;
            case "wSeedSatchelLevel":
                SeedSatchelLevel = value;
                break;
            case "wNumGashaSeeds":
                GashaSeeds = value;
                break;
            case "wNumHeartPieces":
                HeartPieces = value;
                break;
            case "wNumSlates":
                Slates = value;
                break;
            case "wSelectedHarpSong":
                SelectedHarpSong = value;
                break;
            case "wEssencesObtained":
                Essences = value;
                break;
            case "wTradeItem":
                TradeItem = value;
                break;
            case "wTuniNutState":
                TuniNutState = value;
                break;
            case "wRingBoxLevel":
                RingBoxLevel = value;
                break;
        }
    }

    private static int AddBcd(int current, int add)
    {
        int value = FromBcd(current) + FromBcd(add);
        return ToBcd(Math.Min(value, 99));
    }

    private static int FromBcd(int value) => (value >> 4) * 10 + (value & 0x0f);
    private static int ToBcd(int value) => value / 10 % 10 << 4 | value % 10;
    private static int FromBcdWord(int value) =>
        FromBcd(value & 0xff) + FromBcd(value >> 8 & 0xff) * 100;
    private static int ToBcdWord(int value) =>
        ToBcd(value % 100) | ToBcd(value / 100) << 8;
}
