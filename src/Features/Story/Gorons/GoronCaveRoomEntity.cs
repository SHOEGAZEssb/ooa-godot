using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GoronCaveRoomEntity(NpcCharacter actor, bool interactive = true)
    : RoomEntityAdapter<NpcCharacter>(actor, actor.SetTransitionDrawOffset),
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity, IRoomEntityLifetime,
        IScreenTransitionPreloadRoomEntity
{
    internal GoronCaveScriptHost? Host { get; set; }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        // updateInteractions dispatches state zero during scrolling. The Goron
        // handlers run their initial script (including deletion/pose selection)
        // before freezing in state one. Effects are initialized by their owner.
        if(interactive && Host is null)
            throw new InvalidOperationException($"Goron ${Entity.Record.Id:x2}:${Entity.Record.SubId:x2} has no script owner during scroll preload.");
        Host?.Initialize();
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    public NpcCharacter Npc => Entity;
    public bool Finished => !Entity.Active;
    public bool BlocksLink(Vector2 position) => interactive && Entity.ScriptButtonSensitive && Entity.BlocksLinkCenter(position);
    public NpcCharacter? FindTalkTarget(Player player) => interactive && Entity.CanTalkTo(player) ? Entity : null;
}
