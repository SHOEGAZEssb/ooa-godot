using Godot;
using System.Collections.Generic;

namespace Oracles;

/// <summary>Fixed interaction slot for a family actor's script and collision.</summary>
internal sealed class FamilyNpcRoomEntity(NpcCharacter npc)
    : NpcCharacterRoomEntityAdapter(npc, npc.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomBlocker, ITalkTarget,
        IOrdinaryNpcEntity
{
    private bool _initialized;
    internal FamilyNpcScriptHost? Script { get; set; }
    public NpcCharacter Npc => Entity;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active) return;
        if (!_initialized)
        {
            _initialized = true;
            Entity.ShowInitializedPresentation();
            Entity.PreventPlayerPassing(frame.Player);
            Entity.UpdateDrawPriority(frame.Player.Position, withTerrainEffects: true);
            return;
        }
        Script?.Advance(frame.Player);
        Entity.AdvanceAnimationUpdates(1);
        Entity.PreventPlayerPassing(frame.Player);
        Entity.UpdateDrawPriority(frame.Player.Position, withTerrainEffects: true);
    }

    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;
}
