using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public partial class DebugObjectSpawnerScreen : Control
{
    private DebugObjectCatalog _catalog = null!;
    private Label _text = null!;
    private Label _status = null!;
    private TextureRect _preview = null!;
    private Label _noPreview = null!;
    private DebugObjectPreview _previews = null!;
    private int _field;
    private int _enemyIndex;
    private int _dropIndex;
    private int _width;
    private int _height;
    internal bool IsDrop { get; private set; }
    internal Vector2 SpawnPosition { get; private set; }
    private IReadOnlyList<DebugObjectEntry> Entries => IsDrop ? _catalog.Drops : _catalog.Enemies;
    private int Index => IsDrop ? _dropIndex : _enemyIndex;
    internal DebugObjectEntry Selection => Entries[Index];
    internal string Status => _status.Text;

    public override void _Ready()
    {
        _text = GetNode<Label>("Text");
        _status = GetNode<Label>("Status");
        _preview = GetNode<TextureRect>("Preview");
        _noPreview = GetNode<Label>("NoPreview");
    }

    internal void Initialize(EnemyDatabase enemies)
    {
        _catalog = new DebugObjectCatalog(enemies);
        _previews = new DebugObjectPreview(enemies);
    }

    internal void Open(OracleRoomData room, Vector2 position)
    {
        _width = room.Width;
        _height = room.Height;
        SpawnPosition = new Vector2(
            Math.Clamp((int)position.X, 0, _width - 1),
            Math.Clamp((int)position.Y, 0, _height - 1));
        _status.Text = "";
        Refresh();
        Visible = true;
    }

    internal void Close() => Visible = false;

    internal void MoveVertical(int direction)
    {
        _field = (_field + Math.Sign(direction) + 4) % 4;
        Refresh();
    }

    internal void MoveHorizontal(int direction)
    {
        int step = Math.Sign(direction);
        switch (_field)
        {
            case 0: IsDrop = !IsDrop; break;
            case 1:
                MoveObject(step);
                break;
            case 2:
                SpawnPosition = new Vector2(Math.Clamp(SpawnPosition.X + step * 8, 0, _width - 1), SpawnPosition.Y);
                break;
            case 3:
                SpawnPosition = new Vector2(SpawnPosition.X, Math.Clamp(SpawnPosition.Y + step * 8, 0, _height - 1));
                break;
        }
        _status.Text = "";
        Refresh();
    }

    internal void ShowResult(bool success, string message)
    {
        _status.Text = message;
        _status.Modulate = success ? Color.Color8(150, 255, 160) : Color.Color8(255, 180, 140);
    }

    internal void MoveObject(int step)
    {
        int index = ((Index + step) % Entries.Count + Entries.Count) % Entries.Count;
        if (IsDrop) _dropIndex = index;
        else _enemyIndex = index;
        _status.Text = "";
        Refresh();
    }

    private void Refresh()
    {
        string Cursor(int field) => field == _field ? ">" : " ";
        DebugObjectEntry entry = Selection;
        _preview.Texture = _previews.GetTexture(entry, IsDrop);
        _noPreview.Visible = _preview.Texture is null;
        _text.Text = "OBJECT SPAWNER                 F4\n" +
            "Up/down: field   Left/right: edit\n" +
            "A/Z: spawn   B/X or F4: close\n\n" +
            $"{Cursor(0)} Type: {(IsDrop ? "ITEM DROP" : "ENEMY")}\n" +
            $"{Cursor(1)} Object: ${entry.Id:x2}:${entry.SubId:x2}  {Index + 1}/{Entries.Count}\n" +
            $"  {entry.Name}\n" +
            $"  {(entry.Supported ? "" : "No standalone spawn handler")}\n" +
            $"{Cursor(2)} Room X: ${(int)SpawnPosition.X:x2}\n" +
            $"{Cursor(3)} Room Y: ${(int)SpawnPosition.Y:x2}";
    }
}
