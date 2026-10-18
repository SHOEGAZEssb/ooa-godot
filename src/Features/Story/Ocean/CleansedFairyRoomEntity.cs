using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// greatFairy.s:greatFairy_subid1. Always-update applies even during TX_4109.
internal sealed class CleansedFairyRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IPlayerRestriction,
    IUpdatesDuringRoomEntityFreeze
{
    private readonly SeaFairyDatabase _data;
    private readonly Action _fade, _clean;
    private readonly Action<int> _sound;
    private readonly Action<string> _showText;
    private readonly Func<bool> _textActive, _paletteActive;
    private Vector2 _position;
    private int _counter, _angleCounter, _angle, _height = -16;
    internal int State { get; private set; } = -1;
    internal int Counter => _counter;
    internal int Height => _height;
    internal int Angle => _angle;
    public bool Finished { get; private set; }
    public bool DisablesSword => !Finished;
    public bool DisablesItems => !Finished;
    public bool DisablesMovement => !Finished;
    public bool DisablesMenus => !Finished;
    public bool DisablesPlayerContact => !Finished;
    internal CleansedFairyRoomEntity(SeaFairyDatabase data, Vector2 position,
        Action<int> sound, Action<string> showText, Func<bool> textActive,
        Func<bool> paletteActive, Action fade, Action clean)
        : this(data, position, sound, showText, textActive, paletteActive, fade, clean,
            NpcCharacter.CreateFromRecord(data.Visual("fairy", position))) { }
    private CleansedFairyRoomEntity(SeaFairyDatabase data, Vector2 position,
        Action<int> sound, Action<string> showText, Func<bool> textActive,
        Func<bool> paletteActive, Action fade, Action clean, NpcCharacter actor)
        : base(actor, actor.SetTransitionDrawOffset)
    {
        _data = data; _position = position; _sound = sound; _showText = showText;
        _textActive = textActive; _paletteActive = paletteActive; _fade = fade; _clean = clean;
        Entity.SetBlocksLink(false); Entity.TimeWarpSolidMarked = true;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (State == -1)
        {
            State = 0; _counter = _data.Timing[0]; _angleCounter = _data.Timing[3];
            Entity.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex);
            Entity.SetScriptDrawOffset(new(0, _height));
            _sound(SoundId.SndPieceOfPower);
            spawns.Add(new SeaFairySparkleSpawn(_data, "flash", _position, _data.Timing[1]));
            for (int angle = 0; angle < 32; angle += 4)
                spawns.Add(new SeaFairySparkleSpawn(_data, "ray", _position, angle));
            return;
        }
        Entity.AdvanceAnimationUpdates(1);
        switch (State)
        {
            case 0:
                if (--_counter != 0) return;
                _counter = _data.Timing[2]; _angle = 8; State = 1;
                _showText(_data.Thanks); return;
            case 1:
                if (_textActive()) return;
                Entity.Position = OracleObjectMovement.Shared.ApplySpeed(ref _position, 0x78, _angle);
                if (--_angleCounter == 0)
                {
                    _angleCounter = _data.Timing[3]; _angle = (_angle + 1) & 31;
                    if (--_counter == 0) { State = 2; return; }
                }
                Trail(frame.Counter, spawns); return;
            case 2:
                Trail(frame.Counter, spawns);
                _height -= 2; Entity.SetScriptDrawOffset(new(0, _height));
                if (_height >= -80) return;
                _fade(); State = 3; return;
            case 3:
                if (_paletteActive()) return;
                Finished = true; Entity.SetActive(false); _clean(); return;
        }
    }
    private void Trail(int frame, ICollection<RoomEntitySpawn> spawns)
    {
        if ((frame & 7) != 0) return;
        // objectCreateInteraction copies position, including Z.
        spawns.Add(new SeaFairySparkleSpawn(_data, "trail", _position, _height));
        if ((frame & 31) == 0) _sound(SoundId.SndMagicPowder);
    }
}

internal sealed record CleansedFairySpawn(SeaFairyDatabase Data, Vector2 Position,
    Action Fade, Action Clean) : RoomEntitySpawn;
