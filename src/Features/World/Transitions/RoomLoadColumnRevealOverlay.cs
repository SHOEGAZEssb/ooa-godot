using Godot;

namespace oracleofages;

/// <summary>
/// Screen-space copy of the still-cleared BG-map columns. The original map
/// carries attribute $80, so these pixels have priority over room objects
/// while the destination columns are copied into VRAM.
/// </summary>
public partial class RoomLoadColumnRevealOverlay : Control
{
    private Texture2D? _clearedTilemap;
    private int _loadedColumns;

    internal bool Active => _clearedTilemap is not null;
    internal int LoadedColumns => _loadedColumns;
    internal Texture2D? ClearedTilemap => _clearedTilemap;

    public void SetReveal(Texture2D clearedTilemap, int loadedColumns)
    {
        _clearedTilemap = clearedTilemap;
        _loadedColumns = Mathf.Clamp(
            loadedColumns,
            0,
            RoomTransitionController.RoomLoadRevealColumnUpdates);
        Visible = true;
        QueueRedraw();
    }

    public void SetLoadedColumns(int loadedColumns)
    {
        if (_clearedTilemap is null)
            return;
        _loadedColumns = Mathf.Clamp(
            loadedColumns,
            0,
            RoomTransitionController.RoomLoadRevealColumnUpdates);
        QueueRedraw();
    }

    public void ClearReveal()
    {
        _clearedTilemap = null;
        _loadedColumns = 0;
        Visible = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_clearedTilemap is null)
            return;

        (int left, int right) =
            RoomTransitionController.RoomLoadRevealBounds(_loadedColumns);
        int height = Mathf.Min(
            Mathf.RoundToInt(Size.Y),
            _clearedTilemap.GetHeight());
        if (left > 0)
        {
            var region = new Rect2(0, 0, left, height);
            DrawTextureRectRegion(_clearedTilemap, region, region);
        }
        if (right < OracleRoomData.ViewportWidth)
        {
            var region = new Rect2(
                right,
                0,
                OracleRoomData.ViewportWidth - right,
                height);
            DrawTextureRectRegion(_clearedTilemap, region, region);
        }
    }
}
