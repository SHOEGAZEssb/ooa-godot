using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class ColoredCubeFlameRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    private readonly ColoredCubePuzzleState _puzzle;
    private readonly EnemyAnimationPlayer[] _palettes = new EnemyAnimationPlayer[3];
    private int _palette;
    private bool _initialized;

    public Node2D Node => this;
    internal int Palette => _palette;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal ColoredCubeFlameRoomEntity(
        DungeonObjectRecord record,
        DungeonInteractionVisual visual,
        ColoredCubePuzzleState puzzle)
    {
        _puzzle = puzzle;
        Name = $"ColoredCubeFlame_{record.Group}_{record.Room:x2}_{record.Order}";
        Position = record.Position;
        Image source = EnemyVisualSource.LoadComposite(visual.Sprites);
        int[] sourcePalettes = { 2, 3, 1 };
        for (int index = 0; index < _palettes.Length; index++)
        {
            _palettes[index] = new EnemyAnimationPlayer(this, 1);
            _palettes[index].Load(source, visual.Animations, visual.TileBase, sourcePalettes[index]);
            _palettes[index].SetAnimation(0);
        }
        Visible = false;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
        => UpdateState();

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        // updateInteractions dispatches state0 during scrolling. The cube's
        // earlier state0 publishes wRotatingCubePos/Color; an unlit flame still
        // initializes its palette, then falls through to objectSetInvisible.
        if (!_initialized) UpdateState();
        return Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    private void UpdateState()
    {
        if (!_initialized)
        {
            if (_puzzle.CubePosition == 0) return;
            _palette = _puzzle.CubeColor & 0x7f;
            _initialized = true;
        }
        ApplyPuzzleState(advanceAnimation: true);
    }

    private void ApplyPuzzleState(bool advanceAnimation)
    {
        Visible = (_puzzle.CubeColor & 0x80) != 0;
        if (!Visible)
            return;
        _palette = _puzzle.CubeColor & 0x7f;
        if (advanceAnimation)
            // Native color changes replace oamFlags, retaining the single
            // animation cursor. Keep palette renderings at the same frame.
            foreach (var palette in _palettes) palette.Advance();
        QueueRedraw();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        if (Visible && _palettes[_palette].HasFrames)
            DrawTexture(_palettes[_palette].CurrentTexture,
                new Vector2(-16, -16) + TransitionDrawOffset);
    }
}
