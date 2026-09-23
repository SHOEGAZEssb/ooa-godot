using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC $21:$16: trigger-controlled pattern display.</summary>
internal sealed partial class DungeonPatternHintRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly IReadOnlyList<DungeonPatternHintTile> _tiles;
    private readonly Func<int> _triggers;
    private readonly Action<byte,byte> _setTile;
    private readonly Func<Vector2,bool> _tryPuff;
    public Node2D Node => this;
    internal bool Showing { get; private set; }
    public bool UpdatesDuringDialogue => !Showing;

    internal DungeonPatternHintRoomEntity(IReadOnlyList<DungeonPatternHintTile> tiles,
        Func<int> triggers,Action<byte,byte> setTile,Func<Vector2,bool> tryPuff)
    {
        _tiles = tiles; _triggers = triggers; _setTile = setTile; _tryPuff = tryPuff;
        Name = "DungeonPatternHint"; Visible = false;
    }

    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!Showing) Advance();
        return ScreenTransitionPresentation.Hidden;
    }

    private void Advance()
    {
        bool active = _triggers() != 0;
        if (active == Showing) return;
        if (active) Showing = true; // Source increments state before show writes.
        foreach (var tile in _tiles)
        {
            _setTile(tile.Position,active ? tile.ShowTile : tile.RestoreTile);
            // Both setTile and puff allocation may fail independently. The
            // source continues to the next cell regardless of either result.
            _tryPuff(new((tile.Position & 15) * 16 + 8,(tile.Position >> 4) * 16 + 8));
        }
        if (!active) Showing = false; // Restoration clears state after writes.
    }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
