
namespace Oracles;

/// <summary>
/// Advances runtime status-bar values which deliberately trail their live
/// inventory values. updateStatusBar_body moves wDisplayedRupees by one BCD
/// rupee on every original update, while displayed health recovers one quarter
/// heart every four updates and requests SND_GAINHEART at full-heart boundaries.
/// </summary>
internal sealed class StatusBarController : IDisposable
{
    private readonly InventoryState _inventory;
    private readonly Hud _hud;
    private readonly Action<int> _playSound;
    private readonly Func<int>? _frameCounterSource;
    private readonly StatusBarCurrencyDatabase _currency;
    private readonly OracleRuntimeState _memory;
    private double _updateTicks;
    private int _frameCounter;

    internal int DisplayedRupees
    {
        get
        {
            int low = _memory.ReadField("wDisplayedRupees"), high = _memory.ReadField("wDisplayedRupees", 1);
            if ((low & 15) > 9 || (low >> 4) > 9 || high > 9)
                throw new NotSupportedException($"bank2.s:updateStatusBar_body: invalid displayed money BCD ${high:x2}{low:x2}.");
            return high * 100 + (low >> 4) * 10 + (low & 15);
        }
    }
    internal int DisplayedHealth => _memory.ReadField("wDisplayedHearts");

    internal StatusBarController(
        InventoryState inventory,
        Hud hud,
        Action<int> playSound,
        Func<int>? frameCounter = null)
    {
        _inventory = inventory;
        _hud = hud;
        _playSound = playSound;
        _frameCounterSource = frameCounter;
        if (inventory.AssetRoot != hud.AssetRoot)
            throw new ArgumentException("Status display and inventory require the same native asset pack.");
        _currency = hud.Currency; _memory = inventory.RuntimeState;
        hud.LoadCommonGraphics(hud.TilesetFlags);
        _inventory.FullHealthRefillAttempted += PlayGainHeartSound;
        _inventory.RupeeCapExceeded += PlayRupeeSound;
        _hud.HealthQuarters = inventory.HealthQuarters; _hud.MaxHealthQuarters = inventory.MaxHealthQuarters;
        _hud.Rupees = _currency.MoneyValue(inventory,_currency.SelectMoney(hud.TilesetFlags));
        _hud.Refresh();
    }

    internal void InitializeNativeState()
    {
        if (_currency.SelectorAddress >= 0)
            _memory.SetWramByte(_currency.SelectorAddress,(byte)_currency.SelectMoney(_hud.TilesetFlags));
        SynchronizeHealth();
        SynchronizeRupees();
    }

    public void Dispose()
    {
        _inventory.FullHealthRefillAttempted -= PlayGainHeartSound;
        _inventory.RupeeCapExceeded -= PlayRupeeSound;
    }

    internal void Update(double delta)
    {
        if (delta <= 0.0 || _memory.ReadField("wDontUpdateStatusBar") != 0)
            return;

        _updateTicks += delta * OracleSoundEngine.UpdatesPerSecond;
        bool changed = false;
        while (_updateTicks >= 1.0)
        {
            _updateTicks -= 1.0;
            _frameCounter = _frameCounterSource?.Invoke() ?? ((_frameCounter + 1) & 0xff);
            int displayed = DisplayedRupees, target = TargetMoney;
            if (displayed != target)
            {
                WriteDisplayedMoney(displayed + Math.Sign(target - displayed));
                _playSound(SoundId.SndRupee);
                changed = true;
            }
            else if (_hud.Rupees != displayed) { _hud.Rupees = displayed; changed = true; }
            // Native updateStatusBar_body requests the money cue before
            // advancing health, including coincident full-heart updates.
            changed |= UpdateDisplayedHealth();
        }

        if (changed)
            _hud.Refresh();
    }

    internal void UpdateLowHealthWarning()
    {
        // bank2.s:playHeartBeepAtInterval runs from eligible normal-menu
        // dispatch, before opening input. Use live health and global phase,
        // independently of the trailing displayed heart animation.
        int frame = _frameCounterSource?.Invoke() ?? _frameCounter;
        // The native ID $01 exclusion is unused by supported Link states.
        // Mounted Link is ID $09 and still receives this warning.
        if ((frame & 0x3f) != 0) return;
        int doubled = ((_inventory.HealthQuarters - 1) & 0xff) * 2;
        if (doubled > 0xff) return; // Original first ADD A carry gate.
        if (((doubled * 2) & 0xff) < _inventory.MaxHealthQuarters)
            _playSound(SoundId.SndHeartBeep);
    }

    internal void SynchronizeRupees()
    {
        _updateTicks = 0.0;
        WriteDisplayedMoney(TargetMoney);
        _hud.Refresh();
    }

    private int TargetMoney => _currency.MoneyValue(_inventory, _currency.SelectorAddress < 0
        ? _currency.RupeeSelector : _memory.ReadWramByte(_currency.SelectorAddress));

    private void WriteDisplayedMoney(int value)
    {
        if (value is < 0 or > 999) throw new ArgumentOutOfRangeException(nameof(value));
        _memory.SetField("wDisplayedRupees", (byte)(value % 10 | value / 10 % 10 << 4));
        _memory.SetField("wDisplayedRupees", (byte)(value / 100), 1);
        _hud.Rupees = value;
    }

    internal void LoadCommonGraphics(byte tilesetFlags)
    {
        if (_currency.SelectorAddress >= 0)
        {
            int selected = _currency.SelectMoney(tilesetFlags);
            int previous = _memory.ReadWramByte(_currency.SelectorAddress);
            _memory.SetWramByte(_currency.SelectorAddress, (byte)selected);
            if (previous != selected) WriteDisplayedMoney(TargetMoney);
        }
        _hud.LoadCommonGraphics(tilesetFlags);
        Update(ApplicationFixedUpdateScheduler.UpdateDelta);
    }

    internal void RestoreDisplay()
    {
        _hud.Rupees = DisplayedRupees;
        _hud.HealthQuarters = DisplayedHealth;
        _hud.LoadCommonGraphics(_hud.TilesetFlags);
    }

    internal void SynchronizeHealth()
    {
        _memory.SetField("wDisplayedHearts",(byte)_inventory.HealthQuarters);
        _hud.HealthQuarters = _inventory.HealthQuarters;
        _hud.MaxHealthQuarters = _inventory.MaxHealthQuarters;
        _hud.Refresh();
    }

    private bool UpdateDisplayedHealth()
    {
        int displayed = DisplayedHealth;
        int target = _inventory.HealthQuarters;
        if (displayed > target) displayed--;
        else if (displayed < target && (_frameCounter & 3) == 0)
        {
            displayed++;
            if ((displayed & 3) == 0) PlayGainHeartSound();
        }
        _memory.SetField("wDisplayedHearts",(byte)displayed);
        bool changed = _hud.HealthQuarters != displayed;
        _hud.HealthQuarters = displayed;
        return changed;
    }

    private void PlayGainHeartSound() => _playSound(SoundId.SndGainHeart);
    private void PlayRupeeSound() => _playSound(SoundId.SndRupee);
}
