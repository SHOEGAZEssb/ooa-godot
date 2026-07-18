using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace oracleofages;

public partial class DebugFlagScreen : Control
{
    public enum FlagPage { Global, Room, Inventory }

    private const int GlobalVisibleRows = 12;
    private const int InventoryVisibleRows = 11;
    private static readonly int[] RoomTableGroups = { 0, 1, 4, 5 };
    private static readonly string[] RoomBitNames =
    {
        "LAYOUTSWAP / KEYDOOR_UP",
        "KEYDOOR_RIGHT",
        "KEYDOOR_DOWN",
        "PORTALSPOT / KEYDOOR_LEFT",
        "VISITED",
        "ITEM / CHEST",
        "GENERIC_40",
        "GENERIC_80 / KEYBLOCK"
    };

    private OracleSaveData _saveData = null!;
    private GlobalFlagDatabase _globalFlags = null!;
    private InventoryState _inventory = null!;
    private readonly List<TreasureDatabase.TreasureObjectRecord> _treasureObjects = new();
    private Label _text = null!;
    private int _globalCursor;
    private int _roomTable;
    private int _room;
    private int _roomCursor = 2;
    private int _inventoryCursor;

    public FlagPage Page { get; private set; }
    public int GlobalCursor => _globalCursor;
    public int SelectedRoomGroup => RoomTableGroups[_roomTable];
    public int SelectedRoom => _room;
    public int RoomCursor => _roomCursor;
    internal string RenderedText => _text?.Text ?? string.Empty;

    public override void _Ready()
    {
        Size = new Vector2(OracleRoomData.ViewportWidth, OracleRoomData.ViewportHeight);
        MouseFilter = MouseFilterEnum.Ignore;

        var background = new ColorRect
        {
            Size = Size,
            Color = Color.Color8(7, 16, 25, 248),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(background);

        _text = new Label
        {
            Position = new Vector2(3, 2),
            Size = new Vector2(154, 140),
            ClipText = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _text.AddThemeFontSizeOverride("font_size", 7);
        _text.AddThemeColorOverride("font_color", Color.Color8(224, 240, 232));
        _text.AddThemeColorOverride("font_outline_color", Color.Color8(0, 0, 0));
        _text.AddThemeConstantOverride("outline_size", 1);
        _text.AddThemeConstantOverride("line_spacing", -1);
        AddChild(_text);
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey key || !key.Pressed || key.Echo)
            return;
        if (key.Keycode != Key.Tab && key.PhysicalKeycode != Key.Tab && key.Unicode != '\t')
            return;

        TogglePage();
        GetViewport().SetInputAsHandled();
    }

    public void Initialize(
        OracleSaveData saveData,
        GlobalFlagDatabase globalFlags,
        TreasureDatabase treasures,
        InventoryState inventory)
    {
        _saveData = saveData;
        _globalFlags = globalFlags;
        _inventory = inventory;
        _treasureObjects.Clear();
        foreach (TreasureDatabase.TreasureObjectRecord record in treasures.Objects)
        {
            if (record.TreasureId != TreasureDatabase.TreasureNone)
                _treasureObjects.Add(record);
        }
        _inventoryCursor = Math.Clamp(_inventoryCursor, 0, _treasureObjects.Count);
    }

    public void Open(int activeGroup, int activeRoom)
    {
        _roomTable = Array.IndexOf(RoomTableGroups, CanonicalRoomFlagGroup(activeGroup));
        if (_roomTable < 0)
            _roomTable = 0;
        _room = activeRoom & 0xff;
        Refresh();
        Visible = true;
    }

    public void Close() => Visible = false;

    public void TogglePage()
    {
        Page = Page switch
        {
            FlagPage.Global => FlagPage.Room,
            FlagPage.Room => FlagPage.Inventory,
            _ => FlagPage.Global
        };
        Refresh();
    }

    public void MoveVertical(int direction)
    {
        int step = Math.Sign(direction);
        switch (Page)
        {
            case FlagPage.Global:
                _globalCursor = Math.Clamp(_globalCursor + step, 0,
                    OracleSaveData.GlobalFlagCount - 1);
                break;
            case FlagPage.Room:
                _roomCursor = Math.Clamp(_roomCursor + step, 0, 9);
                break;
            case FlagPage.Inventory:
                _inventoryCursor = Math.Clamp(
                    _inventoryCursor + step, 0, _treasureObjects.Count);
                break;
        }
        Refresh();
    }

    public void MoveHorizontal(int direction)
    {
        int step = Math.Sign(direction);
        if (Page == FlagPage.Global)
        {
            _globalCursor = Math.Clamp(_globalCursor + step * GlobalVisibleRows, 0,
                OracleSaveData.GlobalFlagCount - 1);
        }
        else if (Page == FlagPage.Inventory)
        {
            _inventoryCursor = Math.Clamp(
                _inventoryCursor + step * InventoryVisibleRows,
                0,
                _treasureObjects.Count);
        }
        else if (_roomCursor == 0)
        {
            _roomTable = (_roomTable + step + RoomTableGroups.Length) % RoomTableGroups.Length;
        }
        else
        {
            _room = (_room + step + OracleSaveData.RoomsPerFlagTable) %
                OracleSaveData.RoomsPerFlagTable;
        }
        Refresh();
    }

    public void ActivateSelection()
    {
        switch (Page)
        {
            case FlagPage.Global:
                _saveData.SetGlobalFlag(
                    _globalCursor, !_saveData.HasGlobalFlag(_globalCursor));
                break;
            case FlagPage.Room when _roomCursor >= 2:
            {
                byte mask = (byte)(1 << (_roomCursor - 2));
                _saveData.SetRoomFlag(
                    SelectedRoomGroup, _room, mask,
                    !_saveData.HasRoomFlag(SelectedRoomGroup, _room, mask));
                break;
            }
            case FlagPage.Inventory when _inventoryCursor == 0:
                _saveData.SetLinkedGame(!_saveData.IsLinkedGame);
                break;
            case FlagPage.Inventory:
                _inventory.GiveTreasure(_treasureObjects[_inventoryCursor - 1]);
                break;
        }
        Refresh();
    }

    internal void SelectGlobalFlagForValidation(int flag)
    {
        _globalCursor = Math.Clamp(flag, 0, OracleSaveData.GlobalFlagCount - 1);
        Page = FlagPage.Global;
        Refresh();
    }

    internal void SelectRoomFlagForValidation(int group, int room, int bit)
    {
        _roomTable = Array.IndexOf(RoomTableGroups, CanonicalRoomFlagGroup(group));
        _room = room & 0xff;
        _roomCursor = Math.Clamp(bit, 0, 7) + 2;
        Page = FlagPage.Room;
        Refresh();
    }

    internal void SelectLinkedGameForValidation()
    {
        _inventoryCursor = 0;
        Page = FlagPage.Inventory;
        Refresh();
    }

    internal void SelectTreasureForValidation(string name)
    {
        for (int index = 0; index < _treasureObjects.Count; index++)
        {
            if (_treasureObjects[index].Name != name)
                continue;
            _inventoryCursor = index + 1;
            Page = FlagPage.Inventory;
            Refresh();
            return;
        }

        throw new KeyNotFoundException($"Debug treasure {name} was not imported.");
    }

    private void Refresh()
    {
        if (_text is null || _saveData is null || _globalFlags is null)
            return;
        _text.Text = Page switch
        {
            FlagPage.Global => BuildGlobalText(),
            FlagPage.Room => BuildRoomText(),
            _ => BuildInventoryText()
        };
    }

    private string BuildGlobalText()
    {
        var text = new StringBuilder();
        text.Append("GLOBAL FLAGS  $").Append(_globalCursor.ToString("x2"))
            .Append('/').Append((OracleSaveData.GlobalFlagCount - 1).ToString("x2")).Append('\n');
        text.Append(TrimName(_globalFlags.GetName(_globalCursor), 34)).Append('\n');

        int first = Math.Clamp(_globalCursor - GlobalVisibleRows / 2, 0,
            OracleSaveData.GlobalFlagCount - GlobalVisibleRows);
        for (int flag = first; flag < first + GlobalVisibleRows; flag++)
        {
            text.Append(flag == _globalCursor ? '>' : ' ')
                .Append('$').Append(flag.ToString("x2"))
                .Append(_saveData.HasGlobalFlag(flag) ? " [1] " : " [0] ")
                .Append(TrimName(_globalFlags.GetShortName(flag), 27)).Append('\n');
        }
        text.Append("F1 CLOSE  TAB ROOM  A TOGGLE");
        return text.ToString();
    }

    private string BuildRoomText()
    {
        int group = SelectedRoomGroup;
        byte flags = _saveData.GetRoomFlags(group, _room);
        var text = new StringBuilder();
        text.Append("ROOM FLAGS  $").Append(group.ToString("x1")).Append(':')
            .Append(_room.ToString("x2")).Append(" = $").Append(flags.ToString("x2")).Append('\n');
        text.Append("TABLES ALIAS 0/2 1/3 4/6 5/7").Append('\n');
        text.Append(_roomCursor == 0 ? '>' : ' ').Append(" TABLE: ")
            .Append(RoomTableName(_roomTable)).Append('\n');
        text.Append(_roomCursor == 1 ? '>' : ' ').Append(" ROOM:  $")
            .Append(_room.ToString("x2")).Append('\n');
        for (int bit = 0; bit < 8; bit++)
        {
            byte mask = (byte)(1 << bit);
            text.Append(_roomCursor == bit + 2 ? '>' : ' ')
                .Append(" BIT ").Append(bit)
                .Append((flags & mask) != 0 ? " [1] " : " [0] ")
                .Append(RoomBitNames[bit]).Append('\n');
        }
        text.Append("LEFT/RIGHT TABLE OR ROOM").Append('\n');
        text.Append("F1 CLOSE TAB ITEMS A TOGGLE");
        return text.ToString();
    }

    private string BuildInventoryText()
    {
        var text = new StringBuilder();
        text.Append("LINKED / ITEMS  ").Append(_inventoryCursor.ToString("000"))
            .Append('/').Append(_treasureObjects.Count.ToString("000")).Append('\n');
        if (_inventoryCursor == 0)
        {
            text.Append("wIsLinkedGame $c612 = $")
                .Append(_saveData.IsLinkedGame ? "01" : "00").Append('\n');
        }
        else
        {
            TreasureDatabase.TreasureObjectRecord selected =
                _treasureObjects[_inventoryCursor - 1];
            text.Append("ID $").Append(selected.TreasureId.ToString("x2"))
                .Append(" SUB $").Append(selected.SubId.ToString("x2"))
                .Append(" PARAM $").Append(selected.Parameter.ToString("x2")).Append('\n');
        }

        int final = _treasureObjects.Count;
        int first = Math.Clamp(
            _inventoryCursor - InventoryVisibleRows / 2,
            0,
            Math.Max(0, final - InventoryVisibleRows + 1));
        int end = Math.Min(final + 1, first + InventoryVisibleRows);
        for (int cursor = first; cursor < end; cursor++)
        {
            text.Append(cursor == _inventoryCursor ? '>' : ' ');
            if (cursor == 0)
            {
                text.Append("LINKED GAME ")
                    .Append(_saveData.IsLinkedGame ? "[1]" : "[0]").Append('\n');
                continue;
            }

            TreasureDatabase.TreasureObjectRecord record = _treasureObjects[cursor - 1];
            text.Append('$').Append(record.TreasureId.ToString("x2"))
                .Append(_inventory.HasTreasure(record.TreasureId) ? " [1] " : " [0] ")
                .Append(TrimName(ShortTreasureName(record.Name), 25)).Append('\n');
        }
        text.Append("LEFT/RIGHT PAGE  A ")
            .Append(_inventoryCursor == 0 ? "TOGGLE" : "GIVE").Append('\n');
        text.Append("F1 CLOSE  TAB GLOBAL FLAGS");
        return text.ToString();
    }

    private static int CanonicalRoomFlagGroup(int group) => group switch
    {
        0 or 2 => 0,
        1 or 3 => 1,
        4 or 6 => 4,
        5 or 7 => 5,
        _ => 0
    };

    private static string RoomTableName(int table) => table switch
    {
        0 => "$0 / $2 TABLE",
        1 => "$1 / $3 TABLE",
        2 => "$4 / $6 TABLE",
        3 => "$5 / $7 TABLE",
        _ => "?"
    };

    private static string TrimName(string name, int maximum) =>
        name.Length <= maximum ? name : name[..(maximum - 1)] + "~";

    private static string ShortTreasureName(string name)
    {
        const string prefix = "TREASURE_OBJECT_";
        return name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : name;
    }
}
