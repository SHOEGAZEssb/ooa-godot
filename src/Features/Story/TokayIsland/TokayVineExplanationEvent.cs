using Godot;
using System.Linq;

namespace oracleofages;

/// <summary>
/// tokayExplainingVinesScript for INTERAC_TOKAY $48:$1e.
/// </summary>
internal sealed class TokayVineExplanationEvent : IRoomEntryEvent
{
    private readonly RoomEventContext _context;
    private readonly TokayInteractionDatabase _database;
    private bool _active;
    private bool _awaitingApproach;
    private int _counter;
    private TokayCharacter? _actor;
    private TokayAttachedVisualRoomEntity? _effect;
    private readonly TokayNativeDatabase _native = new();

    internal TokayVineExplanationEvent(
        RoomEventContext context,
        TokayInteractionDatabase database)
    {
        _context = context;
        _database = database;
    }

    public bool MenusDisabled => BlocksGameplay;
    public bool HasState => _active || _awaitingApproach;
    public bool BlocksGameplay => _active;
    public bool Matches(int group, OracleRoomData room) => group == 0 && room.Id == 0xbb;
    public void Start(OracleRoomData room)
    {
        _actor = _context.Entities.Entities<TokayCharacter>().Single(npc => npc.Record.SubId == 0x1e);
        _actor.SetFacingDirection(Vector2I.Down);
        _awaitingApproach = !_context.Rooms.SaveData.HasRoomFlag(0, 0xbb, OracleSaveData.RoomFlag40);
    }

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (_active || !npc.Active || npc.Record is not { Id: 0x48, SubId: 0x1e })
            return false;
        _actor = (TokayCharacter)npc;

        bool explained = _context.Rooms.SaveData.HasRoomFlag(
            _context.Rooms.ActiveGroup,
            _context.Rooms.CurrentRoom.Id,
            OracleSaveData.RoomFlag40);
        if (!explained && _awaitingApproach)
            return false;
        _context.Player.BeginCutsceneControl();
        int angle = OracleObjectMovement.Shared.RelativeAngle(_actor.Position, _context.Player.Position);
        Vector2 facing = OracleObjectMath.StrictCardinalVector((angle + 4) & 0x18);
        _actor.SetFacingDirection(new Vector2I((int)facing.X, (int)facing.Y));
        if (!explained)
        {
            _context.Rooms.SaveData.SetRoomFlag(
                _context.Rooms.ActiveGroup,
                _context.Rooms.CurrentRoom.Id,
                OracleSaveData.RoomFlag40);
            _context.Player.Face(Vector2I.Left);
            _effect = _context.Entities.Spawn<TokayAttachedVisualRoomEntity>(new TokayAttachedVisualSpawn(
                _actor, _native.Visual("exclamation", 0, 0xbb), new Vector2(-13, -13)));
            // Both script writes target speedZ low: the resulting $00ff is
            // positive, and the next gravity update clamps Z to the ground.
            int z = 0, speedZ = 0xff;
            OracleObjectMath.UpdateSpeedZ(ref z, ref speedZ, 0x10);
            _counter = 30;
        }
        else
            _context.ShowDialogue(_database.Text(0x0a6b));
        _active = true;
        return true;
    }

    public void UpdateFrame()
    {
        if (_awaitingApproach)
        {
            Vector2 delta = OracleObjectMath.ToPixelPosition(_context.Player.Position) - _actor!.Position;
            bool above = Mathf.Abs(delta.Y) > Mathf.Abs(delta.X) && delta.Y < 0;
            // Native A is direction*2; directly above yields zero in var31.
            if (Mathf.Abs(delta.X) + Mathf.Abs(delta.Y) < 0x18 && !above)
            {
                _awaitingApproach = false;
                TryInteractNpc(_actor);
            }
            return;
        }
        if (_counter != 0)
        {
            if (--_counter == 0)
            {
                if (_effect is { } effect) effect.Retired = true;
                _context.ShowDialogue(_database.Text(0x0a6a));
            }
            return;
        }
        if (_active && !_context.DialogueOpen)
        {
            _actor?.SetFacingDirection(Vector2I.Down);
            _context.Player.EndCutsceneControl();
            _active = false;
        }
    }

    public void Cancel()
    {
        if (_active) _context.Player.EndCutsceneControl();
        if (_effect is { } effect) effect.Retired = true;
        _effect = null;
        _active = false;
        _awaitingApproach = false;
        _counter = 0;
        _actor = null;
    }
}
