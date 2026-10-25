using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// INTERAC_SWORD $5e follows its parent's animParameter and high XYZ bytes.
internal sealed class LinkedSwordRoomEntity(NpcCharacter actor, Func<NpcCharacter?> parent, Func<int, string> animation)
    : IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private bool _initialized;
    private int _lastAnimation = -1;
    public Node2D Node => actor;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    public void SetTransitionDrawOffset(Vector2 offset) => actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        _initialized = true; actor.Visible = false;
        NpcCharacter? source = parent();
        if (source is null || !GodotObject.IsInstanceValid(source) || !source.Active)
        { Finished = true; return; }
        int parameter = source.CurrentAnimationParameter;
        if ((parameter & 0x80) == 0) return;
        int next = parameter & 0x7f;
        if (next != _lastAnimation) { _lastAnimation = next; actor.SetScriptAnimation(animation(next)); }
        actor.SetStatePosition(source.Position.Floor()); actor.SetScriptDrawOffset(source.ScriptDrawOffset);
        actor.SetFixedDrawPriority(ObjectDrawPriority.FixedLowPriorityZIndex); actor.SetScriptVisible(true); actor.Visible = true;
    }
}
