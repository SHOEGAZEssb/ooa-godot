using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_FARORE state-zero initialization and state-one script slot.</summary>
internal sealed class FaroreRoomEntity(NpcCharacter npc)
    : NpcCharacterRoomEntityAdapter(npc, npc.SetTransitionDrawOffset),
      IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity,
      IUpdatesDuringDialogueRoomEntity
{
    private bool _initialized;
    internal FaroreScriptHost? Script { get; set; }
    public NpcCharacter Npc => Entity;
    public bool UpdatesDuringDialogue => true;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active) return;
        if (!_initialized)
        {
            _initialized = true;
            Entity.ShowInitializedPresentation();
            Entity.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
            Script?.InitializeStateZero();
            Entity.PreventPlayerPassing(frame.Player);
            return;
        }
        Entity.SetCollisionRadii(0x14, 0x06);
        Script?.Advance(frame.Player);
        Entity.AdvanceAnimationUpdates(1);
        Entity.PreventPlayerPassing(frame.Player);
    }

    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;
}
