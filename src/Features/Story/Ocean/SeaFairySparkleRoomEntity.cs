using Godot;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC_SPARKLE $84:$04/$0a/$02, with source state-0 setup-only dispatch.
internal sealed class SeaFairySparkleRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity
{
    private readonly string _role;
    private int _value;
    private bool _initialized;
    private Vector2 _position;
    public bool Finished { get; private set; }
    internal SeaFairySparkleRoomEntity(SeaFairySparkleSpawn spawn)
        : this(spawn, NpcCharacter.CreateFromRecord(spawn.Data.Visual(spawn.Role, spawn.Position))) { }
    private SeaFairySparkleRoomEntity(SeaFairySparkleSpawn spawn, NpcCharacter actor)
        : base(actor, actor.SetTransitionDrawOffset)
    {
        _position = spawn.Position; _role = spawn.Role; _value = spawn.Value;
        Entity.SetGraphicsSourceOffset(spawn.Data.SourceOffset(_role));
        // sparkle.s initializes one native animation. Its $84:$04 circle
        // spans 48x48 OAM; the ordinary facing canvas clips its outer cells.
        Entity.SetScriptAnimation(Entity.Record.DownAnimation);
        Entity.SetBlocksLink(false); Entity.TimeWarpSolidMarked = false;
        Entity.SetScriptDrawOffset(new(0, _role == "trail" ? _value : -16));
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (!_initialized)
        {
            _initialized = true;
            Entity.SetFixedDrawPriority(_role == "flash" ? ObjectDrawPriority.FixedHighPriorityZIndex : ObjectDrawPriority.BehindLinkZIndex);
            return;
        }
        if (_role == "flash")
        {
            if (--_value == 0) { Finish(); return; }
            Entity.AdvanceAnimationUpdates(1);
            Entity.SetScriptVisible((frame.Counter & 1) == 0); return;
        }
        if (_role == "ray")
        {
            Entity.Position = OracleObjectMovement.Shared.ApplySpeed(ref _position, 0x78, _value);
            // bank0.s:objectCheckWithinScreenBoundary uses unsigned high bytes and $07 leeway.
            if ((((int)System.MathF.Floor(_position.X) + 7) & 255) >= 0xaf ||
                (((int)System.MathF.Floor(_position.Y) + 7) & 255) >= 0x8f) { Finish(); return; }
        }
        else if (Entity.CurrentAnimationParameter == 0xff) { Finish(); return; }
        Entity.AdvanceAnimationUpdates(1);
    }
    private void Finish() { Finished = true; Entity.SetActive(false); }
}

internal sealed record SeaFairySparkleSpawn(SeaFairyDatabase Data, string Role,
    Vector2 Position, int Value) : RoomEntitySpawn;
