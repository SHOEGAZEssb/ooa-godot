using Godot;

namespace Oracles;

public partial class Player
{
    private int NativeAdjacentWalls
    {
        get => _inventory.RuntimeState.ReadWramByte(0xd033);
        set => _inventory.RuntimeState.SetWramByte(0xd033, checked((byte)value));
    }

    private int _publishedParentPage = 0xd0, _publishedParentId;
    internal (Texture2D Texture, Rect2? Region, Vector2 Offset)? PublishedParentBody { get; private set; }
    private (CutsceneSpriteRenderer Renderer, ObjectSpriteFrame Frame)? _publishedParentSprite;

    internal void ClearNativeParentAnimationClaims()
    {
        // bank0.clearVar3fForParentItems, independently of parent lifetime.
        for (int page = 0xd2; page < 0xd6; page++)
            _inventory.RuntimeState.SetWramByte((page << 8) | 0x3f, 0);
    }

    private Texture2D ScriptedWalkingTexture(bool damage)
    {
        var body = SelectWalkBodyFrame(0, damage);
        int variant = _inventory.RuntimeState.ReadWramByte(0xd034) / 4;
        return _scriptedWalkTextures[variant * 8 + (int)_facing + (damage ? 4 : 0)] ??=
            new AtlasTexture { Atlas = body.Texture, Region = body.Region };
    }

    internal void SetNativePushAnimationOverride(int value) =>
        _inventory.RuntimeState.SetField("wForceLinkPushAnimation", checked((byte)value));

    private bool NativeWalkingAnimationSelected
    {
        get
        {
            if (!Visible || _deathAnimationActive || _roomWarpFallActive || _newGameSlowFalling ||
                _cutsceneSpriteFrame is not null || _cutsceneIdleFacing is not null || _harpPoseActive && !IsUsingHarp ||
                CollapsedActive && _collapseParameter == 0) return false;
            if (_scriptedLinkAnimationMode is { } mode) return mode == 0x10;
            if (_companionRideControlled || _activeTransformation != 0 || _drowning && !_drownRespawning ||
                _fallingInHole && !_fallInHoleInitializePending && !_fallInHoleRespawning ||
                TopDownSwimming || SideScrollSwimming || _squishAnimation is { State: > 0 } ||
                _ledgeJumpState != LedgeJumpState.None || _getItemOneHandPose || _getItemTwoHandPose ||
                _braceletActionPose is not null || _publishedParentPage != 0xd0) return false;
            return !(_topDownAirborne || _sideScrollAirborne) || _airborneLinkAnimationMode == AirborneLinkAnimationMode.Walk;
        }
    }

    internal void PublishNativeWalkAnimation()
    {
        // This is the one loadLinkAndCompanionAnimationFrame tail publication;
        // producers never select a texture or retain their own push lifetime.
        _world.TilePushingDirection = 0xff;
        bool parentGraphics = Visible && !CollapsedActive && !_deathAnimationActive && !_roomWarpFallActive && !_newGameSlowFalling &&
            _cutsceneSpriteFrame is null && _cutsceneIdleFacing is null && (!_harpPoseActive || IsUsingHarp) &&
            (!_scriptedLinkAnimationMode.HasValue || _forcedState08Phase >= 2) && !_companionRideControlled && _activeTransformation == 0;
        _publishedParentPage = parentGraphics ? ParentItemAnimationState.SelectPage(_inventory.RuntimeState) : 0xd0;
        _publishedParentId = _inventory.RuntimeState.ReadWramByte((_publishedParentPage << 8) | 1);
        _publishedParentSprite = null;
        PublishedParentBody = _publishedParentPage == 0xd0 ? null : SelectPublishedParentBody();
        QueueRedraw();
        if (!NativeWalkingAnimationSelected) return;
        var memory = _inventory.RuntimeState;
        int direction = CarriedObjectMotion.DirectionIndex(FacingVector);
        int walls = direction switch { 0 => 0xc0, 1 => 3, 2 => 0x30, _ => 0x0c };
        int key = direction switch { 0 => 0x40, 1 => 0x10, 2 => 0x80, _ => 0x20 };
        bool wallPush = (NativeAdjacentWalls & walls) == walls && (memory.ReadField("wGameKeysPressed") & key) != 0;
        int grab = _braceletLiftCollisionsDisabled ? 0xc2 : _carriedObjectPose ? 0x83 : 0;
        var selection = LinkWalkingAnimation.Select(new(_world.Underwater, grab,
            (memory.ReadField("wLinkObjectIndex") & 1) != 0 && memory.ReadWramByte(0xd101) == SpecialObjectId.Minecart,
            memory.ReadField("wMagnetGloveState"), memory.ReadField("wUsingShield"),
            memory.ReadField("wLinkTurningDisabled") != 0, memory.ReadField("wForceLinkPushAnimation"),
            SideScrollClimbing, _world.NativeTextActive, wallPush,
            _inventory.EquippedA == TreasureId.Shield || _inventory.EquippedB == TreasureId.Shield, _inventory.ShieldLevel));
        if (selection.WritesVariant) memory.SetWramByte(0xd034, checked((byte)selection.Offset));
        if (selection.PublishesPush) _world.TilePushingDirection = direction;
        if (selection.FacesUp) Face(Vector2I.Up);
    }
}
