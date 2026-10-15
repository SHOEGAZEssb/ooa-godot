using Godot;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC_SPARKLE $84:$0f copies its related slot before cfc0 bit 0.
internal sealed class MakuSeedSparkleRoomEntity
    : IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly RoomEventContext _context;
    private readonly MakuSeedFallingRoomEntity _parent;
    private readonly NpcCharacter _actor;
    private bool _initialized;

    internal MakuSeedSparkleRoomEntity(RoomEventContext context, MakuSeedDatabase data, MakuSeedFallingRoomEntity parent)
    {
        _context = context;
        _parent = parent;
        // bank0.s:objectCreateInteraction copies XYZ during allocation.
        // sparkle.s state0 becomes visible and returns without following its
        // parent; its first draw must already have the copied position/height.
        _actor = data.CreateActor(0x84, 0x0f, parent.Node.Position);
        _actor.SetScriptDrawOffset(new(0, parent.ZFixed >> 8));
    }
    public Node2D Node => _actor;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            _initialized = true;
            _actor.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex);
            return;
        }
        _actor.SetStatePosition(_parent.Node.Position);
        _actor.SetScriptDrawOffset(new(0, _parent.ZFixed >> 8));
        if ((_context.Entities.RuntimeState.ReadWramByte(0xcfc0) & 1) != 0)
        { Finished = true; _actor.SetActive(false); return; }
        _actor.AdvanceAnimationUpdates(1);
        _actor.SetScriptVisible((frame.Counter & 1) == 0);
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
}
