using Godot;
using System;

namespace Oracles;

internal partial class GameSelectPreview : Control
{
    [Export] public OracleGame Game { get; set; }
    internal GameSelectPanorama? Panorama { get; private set; }
    internal long AnimationUpdates { get; private set; }
    internal bool IsSelected { get; private set; }
    internal int ScrollX
    {
        get
        {
            int phase = (int)((AnimationUpdates / 8 + 20) % 80);
            return phase <= 40 ? phase : 80 - phase;
        }
    }
    internal event Action<OracleGame, bool>? SelectionRequested;

    public override void _Ready()
    {
        MouseEntered += () => SelectionRequested?.Invoke(Game, false);
        Resized += QueueRedraw;
    }

    internal void Prepare()
    {
        Panorama ??= new GameSelectPanorama(Game);
        AnimationUpdates = 0;
        QueueRedraw();
    }

    internal void SetSelected(bool selected)
    {
        IsSelected = selected;
        ((ShaderMaterial)Material).SetShaderParameter("inactive", !selected);
        Modulate = selected ? Colors.White : new Color(0.8f, 0.8f, 0.8f);
        QueueRedraw();
    }

    internal void AdvanceFrame()
    {
        if (!IsSelected) return;
        AnimationUpdates++;
        if (AnimationUpdates % 8 == 0) QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion)
            SelectionRequested?.Invoke(Game, false);
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } ||
            @event is InputEventScreenTouch { Pressed: true })
        {
            SelectionRequested?.Invoke(Game, true);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        if (Panorama is null || Size.X <= 0 || Size.Y <= 0) return;
        float scale = Math.Max(Size.X / 180, Size.Y / 144);
        Vector2 origin = new(-ScrollX * 0.5f * scale, (Size.Y - 144 * scale) * 0.12f);
        DrawSetTransform(origin, 0, new Vector2(scale, scale));
        DrawTexture(Panorama.Background, Vector2.Zero);
        for (int index = Panorama.Sprites.Count - 1; index >= 0; index--)
        {
            PanoramaSprite sprite = Panorama.Sprites[index];
            DrawTexture(sprite.Texture, sprite.Position);
        }
        DrawSetTransform(Vector2.Zero);
        if (IsSelected)
            DrawRect(new Rect2(0, 0, 3, Size.Y), Game == OracleGame.Ages
                ? new Color("78cfff") : new Color("ffb86b"));
    }
}
