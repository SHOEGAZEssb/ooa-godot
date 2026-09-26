using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class InventoryState
{
    public const int InventoryCapacity = 16;
    public const int NumInventoryItems = 0x20;

    private const int RingsObtainedByteCount = 8;
    private const int GashaSpotCount = 0x10;
    private const int RememberedCompanionIdAddress = 0xc631;
    private const int UnappraisedRingCapacity = 0x40;

    private static readonly int[] RupeeValues =
    {
        0, 1, 2, 5, 10, 20, 40, 30, 60, 70,
        25, 50, 100, 200, 400
    };

    private readonly TreasureDatabase _treasures;
    private readonly OracleSaveData? _saveData;
    private readonly OracleRuntimeState _runtimeState;
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
    private int _dummyC608;
    private int _shortSecretIndex;
    private int _satchelSelectedSeeds;
    private int _shooterSelectedSeeds;

    public event Action? Changed;
    public event Action? HealthChanged;
    public event Action? RupeesChanged;
    internal event Action? FullHealthRefillAttempted;
    internal event Action? RupeeCapExceeded;

    public int EquippedB { get; private set; }
    public int EquippedA { get; private set; }
    public int HealthQuarters { get; private set; }
    public int MaxHealthQuarters { get; private set; }
    public int Rupees { get; private set; }
    public int MaxBombs { get; private set; }
    public int Bombs { get; private set; }
    public int SwordLevel { get; private set; }
    public int ShieldLevel { get; private set; }
    public int BraceletLevel { get; private set; }
    public int SwitchHookLevel { get; private set; }
    public int BoomerangLevel => HasTreasure(TreasureId.Boomerang) ? 1 : 0;
    public int FeatherLevel => HasTreasure(TreasureId.Feather) ? 1 : 0;
    public int SlingshotLevel => 0;
    public int SeedSatchelLevel { get; private set; }
    public int SatchelSelectedSeeds => _satchelSelectedSeeds;
    public int ShooterSelectedSeeds => _shooterSelectedSeeds;
    public int SlingshotSelectedSeeds => 0;
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
    public int FluteIcon => _saveData?.ReadWramByte(WramAddress.wFluteIcon) ?? 0;
    public int RememberedCompanionId { get; private set; }
    public int ObtainedSeasons => 0;
    public int MagnetGlovePolarity => 0;
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
        Func<int>? currentDungeonIndex = null,
        OracleRuntimeState? runtimeState = null)
    {
        _treasures = treasures;
        _saveData = saveData;
        _runtimeState = runtimeState ?? new OracleRuntimeState();
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

    internal bool HasTreasureObjectForDebug(TreasureObjectRecord treasureObject) =>
        HasTreasure(treasureObject.TreasureId) &&
        (treasureObject.TreasureId != TreasureId.Flute ||
            (AnimalCompanion == treasureObject.Parameter &&
             (_saveData is null || _saveData.ReadWramByte(WramAddress.wFluteIcon) == treasureObject.Parameter - 0x0a))) &&
        (treasureObject.TreasureId != TreasureId.TradeItem ||
            TradeItem == treasureObject.Parameter);

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
            heartContainer.TreasureId != TreasureId.HeartContainer)
        {
            throw new InvalidOperationException(
                "A completed Heart Piece display requires its reset counter and Heart Container treasure.");
        }
        // standardTextStatef calls giveTreasure(TREASURE_HEART_CONTAINER, $04)
        // on the A/B press that replaces the piece text with TX_0049.
        GiveTreasure(heartContainer);
    }

    public int StorageItemAt(int index) =>
        index >= 0 && index < InventoryCapacity ? _inventoryStorage[index] : TreasureId.None;

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
        bit is >= 0 and < 8 &&
        (_runtimeState.ReadWramByte(WramAddress.wUpgradesObtained) & (1 << bit)) != 0;

    internal int LevelForInventoryDisplay(int treasure) => treasure switch
    {
        TreasureId.Shield => ShieldLevel,
        TreasureId.Sword => SwordLevel,
        TreasureId.Bracelet => BraceletLevel,
        TreasureId.SwitchHook => SwitchHookLevel,
        TreasureId.Boomerang => BoomerangLevel,
        TreasureId.Feather => FeatherLevel,
        _ => 0
    };

    internal int BcdAmountForInventoryDisplay(int treasure) => treasure switch
    {
        TreasureId.Bombs => Bombs,
        TreasureId.Bombchus => Bombchus,
        TreasureId.EmberSeeds => EmberSeeds,
        TreasureId.ScentSeeds => ScentSeeds,
        TreasureId.PegasusSeeds => PegasusSeeds,
        TreasureId.GaleSeeds => GaleSeeds,
        TreasureId.MysterySeeds => MysterySeeds,
        TreasureId.Ring => ToBcd(UnappraisedRingCount),
        TreasureId.GashaSeed => GashaSeeds,
        _ => 0
    };

    internal bool HasSelectedSatchelSeed()
    {
        int selected = _satchelSelectedSeeds;
        return selected is >= 0 and < 5 &&
            BcdAmountForInventoryDisplay(TreasureId.EmberSeeds + selected) != 0;
    }

    internal bool TryConsumeSelectedSatchelSeed(out int seedItem)
        => TryConsumeSelectedSeed(_satchelSelectedSeeds, out seedItem);

    internal bool HasSelectedShooterSeed()
    {
        int selected = _shooterSelectedSeeds;
        return selected is >= 0 and < 5 &&
            BcdAmountForInventoryDisplay(
                TreasureId.EmberSeeds + selected) != 0;
    }

    internal bool TryConsumeSelectedShooterSeed(out int seedItem)
        => TryConsumeSelectedSeed(_shooterSelectedSeeds, out seedItem);

    private bool TryConsumeSelectedSeed(int selectedSeed, out int seedItem)
    {
        seedItem = TreasureId.EmberSeeds + selectedSeed;
        TreasureVariable? variable = selectedSeed switch
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
        throw new NotSupportedException("TREASURE_SLINGSHOT $13 has no selected-seed state in clean US Ages.");

    public void SelectHarpSong(int song)
    {
        if (song is < 1 or > 3 ||
            !HasTreasure(TreasureId.TuneOfEchoes + song - 1))
        {
            throw new InvalidOperationException(
                $"Cannot select unowned Harp song ${song:x2}.");
        }
        if (SelectedHarpSong == song)
            return;
        SelectedHarpSong = song;
        NotifyChanged();
    }

    internal int[] ObtainedSeedTypes()
    {
        var seeds = new List<int>(5);
        for (int seed = 0; seed < 5; seed++)
        {
            if (HasTreasure(TreasureId.EmberSeeds + seed))
                seeds.Add(seed);
        }
        return seeds.ToArray();
    }

    internal int[] ObtainedHarpSongs()
    {
        var songs = new List<int>(3);
        for (int song = 1; song <= 3; song++)
        {
            if (HasTreasure(TreasureId.TuneOfEchoes + song - 1))
                songs.Add(song);
        }
        return songs.ToArray();
    }

    public bool EquipRingAt(int index)
    {
        if (index < 0 || index >= RingBoxCapacity) return false;
        int ring = RingAt(index);
        if (ring == 0xff && ActiveRing == 0xff)
            return false;
        ActiveRing = ActiveRing == ring ? 0xff : ring;
        NotifyChanged();
        return true;
    }

    public void EquipA(int item) => EquipButton(item, isA: true);
    public void EquipB(int item) => EquipButton(item, isA: false);

    /// <summary>
    /// Native minigame helpers write wInventoryB/A directly while preserving
    /// the ordinary storage slots, then restore the saved button bytes.
    /// </summary>
    internal void SetScriptedEquippedItems(int equippedB, int equippedA)
    {
        if (equippedB is < TreasureId.None or >= NumInventoryItems ||
            equippedA is < TreasureId.None or >= NumInventoryItems)
        {
            throw new ArgumentOutOfRangeException(
                nameof(equippedB),
                $"Invalid scripted equips ${equippedB:x2}/${equippedA:x2}.");
        }
        EquippedB = equippedB;
        EquippedA = equippedA;
        NotifyChanged();
    }

    public void SwapStorageSlotWithButton(int storageIndex, bool isA)
    {
        if (storageIndex < 0 || storageIndex >= InventoryCapacity)
            return;

        int buttonSlot = isA ? 1 : 0;
        // bank2.s:inventoryMenuState1@equipItem: the two-handed sword
        // vacates the selected slot first, then stores B before A.
        if (_inventoryStorage[storageIndex] == TreasureId.BiggoronSword)
        {
            _inventoryStorage[storageIndex] = (byte)GetInventorySlot(buttonSlot);
            SetInventorySlot(buttonSlot, TreasureId.None);
            StoreInFirstBlankSlot(EquippedB);
            StoreInFirstBlankSlot(EquippedA);
            EquippedB = EquippedA = TreasureId.BiggoronSword;
            NotifyChanged();
            return;
        }
        if (GetInventorySlot(buttonSlot) == TreasureId.BiggoronSword)
        {
            EquippedB = EquippedA = TreasureId.None;
            SetInventorySlot(buttonSlot, TreasureId.BiggoronSword);
        }
        int oldButtonItem = GetInventorySlot(buttonSlot);
        SetInventorySlot(buttonSlot, _inventoryStorage[storageIndex]);
        _inventoryStorage[storageIndex] = (byte)oldButtonItem;
        NotifyChanged();
    }

    private void StoreInFirstBlankSlot(int item)
    {
        if (item == TreasureId.None) return;
        int slot = Array.IndexOf(_inventoryStorage, (byte)TreasureId.None);
        if (slot < 0)
            throw new InvalidOperationException(
                "inventoryMenuState1@putItemInFirstBlankSlot: no storage for " +
                $"item ${item:x2} while equipping ITEM_BIGGORON_SWORD $0c.");
        _inventoryStorage[slot] = (byte)item;
    }

    public void GiveTreasure(TreasureObjectRecord treasureObject)
    {
        GiveTreasure(treasureObject.TreasureId, treasureObject.Parameter);
    }

    internal void ToggleTreasureObjectForDebug(TreasureObjectRecord treasureObject)
    {
        if (!HasTreasureObjectForDebug(treasureObject))
        {
            using (_saveData?.BeginMutation())
            {
                if (treasureObject.TreasureId == TreasureId.Flute)
                {
                    // companionScripts.s:companionScript_subid0a_state2 sets
                    // wFluteIcon separately from the Strange Flute treasure.
                    // Debug grants provide the callable flute, without advancing
                    // the forest quest. Retail collection retains mode $08.
                    SetVariable(TreasureVariable.AnimalCompanion, treasureObject.Parameter);
                    _saveData?.WriteWramByte(WramAddress.wFluteIcon, checked((byte)(treasureObject.Parameter - 0x0a)));
                }
                GiveTreasure(treasureObject);
            }
            return;
        }

        if (treasureObject.TreasureId is >= 0x60 and < 0x68)
        {
            // The debug toggle explicitly edits upgrade state; the original
            // loseTreasure_helper deliberately leaves this byte alone.
            _runtimeState.SetWramByte(WramAddress.wUpgradesObtained,
                (byte)(_runtimeState.ReadWramByte(WramAddress.wUpgradesObtained) &
                    ~(1 << (treasureObject.TreasureId & 7))));
            ClearTreasureFlag(treasureObject.TreasureId);
            NotifyChanged();
            return;
        }
        _ = LoseTreasure(treasureObject.TreasureId);
    }

    /// <summary>
    /// Mirrors scriptHelp.loseTreasure and loseTreasure_helper. Related
    /// variables such as wTradeItem and wSwordLevel intentionally remain
    /// unchanged after the obtained bit and first matching inventory slot are
    /// cleared.
    /// </summary>
    internal bool LoseTreasure(int treasure)
    {
        if (treasure is < 0 or >= 0x80)
            return false;
        int slot = treasure is > 0 and < NumInventoryItems ? FindInventoryItem(treasure) : -1;
        bool obtained = (_obtainedTreasureFlags[treasure >> 3] & (1 << (treasure & 7))) != 0;
        if (!obtained && slot < 0)
            return false;

        using (_saveData?.BeginMutation())
        {
            ClearTreasureFlag(treasure);
            // loseTreasure_helper only unsets wObtainedTreasureFlags, even
            // for $60-$67. It never clears transient wUpgradesObtained.

            if (slot >= 0)
                SetInventorySlot(slot, TreasureId.None);
            NotifyChanged();
        }
        return true;
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
        GiveTreasure(TreasureId.Ring, ring);
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

    /// <summary>
    /// Mirrors decNumBombs: the count is packed BCD and changes only after
    /// the ITEM_BOMB child has been allocated successfully.
    /// </summary>
    internal bool TryConsumeBomb()
    {
        int count = FromBcd(Bombs);
        if (count == 0)
            return false;
        Bombs = ToBcd(count - 1);
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Mirrors direct interaction-script writes to wNumMysterySeeds. The
    /// obtained-treasure bit is intentionally preserved; Ambi's guard clears
    /// only the current count before calling giveTreasure with parameter $00.
    /// </summary>
    internal void SetMysterySeedsFromScript(int value)
    {
        if (value is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value));
        MysterySeeds = value;
        NotifyChanged();
    }
    internal void SetScentSeedsFromScript(int value)
    {
        if(value<0||value>0x99||(value&15)>9) throw new ArgumentOutOfRangeException(nameof(value));
        ScentSeeds=value; NotifyChanged();
    }

    /// <summary>
    /// Mirrors the Tokay trading hut's direct BCD subtraction from a seed
    /// count. The obtained bit remains set when the count reaches zero.
    /// </summary>
    internal bool TryConsumeSeedsFromScript(int treasure, int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        int current = treasure switch
        {
            TreasureId.EmberSeeds => EmberSeeds,
            TreasureId.ScentSeeds => ScentSeeds,
            TreasureId.MysterySeeds => MysterySeeds,
            _ => throw new ArgumentOutOfRangeException(
                nameof(treasure), $"Treasure ${treasure:x2} is not a seed count.")
        };
        int count = FromBcd(current);
        if (count < amount)
            return false;
        int next = ToBcd(count - amount);
        if (treasure == TreasureId.EmberSeeds)
            EmberSeeds = next;
        else if (treasure == TreasureId.ScentSeeds)
            ScentSeeds = next;
        else
            MysterySeeds = next;
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Mirrors the extra work in scriptHelp.tokayGiveItemToLink before the
    /// stolen Seed Satchel treasure is created: Mystery Seeds are restored,
    /// every currently obtained seed type is refilled, and the level is
    /// decremented so the subsequent satchel pickup's Increment behavior
    /// leaves the original level unchanged.
    /// </summary>
    internal void PrepareReturnedTokaySeedSatchel()
    {
        using (_saveData?.BeginMutation())
        {
            GiveTreasureCore(TreasureId.MysterySeeds, 0x99);
            for (int treasure = TreasureId.EmberSeeds;
                 treasure <= 0x24;
                 treasure++)
            {
                if (HasTreasure(treasure))
                    GiveTreasureCore(treasure, 0x99);
            }
            SeedSatchelLevel = Math.Max(0, SeedSatchelLevel - 1);
            NotifyChanged();
        }
    }

    /// <summary>
    /// Mirrors scriptHelp.tokayGiveBombUpgrade's adjacent wMaxBombs/wNumBombs
    /// writes. The source adds packed-BCD $20 and fully refills the result.
    /// </summary>
    internal void ApplyTokayBombCapacityUpgrade()
    {
        int upgraded = (MaxBombs + 0x20) & 0xff;
        MaxBombs = upgraded;
        Bombs = upgraded;
        NotifyChanged();
    }

    // scriptHelp.bombUpgradeFairy_giveBombUpgrade writes packed BCD capacity
    // before giveTreasure adds/clamps the bomb count and restores ownership.
    internal void ApplyFairyBombCapacityUpgrade(int capacity)
    {
        MaxBombs = capacity;
        GiveTreasureCore(TreasureId.Bombs, capacity);
        NotifyChanged();
    }

    internal void ConfiscateBombs()
    {
        // The helper writes $01, then calls the ordinary packed-BCD decrement.
        Bombs = 1;
        TryConsumeBomb();
    }

    internal bool ApplyFairyHealthPenalty()
    {
        if (HealthQuarters < 4) return false;
        HealthQuarters = 4;
        HealthChanged?.Invoke();
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Mirrors mapleCheckLinkCanDropItem, including its original mistaken
    /// treasure-index checks and the one-rupee-to-five-rupee output bug.
    /// Values stored in WRAM remain packed BCD where the source uses DAA.
    /// </summary>
    internal bool TryTakeMapleDrop(int selectedIndex, out int actualIndex)
    {
        actualIndex = selectedIndex;
        bool changed;
        switch (selectedIndex)
        {
            case >= 5 and <= 9:
            {
                // The bug checks treasure IDs $05-$09 rather than the seed
                // treasure IDs $20-$24.
                if (!HasTreasure(selectedIndex))
                    return false;
                int value = selectedIndex switch
                {
                    5 => EmberSeeds,
                    6 => ScentSeeds,
                    7 => PegasusSeeds,
                    8 => GaleSeeds,
                    _ => MysterySeeds
                };
                int count = FromBcd(value);
                if (count < 5)
                    return false;
                int next = ToBcd(count - 5);
                switch (selectedIndex)
                {
                    case 5: EmberSeeds = next; break;
                    case 6: ScentSeeds = next; break;
                    case 7: PegasusSeeds = next; break;
                    case 8: GaleSeeds = next; break;
                    default: MysterySeeds = next; break;
                }
                changed = true;
                break;
            }

            case 10:
            {
                // $0a is TREASURE_SWITCH_HOOK in this mistaken check.
                if (!HasTreasure(TreasureId.SwitchHook))
                    return false;
                int count = FromBcd(Bombs);
                if (count < 4)
                    return false;
                Bombs = ToBcd(count - 4);
                changed = true;
                break;
            }

            case 11 or 12:
                if (HealthQuarters < 12)
                    return false;
                HealthQuarters -= 4;
                HealthChanged?.Invoke();
                actualIndex = 11;
                changed = true;
                break;

            case 13:
                if (Rupees <= 0)
                    return false;
                AddRupeesCore(-1);
                // The routine writes $0c, so Link loses one rupee but scatters
                // the five-rupee Maple item.
                actualIndex = 12;
                changed = true;
                break;

            default:
                return false;
        }

        if (changed)
            NotifyChanged();
        return changed;
    }

    internal void ApplySmogResetPenalty()
    {
        // INTERAC$33 @buttonPressed writes wLinkHealth directly: subtract4
        // only at >=$0c, without damage rings, recoil or invincibility.
        if (HealthQuarters < 0x0c) return;
        HealthQuarters -= 4;
        HealthChanged?.Invoke();
        NotifyChanged();
    }

    public bool ApplyDamage(int quarters)
    {
        if (quarters <= 0 || HealthQuarters <= 0)
            return false;

        int previous = HealthQuarters;
        HealthQuarters = Math.Max(0, HealthQuarters - quarters);
        if (previous == HealthQuarters)
            return false;

        // linkApplyDamage consumes TREASURE_POTION $2f at zero health before
        // wLinkDeathTrigger is armed. The potion is not an inventory slot
        // item, so loseTreasure clears only its obtained flag.
        if (HealthQuarters == 0 &&
            HasTreasure(TreasureId.Potion))
        {
            HealthQuarters = MaxHealthQuarters;
            ClearTreasureFlag(TreasureId.Potion);
        }

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
        if (_saveData?.HasGlobalFlag(GlobalFlag.Flag1000EnemiesKilled) != true)
        {
            TotalEnemiesKilled = Math.Min(0xffff, TotalEnemiesKilled + 1);
            if (TotalEnemiesKilled >= 1000)
                _saveData?.SetGlobalFlag(GlobalFlag.Flag1000EnemiesKilled);
        }
        if (_saveData is not null)
        {
            int maple = _saveData.ReadWramByte(WramAddress.wMapleKillCounter);
            _saveData.WriteWramByte(
                WramAddress.wMapleKillCounter, (byte)Math.Min(0xff, maple + 1));

            int credits = RingEffects.GashaKillCredits(this);
            for (int spot = 0; spot < GashaSpotCount; spot++)
            {
                int address = WramAddress.wGashaSpotKillCounters + spot;
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
        if (_saveData?.HasGlobalFlag(GlobalFlag.Flag10000RupeesCollected) == true)
            return false;
        long total = (long)TotalRupeesCollected + amount;
        if (total >= 10000)
        {
            // addDecimalToHlRef is a two-byte BCD addition. Its carry sets
            // GLOBALFLAG_10000_RUPEES_COLLECTED while the counter itself wraps.
            TotalRupeesCollected = (int)(total % 10000);
            _saveData?.SetGlobalFlag(GlobalFlag.Flag10000RupeesCollected);
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
        SetTreasureFlag(TreasureId.Punch);
    }

    private void LoadFromSaveData()
    {
        EquippedB = _saveData!.ReadWramByte(WramAddress.wInventoryB);
        EquippedA = _saveData.ReadWramByte(WramAddress.wInventoryA);
        _saveData.ReadWramBytes(WramAddress.wInventoryStorage, _inventoryStorage);
        _saveData.ReadWramBytes(WramAddress.wObtainedTreasureFlags, _obtainedTreasureFlags);
        _saveData.ReadWramBytes(WramAddress.wDungeonSmallKeys, _dungeonSmallKeys);
        _saveData.ReadWramBytes(WramAddress.wDungeonBossKeys, _dungeonBossKeys);
        _saveData.ReadWramBytes(WramAddress.wDungeonCompasses, _dungeonCompasses);
        _saveData.ReadWramBytes(WramAddress.wDungeonMaps, _dungeonMaps);
        _saveData.ReadWramBytes(WramAddress.wRingBoxContents, _ringBoxContents);
        _saveData.ReadWramBytes(WramAddress.wRingsObtained, _ringsObtained);
        TotalEnemiesKilled = _saveData.ReadWramByte(WramAddress.wTotalEnemiesKilled) |
            _saveData.ReadWramByte(WramAddress.wTotalEnemiesKilled + 1) << 8;
        TotalRupeesCollected = FromBcdWord(
            _saveData.ReadWramByte(WramAddress.wTotalRupeesCollected) |
            _saveData.ReadWramByte(WramAddress.wTotalRupeesCollected + 1) << 8);
        _saveData.ReadWramBytes(WramAddress.wUnappraisedRings, _unappraisedRings);
        _dummyC608 = _saveData.ReadWramByte(WramAddress.wc608);
        AnimalCompanion = _saveData.ReadWramByte(WramAddress.wAnimalCompanion);
        RememberedCompanionId = _saveData.ReadWramByte(RememberedCompanionIdAddress);
        HealthQuarters = _saveData.ReadWramByte(WramAddress.wLinkHealth);
        MaxHealthQuarters = _saveData.ReadWramByte(WramAddress.wLinkMaxHealth);
        HeartPieces = _saveData.ReadWramByte(WramAddress.wNumHeartPieces);
        Rupees = FromBcdWord(
            _saveData.ReadWramByte(WramAddress.wNumRupees) | _saveData.ReadWramByte(0xc6ae) << 8);
        ShieldLevel = _saveData.ReadWramByte(WramAddress.wShieldLevel);
        Bombs = _saveData.ReadWramByte(WramAddress.wNumBombs);
        MaxBombs = _saveData.ReadWramByte(WramAddress.wMaxBombs);
        SwordLevel = _saveData.ReadWramByte(WramAddress.wSwordLevel);
        Bombchus = _saveData.ReadWramByte(WramAddress.wNumBombchus);
        SeedSatchelLevel = _saveData.ReadWramByte(WramAddress.wSeedSatchelLevel);
        SwitchHookLevel = _saveData.ReadWramByte(WramAddress.wSwitchHookLevel);
        SelectedHarpSong = _saveData.ReadWramByte(WramAddress.wSelectedHarpSong);
        BraceletLevel = _saveData.ReadWramByte(WramAddress.wBraceletLevel);
        EmberSeeds = _saveData.ReadWramByte(WramAddress.wNumEmberSeeds);
        ScentSeeds = _saveData.ReadWramByte(WramAddress.wNumEmberSeeds + 1);
        PegasusSeeds = _saveData.ReadWramByte(WramAddress.wNumEmberSeeds + 2);
        GaleSeeds = _saveData.ReadWramByte(WramAddress.wNumEmberSeeds + 3);
        MysterySeeds = _saveData.ReadWramByte(WramAddress.wNumEmberSeeds + 4);
        GashaSeeds = _saveData.ReadWramByte(WramAddress.wNumGashaSeeds);
        Essences = _saveData.ReadWramByte(WramAddress.wEssencesObtained);
        TradeItem = _saveData.ReadWramByte(WramAddress.wTradeItem);
        TuniNutState = _saveData.ReadWramByte(WramAddress.wTuniNutState);
        Slates = _saveData.ReadWramByte(WramAddress.wNumSlates);
        ActiveRing = _saveData.ReadWramByte(WramAddress.wActiveRing);
        RingBoxLevel = _saveData.ReadWramByte(WramAddress.wRingBoxLevel);
        RingsAppraised = _saveData.ReadWramByte(WramAddress.wNumRingsAppraised);
        _shortSecretIndex = _saveData.ReadWramByte(WramAddress.wShortSecretIndex);
        _satchelSelectedSeeds = _saveData.ReadWramByte(WramAddress.wSatchelSelectedSeeds);
        _shooterSelectedSeeds = _saveData.ReadWramByte(WramAddress.wShooterSelectedSeeds);
    }

    private void NotifyChanged()
    {
        if (_saveData is not null)
        {
            _saveData.WriteWramByte(WramAddress.wInventoryB, (byte)EquippedB);
            _saveData.WriteWramByte(WramAddress.wInventoryA, (byte)EquippedA);
            _saveData.WriteWramBytes(WramAddress.wInventoryStorage, _inventoryStorage);
            _saveData.WriteWramBytes(WramAddress.wObtainedTreasureFlags, _obtainedTreasureFlags);
            _saveData.WriteWramBytes(WramAddress.wDungeonSmallKeys, _dungeonSmallKeys);
            _saveData.WriteWramBytes(WramAddress.wDungeonBossKeys, _dungeonBossKeys);
            _saveData.WriteWramBytes(WramAddress.wDungeonCompasses, _dungeonCompasses);
            _saveData.WriteWramBytes(WramAddress.wDungeonMaps, _dungeonMaps);
            _saveData.WriteWramBytes(WramAddress.wRingBoxContents, _ringBoxContents);
            _saveData.WriteWramBytes(WramAddress.wRingsObtained, _ringsObtained);
            _saveData.WriteWramByte(
                WramAddress.wTotalEnemiesKilled, (byte)TotalEnemiesKilled);
            _saveData.WriteWramByte(
                WramAddress.wTotalEnemiesKilled + 1, (byte)(TotalEnemiesKilled >> 8));
            int totalRupeesBcd = ToBcdWord(TotalRupeesCollected);
            _saveData.WriteWramByte(
                WramAddress.wTotalRupeesCollected, (byte)totalRupeesBcd);
            _saveData.WriteWramByte(
                WramAddress.wTotalRupeesCollected + 1, (byte)(totalRupeesBcd >> 8));
            _saveData.WriteWramBytes(WramAddress.wUnappraisedRings, _unappraisedRings);
            _saveData.WriteWramByte(WramAddress.wLinkHealth, (byte)HealthQuarters);
            _saveData.WriteWramByte(WramAddress.wLinkMaxHealth, (byte)MaxHealthQuarters);
            _saveData.WriteWramByte(WramAddress.wNumHeartPieces, (byte)HeartPieces);
            int rupeesBcd = ToBcdWord(Rupees);
            _saveData.WriteWramByte(WramAddress.wNumRupees, (byte)rupeesBcd);
            _saveData.WriteWramByte(0xc6ae, (byte)(rupeesBcd >> 8));
            _saveData.WriteWramByte(WramAddress.wShieldLevel, (byte)ShieldLevel);
            _saveData.WriteWramByte(WramAddress.wNumBombs, (byte)Bombs);
            _saveData.WriteWramByte(WramAddress.wMaxBombs, (byte)MaxBombs);
            _saveData.WriteWramByte(WramAddress.wSwordLevel, (byte)SwordLevel);
            _saveData.WriteWramByte(WramAddress.wNumBombchus, (byte)Bombchus);
            _saveData.WriteWramByte(WramAddress.wSeedSatchelLevel, (byte)SeedSatchelLevel);
            _saveData.WriteWramByte(WramAddress.wSwitchHookLevel, (byte)SwitchHookLevel);
            _saveData.WriteWramByte(WramAddress.wSelectedHarpSong, (byte)SelectedHarpSong);
            _saveData.WriteWramByte(WramAddress.wBraceletLevel, (byte)BraceletLevel);
            _saveData.WriteWramByte(WramAddress.wNumEmberSeeds, (byte)EmberSeeds);
            _saveData.WriteWramByte(WramAddress.wNumEmberSeeds + 1, (byte)ScentSeeds);
            _saveData.WriteWramByte(WramAddress.wNumEmberSeeds + 2, (byte)PegasusSeeds);
            _saveData.WriteWramByte(WramAddress.wNumEmberSeeds + 3, (byte)GaleSeeds);
            _saveData.WriteWramByte(WramAddress.wNumEmberSeeds + 4, (byte)MysterySeeds);
            _saveData.WriteWramByte(WramAddress.wNumGashaSeeds, (byte)GashaSeeds);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, (byte)Essences);
            _saveData.WriteWramByte(WramAddress.wTradeItem, (byte)TradeItem);
            _saveData.WriteWramByte(WramAddress.wTuniNutState, (byte)TuniNutState);
            _saveData.WriteWramByte(WramAddress.wNumSlates, (byte)Slates);
            _saveData.WriteWramByte(WramAddress.wActiveRing, (byte)ActiveRing);
            _saveData.WriteWramByte(WramAddress.wRingBoxLevel, (byte)RingBoxLevel);
            _saveData.WriteWramByte(WramAddress.wNumRingsAppraised, (byte)RingsAppraised);
            _saveData.WriteWramByte(
                WramAddress.wNumUnappraisedRingsBcd, (byte)ToBcd(UnappraisedRingCount));
            PersistAuxiliaryVariables();
            _saveData.CommitInventoryChange();
        }
        Changed?.Invoke();
    }

    internal void AssignAnimalCompanion(int companion)
    {
        if (companion is < 0x0b or > 0x0d)
            throw new ArgumentOutOfRangeException(nameof(companion));
        SetVariable(TreasureVariable.AnimalCompanion, companion);
        NotifyChanged();
    }

    internal void GiveTreasure(int treasure, int parameter)
    {
        using (_saveData?.BeginMutation())
        {
            GiveTreasureCore(treasure, parameter);
            // The source invokes @giveTreasure once for the extra item;
            // it does not recursively apply @extraItemsToAddTable.
            if (_treasures.TryGetExtraItem(treasure, out var extra))
                GiveTreasureCore(extra.TreasureId, extra.Parameter);
            NotifyChanged();
        }
    }

    /// <summary>Mirrors scriptHelp.interaction6b_refillBombs.</summary>
    internal void RefillBombs()
    {
        using (_saveData?.BeginMutation())
        {
            Bombs = MaxBombs;
            NotifyChanged();
        }
    }

    private void GiveTreasureCore(int treasure, int parameter)
    {
        int maturity = _treasures.GetGashaMaturityGain(treasure, parameter);
        if (maturity != 0)
            _saveData?.AddGashaMaturity(maturity);
        if (treasure == TreasureId.RingBox && RingBoxLevel == 0)
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
                _runtimeState.SetWramByte(WramAddress.wUpgradesObtained,
                    (byte)(_runtimeState.ReadWramByte(WramAddress.wUpgradesObtained) |
                        (1 << (parameter & 7))));
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

        int empty = FindInventoryItem(TreasureId.None);
        if (empty >= 0)
        {
            SetInventorySlot(empty, item);
            // treasureAndDrops.s:addTreasureToInventory handles a newly
            // awarded Biggoron sword in either button by moving the other
            // button's old item to the first available slot.
            if (item == TreasureId.BiggoronSword && empty < 2)
            {
                int other = empty ^ 1;
                int displaced = GetInventorySlot(other);
                SetInventorySlot(other, item);
                if (displaced != TreasureId.None)
                    AddTreasureToInventory(displaced);
            }
        }
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

    private void ClearTreasureFlag(int treasure)
    {
        if (treasure < 0 || treasure >= 0x80)
            return;
        _obtainedTreasureFlags[treasure >> 3] &=
            (byte)~(1 << (treasure & 7));
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
        TreasureVariable.BoomerangLevel => BoomerangLevel,
        TreasureVariable.MagnetGlovePolarity => MagnetGlovePolarity,
        TreasureVariable.SlingshotLevel => SlingshotLevel,
        TreasureVariable.FeatherLevel => FeatherLevel,
        TreasureVariable.SatchelSelectedSeeds => _satchelSelectedSeeds,
        TreasureVariable.ShooterSelectedSeeds => _shooterSelectedSeeds,
        TreasureVariable.SlingshotSelectedSeeds => SlingshotSelectedSeeds,
        _ => throw new InvalidOperationException(
            $"Treasure WRAM variable {variable} is not a scalar binding.")
    };

    internal void SetRestorationItemState(TreasureVariable variable, int value)
    {
        if (variable is not (TreasureVariable.TuniNutState or TreasureVariable.TradeItem))
            throw new ArgumentOutOfRangeException(nameof(variable));
        SetVariable(variable, value);
        NotifyChanged();
    }

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
            TreasureVariable.SatchelSelectedSeeds or
            TreasureVariable.ShooterSelectedSeeds)
        {
            _dirtyAuxiliaryVariables.Add(variable);
        }
    }

    private void PersistAuxiliaryVariables()
    {
        _dummyC608 = PersistAuxiliaryVariable(
            TreasureVariable.DummyC608, WramAddress.wc608, _dummyC608);
        AnimalCompanion = PersistAuxiliaryVariable(
            TreasureVariable.AnimalCompanion,
            WramAddress.wAnimalCompanion,
            AnimalCompanion);
        RememberedCompanionId = PersistAuxiliaryVariable(
            TreasureVariable.RememberedCompanionId,
            RememberedCompanionIdAddress,
            RememberedCompanionId);
        _shortSecretIndex = PersistAuxiliaryVariable(
            TreasureVariable.ShortSecretIndex,
            WramAddress.wShortSecretIndex,
            _shortSecretIndex);
        _satchelSelectedSeeds = PersistAuxiliaryVariable(
            TreasureVariable.SatchelSelectedSeeds,
            WramAddress.wSatchelSelectedSeeds,
            _satchelSelectedSeeds);
        _shooterSelectedSeeds = PersistAuxiliaryVariable(
            TreasureVariable.ShooterSelectedSeeds,
            WramAddress.wShooterSelectedSeeds,
            _shooterSelectedSeeds);

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
