using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Ports bank 2's MENU_RING_APPRAISAL / MENU_RING_LIST state machines. Vasu
/// owns the surrounding script; this controller owns the modal lifecycle,
/// ring transactions, text states, cursor/page motion, and the callback made
/// when closeMenu has completed.
/// </summary>
internal sealed class RingMenuController : IOracleMenuLifecycleClient
{

    private readonly RingMenuScreen _screen;
    private readonly DialogueBox _dialogue;
    private readonly OracleMenuLifecycle _lifecycle;
    private readonly InventoryState _inventory;
    private readonly OracleSaveData _save;
    private readonly TreasureDatabase _treasures;
    private readonly VasuShopDatabase _database;
    private readonly Action<int> _playSound;
    private readonly FixedUpdateAccumulator _updates = new();
    private RingMenuMode _mode;
    private AppraisalState _appraisalState;
    private Action? _completion;
    private int _appraisalIndex;
    private int _appraisedRing;
    private int _pendingRefund;
    private int _delay;
    private bool _pageTransitionStartPending;
    private bool _listResumePending;
    private int _ringNameTextIndex;
    private int _ringDescriptionTextIndex;
    private int _ringTextDelay;

    string IOracleMenuLifecycleClient.MenuName => _mode == RingMenuMode.Appraisal
        ? "MENU_RING_APPRAISAL"
        : "MENU_RING_LIST";

    internal bool IsActive => _lifecycle.IsOwnedBy(this);

    internal RingMenuController(
        RingMenuScreen screen,
        DialogueBox dialogue,
        OracleMenuLifecycle lifecycle,
        InventoryState inventory,
        OracleSaveData save,
        TreasureDatabase treasures,
        VasuShopDatabase database,
        Action<int> playSound)
    {
        _screen = screen;
        _dialogue = dialogue;
        _lifecycle = lifecycle;
        _inventory = inventory;
        _save = save;
        _treasures = treasures;
        _database = database;
        _playSound = playSound;
    }

    internal bool Open(RingMenuMode mode, Action completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        if (!_lifecycle.TryBeginOpening(this))
            return false;
        _mode = mode;
        _completion = completion;
        _updates.Reset();
        _delay = 0;
        _pendingRefund = 0;
        _pageTransitionStartPending = false;
        _listResumePending = false;
        return true;
    }

    internal void Update(double delta)
    {
        if (!IsActive)
            return;
        if (!_lifecycle.IsOpenFor(this))
        {
            _lifecycle.Update(this, delta);
            // The palette thread precedes bank2.runRingMenu. Its state 1
            // dispatch runs on the update that clears the opening fade.
            if (!_lifecycle.IsOpenFor(this)) return;
        }

        _screen.BeginSpriteUpdate(_screen.PageTransitionActive);
        if (_screen.PageTransitionActive)
        {
            if (_pageTransitionStartPending)
            {
                _pageTransitionStartPending = false;
                bool replacedDescription = _screen.InitializePageTransition();
                _playSound(SoundId.SndOpenMenu);
                if (replacedDescription) _dialogue.Close();
                if (_mode == RingMenuMode.List)
                {
                    _ringNameTextIndex = 0;
                    _screen.SetRingName(null);
                }
                return;
            }
            _screen.AdvanceAnimation(delta, out bool completed);
            if (completed && _mode == RingMenuMode.List)
            {
                // ringMenu_setState clears wSubmenuState. The following
                // ringMenu_ringList_substate0 consumes an update restoring
                // the list because its box flicker counter is still zero.
                _screen.SetSelectingList(false, resetBoxFlicker: false);
                _listResumePending = true;
            }
            return;
        }
        if (_listResumePending)
        {
            _listResumePending = false;
            _screen.SetSelectingList(true);
            _screen.SetRingNumberComparator(0x80);
            _ringDescriptionTextIndex = 0xff;
            return;
        }
        _screen.AdvanceAnimation(delta, out _);

        if (_mode == RingMenuMode.Appraisal)
        {
            _screen.SubmitListSprites();
            UpdateAppraisal(delta);
        }
        else
            UpdateList();
    }

    private void UpdateAppraisal(double delta)
    {
        switch (_appraisalState)
        {
            case AppraisalState.Browse:
                UpdateAppraisalBrowse();
                break;
            case AppraisalState.Confirm:
                UpdateAppraisalConfirmation();
                break;
            case AppraisalState.RingName:
                if (!_dialogue.IsOpen)
                {
                    ShowRingDescription(_appraisedRing, exitable: true);
                    _appraisalState = AppraisalState.RingDescription;
                }
                break;
            case AppraisalState.RingDescription:
                if (!_dialogue.IsOpen)
                    FinishAppraisalDescription();
                break;
            case AppraisalState.ResultDelay:
                if (TickDelayAfterText(delta))
                    FinishAppraisalDelay();
                break;
            case AppraisalState.ExitDelay:
                if (TickDelayAfterText(delta))
                    BeginClosing();
                break;
        }
    }

    private void UpdateAppraisalBrowse()
    {
        if (!_dialogue.IsOpen)
            ShowPassive(_database.Text(0x3004), 2);
        if (Input.IsActionJustPressed("item"))
        {
            if (HasObtainedRingBox())
                BeginClosing();
            else
                ShowPassive(_database.Text(0x3012), 2);
            return;
        }
        if (Input.IsActionJustPressed("attack"))
        {
            int index = _screen.Page * 16 + _screen.ListCursor;
            if (_inventory.UnappraisedRingAt(index) == 0xff)
                return;
            _appraisalIndex = index;
            _dialogue.Close();
            _dialogue.ShowChoiceMessage(
                _database.Text(HasObtainedRingBox() ? 0x3005 : 0x3011),
                DialogueScreenContext.FullScreen(0), textPosition: 2, textboxFlags: 0x0b);
            PositionAppraisalDialogue();
            _appraisalState = AppraisalState.Confirm;
            return;
        }
        if (Input.IsActionJustPressed("map"))
        {
            ScrollPage(1);
            return;
        }
        HandleListDirection();
    }

    private void UpdateAppraisalConfirmation()
    {
        if (!_dialogue.PrintingComplete)
            return;
        if (!_dialogue.TryTakeChoiceResult(out int choice))
            throw new InvalidOperationException(
                "Ring appraisal confirmation closed without an option result.");
        if (choice != 0)
        {
            EnterAppraisalBrowse(retainPresentation: true);
            return;
        }

        int cost = HasObtainedRingBox() ? _database.AppraisalCost : 0;
        if (!_inventory.TryBeginRingAppraisal(
            _appraisalIndex, cost, out _appraisedRing))
        {
            ShowPassive(_database.Text(0x3006), 2);
            BeginDelay(_database.MenuExitWait, AppraisalState.ExitDelay);
            return;
        }

        string name = RingName(_appraisedRing);
        string reveal = _database.Text(0x301c).Replace("\\call(0xfd)", name,
            StringComparison.Ordinal);
        _dialogue.ShowMessage(reveal, DialogueScreenContext.FullScreen(0), 2, textboxFlags: 0x09);
        PositionAppraisalDialogue();
        _appraisalState = AppraisalState.RingName;
        _screen.RefreshAppraisalGraphics();
    }

    private void FinishAppraisalDescription()
    {
        RingAppraisalResult result =
            _inventory.CompleteRingAppraisal(
                _appraisalIndex, _database.DuplicateRefund);
        _pendingRefund = result.Refund;
        if (!HasObtainedRingBox())
        {
            BeginClosing();
            return;
        }

        ShowPassive(_database.Text(result.Duplicate ? 0x3007 : 0x3017), 2);
        BeginDelay(_database.AppraisalResultWait, AppraisalState.ResultDelay);
    }

    private void FinishAppraisalDelay()
    {
        if (_pendingRefund > 0)
        {
            _inventory.ApplyRingAppraisalRefund(_pendingRefund);
            _pendingRefund = 0;
        }
        _inventory.RefreshUnappraisedRingCount();
        bool replacedDescription = _screen.RefreshAppraisalGraphics();
        if (_inventory.RingsAppraised == 100)
        {
            _save.SetGlobalFlag(_database.GlobalAppraisedHundredth);
            ShowPassive(_database.Text(0x303c), 2);
            BeginDelay(_database.MenuExitWait, AppraisalState.ExitDelay);
            return;
        }
        if (_inventory.UnappraisedRingCount > 0)
        {
            EnterAppraisalBrowse(retainPresentation: !replacedDescription);
            return;
        }
        ShowPassive(_database.Text(0x3002), 2);
        BeginDelay(_database.MenuExitWait, AppraisalState.ExitDelay);
    }

    private void EnterAppraisalBrowse(bool showText = false, bool retainPresentation = false)
    {
        _appraisalState = AppraisalState.Browse;
        _delay = 0;
        _updates.Reset();
        if (retainPresentation) _dialogue.CloseRetainingPresentation();
        else _dialogue.Close();
        // ringMenu_state1_restart clears text and returns. Its next Browse
        // dispatch requests TX_3004; it cannot print on the restart update.
        if (showText) ShowPassive(_database.Text(0x3004), 2);
    }

    private bool TickDelayAfterText(double delta)
    {
        if (!_dialogue.PrintingComplete)
            return false;
        int updates = _updates.Consume(delta);
        for (int update = 0; update < updates; update++)
        {
            if (_delay == 0)
                return true;
            _delay--;
        }
        return false;
    }

    private void BeginDelay(int updates, AppraisalState state)
    {
        _delay = updates;
        _updates.Reset();
        _appraisalState = state;
    }

    private void UpdateList()
    {
        if (!_screen.SelectingList)
        {
            // Box text is dispatched before input. A name replacement starts
            // inventory text and a one-update delay; its later description
            // uses the standard non-exitable thread (bank2.updateRingText).
            if (_ringTextDelay != 0) _ringTextDelay--;
            else
            {
                int ring = _inventory.RingAt(_screen.BoxCursor);
                bool replacedDescription = _screen.UpdateDisplayedRingNumber(ring, ring);
                UpdateRingText(ring, retainDescription: !replacedDescription);
            }
            if (Input.IsActionJustPressed("item"))
            {
                _inventory.DeactivateRingIfMissingFromBox();
                BeginClosing();
                return;
            }
            if (Input.IsActionJustPressed("attack"))
            {
                _screen.SetSelectingList(true);
                _screen.SetRingNumberComparator(0x80);
                _ringDescriptionTextIndex = 0xff;
                return;
            }
            int directions = _lifecycle.DirectionInputWithAutofire();
            if ((directions & 1) != 0)
                MoveBoxCursor(1);
            else if ((directions & 2) != 0)
                MoveBoxCursor(-1);
            return;
        }

        if (Input.IsActionJustPressed("attack"))
        {
            int ring = _screen.Page * 16 + _screen.ListCursor;
            if (!_inventory.HasAppraisedRing(ring))
                ring = 0xff;
            _inventory.SetRingBoxSlotFromList(_screen.BoxCursor, ring);
            _playSound(SoundId.SndSelectItem);
            ReturnToBox();
            return;
        }
        if (Input.IsActionJustPressed("item"))
        {
            ReturnToBox();
            return;
        }
        if (Input.IsActionJustPressed("map"))
        {
            _screen.SetRingNumberComparator(1);
            ScrollPage(1);
            return;
        }
        HandleListDirection();
        bool replacedListDescription = _screen.UpdateDisplayedRingNumber(_screen.SelectedListRing & 0x0f, _screen.SelectedListRing);
        _screen.SubmitListSprites();
        UpdateRingText(_screen.SelectedListRing, retainDescription: !replacedListDescription);
    }

    private void HandleListDirection()
    {
        int directions = _lifecycle.DirectionInputWithAutofire();
        Vector2I direction;
        if ((directions & 1) != 0)
            direction = Vector2I.Right;
        else if ((directions & 2) != 0)
            direction = Vector2I.Left;
        else if ((directions & 4) != 0)
            direction = Vector2I.Up;
        else if ((directions & 8) != 0)
            direction = Vector2I.Down;
        else
            return;
        MoveListCursor(direction);
    }

    private void MoveListCursor(Vector2I direction)
    {
        int offset = direction switch
        {
            { X: 1 } => 1,
            { X: -1 } => -1,
            { Y: -1 } => -8,
            { Y: 1 } => 8,
            _ => 0
        };
        int before = _screen.ListCursor;
        int raw = before + offset;
        int cursor = raw & 0x0f;
        if (direction.X != 0 && (raw < 0 || raw >= 16))
            ScrollPage(direction.X, cursor);
        else
            _screen.SetPageAndCursor(_screen.Page, cursor);
        _playSound(SoundId.SndMenuMove);
    }

    private void ScrollPage(int direction, int cursor = 0)
    {
        if (_screen.PageCount <= 1)
            return;
        int page = (_screen.Page + direction + _screen.PageCount) % _screen.PageCount;
        if (!_screen.BeginPageTransition(page, cursor, direction))
            return;
        _pageTransitionStartPending = true;
        _ringDescriptionTextIndex = 0xff;
        _dialogue.CloseRetainingPresentation();
    }

    private void MoveBoxCursor(int delta)
    {
        int next = _screen.BoxCursor + delta;
        if (next < 0 || next >= _inventory.RingBoxCapacity)
            return;
        _screen.SetBoxCursor(next);
        _playSound(SoundId.SndMenuMove);
    }

    private void ReturnToBox()
    {
        _screen.SetSelectingList(false);
        _ringNameTextIndex = _ringDescriptionTextIndex = 0xff;
        _dialogue.RequestClosing();
    }

    private void UpdateRingText(int ring, bool retainDescription)
    {
        bool owned = ring != 0xff && _inventory.HasAppraisedRing(ring);
        // checkFlag leaves A=$00 for an unowned ring. That requests the
        // blank inventory name, not a treasure text indexed by the ring ID.
        int nameIndex = owned ? ring | 0x80 : 0;
        if (nameIndex != _ringNameTextIndex)
        {
            _ringNameTextIndex = nameIndex;
            _screen.SetRingName(owned ? RingName(ring) : null);
            if (retainDescription) _dialogue.CloseRetainingPresentation();
            else _dialogue.Close();
            _ringTextDelay = 1;
            return;
        }
        int descriptionIndex = owned ? 0x80 + ring : 0xc0;
        if (descriptionIndex == _ringDescriptionTextIndex) return;
        _ringDescriptionTextIndex = descriptionIndex;
        if (owned) ShowRingDescription(ring, exitable: false);
        else ShowPassive(string.Empty, 4);
    }

    private string RingName(int ring)
    {
        string message = DialogueBox.PlainText(_treasures.GetRingText(ring).Message)
            .Replace("\r", string.Empty, StringComparison.Ordinal);
        int newline = message.IndexOf('\n');
        return newline < 0 ? message : message[..newline];
    }

    private void ShowRingDescription(int ring, bool exitable)
    {
        string message = _treasures.GetRingText(ring).Message.Replace(
            "\r", string.Empty, StringComparison.Ordinal);
        int newline = message.IndexOf('\n');
        string description = newline < 0 ? string.Empty : message[(newline + 1)..];
        if (exitable)
        {
            _dialogue.ShowMessage(description, DialogueScreenContext.FullScreen(0), 2, textboxFlags: 0x09);
            PositionAppraisalDialogue();
        }
        else
            ShowPassive(description, 4);
    }

    private void ShowPassive(string message, int position)
    {
        _dialogue.ShowPassiveMessage(message, 0, position);
        if (_mode == RingMenuMode.Appraisal)
            PositionAppraisalDialogue();
    }

    private void PositionAppraisalDialogue()
    {
        // Appraisal graphics state $0f keeps the status bar at y=$00-$0f,
        // starts w4TileMap at y=$10, and displays textbox position $02's
        // source row 10 at screen y=$60. The generic textbox mapping assumes
        // the ordinary gameplay scanout and therefore needs this menu offset.
        _dialogue.Position = new Vector2(0, 96);
    }

    private bool HasObtainedRingBox() =>
        _save.HasGlobalFlag(_database.GlobalObtainedRingBox);

    private void BeginClosing()
    {
        _dialogue.CloseRetainingPresentation();
        _playSound(SoundId.SndCloseMenu);
        _lifecycle.BeginClosing(this);
    }

    void IOracleMenuLifecycleClient.OpenAtWhite()
    {
        // bank2.menuStateFadeIntoMenu@openMenu requests SND_OPENMENU $54
        // before dispatching either ring menu's graphics initialization.
        _playSound(SoundId.SndOpenMenu);
        if (_mode == RingMenuMode.Appraisal) _inventory.PrepareRingAppraisal();
        _screen.Open(_mode);
        _listResumePending = false;
        if (_mode == RingMenuMode.Appraisal)
            // State 0 only clears the name strip. The first eligible state-1
            // Browse dispatch opens TX_3004 after the opening fade.
            EnterAppraisalBrowse(showText: false);
        else
        {
            _screen.SetSelectingList(false);
            _ringNameTextIndex = _ringDescriptionTextIndex = _ringTextDelay = 0;
            _screen.SetRingName(null);
            _dialogue.Close();
        }
    }

    void IOracleMenuLifecycleClient.CloseAtWhite()
    {
        _dialogue.Close();
        _screen.Close();
        _pageTransitionStartPending = false;
        _listResumePending = false;
    }

    void IOracleMenuLifecycleClient.LifecycleClosed()
    {
        Action? completion = _completion;
        _completion = null;
        completion?.Invoke();
    }

    internal void OpenImmediatelyForValidation(RingMenuMode mode, Action completion)
    {
        _mode = mode;
        _completion = completion;
        _lifecycle.OpenImmediately(this);
    }

    internal void CloseImmediatelyForValidation()
    {
        if (IsActive)
            _lifecycle.CloseImmediately(this);
    }
}

internal enum AppraisalState
{
    Browse,
    Confirm,
    RingName,
    RingDescription,
    ResultDelay,
    ExitDelay
}
