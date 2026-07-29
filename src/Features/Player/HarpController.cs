using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Ports ITEM_HARP's parent item, including its shared instrument state,
/// floating notes, overworld restrictions, and tune effects.
/// </summary>
public sealed class HarpController
{
    private static readonly int[] NoteSwaySteps =
        { -1, -2, -1, 0, 1, 2, 1, 0 };

    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly RoomTransitionController _transitions;
    private readonly InteractionController _interactions;
    private readonly OracleSoundEngine _sound;
    private readonly HarpItemDatabase _database;
    private readonly FixedUpdateAccumulator _noteUpdates = new();
    private readonly List<PlayableHarpMusicNoteState> _notes = new();
    private int _noteSerial;

    internal int PlayingSong { get; private set; }
    internal bool IsPlaying => PlayingSong != 0 || _emptySongPlaying;
    internal HarpItemDatabase Database => _database;
    internal int NoteSpawnCount => _noteSerial;
    private bool _emptySongPlaying;

    internal HarpController(
        RoomSession rooms,
        RoomEntityManager entities,
        RoomTransitionController transitions,
        InteractionController interactions,
        OracleSoundEngine sound,
        HarpItemDatabase? database = null)
    {
        _rooms = rooms;
        _entities = entities;
        _transitions = transitions;
        _interactions = interactions;
        _sound = sound;
        _database = database ?? new HarpItemDatabase();
    }

    internal int TryStart(Player player)
    {
        if (IsPlaying)
            return -1;
        int song = player.Inventory.SelectedHarpSong;
        _ = _database.SoundForSong(song);
        PlayingSong = song;
        _emptySongPlaying = song == 0;
        _sound.PlaySound(_database.SoundForSong(song));
        return song;
    }

    internal void Advance(Player player, int actionUpdate)
    {
        HarpItemRecord record = _database.Record;
        if (!IsPlaying || actionUpdate <= 0 ||
            actionUpdate % record.NoteInterval != 0)
        {
            return;
        }

        bool spawnOnRight =
            (_database.AnimationParameterAtUpdate(actionUpdate) & 1) != 0;
        bool floatsRight = (_entities.NextRandomValue() & 1) != 0;
        SpawnMusicNote(
            player.Position + new Vector2(spawnOnRight ? 8 : -8, -4),
            floatsRight);
    }

    internal void Complete(Player player, int song)
    {
        if (!IsPlaying)
            return;
        PlayingSong = 0;
        _emptySongPlaying = false;

        HarpItemRecord record = _database.Record;
        byte tilesetFlags = _rooms.CurrentRoom.TilesetFlags;
        if ((tilesetFlags & record.ProhibitedTilesetMask) != 0)
            return;

        switch (song)
        {
            case 0:
                return;
            case 1:
                if (_rooms.SaveData.HasRoomFlag(
                    _rooms.ActiveGroup,
                    _rooms.CurrentRoom.Id,
                    (byte)record.PortalRoomFlag))
                {
                    return;
                }
                _interactions.ShowRoomInteractionMessage(
                    record.NoEffectText, player);
                return;
            case 2:
                if ((tilesetFlags & record.PastMask) == 0)
                {
                    _interactions.ShowRoomInteractionMessage(
                        record.NoEffectText, player);
                    return;
                }
                break;
            case 3:
                break;
            default:
                throw new InvalidOperationException(
                    $"ITEM_HARP completed unsupported song ${song:x2}.");
        }

        ClearNotes();
        _transitions.ApplyHarpTimeWarp(player, player.Position);
    }

    internal void Cancel()
    {
        PlayingSong = 0;
        _emptySongPlaying = false;
    }

    internal void Update(double delta)
    {
        int updates = _noteUpdates.Consume(delta);
        for (int update = 0; update < updates; update++)
            UpdateMusicNotes();
    }

    private void SpawnMusicNote(Vector2 position, bool floatsRight)
    {
        EffectRecord effect = _database.MusicNote;
        NpcCharacter actor = _entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(
                effect.ToNpcRecord(
                    _rooms.ActiveGroup,
                    _rooms.CurrentRoom.Id,
                    Mathf.RoundToInt(position.Y),
                    Mathf.RoundToInt(position.X)),
                $"PlayableHarpMusicNote{_noteSerial}"));
        actor.Position = position;
        actor.SetScriptAnimation(effect.Animation);
        actor.SetFixedDrawPriority(NpcCharacter.InFrontOfLinkZIndex);
        float velocityX = effect.VelocityXFixed / 256.0f;
        if (!floatsRight)
            velocityX = -velocityX;
        _notes.Add(new PlayableHarpMusicNoteState(
            actor,
            effect.Duration,
            new Vector2(velocityX, effect.VelocityYFixed / 256.0f),
            effect.Sway));
        _noteSerial++;
    }

    private void UpdateMusicNotes()
    {
        for (int index = _notes.Count - 1; index >= 0; index--)
        {
            PlayableHarpMusicNoteState note = _notes[index];
            note.Actor.Position += note.Velocity;
            if (note.Sway && (_entities.FrameCounter & 7) == 0)
            {
                note.Actor.Position += Vector2.Right *
                    NoteSwaySteps[(_entities.FrameCounter >> 3) & 7];
            }
            note.Remaining--;
            if (note.Remaining > 0)
                continue;
            note.Actor.SetActive(false);
            _notes.RemoveAt(index);
        }
    }

    private void ClearNotes()
    {
        foreach (PlayableHarpMusicNoteState note in _notes)
            note.Actor.SetActive(false);
        _notes.Clear();
    }
}

internal sealed class PlayableHarpMusicNoteState(
    NpcCharacter actor,
    int remaining,
    Vector2 velocity,
    bool sway)
{
    internal NpcCharacter Actor { get; } = actor;
    internal int Remaining { get; set; } = remaining;
    internal Vector2 Velocity { get; } = velocity;
    internal bool Sway { get; } = sway;
}
