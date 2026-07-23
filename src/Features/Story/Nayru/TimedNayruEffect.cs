using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed class TimedNayruEffect(NpcCharacter actor, int duration, Vector2 velocity, bool sway, bool musicNote, bool floatsLeft, Vector2 spawnPosition, int soundId)
{
    public NpcCharacter Actor { get; } = actor;
    public int Remaining { get; set; } = duration;
    public Vector2 Velocity { get; } = velocity;
    public bool Sway { get; } = sway;
    public bool MusicNote { get; } = musicNote;
    public bool FloatsLeft { get; } = floatsLeft;
    public Vector2 SpawnPosition { get; } = spawnPosition;
    public int SoundId { get; } = soundId;
    public bool SoundPending { get; set; } = soundId != 0;
}
