using Godot;
using System;

namespace Oracles;

// Port-only presentation. Actors have no room, interaction, save or audio owner.
internal partial class BootLoadingScreen : Node2D
{
    private const int ActorScale = 4;
    private Label _versionLabel = null!;
    private Window? _window;
    private Vector2I _previousSize;
    private Window.ContentScaleAspectEnum _previousAspect;
    private NpcCharacter? _actor;
    private double _fractionalUpdates;
    private long _presentationTick;
    private double _fade = -1;
    private OracleGame _game;
    internal float Progress { get; private set; }
    internal bool Finished => _fade >= 0.18;

    public override void _Ready()
    {
        _versionLabel = GetNode<Label>("VersionLabel");
        _versionLabel.Text = $"v{ProjectSettings.GetSetting("application/config/version").AsString()}";
    }

    internal void Begin(OracleGame game)
    {
        _ = OracleAssetPack.ForGame(game);
        _game = game;
        if (_window is null)
        {
            _window = GetWindow();
            _previousSize = _window.ContentScaleSize;
            _previousAspect = _window.ContentScaleAspect;
        }
        _window.ContentScaleSize = new Vector2I(480, 270);
        _window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        ClearActors();
        if (game == OracleGame.Seasons)
        {
            _actor = SeasonsOpeningData.CreateLoadingDin(); _actor.Name = "LoadingDin";
            _actor.Scale = new(ActorScale, ActorScale); AddChild(_actor);
        }
        else
        {
            BeginAgesActors();
        }
        _fractionalUpdates = 0;
        _presentationTick = 0;
        _fade = -1;
        Progress = 0;
        Modulate = Colors.White;
        Visible = true;
        PositionActors();
        QueueRedraw();
    }

    internal void SetProgress(float progress)
    {
        Progress = Math.Clamp(progress, 0, 1);
        QueueRedraw();
    }

    internal void Finish() => _fade = 0;

    internal void End()
    {
        Visible = false;
        ClearActors();
        if (_window is null) return;
        _window.ContentScaleSize = _previousSize;
        _window.ContentScaleAspect = _previousAspect;
        _window = null;
    }

    public override void _ExitTree() => End();

    private void ClearActors()
    {
        _actor?.Free();
        _actor = null;
        ClearAgesNotes();
    }

    internal void AdvancePresentation(double delta)
    {
        _fractionalUpdates += Math.Max(0, delta) * 60;
        while (_fractionalUpdates >= 1)
        {
            _fractionalUpdates--;
            _actor?.AdvanceAnimationUpdates(1);
            if (_game == OracleGame.Ages) AdvanceAgesNotes();
            _presentationTick++;
        }
        if (_fade >= 0)
        {
            _fade += Math.Max(0, delta);
            Modulate = new Color(1, 1, 1, (float)Math.Max(0, 1 - _fade / 0.18));
        }
        PositionActors();
        QueueRedraw();
    }

    private void PositionActors()
    {
        Vector2 size = GetViewportRect().Size;
        _versionLabel.Position = (size - _versionLabel.Size - new Vector2(8, 6)).Floor();
        if (_actor is null) return;
        Vector2 center = (size / 2).Floor();
        _actor.Position = center;
        if (_game == OracleGame.Ages) PositionAgesNotes(center);
    }

    public override void _Draw()
    {
        Vector2 size = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, size), Colors.Black);
        Vector2 bar = new(Mathf.Floor(size.X / 2 - 36), size.Y - 18);
        DrawRect(new Rect2(bar, new Vector2(72, 2)), new Color("303030"));
        DrawRect(new Rect2(bar, new Vector2(Mathf.Floor(72 * Progress), 2)), new Color("eee4b5"));
    }
}
