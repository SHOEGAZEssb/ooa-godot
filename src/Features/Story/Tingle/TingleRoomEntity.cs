using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_TINGLE $c8:$00 and its linked PART_TINGLE_BALLOON $44. The root
/// keeps both source objects in one collision/lifetime owner while exposing
/// only grounded Tingle to the NPC interaction pass.
/// </summary>
internal sealed partial class TingleRoomEntity : Node2D,
    IRoomEntity,
    IFixedRoomEntity,
    IScreenTransitionPreloadRoomEntity,
    IAlwaysUpdateDuringScreenTransitionRoomEntity,
    IUpdatesDuringDialogueRoomEntity,
    IRoomBlocker,
    ITalkTarget,
    IOrdinaryNpcEntity,
    ISwordHittableRoomEntity,
    IItemCollisionHittableRoomEntity,
    IObjectCollisionHeightRoomEntity
{
    private readonly TingleDatabase _database;
    private readonly TingleRecord _record;
    private readonly NpcCharacter _tingle;
    private readonly NpcCharacter _balloon;
    private int _state;
    private int _counter;
    private int _zFixed;
    private int _speedZ;
    private bool _balloonActive = true;
    private bool _koolooActive;
    private bool _koolooJumpStarted;

    public Node2D Node => this;
    public NpcCharacter Npc => _tingle;
    internal bool Grounded => _state == 4;
    internal bool BalloonActive => _balloonActive;
    internal int State => _state;
    internal int ZFixed => _zFixed;
    internal int BalloonCounter => _counter;
    internal int BalloonSpeedZ => _speedZ;
    internal bool KoolooActive => _koolooActive;
    internal bool KoolooComplete { get; private set; }
    public int CollisionZ => _zFixed >> 8;

    internal bool HasEnoughSeedTypes { get; }

    internal TingleRoomEntity(
        NpcRecord record,
        TingleDatabase database,
        InventoryState inventory)
    {
        if (!database.Matches(record) || record.Implementation !=
            NpcImplementationClassification.SpecializedNative)
        {
            throw new InvalidOperationException(
                $"NPC {record.Group:x1}:{record.Room:x2} " +
                $"${record.Id:x2}:${record.SubId:x2} cannot use Tingle's native owner.");
        }

        _database = database;
        _record = database.Record;
        int seedTypes = 0;
        for (int treasure = TreasureDatabase.TreasureEmberSeeds;
             treasure <= TreasureDatabase.TreasureEmberSeeds + 4;
             treasure++)
        {
            if (inventory.HasTreasure(treasure))
                seedTypes++;
        }
        HasEnoughSeedTypes = seedTypes >= _record.SeedThreshold;
        _zFixed = _record.InitialZ << 8;
        _speedZ = _record.BalloonSpeedZ;
        _counter = _record.BalloonCounter;

        _tingle = new NpcCharacter
        {
            Name = "Tingle",
            // objectSetVisiblec0 fixes airborne Tingle at source priority $00.
            // His first grounded interactionAnimateAsNpc update replaces this
            // with Link-relative priority.
            ZIndex = NpcCharacter.InFrontOfLinkZIndex
        };
        _tingle.Initialize(record);
        _tingle.SetScriptAnimation(database.Animation("tingle", 0));
        _tingle.SetScriptButtonSensitive(false);
        _tingle.SetBlocksLink(false);
        _tingle.SetScriptDrawOffset(new Vector2(0, _record.InitialZ));

        string balloonAnimation = database.Animation("balloon", 0);
        NpcRecord balloonRecord = record with
        {
            TextId = 0,
            TileBase = _record.BalloonTileBase,
            Palette = _record.BalloonPalette,
            DefaultAnimation = 0,
            CanFace = false,
            UpAnimation = balloonAnimation,
            RightAnimation = balloonAnimation,
            DownAnimation = balloonAnimation,
            LeftAnimation = balloonAnimation,
            Message = string.Empty
        };
        _balloon = new NpcCharacter
        {
            Name = "TingleBalloon",
            ZIndex = NpcCharacter.InFrontOfLinkZIndex
        };
        _balloon.Initialize(balloonRecord);
        _balloon.SetScriptAnimation(balloonAnimation);
        _balloon.SetBlocksLink(false);
        _balloon.SetScriptDrawOffset(new Vector2(0, _record.InitialZ));

        AddChild(_tingle);
        AddChild(_balloon);
        Name = "TingleInteraction";
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        return ScreenTransitionPresentation.Visible;
    }

    public void SetTransitionDrawOffset(Vector2 offset)
    {
        _tingle.SetTransitionDrawOffset(offset);
        _balloon.SetTransitionDrawOffset(offset);
    }

    public void UpdateDuringScreenTransition() =>
        AdvancePhysical(player: null, spawns: null);

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        AdvancePhysical(frame.Player, spawns);

    public bool BlocksLink(Vector2 linkCenter) =>
        Grounded && _tingle.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Grounded && !_koolooActive &&
        _tingle.CanScriptTalkTo(
            player,
            NpcCharacter.CollisionRadius,
            NpcCharacter.CollisionRadius,
            NpcCharacter.AButtonPointOffset)
            ? _tingle
            : null;

    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = sourcePosition;
        _ = damage;
        _ = knockbackStrength;
        if (!_balloonActive || _state != 1)
            return false;
        Rect2 collision = new(
            _balloon.Position - new Vector2(4, 4),
            new Vector2(8, 8));
        if (!collision.Intersects(hitbox))
            return false;

        _balloonActive = false;
        _balloon.SetScriptVisible(false);
        _state = 2;
        spawns.Add(new TingleBalloonExplosionSpawn(
            _balloon.Position + new Vector2(
                _record.ExplosionXOffset,
                _record.ExplosionYOffset),
            _zFixed >> 8));
        return true;
    }

    public bool ApplyItemCollision(
        RoomEntityItemCollision collision,
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!_record.BalloonAcceptsItemCollision((int)collision))
            return false;
        return ApplySwordHit(
            hitbox,
            sourcePosition,
            damage,
            EnemyKnockbackStrength.Normal,
            spawns);
    }

    internal void SetInteractionEnabled(bool enabled) =>
        _tingle.SetScriptButtonSensitive(enabled && Grounded);

    internal void StartKooloo()
    {
        if (!Grounded)
            throw new InvalidOperationException("Airborne Tingle cannot start kooloo-limpah.");
        _koolooActive = true;
        KoolooComplete = false;
        _koolooJumpStarted = false;
        _zFixed = 0;
        _speedZ = 0;
        _tingle.SetScriptAnimation(_database.Animation("tingle", 3));
    }

    private void AdvancePhysical(
        Player? player,
        ICollection<RoomEntitySpawn>? spawns)
    {
        switch (_state)
        {
            case 0:
                // interaction state 0 and balloon state 0 both execute on
                // their first ordered update.
                _state = 1;
                return;
            case 1:
                AdvanceBalloon();
                return;
            case 2:
                _state = 3;
                _counter = _record.FallWait;
                _speedZ = 0;
                _tingle.SetScriptAnimation(_database.Animation("tingle", 2));
                return;
            case 3:
                if (_counter > 0)
                {
                    _counter--;
                    return;
                }
                if (!OracleObjectMath.UpdateSpeedZ(
                        ref _zFixed, ref _speedZ, _record.FallGravity))
                {
                    UpdateZOffsets();
                    return;
                }
                _state = 4;
                _zFixed = 0;
                _speedZ = 0;
                _tingle.SetScriptAnimation(_database.Animation("tingle", 1));
                _tingle.SetScriptButtonSensitive(true);
                _tingle.SetBlocksLink(true);
                UpdateZOffsets();
                return;
            case 4:
                if (player is null)
                    _tingle.AdvanceAnimationUpdates(1);
                else
                    _tingle.AnimateAsNpcOneUpdate(player);
                AdvanceKooloo(spawns);
                return;
            default:
                throw new InvalidOperationException(
                    $"INTERAC_TINGLE entered unsupported state ${_state:x2}.");
        }
    }

    private void AdvanceBalloon()
    {
        if (!_balloonActive)
            return;
        _counter--;
        if (_counter == 0)
        {
            _counter = _record.BalloonCounter;
            _speedZ = unchecked((short)-_speedZ);
        }
        OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0);
        _balloon.AdvanceAnimationUpdates(1);
        UpdateZOffsets();
    }

    private void AdvanceKooloo(ICollection<RoomEntitySpawn>? spawns)
    {
        if (!_koolooActive)
            return;
        int parameter = _tingle.CurrentAnimationParameter;
        if ((parameter & 0x01) != 0 && !_koolooJumpStarted)
        {
            _koolooJumpStarted = true;
            _speedZ = _record.KoolooSpeedZ;
            if (spawns is null)
            {
                throw new InvalidOperationException(
                    "INTERAC_TINGLE $c8:$00 cannot create its three " +
                    "INTERAC_SPARKLE $84:$00 children during a transition.");
            }
            foreach (Vector2 offset in _record.KoolooSparkleOffsets)
            {
                spawns.Add(new TingleKoolooSparkleSpawn(
                    _tingle.Position + offset,
                    _record.KoolooSparkleAngle,
                    _database.KoolooSparkleVisual));
            }
        }

        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, _record.KoolooGravity);
        UpdateZOffsets();
        if (!landed || (parameter & 0x80) == 0)
            return;

        _koolooActive = false;
        KoolooComplete = true;
        _tingle.SetScriptAnimation(_database.Animation("tingle", 1));
    }

    private void UpdateZOffsets()
    {
        Vector2 offset = new(0, _zFixed >> 8);
        _tingle.SetScriptDrawOffset(offset);
        if (_balloonActive)
            _balloon.SetScriptDrawOffset(offset);
    }
}
