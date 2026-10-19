using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MALE_VILLAGER $3a:$02 path blocker.</summary>
internal sealed class BlackTowerBlockingVillagerRoomEntity : BlackTowerNpcRoomEntity, IFixedRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly BlackTowerWorkerDatabase _data;
    private OracleObjectPosition _position;
    private int _savedX, _savedLinkY, _direction;
    private bool _initialized, _moving;
    internal BlackTowerBlockerScriptHost? Script { get; set; }
    internal int Direction => _direction;
    internal int SavedLinkY => _savedLinkY;
    internal OracleObjectPosition PrecisePosition => _position;
    internal bool Initialized => _initialized;
    internal bool Moving => _moving;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal BlackTowerBlockingVillagerRoomEntity(NpcCharacter npc, BlackTowerWorkerDatabase data)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _data = data;
        npc.ResetNativeNpcFacingState();
        npc.SetScriptButtonSensitive(false);
    }

    public override NpcCharacter? FindTalkTarget(Player player) =>
        _initialized && Entity.ScriptButtonSensitive && Entity.CanTalkTo(player) ? Entity : null;

    internal void Move(OracleRuntimeState memory, int speed, int angle)
    {
        if (speed != _data.Speed100 || angle is not (ObjectAngle.Left or ObjectAngle.Right))
            throw new InvalidOperationException($"Villager $3a:$02 has unsupported motion ${speed:x2}/${angle:x2}.");
        _position = NativeObjectMovement.ApplySpeed(memory, _position, speed, angle);
        Entity.Position = _position.PixelPosition;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active) return;
        var script = Script ?? throw new InvalidOperationException("Villager $3a:$02 has no script owner.");
        if (!_initialized)
        {
            _initialized = true;
            _position = OracleObjectPosition.FromPixels(Entity.Position);
            Entity.Position = _position.PixelPosition;
            _savedX = OracleObjectPosition.HighByte(Entity.Position.X);
            Entity.SetScriptAnimation(Entity.Record.UpAnimation); // interactionInitGraphics animation $00.
            Entity.SetCollisionRadii(0, 0);
            Entity.SetDialogue(0, string.Empty, Entity.Record.CanFace);
            Entity.SetScriptButtonSensitive(true);
            return; // villager.s state0 does not fall through into state1.
        }
        if (!_moving)
        {
            Entity.FaceLinkAndAnimateOneUpdate(frame.Player);
            script.Advance(frame.Player);
            Vector2 open = new((_savedX + (_direction == 0 ? 0x11 : -0x11)) & 0xff,
                OracleObjectPosition.HighByte(Entity.Position.Y));
            Vector2 delta = OracleObjectMath.ToPixelPosition(frame.Player.Position) - open;
            if (OracleObjectMath.ObjectAxisOverlaps((int)delta.X, 3, (int)NpcCharacter.LinkCollisionRadius) &&
                OracleObjectMath.ObjectAxisOverlaps((int)delta.Y, 5, (int)NpcCharacter.LinkCollisionRadius))
            {
                _moving = true;
                script.StartMoving(); // interactionSetScript follows this visit's runScript.
            }
            _savedLinkY = OracleObjectPosition.HighByte(frame.Player.Position.Y);
            _position = _position with { XFixed = (ushort)((_savedX << 8) | (_position.XFixed & 0xff)) };
            Entity.Position = _position.PixelPosition;
            return;
        }
        Entity.AnimateAsNpcOneUpdate(frame.Player);
        script.Advance(frame.Player);
        if (script.HasState)
        {
            // One animate call, plus one at SPEED_100 while counter2 != 0.
            Entity.AdvanceAnimationUpdates(script.MovementCounter != 0 ? 2 : 1);
            return;
        }
        _savedX = OracleObjectPosition.HighByte(Entity.Position.X);
        _direction ^= 1;
        _moving = false;
        script.StartWatching();
    }
}
