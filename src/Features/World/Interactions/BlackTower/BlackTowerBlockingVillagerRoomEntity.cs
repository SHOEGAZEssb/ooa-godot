using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MALE_VILLAGER $3a:$02 path blocker.</summary>
internal sealed class BlackTowerBlockingVillagerRoomEntity : BlackTowerNpcRoomEntity,
    IFixedRoomEntity, INpcTalkLifecycle, IPlayerRestriction
{

    private readonly BlackTowerWorkerDatabase _data;
    private BlockState _state;
    private float _savedX;
    private int _savedLinkY;
    private int _movementUpdates;
    private int _wait;
    private int _talkWait;
    private bool _movingRight = true;
    private bool _talking;

    internal BlackTowerBlockingVillagerRoomEntity(
        NpcCharacter npc,
        BlackTowerWorkerDatabase data)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _data = data;
        _savedX = npc.Position.X;
        npc.SetCollisionRadii(6, 6);
        npc.SetDialogue(0x1441, npc.Message, canFace: true);
    }

    internal bool MovingRight => _movingRight;
    internal int MovementUpdates => _movementUpdates;
    internal int SavedLinkY => _savedLinkY;
    public NpcCharacter TalkNpc => Entity;
    public bool DisablesSword => _state != BlockState.Watching;
    public bool DisablesMovement => DisablesSword;

    public override NpcCharacter? FindTalkTarget(Player player) =>
        _state == BlockState.Watching && !_talking && _talkWait == 0
            ? base.FindTalkTarget(player)
            : null;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active)
            return;

        bool armedThisUpdate = false;
        if (_state == BlockState.Watching)
        {
            Entity.FaceToward(frame.Player.Position);
            Entity.AdvanceAnimationUpdates(1);
            // npcFaceLinkAndAnimate falls through interactionAnimateAsNpc,
            // which separates Link before this state's open-side collision
            // check runs.
            Entity.PreventPlayerPassing(frame.Player);
            if (_talkWait > 0)
            {
                _talkWait--;
                if (_talkWait == 0)
                    Entity.SetScriptAnimation(Entity.Record.DownAnimation);
            }

            Vector2 openPosition = new(
                _savedX + (_movingRight ? 0x11 : -0x11), Entity.Position.Y);
            Vector2 delta = OracleObjectMath.ToPixelPosition(frame.Player.Position) -
                OracleObjectMath.ToPixelPosition(openPosition);
            if (Mathf.Abs(delta.X) < 3 + NpcCharacter.LinkCollisionRadius &&
                Mathf.Abs(delta.Y) < 5 + NpcCharacter.LinkCollisionRadius)
            {
                _savedLinkY = Mathf.FloorToInt(frame.Player.Position.Y);
                _state = BlockState.Moving;
                _movementUpdates = 0;
                // interactionSetScript runs after this update's
                // interactionRunScript call; part 2 starts next update.
                armedThisUpdate = true;
            }
        }

        if (_state == BlockState.Moving && !armedThisUpdate)
        {
            Entity.AdvanceAnimationUpdates(2);
            // The native substate calls interactionAnimateAsNpc before the
            // movement script. This keeps Link outside the worker's current
            // collision box as the worker closes the passage.
            Entity.PreventPlayerPassing(frame.Player);
            float step = _data.Speed100 / 40.0f;
            _savedX += _movingRight ? step : -step;
            _movementUpdates++;
            Entity.SetStatePosition(new Vector2(_savedX, Entity.Position.Y));
            frame.Player.SetScriptedCoordinateHigh(horizontal: false, _savedLinkY);
            if (_movementUpdates >= _data.BlockerDistance)
            {
                _state = BlockState.Waiting;
                _wait = _data.BlockerWait;
            }
        }
        else if (_state == BlockState.Waiting)
        {
            Entity.AdvanceAnimationUpdates(_wait == 1 ? 1 : 2);
            // The final wait update performs this separation before
            // enableinput, so Link cannot regain control inside the blocker.
            Entity.PreventPlayerPassing(frame.Player);
            _wait--;
            if (_wait == 0)
            {
                _movingRight = !_movingRight;
                _state = BlockState.Watching;
            }
        }

        Entity.UpdateDrawPriority(frame.Player.Position);
    }

    public void OnNpcTalkStarted() => _talking = true;

    public void OnNpcTalkEnded()
    {
        _talking = false;
        // A collision can replace part 1 with part 2 while its textbox is
        // open. In that case the old post-text wait/setanimation commands no
        // longer exist.
        _talkWait = _state == BlockState.Watching ? _data.BlockerWait : 0;
    }
}
