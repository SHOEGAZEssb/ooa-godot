using Godot;
using System;

namespace oracleofages;

// Port-only presentation. Actors have no room, interaction, save or audio owner.
internal partial class BootLoadingScreen : Node2D
{
    private const int SingerScale = 4;
    private Window? _window;
    private Vector2I _previousSize;
    private Window.ContentScaleAspectEnum _previousAspect;
    private NpcCharacter? _nayru;
    private readonly NpcCharacter[] _notes = new NpcCharacter[2];
    private readonly Vector2[] _notePositions = new Vector2[2];
    private readonly int[] _noteRemaining = new int[2];
    private EffectRecord _noteEffect;
    private double _fractionalUpdates;
    private long _presentationTick;
    private double _fade = -1;
    internal float Progress { get; private set; }
    internal bool Finished => _fade >= 0.18;

    internal void Begin()
    {
        if (_window is null)
        {
            _window = GetWindow();
            _previousSize = _window.ContentScaleSize;
            _previousAspect = _window.ContentScaleAspect;
        }
        _window.ContentScaleSize = new Vector2I(480, 270);
        _window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        ClearActors();
        var database = new NayruIntroEventDatabase();
        ActorRecord actor = database.Actor("Nayru");
        _nayru = new NpcCharacter { Name = "LoadingNayru", Scale = new Vector2(SingerScale, SingerScale) };
        AddChild(_nayru);
        _nayru.Initialize(actor.ToNpcRecord(0, 0x39));
        _nayru.AppendScriptGraphics(actor.ExtraSprite);
        _nayru.SetScriptAnimation(actor.Animation(4));
        _noteEffect = database.Effect("MusicNote");
        for (int index = 0; index < _notes.Length; index++)
        {
            var note = new NpcCharacter { Name = $"LoadingNote{index}", Scale = new Vector2(SingerScale, SingerScale) };
            AddChild(note);
            note.Initialize(_noteEffect.ToNpcRecord(0, 0x39, 0, 0));
            note.SetScriptAnimation(_noteEffect.Animation);
            note.Visible = false;
            _notes[index] = note;
            _noteRemaining[index] = 0;
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
        _nayru?.Free();
        _nayru = null;
        for (int index = 0; index < _notes.Length; index++)
        {
            _notes[index]?.Free();
            _notes[index] = null!;
        }
    }

    internal void AdvancePresentation(double delta)
    {
        _fractionalUpdates += Math.Max(0, delta) * 60;
        while (_fractionalUpdates >= 1)
        {
            _fractionalUpdates--;
            _nayru?.AdvanceAnimationUpdates(1);
            // Nayru's intro alternates notes at phases 0 and 45 of a 90-tick cycle.
            if (_presentationTick % 45 == 0)
            {
                int index = (int)(_presentationTick / 45 % 2);
                _notePositions[index] = new Vector2(index == 0 ? -6 : 8, -4);
                _noteRemaining[index] = _noteEffect.Duration;
            }
            ReadOnlySpan<int> sway = [-1, -2, -1, 0, 1, 2, 1, 0];
            for (int index = 0; index < _notes.Length; index++)
            {
                if (_noteRemaining[index] <= 0) continue;
                _notePositions[index] += new Vector2(
                    _noteEffect.VelocityXFixed / 256.0f * (index == 0 ? -1 : 1),
                    _noteEffect.VelocityYFixed / 256.0f);
                if (_noteEffect.Sway && (_presentationTick & 7) == 0)
                    _notePositions[index] += Vector2.Right * sway[(int)(_presentationTick >> 3) & 7];
                _notes[index].AdvanceAnimationUpdates(1);
                _noteRemaining[index]--;
            }
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
        if (_nayru is null) return;
        Vector2 center = (GetViewportRect().Size / 2).Floor();
        _nayru.Position = center;
        for (int index = 0; index < _notes.Length; index++)
        {
            _notes[index].Position = center + (_notePositions[index] * SingerScale).Floor();
            _notes[index].Visible = _noteRemaining[index] > 0;
        }
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
