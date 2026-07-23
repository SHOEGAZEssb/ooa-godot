using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class InventoryState
{
    public const int InventoryCapacity = 16;
    public const int NumInventoryItems = 0x20;
    public const int ItemNone = 0x00;
    public const int ItemShield = 0x01;
    public const int ItemSword = 0x05;
    public const int ItemShovel = 0x15;
    public const int ItemBracelet = 0x16;
    public const int ItemSeedSatchel = 0x19;
    public const int TreasurePunch = 0x02;

    private const int UnappraisedRingsAddress = 0xc5c0;
    private const int DummyC608Address = 0xc608;
    private const int AnimalCompanionAddress = 0xc610;
    private const int RingsObtainedAddress = 0xc616;
    private const int RingsObtainedByteCount = 8;
    private const int TotalEnemiesKilledAddress = 0xc620;
    private const int TotalRupeesCollectedAddress = 0xc627;
    private const int MapleKillCounterAddress = 0xc641;
    private const int GashaSpotKillCountersAddress = 0xc64f;
    private const int GashaSpotCount = 0x10;
    private const int RememberedCompanionIdAddress = 0xc631;
    private const int BombchusAddress = 0xc6b3;
    private const int EmberSeedsAddress = 0xc6b9;
    private const int UnappraisedRingCountAddress = 0xc6cd;
    private const int RingsAppraisedAddress = 0xc6ce;
    private const int MagnetGlovePolarityAddress = 0xc6f0;
    private const int ShortSecretIndexAddress = 0xc6fb;
    private const int SlingshotLevelAddress = 0xc6ff;
    private const int BoomerangLevelAddress = 0xc700;
    private const int FeatherLevelAddress = 0xc701;
    private const int ObtainedSeasonsAddress = 0xc702;
    private const int SatchelSelectedSeedsAddress = 0xc703;
    private const int ShooterSelectedSeedsAddress = 0xc704;
    private const int SlingshotSelectedSeedsAddress = 0xc705;
    private const int UnappraisedRingCapacity = 0x40;

    private static readonly int[] RupeeValues =
    {
        0, 1, 2, 5, 10, 20, 40, 30, 60, 70,
        25, 50, 100, 200, 400
    };

    private readonly TreasureDatabase _treasures;
    private readonly OracleSaveData? _saveData;
    private readonly Func<int> _currentDungeonIndex;
    private readonly byte[] _obtainedTreasureFlags = new byte[16];
    private readonly byte[] _inventoryStorage = new byte[InventoryCapacity];
    private readonly byte[] _dungeonSmallKeys = new byte[16];
    private readonly byte[] _dungeonBossKeys = new byte[2];
    private readonly byte[] _dungeonCompasses = new byte[2];
    private readonly byte[] _dungeonMaps = new byte[2];
    private readonly byte[] _ringBoxContents = new byte[5];
    private readonly byte[] _ringsObtained = new byte[RingsObtainedByteCount];
    private readonly byte[] _unappraisedRings = new byte[UnappraisedRingCapacity];
    private readonly HashSet<TreasureVariable> _dirtyAuxiliaryVariables = new();
    private byte _upgradesObtained;
    private int _dummyC608;
    private int _shortSecretIndex;
    private int _boomerangLevel;
    private int _featherLevel;
    private int _slingshotLevel;
    private int _satchelSelectedSeeds;
    private int _shooterSelectedSeeds;
    private int _slingshotSelectedSeeds;

    public event Action? Changed;
    public event Action? HealthChanged;
    public event Action? RupeesChanged;
    internal event Action? FullHealthRefillAttempted;
    internal event Action? RupeeCapExceeded;

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
    public int BoomerangLevel => _boomerangLevel;
    public int FeatherLevel => _featherLevel;
    public int SlingshotLevel => _slingshotLevel;
    public int SeedSatchelLevel { get; private set; }
    public int SatchelSelectedSeeds => _satchelSelectedSeeds;
    public int ShooterSelectedSeeds => _shooterSelectedSeeds;
    public int SlingshotSelectedSeeds => _slingshotSelectedSeeds;
    public int SelectedHarpSong { get; private set; }
    public int Bombchus { get; private set; }
    public int EmberSeeds { get; private set; }
    public int ScentSeeds { get; private set; }
    public int PegasusSeeds { get; private set; }
    public int GaleSeeds { get; private set; }
    public int MysterySeeds { get; private set; }
    public int GashaSeeds { get; private set; }
    public int HeartPieces { get; private set; }
    public int Slates { get; private set; }
    public int Essences { get; private set; }
    public int TradeItem { get; private set; }
    public int TuniNutState { get; private set; }
    public int ActiveRing { get; private set; }
    public int RingBoxLevel { get; private set; }
    public int RingsAppraised { get; private set; }
    public int TotalEnemiesKilled { get; private set; }
    public int TotalRupeesCollected { get; private set; }
    public int RingBoxCapacity => RingBoxLevel switch { 1 => 1, 2 => 3, >= 3 => 5, _ => 0 };
    public int AnimalCompanion { get; private set; }
    public int RememberedCompanionId { get; private set; }
    public int ObtainedSeasons { get; private set; }
    public int MagnetGlovePolarity { get; private set; }
    public int UnappraisedRingCount => CountUnappraisedRings();
    public bool IsRingActive(RingId ring) => ActiveRing == (int)ring;

    public int GetDungeonSmallKeys(int dungeon) =>
        dungeon is >= 0 and < 16 ? _dungeonSmallKeys[dungeon] : 0;

    public bool TryUseDungeonSmallKey(int dungeon)
    {
        if (dungeon is < 0 or >= 16 || _dungeonSmallKeys[dungeon] == 0)
            return false;
        _dungeonSmallKeys[dungeon]--;
        NotifyChanged();
        return true;
    }

    public bool HasDungeonBossKey(int dungeon) => HasDungeonBit(_dungeonBossKeys, dungeon);
    public bool HasDungeonCompass(int dungeon) => HasDungeonBit(_dungeonCompasses, dungeon);
    public bool HasDungeonMap(int dungeon) => HasDungeonBit(_dungeonMaps, dungeon);

    public InventoryState(
        TreasureDatabase treasures,
        OracleSaveData? saveData = null,
        Func<int>? currentDungeonIndex = null)
    {
        _treasures = treasures;
        _saveData = saveData;
        _currentDungeonIndex = currentDungeonIndex ?? (() => -1);
        if (_saveData is null)
            ApplyStandardGameInitialVariables();
        else
            LoadFromSaveData();
    }

    public bool HasTreasure(int treasure) => treasure switch
    {
        >= 0x60 and < 0x68 => HasUpgrade(treasure & 7),
        >= 0 and < 0x80 =>
            (_obtainedTreasureFlags[treasure >> 3] & (1 << (treasure & 7))) != 0,
        _ => false
    };

    internal void CompleteHeartPieceSet(
        TreasureObjectRecord heartContainer)
    {
        ResetCompletedHeartPieceSet();
        GiveCompletedHeartContainer(heartContainer);
    }

    internal void ResetCompletedHeartPieceSet()
    {
        if (HeartPieces != 4)
            throw new InvalidOperationException("A completed Heart Piece set requires four pieces.");

        // textbox.s:func_53eb clears wNumHeartPieces when the 30-update inline
        // display fills its fourth quarter, before the player accepts TX_0049.
        HeartPieces = 0;
        NotifyChanged();
    }

    internal void GiveCompletedHeartContainer(
        TreasureObjectRecord heartContainer)
    {
        if (HeartPieces != 0 ||
            heartContainer.TreasureId != TreasureDatabase.TreasureHeartContainer)
        {
            throw new InvalidOperationException(
                "A completed Heart Piece display requires its reset counter and Heart Container treasure.");
        }
        // standardTextStatef calls giveTreasure(TREASURE_HEART_CONTAINER, $04)
        // on the A/B press that replaces the piece text with TX_0049.
        GiveTreasure(heartContainer);
    }

    public int StorageItemAt(int index) =>
        index >= 0 && index < InventoryCapacity ? _inventoryStorage[index] : ItemNone;

    public int RingAt(int index) =>
        index >= 0 && index < RingBoxCapacity ? _ringBoxContents[index] : 0xff;

    public int UnappraisedRingAt(int index) =>
        index >= 0 && index < UnappraisedRingCapacity ? _unappraisedRings[index] : 0xff;

    public bool HasAppraisedRing(int ring) =>
        ring is >= 0 and < 0x40 &&
        (_ringsObtained[ring >> 3] & (1 << (ring & 7))) != 0;

    internal void GrantAppraisedRingForDebug(int ring)
    {
        if (ring is < 0 or >= 0x40)
            throw new ArgumentOutOfRangeException(nameof(ring));
        _ringsObtained[ring >> 3] |= (byte)(1 << (ring & 7));
        NotifyChanged();
    }

    /// <summary>
    /// Performs the paid portion of bank 2's ringMenu_unappraisedRings_state1:
    /// debit the appraisal price, count the appraisal, and reveal the selected
    /// entry by clearing its $40 unidentified bit. The entry remains in the
    /// appraisal list until its name and description have both closed.
    /// </summary>
    internal bool TryBeginRingAppraisal(int index, int cost, out int ring)
    {
        ring = UnappraisedRingAt(index);
        if (ring == 0xff || cost < 0 || Rupees < cost)
            return false;

        ring &= 0x3f;
        AddRupeesCore(-cost);
        RingsAppraised = Math.Min(0xff, RingsAppraised + 1);
        _unappraisedRings[index] = (byte)ring;
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Ports ringMenu_addRingToList: remove the selected appraisal entry,
    /// register a new ring in wRingsObtained, or issue the duplicate refund.
    /// </summary>
    internal RingAppraisalResult CompleteRingAppraisal(int index, int duplicateRefund)
    {
        int ring = UnappraisedRingAt(index);
        if (ring == 0xff || (ring & 0x40) != 0)
            throw new InvalidOperationException(
                "Only a revealed unappraised ring can be added to the ring list.");

        ring &= 0x3f;
        bool duplicate = HasAppraisedRing(ring);
        _unappraisedRings[index] = 0xff;
        RealignUnappraisedRings();
        if (!duplicate)
            _ringsObtained[ring >> 3] |= (byte)(1 << (ring & 7));
        NotifyChanged();
        return new RingAppraisalResult(ring, duplicate,
            duplicate ? Math.Max(0, duplicateRefund) : 0);
    }

    internal void ApplyRingAppraisalRefund(int amount)
    {
        if (amount <= 0)
            return;
        AddRupeesCore(amount);
        NotifyChanged();
    }

    /// <summary>
    /// Implements the ring-list A-button transaction. A ring may occur in at
    /// most one box slot; selecting it in its current destination removes it.
    /// </summary>
    internal bool SetRingBoxSlotFromList(int slot, int ring)
    {
        if (slot < 0 || slot >= RingBoxCapacity)
            return false;
        if (ring != 0xff && !HasAppraisedRing(ring))
            return false;

        int previousSlot = -1;
        if (ring != 0xff)
        {
            for (int index = 0; index < _ringBoxContents.Length; index++)
            {
                if (_ringBoxContents[index] == ring)
                {
                    previousSlot = index;
                    _ringBoxContents[index] = 0xff;
                    break;
                }
            }
        }
        _ringBoxContents[slot] = previousSlot == slot ? (byte)0xff : (byte)ring;
        NotifyChanged();
        return true;
    }

    internal bool DeactivateRingIfMissingFromBox()
    {
        if (ActiveRing == 0xff)
            return false;
        for (int slot = 0; slot < RingBoxCapacity; slot++)
        {
            if (_ringBoxContents[slot] == ActiveRing)
                return false;
        }
        ActiveRing = 0xff;
        NotifyChanged();
        return true;
    }

    public bool HasUpgrade(int bit) =>
        bit is >= 0 and < 8 && (_upgradesObtained & (1 << bit)) != 0;

    internal int LevelForInventoryDisplay(int treasure) => treasure switch
    {
        TreasureDatabase.TreasureShield => ShieldLevel,
        TreasureDatabase.TreasureSword => SwordLevel,
        TreasureDatabase.TreasureBracelet => BraceletLevel,
        TreasureDatabase.TreasureSwitchHook => SwitchHookLevel,
        TreasureDatabase.TreasureBoomerang => BoomerangLevel,
        TreasureDatabase.TreasureFeather => FeatherLevel,
        _ => 0
    };

    internal int BcdAmountForInventoryDisplay(int treasure) => treasure switch
    {
        TreasureDatabase.TreasureBombs => Bombs,
        0x0d => Bombchus,
        TreasureDatabase.TreasureEmberSeeds => EmberSeeds,
        0x21 => ScentSeeds,
        0x22 => PegasusSeeds,
        0x23 => GaleSeeds,
        0x24 => MysterySeeds,
        TreasureDatabase.TreasureRing => ToBcd(UnappraisedRingCount),
        0x34 => GashaSeeds,
        _ => 0
    };

    internal bool HasSelectedSatchelSeed()
    {
        int selected = _satchelSelectedSeeds;
        return selected is >= 0 and < 5 &&
            BcdAmountForInventoryDisplay(TreasureDatabase.TreasureEmberSeeds + selected) != 0;
    }

    internal bool TryConsumeSelectedSatchelSeed(out int seedItem)
    {
        seedItem = TreasureDatabase.TreasureEmberSeeds + _satchelSelectedSeeds;
        TreasureVariable? variable = _satchelSelectedSeeds switch
        {
            0 => TreasureVariable.EmberSeeds,
            1 => TreasureVariable.ScentSeeds,
            2 => TreasureVariable.PegasusSeeds,
            3 => TreasureVariable.GaleSeeds,
            4 => TreasureVariable.MysterySeeds,
            _ => null
        };
        if (!variable.HasValue)
            return false;
        int count = GetVariable(variable.Value);
        if (count == 0)
            return false;

        int decimalCount = (count >> 4) * 10 + (count & 0x0f);
        decimalCount--;
        SetVariable(variable.Value, ((decimalCount / 10) << 4) | decimalCount % 10);
        NotifyChanged();
        return true;
    }

    public void SelectSatchelSeeds(int seeds) =>
        SetSelectedSeeds(ref _satchelSelectedSeeds,
            TreasureVariable.SatchelSelectedSeeds, seeds);

    public void SelectShooterSeeds(int seeds) =>
        SetSelectedSeeds(ref _shooterSelectedSeeds,
            TreasureVariable.ShooterSelectedSeeds, seeds);

    public void SelectSlingshotSeeds(int seeds) =>
        SetSelectedSeeds(ref _slingshotSelectedSeeds,
            TreasureVariable.SlingshotSelectedSeeds, seeds);

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

    public void GiveTreasure(TreasureObjectRecord treasureObject)
    {
        GiveTreasure(treasureObject.TreasureId, treasureObject.Parameter);
    }

    /// <summary>
    /// The original giveRingToLink overrides INTERAC_TREASURE's var34 with
    /// the concrete ring index before the ordinary TREASURE_RING behavior
    /// runs. Treasure-object rows keep $ff there as a placeholder.
    /// </summary>
    internal void GiveUnappraisedRing(int ring)
    {
        if (ring is < 0 or >= 0x40)
            throw new ArgumentOutOfRangeException(nameof(ring));
        GiveTreasure(TreasureDatabase.TreasureRing, ring);
    }

    internal bool ConsumeGashaSeed()
    {
        int count = FromBcd(GashaSeeds);
        if (count == 0)
            return false;
        GashaSeeds = ToBcd(count - 1);
        NotifyChanged();
        return true;
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
        {
            if (previous == MaxHealthQuarters)
                FullHealthRefillAttempted?.Invoke();
            return false;
        }

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
        if (AddRupeesCore(amount))
            NotifyChanged();
    }

    private bool AddRupeesCore(int amount)
    {
        bool totalChanged = amount > 0 && RecordCollectedRupees(amount);
        int previous = Rupees;
        long result = (long)Rupees + amount;
        Rupees = (int)Math.Clamp(result, 0, 999);
        if (result > 999)
        {
            // giveTreasure mode $0e requests SND_RUPEE when its BCD addition
            // exceeds $0999, including when the displayed count is already full.
            RupeeCapExceeded?.Invoke();
        }
        if (previous == Rupees)
            return totalChanged;

        RupeesChanged?.Invoke();
        return true;
    }

    internal void RecordEnemyKill()
    {
        // enemyDie stops only the lifetime Slayer counter after its award flag
        // is set. Maple, every planted Gasha spot, and Gasha maturity continue
        // advancing for every counted enemy death.
        if (_saveData?.HasGlobalFlag(0x00) != true)
        {
            TotalEnemiesKilled = Math.Min(0xffff, TotalEnemiesKilled + 1);
            if (TotalEnemiesKilled >= 1000)
                _saveData?.SetGlobalFlag(0x00);
        }
        if (_saveData is not null)
        {
            int maple = _saveData.ReadWramByte(MapleKillCounterAddress);
            _saveData.WriteWramByte(
                MapleKillCounterAddress, (byte)Math.Min(0xff, maple + 1));

            int credits = RingEffects.GashaKillCredits(this);
            for (int spot = 0; spot < GashaSpotCount; spot++)
            {
                int address = GashaSpotKillCountersAddress + spot;
                int count = _saveData.ReadWramByte(address);
                _saveData.WriteWramByte(
                    address, (byte)Math.Min(0xff, count + credits));
            }
            _saveData.AddGashaMaturity(3);
        }
        NotifyChanged();
    }

    private bool RecordCollectedRupees(int amount)
    {
        if (_saveData?.HasGlobalFlag(0x01) == true)
            return false;
        long total = (long)TotalRupeesCollected + amount;
        if (total >= 10000)
        {
            // addDecimalToHlRef is a two-byte BCD addition. Its carry sets
            // GLOBALFLAG_10000_RUPEES_COLLECTED while the counter itself wraps.
            TotalRupeesCollected = (int)(total % 10000);
            _saveData?.SetGlobalFlag(0x01);
        }
        else
        {
            TotalRupeesCollected = (int)total;
        }
        return true;
    }

    private void ApplyStandardGameInitialVariables()
    {
        Array.Fill(_ringBoxContents, (byte)0xff);
        Array.Fill(_unappraisedRings, (byte)0xff);
        _dummyC608 = 1;
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
        _saveData.ReadWramBytes(RingsObtainedAddress, _ringsObtained);
        TotalEnemiesKilled = _saveData.ReadWramByte(TotalEnemiesKilledAddress) |
            _saveData.ReadWramByte(TotalEnemiesKilledAddress + 1) << 8;
        TotalRupeesCollected = FromBcdWord(
            _saveData.ReadWramByte(TotalRupeesCollectedAddress) |
            _saveData.ReadWramByte(TotalRupeesCollectedAddress + 1) << 8);
        _saveData.ReadWramBytes(UnappraisedRingsAddress, _unappraisedRings);
        _dummyC608 = _saveData.ReadWramByte(DummyC608Address);
        AnimalCompanion = _saveData.ReadWramByte(AnimalCompanionAddress);
        RememberedCompanionId = _saveData.ReadWramByte(RememberedCompanionIdAddress);
        HealthQuarters = _saveData.ReadWramByte(0xc6aa);
        MaxHealthQuarters = _saveData.ReadWramByte(0xc6ab);
        HeartPieces = _saveData.ReadWramByte(0xc6ac);
        Rupees = FromBcdWord(
            _saveData.ReadWramByte(0xc6ad) | _saveData.ReadWramByte(0xc6ae) << 8);
        ShieldLevel = _saveData.ReadWramByte(0xc6af);
        Bombs = _saveData.ReadWramByte(0xc6b0);
        MaxBombs = _saveData.ReadWramByte(0xc6b1);
        SwordLevel = _saveData.ReadWramByte(0xc6b2);
        Bombchus = _saveData.ReadWramByte(BombchusAddress);
        SeedSatchelLevel = _saveData.ReadWramByte(0xc6b4);
        SwitchHookLevel = _saveData.ReadWramByte(0xc6b6);
        SelectedHarpSong = _saveData.ReadWramByte(0xc6b7);
        BraceletLevel = _saveData.ReadWramByte(0xc6b8);
        EmberSeeds = _saveData.ReadWramByte(EmberSeedsAddress);
        ScentSeeds = _saveData.ReadWramByte(EmberSeedsAddress + 1);
        PegasusSeeds = _saveData.ReadWramByte(EmberSeedsAddress + 2);
        GaleSeeds = _saveData.ReadWramByte(EmberSeedsAddress + 3);
        MysterySeeds = _saveData.ReadWramByte(EmberSeedsAddress + 4);
        GashaSeeds = _saveData.ReadWramByte(0xc6be);
        Essences = _saveData.ReadWramByte(0xc6bf);
        TradeItem = _saveData.ReadWramByte(0xc6c0);
        TuniNutState = _saveData.ReadWramByte(0xc6c2);
        Slates = _saveData.ReadWramByte(0xc6c3);
        ActiveRing = _saveData.ReadWramByte(0xc6cb);
        RingBoxLevel = _saveData.ReadWramByte(0xc6cc);
        RingsAppraised = _saveData.ReadWramByte(RingsAppraisedAddress);
        MagnetGlovePolarity = _saveData.ReadWramByte(MagnetGlovePolarityAddress);
        _shortSecretIndex = _saveData.ReadWramByte(ShortSecretIndexAddress);
        _slingshotLevel = _saveData.ReadWramByte(SlingshotLevelAddress);
        _boomerangLevel = _saveData.ReadWramByte(BoomerangLevelAddress);
        _featherLevel = _saveData.ReadWramByte(FeatherLevelAddress);
        ObtainedSeasons = _saveData.ReadWramByte(ObtainedSeasonsAddress);
        _satchelSelectedSeeds = _saveData.ReadWramByte(SatchelSelectedSeedsAddress);
        _shooterSelectedSeeds = _saveData.ReadWramByte(ShooterSelectedSeedsAddress);
        _slingshotSelectedSeeds = _saveData.ReadWramByte(SlingshotSelectedSeedsAddress);
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
            _saveData.WriteWramBytes(RingsObtainedAddress, _ringsObtained);
            _saveData.WriteWramByte(
                TotalEnemiesKilledAddress, (byte)TotalEnemiesKilled);
            _saveData.WriteWramByte(
                TotalEnemiesKilledAddress + 1, (byte)(TotalEnemiesKilled >> 8));
            int totalRupeesBcd = ToBcdWord(TotalRupeesCollected);
            _saveData.WriteWramByte(
                TotalRupeesCollectedAddress, (byte)totalRupeesBcd);
            _saveData.WriteWramByte(
                TotalRupeesCollectedAddress + 1, (byte)(totalRupeesBcd >> 8));
            _saveData.WriteWramBytes(UnappraisedRingsAddress, _unappraisedRings);
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
            _saveData.WriteWramByte(BombchusAddress, (byte)Bombchus);
            _saveData.WriteWramByte(0xc6b4, (byte)SeedSatchelLevel);
            _saveData.WriteWramByte(0xc6b6, (byte)SwitchHookLevel);
            _saveData.WriteWramByte(0xc6b7, (byte)SelectedHarpSong);
            _saveData.WriteWramByte(0xc6b8, (byte)BraceletLevel);
            _saveData.WriteWramByte(EmberSeedsAddress, (byte)EmberSeeds);
            _saveData.WriteWramByte(EmberSeedsAddress + 1, (byte)ScentSeeds);
            _saveData.WriteWramByte(EmberSeedsAddress + 2, (byte)PegasusSeeds);
            _saveData.WriteWramByte(EmberSeedsAddress + 3, (byte)GaleSeeds);
            _saveData.WriteWramByte(EmberSeedsAddress + 4, (byte)MysterySeeds);
            _saveData.WriteWramByte(0xc6be, (byte)GashaSeeds);
            _saveData.WriteWramByte(0xc6bf, (byte)Essences);
            _saveData.WriteWramByte(0xc6c0, (byte)TradeItem);
            _saveData.WriteWramByte(0xc6c2, (byte)TuniNutState);
            _saveData.WriteWramByte(0xc6c3, (byte)Slates);
            _saveData.WriteWramByte(0xc6cb, (byte)ActiveRing);
            _saveData.WriteWramByte(0xc6cc, (byte)RingBoxLevel);
            _saveData.WriteWramByte(RingsAppraisedAddress, (byte)RingsAppraised);
            _saveData.WriteWramByte(
                UnappraisedRingCountAddress, (byte)ToBcd(UnappraisedRingCount));
            PersistAuxiliaryVariables();
            _saveData.CommitInventoryChange();
        }
        Changed?.Invoke();
    }

    internal void GiveTreasure(int treasure, int parameter)
    {
        if (treasure == TreasureDatabase.TreasureSeedSatchel)
        {
            GiveTreasureCore(treasure, parameter);
            GiveTreasureCore(TreasureDatabase.TreasureEmberSeeds, 0x20);
        }
        else if (treasure == TreasureDatabase.TreasureHeartContainer)
        {
            GiveTreasureCore(treasure, parameter);
            GiveTreasureCore(TreasureDatabase.TreasureHeartRefill, 0x40);
        }
        else
        {
            GiveTreasureCore(treasure, parameter);
        }
        NotifyChanged();
    }

    private void GiveTreasureCore(int treasure, int parameter)
    {
        int maturity = _treasures.GetGashaMaturityGain(treasure, parameter);
        if (maturity != 0)
            _saveData?.AddGashaMaturity(maturity);
        if (treasure == TreasureDatabase.TreasureRingBox && RingBoxLevel == 0)
        {
            Array.Fill(_ringBoxContents, (byte)0xff);
            ActiveRing = 0xff;
        }
        AddTreasureToInventory(treasure);
        SetTreasureFlag(treasure);

        BehaviourRecord behaviour = _treasures.GetBehaviour(treasure);
        ApplyParameter(behaviour, parameter);
    }

    private void ApplyParameter(BehaviourRecord behaviour, int parameter)
    {
        TreasureVariable variable = behaviour.Variable;
        switch (behaviour.Mode)
        {
            case CollectionMode.None:
                return;
            case CollectionMode.SetBit:
                SetBitVariable(variable, parameter);
                return;
            case CollectionMode.Increment:
                SetVariable(variable, GetVariable(variable) + 1);
                return;
            case CollectionMode.IncrementBcd:
                SetVariable(variable, AddBcd(GetVariable(variable), 1));
                return;
            case CollectionMode.AddBcd:
                SetVariable(variable, AddBcd(GetVariable(variable), parameter));
                return;
            case CollectionMode.Set:
                SetVariable(variable, parameter);
                return;
            case CollectionMode.SetDungeonBit:
            {
                int dungeon = GetCurrentDungeonIndex();
                if (dungeon >= 0)
                    SetBitVariable(variable, dungeon);
                return;
            }
            case CollectionMode.IncrementDungeonKey:
            {
                int dungeon = GetCurrentDungeonIndex();
                if (dungeon >= 0)
                    _dungeonSmallKeys[dungeon]++;
                return;
            }
            case CollectionMode.SetMinimum:
                if (GetVariable(variable) < parameter)
                    SetVariable(variable, parameter);
                return;
            case CollectionMode.AddUnappraisedRing:
                AddUnappraisedRing(parameter);
                return;
            case CollectionMode.Add:
                SetVariable(variable, GetVariable(variable) + parameter);
                return;
            case CollectionMode.SetUpgradeBit:
                _upgradesObtained |= (byte)(1 << (parameter & 7));
                return;
            case CollectionMode.AddCapped:
                AddCapped(variable, parameter, bcd: false);
                return;
            case CollectionMode.AddBcdCapped:
                AddCapped(variable, parameter, bcd: true);
                return;
            case CollectionMode.AddRupees:
                AddRupeesCore(RupeeValues[Math.Min(parameter, RupeeValues.Length - 1)]);
                return;
            case CollectionMode.AddSeeds:
                SetVariable(variable, Math.Min(
                    AddBcd(GetVariable(variable), parameter),
                    SeedSatchelLevel switch { 2 => 0x50, >= 3 => 0x99, _ => 0x20 }));
                return;
            default:
                throw new InvalidOperationException(
                    $"Treasure ${behaviour.TreasureId:x2} has unsupported mode ${behaviour.RawMode:x2}.");
        }
    }

    private void AddCapped(
        TreasureVariable variable,
        int parameter,
        bool bcd)
    {
        int value = bcd ? AddBcd(GetVariable(variable), parameter) : GetVariable(variable) + parameter;
        int cap = variable switch
        {
            TreasureVariable.LinkHealth => MaxHealthQuarters,
            TreasureVariable.Bombs => MaxBombs,
            _ => 0xff
        };
        int previous = GetVariable(variable);
        SetVariable(variable, Math.Min(value, cap));
        if (variable == TreasureVariable.LinkHealth)
        {
            if (previous != HealthQuarters)
                HealthChanged?.Invoke();
            else if (previous == cap && parameter > 0)
                FullHealthRefillAttempted?.Invoke();
        }
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

    private void SetBitVariable(TreasureVariable variable, int bit)
    {
        byte mask = (byte)(1 << (bit & 7));
        switch (variable)
        {
            case TreasureVariable.DungeonBossKeys:
                SetIndexedBit(_dungeonBossKeys, bit, mask);
                break;
            case TreasureVariable.DungeonCompasses:
                SetIndexedBit(_dungeonCompasses, bit, mask);
                break;
            case TreasureVariable.DungeonMaps:
                SetIndexedBit(_dungeonMaps, bit, mask);
                break;
            default:
                SetVariable(variable, GetVariable(variable) | mask);
                break;
        }
    }

    private int GetCurrentDungeonIndex()
    {
        int dungeon = _currentDungeonIndex();
        return dungeon is >= 0 and < 16 ? dungeon : -1;
    }

    private static void SetIndexedBit(byte[] values, int bit, byte mask)
    {
        if (bit is < 0 || bit >= values.Length * 8)
            return;
        values[bit >> 3] |= mask;
    }

    private static bool HasDungeonBit(byte[] values, int dungeon) =>
        dungeon >= 0 && dungeon < values.Length * 8 &&
        (values[dungeon >> 3] & (1 << (dungeon & 7))) != 0;

    private int GetVariable(TreasureVariable variable) => variable switch
    {
        TreasureVariable.ShortSecretIndex => _shortSecretIndex,
        TreasureVariable.DummyC608 => _dummyC608,
        TreasureVariable.AnimalCompanion => AnimalCompanion,
        TreasureVariable.RememberedCompanionId => RememberedCompanionId,
        TreasureVariable.Bombchus => Bombchus,
        TreasureVariable.LinkHealth => HealthQuarters,
        TreasureVariable.LinkMaxHealth => MaxHealthQuarters,
        TreasureVariable.HeartPieces => HeartPieces,
        TreasureVariable.Rupees => Rupees,
        TreasureVariable.ShieldLevel => ShieldLevel,
        TreasureVariable.Bombs => Bombs,
        TreasureVariable.SwordLevel => SwordLevel,
        TreasureVariable.SeedSatchelLevel => SeedSatchelLevel,
        TreasureVariable.SwitchHookLevel => SwitchHookLevel,
        TreasureVariable.SelectedHarpSong => SelectedHarpSong,
        TreasureVariable.BraceletLevel => BraceletLevel,
        TreasureVariable.EmberSeeds => EmberSeeds,
        TreasureVariable.ScentSeeds => ScentSeeds,
        TreasureVariable.PegasusSeeds => PegasusSeeds,
        TreasureVariable.GaleSeeds => GaleSeeds,
        TreasureVariable.MysterySeeds => MysterySeeds,
        TreasureVariable.GashaSeeds => GashaSeeds,
        TreasureVariable.EssencesObtained => Essences,
        TreasureVariable.TradeItem => TradeItem,
        TreasureVariable.TuniNutState => TuniNutState,
        TreasureVariable.Slates => Slates,
        TreasureVariable.RingBoxLevel => RingBoxLevel,
        TreasureVariable.ObtainedSeasons => ObtainedSeasons,
        TreasureVariable.BoomerangLevel => _boomerangLevel,
        TreasureVariable.MagnetGlovePolarity => MagnetGlovePolarity,
        TreasureVariable.SlingshotLevel => _slingshotLevel,
        TreasureVariable.FeatherLevel => _featherLevel,
        TreasureVariable.SatchelSelectedSeeds => _satchelSelectedSeeds,
        TreasureVariable.ShooterSelectedSeeds => _shooterSelectedSeeds,
        TreasureVariable.SlingshotSelectedSeeds => _slingshotSelectedSeeds,
        _ => throw new InvalidOperationException(
            $"Treasure WRAM variable {variable} is not a scalar binding.")
    };

    private void SetVariable(TreasureVariable variable, int value)
    {
        int byteValue = value & 0xff;
        switch (variable)
        {
            case TreasureVariable.ShortSecretIndex:
                _shortSecretIndex = byteValue;
                break;
            case TreasureVariable.DummyC608:
                _dummyC608 = byteValue;
                break;
            case TreasureVariable.AnimalCompanion:
                AnimalCompanion = byteValue;
                break;
            case TreasureVariable.RememberedCompanionId:
                RememberedCompanionId = byteValue;
                break;
            case TreasureVariable.Bombchus:
                Bombchus = byteValue;
                break;
            case TreasureVariable.LinkHealth:
                HealthQuarters = Math.Min(value, MaxHealthQuarters);
                HealthChanged?.Invoke();
                break;
            case TreasureVariable.LinkMaxHealth:
                MaxHealthQuarters = byteValue;
                HealthQuarters = Math.Min(HealthQuarters, MaxHealthQuarters);
                HealthChanged?.Invoke();
                break;
            case TreasureVariable.HeartPieces:
                HeartPieces = byteValue;
                break;
            case TreasureVariable.Rupees:
                AddRupeesCore(value - Rupees);
                break;
            case TreasureVariable.ShieldLevel:
                ShieldLevel = byteValue;
                break;
            case TreasureVariable.Bombs:
                Bombs = Math.Min(value, MaxBombs);
                break;
            case TreasureVariable.SwordLevel:
                SwordLevel = byteValue;
                break;
            case TreasureVariable.SeedSatchelLevel:
                SeedSatchelLevel = byteValue;
                break;
            case TreasureVariable.SwitchHookLevel:
                SwitchHookLevel = byteValue;
                break;
            case TreasureVariable.SelectedHarpSong:
                SelectedHarpSong = byteValue;
                break;
            case TreasureVariable.BraceletLevel:
                BraceletLevel = byteValue;
                break;
            case TreasureVariable.EmberSeeds:
                EmberSeeds = byteValue;
                break;
            case TreasureVariable.ScentSeeds:
                ScentSeeds = byteValue;
                break;
            case TreasureVariable.PegasusSeeds:
                PegasusSeeds = byteValue;
                break;
            case TreasureVariable.GaleSeeds:
                GaleSeeds = byteValue;
                break;
            case TreasureVariable.MysterySeeds:
                MysterySeeds = byteValue;
                break;
            case TreasureVariable.GashaSeeds:
                GashaSeeds = byteValue;
                break;
            case TreasureVariable.EssencesObtained:
                Essences = byteValue;
                break;
            case TreasureVariable.TradeItem:
                TradeItem = byteValue;
                break;
            case TreasureVariable.TuniNutState:
                TuniNutState = byteValue;
                break;
            case TreasureVariable.Slates:
                Slates = byteValue;
                break;
            case TreasureVariable.RingBoxLevel:
                RingBoxLevel = byteValue;
                break;
            case TreasureVariable.ObtainedSeasons:
                ObtainedSeasons = byteValue;
                break;
            case TreasureVariable.BoomerangLevel:
                _boomerangLevel = byteValue;
                break;
            case TreasureVariable.MagnetGlovePolarity:
                MagnetGlovePolarity = byteValue;
                break;
            case TreasureVariable.SlingshotLevel:
                _slingshotLevel = byteValue;
                break;
            case TreasureVariable.FeatherLevel:
                _featherLevel = byteValue;
                break;
            default:
                throw new InvalidOperationException(
                    $"Treasure WRAM variable {variable} is not a writable scalar binding.");
        }
        MarkAuxiliaryVariableDirty(variable);
    }

    private void AddUnappraisedRing(int parameter)
    {
        int count = RealignUnappraisedRings();
        if (count >= UnappraisedRingCapacity)
        {
            var duplicates = new int[UnappraisedRingCapacity];
            foreach (byte ring in _unappraisedRings)
                duplicates[ring & 0x3f]++;

            int mostDuplicatedRing = 0;
            int mostDuplicates = 0;
            for (int ring = 0; ring < duplicates.Length; ring++)
            {
                // The original updates on ties, choosing the highest ring ID.
                if (duplicates[ring] >= mostDuplicates)
                {
                    mostDuplicates = duplicates[ring];
                    mostDuplicatedRing = ring;
                }
            }

            byte duplicate = (byte)(mostDuplicatedRing | 0x40);
            for (int index = _unappraisedRings.Length - 1; index >= 0; index--)
            {
                if (_unappraisedRings[index] != duplicate)
                    continue;
                _unappraisedRings[index] = 0xff;
                break;
            }
            count = RealignUnappraisedRings();
        }

        _unappraisedRings[count] = (byte)(parameter | 0x40);
        RealignUnappraisedRings();
    }

    private int RealignUnappraisedRings()
    {
        int write = 0;
        for (int read = 0; read < _unappraisedRings.Length; read++)
        {
            byte ring = _unappraisedRings[read];
            if (ring == 0xff)
                continue;
            _unappraisedRings[write++] = ring;
        }
        Array.Fill(_unappraisedRings, (byte)0xff, write, _unappraisedRings.Length - write);
        return write;
    }

    private int CountUnappraisedRings()
    {
        int count = 0;
        foreach (byte ring in _unappraisedRings)
        {
            if (ring != 0xff)
                count++;
        }
        return count;
    }

    private void SetSelectedSeeds(
        ref int selectedSeeds,
        TreasureVariable variable,
        int seeds)
    {
        int selected = Math.Clamp(seeds, 0, 4);
        if (selectedSeeds == selected)
            return;
        selectedSeeds = selected;
        _dirtyAuxiliaryVariables.Add(variable);
        NotifyChanged();
    }

    private void MarkAuxiliaryVariableDirty(TreasureVariable variable)
    {
        if (variable is TreasureVariable.ShortSecretIndex or
            TreasureVariable.DummyC608 or
            TreasureVariable.AnimalCompanion or
            TreasureVariable.RememberedCompanionId or
            TreasureVariable.ObtainedSeasons or
            TreasureVariable.BoomerangLevel or
            TreasureVariable.MagnetGlovePolarity or
            TreasureVariable.SlingshotLevel or
            TreasureVariable.FeatherLevel or
            TreasureVariable.SatchelSelectedSeeds or
            TreasureVariable.ShooterSelectedSeeds or
            TreasureVariable.SlingshotSelectedSeeds)
        {
            _dirtyAuxiliaryVariables.Add(variable);
        }
    }

    private void PersistAuxiliaryVariables()
    {
        _dummyC608 = PersistAuxiliaryVariable(
            TreasureVariable.DummyC608, DummyC608Address, _dummyC608);
        AnimalCompanion = PersistAuxiliaryVariable(
            TreasureVariable.AnimalCompanion,
            AnimalCompanionAddress,
            AnimalCompanion);
        RememberedCompanionId = PersistAuxiliaryVariable(
            TreasureVariable.RememberedCompanionId,
            RememberedCompanionIdAddress,
            RememberedCompanionId);
        MagnetGlovePolarity = PersistAuxiliaryVariable(
            TreasureVariable.MagnetGlovePolarity,
            MagnetGlovePolarityAddress,
            MagnetGlovePolarity);
        _shortSecretIndex = PersistAuxiliaryVariable(
            TreasureVariable.ShortSecretIndex,
            ShortSecretIndexAddress,
            _shortSecretIndex);
        _slingshotLevel = PersistAuxiliaryVariable(
            TreasureVariable.SlingshotLevel,
            SlingshotLevelAddress,
            _slingshotLevel);
        _boomerangLevel = PersistAuxiliaryVariable(
            TreasureVariable.BoomerangLevel,
            BoomerangLevelAddress,
            _boomerangLevel);
        _featherLevel = PersistAuxiliaryVariable(
            TreasureVariable.FeatherLevel,
            FeatherLevelAddress,
            _featherLevel);
        ObtainedSeasons = PersistAuxiliaryVariable(
            TreasureVariable.ObtainedSeasons,
            ObtainedSeasonsAddress,
            ObtainedSeasons);
        _satchelSelectedSeeds = PersistAuxiliaryVariable(
            TreasureVariable.SatchelSelectedSeeds,
            SatchelSelectedSeedsAddress,
            _satchelSelectedSeeds);
        _shooterSelectedSeeds = PersistAuxiliaryVariable(
            TreasureVariable.ShooterSelectedSeeds,
            ShooterSelectedSeedsAddress,
            _shooterSelectedSeeds);
        _slingshotSelectedSeeds = PersistAuxiliaryVariable(
            TreasureVariable.SlingshotSelectedSeeds,
            SlingshotSelectedSeedsAddress,
            _slingshotSelectedSeeds);

    }

    private int PersistAuxiliaryVariable(
        TreasureVariable variable,
        int address,
        int value)
    {
        if (_dirtyAuxiliaryVariables.Remove(variable))
        {
            _saveData!.WriteWramByte(address, (byte)value);
            return value;
        }
        return _saveData!.ReadWramByte(address);
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

public enum CollectionMode
{
    None = 0x0,
    SetBit = 0x1,
    Increment = 0x2,
    IncrementBcd = 0x3,
    AddBcd = 0x4,
    Set = 0x5,
    SetDungeonBit = 0x6,
    IncrementDungeonKey = 0x7,
    SetMinimum = 0x8,
    AddUnappraisedRing = 0x9,
    Add = 0xa,
    SetUpgradeBit = 0xb,
    AddCapped = 0xc,
    AddBcdCapped = 0xd,
    AddRupees = 0xe,
    AddSeeds = 0xf
}

internal readonly record struct RingAppraisalResult(int Ring, bool Duplicate, int Refund);

public enum TreasureVariable
{
    ShortSecretIndex,
    DummyC608,
    AnimalCompanion,
    RememberedCompanionId,
    Bombchus,
    LinkHealth,
    LinkMaxHealth,
    HeartPieces,
    Rupees,
    ShieldLevel,
    Bombs,
    SwordLevel,
    SeedSatchelLevel,
    SwitchHookLevel,
    SelectedHarpSong,
    BraceletLevel,
    EmberSeeds,
    ScentSeeds,
    PegasusSeeds,
    GaleSeeds,
    MysterySeeds,
    GashaSeeds,
    EssencesObtained,
    TradeItem,
    TuniNutState,
    Slates,
    RingBoxLevel,
    UnappraisedRings,
    DungeonSmallKeys,
    DungeonBossKeys,
    DungeonCompasses,
    DungeonMaps,
    ObtainedSeasons,
    BoomerangLevel,
    MagnetGlovePolarity,
    SlingshotLevel,
    FeatherLevel,
    SatchelSelectedSeeds,
    ShooterSelectedSeeds,
    SlingshotSelectedSeeds
}
