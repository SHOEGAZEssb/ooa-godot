
namespace Oracles;

internal partial class BootLoadingScreen
{
    private readonly NpcCharacter[] _notes = new NpcCharacter[2];
    private readonly Vector2[] _notePositions = new Vector2[2];
    private readonly int[] _noteRemaining = new int[2];
    private EffectRecord _noteEffect;

    private void BeginAgesActors()
    {
        var database = new NayruIntroEventDatabase();
        ActorRecord actor = database.Actor("Nayru");
        _actor = new NpcCharacter { Name = "LoadingNayru", Scale = new Vector2(ActorScale, ActorScale) };
        AddChild(_actor);
        _actor.Initialize(actor.ToNpcRecord(0, 0x39), AgesGameContent.AssetRoot);
        _actor.AppendScriptGraphics(actor.ExtraSprite);
        _actor.SetScriptAnimation(actor.Animation(4));
        _actor.SetScriptVisible(true);
        _noteEffect = database.Effect("MusicNote");
        for (int index = 0; index < _notes.Length; index++)
        {
            var note = new NpcCharacter { Name = $"LoadingNote{index}", Scale = new Vector2(ActorScale, ActorScale) };
            AddChild(note);
            note.Initialize(_noteEffect.ToNpcRecord(0, 0x39, 0, 0), AgesGameContent.AssetRoot);
            note.SetScriptAnimation(_noteEffect.Animation);
            note.SetScriptVisible(false);
            _notes[index] = note;
            _noteRemaining[index] = 0;
        }
    }

    private void ClearAgesNotes()
    {
        for (int index = 0; index < _notes.Length; index++)
        {
            _notes[index]?.Free();
            _notes[index] = null!; _noteRemaining[index] = 0;
        }
    }

    private void AdvanceAgesNotes()
    {
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
    }

    private void PositionAgesNotes(Vector2 center)
    {
        for (int index = 0; index < _notes.Length; index++)
        {
            if (_notes[index] is null) continue;
            _notes[index].Position = center + (_notePositions[index] * ActorScale).Floor();
            _notes[index].SetScriptVisible(_noteRemaining[index] > 0);
        }
    }
}
