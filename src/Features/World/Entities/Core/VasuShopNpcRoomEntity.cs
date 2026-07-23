using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Native update behavior shared by the five placed actors in Vasu Jewelers.
/// Vasu uses the original asymmetric solid radius and object-side separation;
/// snakes restart their hidden idle frame while Link is outside the source's
/// $18 Manhattan-distance check.
/// </summary>
internal sealed class VasuShopNpcRoomEntity
    : RoomEntityAdapter<NpcCharacter>, IVariableRoomEntity, IFixedRoomEntity,
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity, IPlayerRestriction
{
    private readonly VasuShopDatabase _database;
    private readonly bool _vasu;
    private readonly bool _snake;
    private readonly string _idleAnimation;

    public VasuShopNpcRoomEntity(
        NpcCharacter npc,
        VasuShopDatabase database)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _database = database;
        _vasu = npc.Record is { Id: 0x89, SubId: 0x00 };
        _snake = npc.Record.Id == 0x89 && npc.Record.SubId != 0;
        npc.SetDialogue(0, string.Empty, canFace: false, database.TextboxPosition);
        npc.SetScriptButtonSensitive(true);
        if (_vasu)
        {
            npc.SetCollisionRadii(database.VasuRadiusY, database.VasuRadiusX);
            _idleAnimation = database.Animation(0x89, 0);
            npc.SetScriptAnimation(_idleAnimation);
        }
        else if (_snake)
        {
            npc.SetCollisionRadii(database.SnakeRadius, database.SnakeRadius);
            _idleAnimation = database.Animation(0x89, npc.Record.SubId);
            npc.SetScriptAnimation(_idleAnimation);
        }
        else
        {
            npc.SetCollisionRadii(database.SnakeRadius, database.SnakeRadius);
            // interactionInitGraphics leaves both books on the graphics
            // record's default animation $00. Subid $01 changes only palette.
            _idleAnimation = database.Animation(0xe5, 0);
            npc.SetScriptAnimation(_idleAnimation);
        }
    }

    public NpcCharacter Npc => Entity;
    public bool DisablesSword => false;
    public bool DisablesItems => true;
    public bool DisablesRingTransformations => true;

    public void Update(double delta, Player player) =>
        Entity.UpdateNpc(delta, player.Position);

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_vasu)
        {
            Entity.PreventPlayerPassing(frame.Player);
            Entity.UpdateDrawPriority(frame.Player.Position);
            return;
        }

        if (!_snake || Entity.CurrentScriptAnimationSource != _idleAnimation)
            return;
        Vector2 delta = frame.Player.Position - Entity.Position;
        if (Mathf.Abs(delta.X) + Mathf.Abs(delta.Y) >=
            _database.SnakeProximityRadius)
        {
            // interactionSetAnimation runs on every out-of-range update,
            // pinning the snake to the first (hidden) idle frame.
            Entity.SetScriptAnimation(_idleAnimation);
        }
    }

    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanScriptTalkTo(
            player,
            _vasu ? _database.VasuRadiusY : _database.SnakeRadius,
            _vasu ? _database.VasuRadiusX : _database.SnakeRadius,
            _database.AButtonPointOffset)
            ? Entity
            : null;
}
