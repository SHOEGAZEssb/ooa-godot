using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_GASHA_SPOT $b6. Room graphics own the sprout/tree; this object owns
/// soft-soil interaction, the slashable nut, Link's reward pose, and the
/// nine-frame background-tile disappearance sequence.
/// </summary>
internal partial class GashaSpotInteraction : TransitionOffsetNode2D
{

    private const float PlantRadius = 10.0f;
    private const float LinkRadius = 6.0f;
    private const float NutReadyRadius = 4.0f;
    private const float NutAirborneRadius = 6.0f;

    private GashaSpotDatabase _database = null!;
    private OracleRoomData _room = null!;
    private OracleSaveData _save = null!;
    private InventoryState? _inventory;
    private Action<GashaSpotInteraction, Player> _interactionRequested = null!;
    private Action<GashaSpotInteraction, Player> _nutCaught = null!;
    private Action<int> _soundRequested = null!;
    private Action _roomTileChanged = null!;
    private Func<long> _animationTick = null!;
    private Texture2D? _texture;
    private Vector2 _textureOffset;
    private Vector2 _precisePosition;
    private int _zFixed;
    private int _speedZ;
    private int _angle;
    private int _disappearanceCounter;
    private int _disappearancePhase;
    private Player? _heldBy;
    private bool _initialUpdateComplete;

    internal SpotRecord Spot { get; private set; }
    internal GashaSpotDatabase Database => _database;
    internal OracleSaveData Save => _save;
    internal InteractionState State { get; private set; }
    internal bool RestrictsPlayer => State is
        InteractionState.NutAirborne or
        InteractionState.AwaitingNutText or
        InteractionState.RewardHeld or
        InteractionState.Disappearing;
    internal bool Finished => State == InteractionState.Finished;
    internal int RewardType { get; private set; } = -1;
    internal int ZFixed => _zFixed;
    internal int DisappearancePhase => _disappearancePhase;

    internal void Initialize(
        GashaSpotDatabase database,
        SpotRecord spot,
        OracleRoomData room,
        OracleSaveData save,
        InventoryState? inventory,
        Action<GashaSpotInteraction, Player> interactionRequested,
        Action<GashaSpotInteraction, Player> nutCaught,
        Action<int> soundRequested,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _database = database;
        Spot = spot;
        _room = room;
        _save = save;
        _inventory = inventory;
        _interactionRequested = interactionRequested;
        _nutCaught = nutCaught;
        _soundRequested = soundRequested;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;

        if (!save.IsGashaSpotPlanted(spot.SubId))
        {
            Position = spot.Position;
            _precisePosition = Position;
            State = InteractionState.WaitingForPlant;
            Visible = false;
            return;
        }

        if (save.GetGashaSpotKillCounter(spot.SubId) < database.NutKills)
            throw new InvalidOperationException("An immature Gasha spot cannot create its interaction.");
        Position = spot.Position + new Vector2(8, -8);
        _precisePosition = Position;
        State = InteractionState.NutReady;
        Visible = false;
    }

    internal bool TryInteract(Player player)
    {
        if (State != InteractionState.WaitingForPlant ||
            !_initialUpdateComplete ||
            _room.GetMetatile(Spot.Position) != _database.SoftSoilTile)
            return false;
        Vector2 delta = OracleObjectMath.ToPixelPosition(player.Position) -
            OracleObjectMath.ToPixelPosition(Spot.Position);
        if (Mathf.Abs(delta.X) >= PlantRadius + LinkRadius ||
            Mathf.Abs(delta.Y) >= PlantRadius + LinkRadius)
        {
            return false;
        }
        _interactionRequested(this, player);
        return true;
    }

    internal bool Plant()
    {
        if (State != InteractionState.WaitingForPlant ||
            _room.GetMetatile(Spot.Position) != _database.SoftSoilTile ||
            _inventory?.ConsumeGashaSeed() != true)
        {
            return false;
        }
        _room.SetPositionTileAndCollision(
            Spot.Position, (byte)_database.PlantedSoilTile, 0, _animationTick());
        _save.SetGashaSpotPlanted(Spot.SubId, true);
        _save.SetGashaSpotKillCounter(Spot.SubId, 0);
        _soundRequested(OracleSoundEngine.SndGetSeed);
        _roomTileChanged();
        State = InteractionState.Finished;
        return true;
    }

    internal bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition)
    {
        if (State != InteractionState.NutReady)
            return false;
        Rect2 bounds = new(
            Position - new Vector2(NutReadyRadius, NutReadyRadius),
            new Vector2(NutReadyRadius * 2, NutReadyRadius * 2));
        if (!bounds.Intersects(hitbox))
            return false;

        State = InteractionState.NutAirborne;
        _zFixed = 0;
        _speedZ = _database.NutSpeedZ;
        _angle = OracleObjectMath.AngleToward(Position, sourcePosition);
        Visible = true;
        return true;
    }

    internal void UpdateFrame(Player player)
    {
        if (!_initialUpdateComplete)
        {
            _initialUpdateComplete = true;
            if (State == InteractionState.WaitingForPlant)
            {
                if (_inventory is not null && RingEffects.DetectsSoftSoil(_inventory))
                    _soundRequested(OracleSoundEngine.SndCompass);
            }
            else if (State == InteractionState.NutReady)
            {
                SetVisual(_database.NutVisual);
                Visible = true;
            }
            return;
        }

        switch (State)
        {
            case InteractionState.NutAirborne:
                if (player.HealthQuarters <= 0)
                {
                    State = InteractionState.Finished;
                    Visible = false;
                    return;
                }
                Vector2 delta = OracleObjectMath.ToPixelPosition(player.Position) -
                    OracleObjectMath.ToPixelPosition(Position);
                if (Mathf.Abs(delta.X) < NutAirborneRadius + LinkRadius &&
                    Mathf.Abs(delta.Y) < NutAirborneRadius + LinkRadius &&
                    !player.IsCarryingObject && !player.IsHoldingItemOneHand &&
                    !player.IsHoldingItemTwoHands)
                {
                    State = InteractionState.AwaitingNutText;
                    Visible = false;
                    _nutCaught(this, player);
                    return;
                }
                _precisePosition += OracleObjectMath.VectorFromAngle32(_angle) *
                    (_database.NutSpeedRaw / 40.0f);
                Position = OracleObjectMath.ToPixelPosition(_precisePosition);
                OracleObjectMath.UpdateSpeedZ(
                    ref _zFixed, ref _speedZ, _database.NutGravity);
                QueueRedraw();
                return;

            case InteractionState.RewardHeld when _heldBy is not null:
                Position = _heldBy.Position + new Vector2(0, -13);
                QueueRedraw();
                return;

            case InteractionState.Disappearing:
                UpdateDisappearance();
                return;
        }
    }

    internal void BeginReward(
        int rewardType,
        RewardRecord reward,
        Player player)
    {
        if (State != InteractionState.AwaitingNutText || reward.Type != rewardType)
            throw new InvalidOperationException("Gasha reward began from the wrong state.");
        RewardType = rewardType;
        _heldBy = player;
        player.BeginGetItemTwoHandPose();
        Position = player.Position + new Vector2(0, -13);
        _zFixed = 0;
        SetVisual(reward.Visual);
        Visible = true;
        State = InteractionState.RewardHeld;
        if (rewardType != 0)
            _soundRequested(OracleSoundEngine.SndGetItem);
    }

    internal void BeginDisappearance()
    {
        if (State != InteractionState.RewardHeld || _heldBy is null)
            throw new InvalidOperationException("Gasha disappearance began without a held reward.");
        _heldBy.EndGetItemTwoHandPose();
        _heldBy = null;
        Position = Spot.Position;
        Visible = false;
        _soundRequested(OracleSoundEngine.SndFairyCutscene);
        _database.BeginDisappearance(_room, Spot, _animationTick());
        _roomTileChanged();
        State = InteractionState.Disappearing;
        _disappearanceCounter = _database.DisappearancePeriod - 1;
        _disappearancePhase = 0;
    }

    private void UpdateDisappearance()
    {
        _disappearanceCounter--;
        if (_disappearanceCounter > 0)
            return;
        _disappearanceCounter = _database.DisappearancePeriod;
        _disappearancePhase++;
        if (_disappearancePhase <= _database.DisappearancePhases)
        {
            _database.ApplyDisappearanceFrame(
                _room, Spot, _disappearancePhase, _animationTick());
            _roomTileChanged();
            return;
        }

        _save.SetGashaSpotPlanted(Spot.SubId, false);
        _database.CompleteHarvest(_room, Spot, _animationTick());
        _roomTileChanged();
        State = InteractionState.Finished;
    }

    private void SetVisual(GashaSpotDatabaseVisualRecord visual)
    {
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{visual.Sprite}.png");
        AnimationDefinition animation =
            OracleGraphicsCache.GetAnimationDefinition(visual.Animation);
        if (animation.Frames.Length == 0)
            throw new InvalidOperationException("Gasha visual has no animation frame.");
        (_texture, _textureOffset) = NpcCharacter.BuildPositionedOamTexture(
            source, animation.Frames[0].EncodedOam,
            visual.TileBase, visual.Palette,
            paletteOverride: null,
            sourceGrayscaleInverted: true);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Visible && _texture is not null)
        {
            DrawTexture(
                _texture,
                _textureOffset + new Vector2(0, _zFixed >> 8) +
                TransitionDrawOffset);
        }
    }
}

internal enum InteractionState
{
    WaitingForPlant,
    NutReady,
    NutAirborne,
    AwaitingNutText,
    RewardHeld,
    Disappearing,
    Finished
}
