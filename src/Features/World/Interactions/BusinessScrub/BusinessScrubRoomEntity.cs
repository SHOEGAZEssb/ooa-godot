using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Native INTERAC_BUSINESS_SCRUB $ce:$03 and its $ce:$80 mimicked-bush child.
/// The scrub emerges at strict Manhattan distance $20 and remains an
/// always-updating interaction while gameplay text is active.
/// </summary>
internal sealed class BusinessScrubRoomEntity
    : IRoomEntity, IFixedRoomEntity, ITalkTarget, IOrdinaryNpcEntity,
        IUpdatesDuringDialogueRoomEntity
{
    private readonly BusinessScrubDatabase _database;
    private readonly NpcCharacter _npc;
    private readonly Sprite2D _bush;
    private Vector2 _transitionDrawOffset;
    private bool _linkWasNear;

    public NpcCharacter Npc => _npc;
    public Node2D Node => _npc;
    internal bool LinkWasNear => _linkWasNear;
    internal Sprite2D Bush => _bush;

    public BusinessScrubRoomEntity(
        NpcCharacter npc,
        BusinessScrubDatabase database,
        OracleRoomData room,
        long animationTick,
        Action roomTileChanged)
    {
        _npc = RequireBusinessScrub(npc, database);
        _database = database;

        Texture2D bushTexture = room.BuildMimickedMetatileTexture(
            (byte)database.BushTile);
        room.SetPositionTileAndCollision(
            npc.Position,
            (byte)database.FloorTile,
            (byte)database.FloorCollision,
            animationTick,
            preserveRenderedTile: true);
        roomTileChanged();

        npc.SetDialogue(0, string.Empty, canFace: false);
        npc.SetCollisionRadii(
            database.CollisionRadius,
            database.CollisionRadius);
        npc.SetScriptButtonSensitive(true);
        npc.SetSourceGrayscaleInverted(
            database.SourceGrayscaleInverted);
        npc.SetScriptAnimation(database.Animation(0));

        _bush = new Sprite2D
        {
            Name = "BusinessScrubBush",
            Texture = bushTexture,
            Centered = true,
            Position = Vector2.Zero,
            // Subid $80 remains at visible priority $80 while the scrub's
            // priority changes relative to Link, keeping the bush in front.
            ShowBehindParent = false
        };
        npc.AddChild(_bush);
    }

    public void SetTransitionDrawOffset(Vector2 offset)
    {
        _transitionDrawOffset = offset;
        _npc.SetTransitionDrawOffset(offset);
        UpdateBushPosition();
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _npc.AnimateAndUpdateDrawPriorityOneUpdate(frame.Player);

        Vector2 delta = frame.Player.Position - _npc.Position;
        bool near = Mathf.Abs(delta.X) + Mathf.Abs(delta.Y) <
            _database.ProximityRadius;
        if (near && !_linkWasNear)
        {
            _linkWasNear = true;
            _npc.SetScriptAnimation(_database.Animation(1));
        }
        else if (!near && _linkWasNear)
        {
            _linkWasNear = false;
            _npc.SetScriptAnimation(_database.Animation(3));
        }

        UpdateBushPosition();
    }

    public NpcCharacter? FindTalkTarget(Player player) =>
        _linkWasNear &&
        _npc.CanScriptTalkTo(
            player,
            _database.CollisionRadius,
            _database.CollisionRadius,
            _database.AButtonPointOffset)
            ? _npc
            : null;

    private void UpdateBushPosition() =>
        _bush.Position = _transitionDrawOffset + new Vector2(
            0,
            _database.BushOffsetForParameter(
                _npc.CurrentAnimationParameter));

    private static NpcCharacter RequireBusinessScrub(
        NpcCharacter npc,
        BusinessScrubDatabase database)
    {
        if (npc.Record.Implementation !=
                NpcImplementationClassification.SpecializedNative ||
            !database.Matches(npc.Record))
        {
            throw new InvalidOperationException(
                $"NPC {npc.Record.Group}:{npc.Record.Room:x2} " +
                $"${npc.Record.Id:x2}:${npc.Record.SubId:x2} cannot use " +
                "the room 1:81 Business Scrub adapter.");
        }
        return npc;
    }
}
