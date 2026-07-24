using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// SPECIALOBJECT_MAPLE $0e, states $00-$0c. This owns the entrance flight,
/// collision recoil, item race, result departure, and Ages Touching Book
/// exchange while PART_ITEM_FROM_MAPLE actors remain independent room entities.
/// </summary>
public partial class MapleEncounter : TransitionOffsetNode2D
{
    private MapleEventDatabase _database = null!;
    private MapleEncounterState _encounter = null!;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private OracleSaveData _save = null!;
    private InventoryState _inventory = null!;
    private TreasureDatabase _treasures = null!;
    private Action<int, string, Player> _dialogueRequested = null!;
    private Func<bool> _dialogueOpen = null!;
    private Action<MapleItemRecord, Player> _itemCollected = null!;
    private Action<int> _soundRequested = null!;
    private Action<int> _horizontalShakeRequested = null!;
    private Action<int, int> _roomMusicRequested = null!;
    private EnemyAnimationPlayer _animation = null!;
    private Texture2D _shadowTexture = null!;
    private Vector2 _shadowOffset;
    private Texture2D _bookTexture = null!;
    private Vector2 _precisePosition;
    private MaplePathRecord _path = null!;
    private int _pathStep;
    private int _pathCounter;
    private int _turnDelay;
    private int _turnCounter;
    private int _targetAngle;
    private int _angle;
    private int _speedRaw;
    private int _zFixed;
    private int _speedZ;
    private int _oscillationCounter;
    private int _counter;
    private int _substate;
    private int _variation;
    private int _vehicle;
    private int _dropPattern;
    private int _uniqueItemMask;
    private bool _heartPieceSpawned;
    private bool _mainFlight;
    private bool _raceMusicStarted;
    private bool _screenTransitionsDisabled;
    private bool _menusDisabled;
    private bool _bookExchange;
    private MapleDroppedItem? _targetItem;
    private int _targetItemIndex;
    private int _recoilBounce;
    private bool _risingInitialized;
    private bool _bookFlightStarted;
    private bool _bookVisible;
    private Vector2 _bookPrecisePosition;
    private int _bookZFixed;
    private int _bookSpeedZ;
    private int _bookAngle;
    private bool _bookLanded;
    private bool _holdingOarPose;
    private int _globalFrameCounter;

    internal MapleEncounterStage Stage { get; private set; }
    internal int Substate => _substate;
    internal int Vehicle => _vehicle;
    internal int Variation => _variation;
    internal int DropPattern => _dropPattern;
    internal int Angle => _angle;
    internal int TargetAngle => _targetAngle;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int Counter => _counter;
    internal int AnimationIndex => _animation.AnimationIndex;
    internal int AnimationFrame => _animation.FrameIndex;
    internal Texture2D CurrentTexture => _animation.CurrentTexture;
    internal Texture2D ShadowTexture => _shadowTexture;
    internal Vector2 ShadowOffset => _shadowOffset;
    internal bool ShadowDrawn =>
        _zFixed < 0 && (_globalFrameCounter & 1) == 0;
    internal int MapleScore => _encounter.MapleScore;
    internal int LinkScore => _encounter.LinkScore;
    internal bool MainFlight => _mainFlight;
    internal bool ScreenTransitionsDisabled => _screenTransitionsDisabled;
    internal bool MenusDisabled => _menusDisabled;
    internal bool ObjectsDisabled => _encounter.ObjectsDisabled;
    internal bool Finished { get; private set; }

    internal void Initialize(
        int group,
        MapleEventDatabase database,
        MapleEncounterState encounter,
        OracleRoomData room,
        OracleRandom random,
        OracleSaveData save,
        InventoryState inventory,
        TreasureDatabase treasures,
        Action<int, string, Player> dialogueRequested,
        Func<bool> dialogueOpen,
        Action<MapleItemRecord, Player> itemCollected,
        Action<int> soundRequested,
        Action<int> horizontalShakeRequested,
        Action<int, int> roomMusicRequested)
    {
        Group = group;
        _database = database;
        _encounter = encounter;
        _room = room;
        _random = random;
        _save = save;
        _inventory = inventory;
        _treasures = treasures;
        _dialogueRequested = dialogueRequested;
        _dialogueOpen = dialogueOpen;
        _itemCollected = itemCollected;
        _soundRequested = soundRequested;
        _horizontalShakeRequested = horizontalShakeRequested;
        _roomMusicRequested = roomMusicRequested;

        MapleVisualRecord visual = database.Visual;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(
            OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/{visual.Sprite}.png"),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            positionedOam: true);
        TerrainShadowDefinition shadow = TerrainShadow.Load();
        _shadowTexture = shadow.Texture;
        _shadowOffset = shadow.Offset;
        _bookTexture = LoadBookTexture(database.BookVisual);
        Stage = MapleEncounterStage.Initializing;
        Visible = true;
        QueueRedraw();
    }

    private int Group { get; set; }

    internal void UpdateFrame(
        Player player,
        ICollection<RoomEntitySpawn> spawns,
        int globalFrameCounter)
    {
        if (Finished)
            return;

        _globalFrameCounter = globalFrameCounter & 0xff;
        switch (Stage)
        {
            case MapleEncounterStage.Initializing:
                InitializeState();
                break;
            case MapleEncounterStage.EntryDelay:
                UpdateEntryDelay();
                break;
            case MapleEncounterStage.Flying:
                UpdateFlying(player, spawns);
                break;
            case MapleEncounterStage.Recoiling:
                UpdateRecoiling(player);
                break;
            case MapleEncounterStage.GroundWait:
                UpdateGroundWait();
                break;
            case MapleEncounterStage.Rising:
                UpdateRising(player);
                break;
            case MapleEncounterStage.Racing:
                UpdateRace(player);
                break;
            case MapleEncounterStage.Collecting:
                UpdateCollecting();
                break;
            case MapleEncounterStage.BombStun:
                UpdateBombStun();
                break;
            case MapleEncounterStage.Outcome:
                UpdateOutcome(player);
                break;
            case MapleEncounterStage.BroomSweep:
                UpdateBroomSweep();
                break;
            case MapleEncounterStage.BookExchange:
                UpdateBookExchange(player);
                break;
            case MapleEncounterStage.BookDeparture:
                UpdateBookDeparture(player);
                break;
        }

        UpdateBookFlight(player);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Finished)
            return;

        if (ShadowDrawn)
        {
            DrawTexture(
                _shadowTexture,
                _shadowOffset + TransitionDrawOffset);
        }
        DrawTexture(
            _animation.CurrentTexture,
            _animation.CurrentOffset +
            new Vector2(0, _zFixed >> 8) +
            TransitionDrawOffset);
        if (_bookVisible)
        {
            DrawTexture(
                _bookTexture,
                _bookPrecisePosition - _precisePosition +
                new Vector2(-16, -16 + (_bookZFixed >> 8)) +
                TransitionDrawOffset);
        }
    }

    private void InitializeState()
    {
        int meetings = _save.MapleState & 0x0f;
        _variation = meetings == 0x0f ? 2 : meetings >= 8 ? 1 : 0;
        _vehicle = _variation == 0 ? 0 : _save.IsLinkedGame ? 2 : 1;
        _precisePosition = new Vector2(
            _database.Constant("initial-x"),
            _database.Constant("initial-y"));
        _zFixed = _database.Constant("initial-z") << 8;
        _speedRaw = 0x32;
        _counter = _database.Constant("entry-delay");
        _dropPattern = (_random.Next().Value & 7) == 0 ? 0 : 1;
        StartPath(
            _database.Path(MaplePathKind.Shadow, _dropPattern),
            setPosition: false);
        SetAnimation(0x19);
        Stage = MapleEncounterStage.EntryDelay;
    }

    private void UpdateEntryDelay()
    {
        _counter--;
        if (_counter != 0)
            return;
        Stage = MapleEncounterStage.Flying;
        _soundRequested(OracleSoundEngine.MusMapleTheme);
    }

    private void UpdateFlying(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_dialogueOpen())
        {
            UpdateOscillation();
            _animation.Advance();
            return;
        }
        if (_counter > 0)
        {
            _counter--;
            return;
        }

        bool end = UpdatePathAndMove();
        if (end)
        {
            if (_mainFlight)
            {
                FinishWithoutMeeting();
                return;
            }
            BeginMainFlight();
            return;
        }

        if (!_mainFlight)
            return;

        UpdateOscillation();
        if (PlayerVulnerable(player) && OverlapsPlayer(player))
            CollideWithPlayer(player, spawns);
        else
            _animation.Advance();
    }

    private void BeginMainFlight()
    {
        _mainFlight = true;
        _zFixed = -8 << 8;
        _speedZ = 0x40;
        _oscillationCounter = 0x16;
        _speedRaw = _database.Constant("race-speed-raw");
        _counter = 0x3c;
        int mask = _variation switch { 0 => 3, 1 => 7, _ => 15 };
        int pattern = _database.MovementPattern(
            _random.Next().Value & mask);
        StartPath(
            _database.Path(MaplePathKind.Movement, pattern),
            setPosition: true);
        DecideFlightAnimation();
    }

    private void CollideWithPlayer(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        GenerateDropsOrBook(player, spawns);
        _screenTransitionsDisabled = true;
        _menusDisabled = true;
        int towardLink =
            OracleObjectMath.AngleToward(_precisePosition, player.Position) & 0x18;
        player.ApplyMapleKnockback(_precisePosition);
        _angle = towardLink ^ 0x10;
        _speedRaw = _vehicle switch { 0 => 0x28, 1 => 0x32, _ => 0x3c };
        _recoilBounce = -4;
        _horizontalShakeRequested(
            _database.Constant("horizontal-shake-updates"));
        Stage = MapleEncounterStage.Recoiling;
        SetAnimation(HitAnimation(towardLink));
        _soundRequested(OracleSoundEngine.SndScentSeed);
    }

    private void UpdateRecoiling(Player player)
    {
        if (player.KnockbackFrames <= 0)
            _encounter.ObjectsDisabled = true;

        if (_recoilBounce != 0)
        {
            if (_zFixed == 0)
            {
                _recoilBounce++;
                _speedZ = _recoilBounce << 8;
            }
            OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, 0x40);
            ApplySpeed();
            KeepInBounds();
            if (_room.IsSolid(Position))
            {
                _precisePosition +=
                    OracleObjectMath.StrictCardinalVector(_angle ^ 0x10) * 4;
                KeepInBounds();
            }
            return;
        }

        if (!_encounter.ObjectsDisabled)
            return;
        if (_animation.CurrentParameter != 0xff)
        {
            _animation.Advance();
            return;
        }
        _counter = 0x78;
        Stage = MapleEncounterStage.GroundWait;
    }

    private void UpdateGroundWait()
    {
        _counter--;
        if (_counter == 0)
            Stage = MapleEncounterStage.Rising;
    }

    private void UpdateRising(Player player)
    {
        if (!_risingInitialized)
        {
            _risingInitialized = true;
            _zFixed = -0x100;
            _speedZ = 0x40;
            _oscillationCounter = 0x16;
            _turnDelay = 1;
            _turnCounter = 1;
            _angle ^= 0x10;
            DecideFlightAnimation();
        }

        _zFixed -= 0x100;
        int targetHeight = _vehicle == 0 ? -8 : -24;
        if ((_zFixed >> 8) >= targetHeight)
            return;

        if (_bookExchange)
        {
            Stage = MapleEncounterStage.BookExchange;
            _substate = 0;
            _speedRaw = 0x28;
            _angle = 0xff;
            RequestDialogue(0x070d, player);
            return;
        }

        Stage = MapleEncounterStage.Racing;
        _menusDisabled = false;
        RequestGreeting(player);
        _encounter.ObjectsDisabled = false;
        ChooseNextTarget();
    }

    private void UpdateRace(Player player)
    {
        UpdateOscillation();
        _animation.Advance();
        if (_dialogueOpen())
            return;
        if (!_raceMusicStarted)
        {
            _raceMusicStarted = true;
            _soundRequested(OracleSoundEngine.MusMapleGame);
        }
        if (_targetAngle != _angle)
            NudgeAngleTowardTarget();
        if (!ChooseNextTarget())
            return;

        ApplySpeed();
        KeepInBounds();
        if (_targetItem is null || !_targetItem.CanMapleTarget)
            return;
        int radius = _vehicle == 2 ? 4 : 2;
        Vector2 delta = _targetItem.Position - Position;
        if (Mathf.Abs(delta.X) >= radius + 6 ||
            Mathf.Abs(delta.Y) >= radius + 6)
        {
            return;
        }

        _targetItemIndex = _targetItem.ItemIndex;
        _targetItem.BeginMapleCollection(
            _vehicle, () => _precisePosition);
        Stage = MapleEncounterStage.Collecting;
        if (_vehicle == 0)
        {
            _counter = 0x30;
            if (!_room.IsSolid(Position))
            {
                _zFixed = 0;
                SetAnimation(0x16);
            }
        }
        else
        {
            SetAnimation(_vehicle + 0x16);
        }
    }

    private void UpdateCollecting()
    {
        _animation.Advance();
        if (_targetItem is null ||
            _targetItem.Finished && !_targetItem.MapleCollectionReady)
        {
            Stage = MapleEncounterStage.Racing;
            _angle = _targetAngle;
            _targetItem = null;
            return;
        }
        if (!_targetItem.MapleCollectionReady)
            return;

        _encounter.MapleScore =
            (_encounter.MapleScore +
             _database.Item(_targetItemIndex).Value) & 0xff;
        _targetItem.CompleteMapleCollection();
        _targetItem = null;
        _angle = _targetAngle;
        Stage = _vehicle == 0
            ? MapleEncounterStage.BroomSweep
            : MapleEncounterStage.Racing;
    }

    private void UpdateBroomSweep()
    {
        _animation.Advance();
        _counter--;
        if (_counter != 0)
            return;
        Stage = MapleEncounterStage.Racing;
        SetAnimation(4);
    }

    // No player bomb item exists in the current runtime. The complete original
    // stun state remains represented so a future ITEM_BOMB adapter can enter it
    // without changing Maple's timing or score contract.
    private void UpdateBombStun()
    {
        _animation.Advance();
        switch (_substate)
        {
            case 0 when --_counter == 0:
                _substate = 1;
                _speedZ = 0;
                SetAnimation(0x13);
                break;
            case 1:
                if (OracleObjectMath.UpdateSpeedZ(
                    ref _zFixed, ref _speedZ, 0x40))
                {
                    _substate = 2;
                    _counter = 0x40;
                }
                break;
            case 2 when --_counter == 0:
                _substate = 3;
                SetAnimation(8);
                break;
            case 3:
                _zFixed -= 0x100;
                if ((_zFixed >> 8) < -23)
                {
                    _encounter.MapleScore =
                        (_encounter.MapleScore + 1) & 0xff;
                    _speedZ = 0x40;
                    Stage = MapleEncounterStage.Racing;
                    ChooseNextTarget();
                }
                break;
        }
    }

    private void UpdateOutcome(Player player)
    {
        _animation.Advance();
        switch (_substate)
        {
            case 0:
                if (_dialogueOpen())
                    return;
                _substate = 1;
                int result = _encounter.MapleScore == 0 &&
                    _encounter.LinkScore == 0 ? 0
                    : _encounter.LinkScore == _encounter.MapleScore ? 1
                    : _encounter.LinkScore < _encounter.MapleScore ? 2 : 3;
                RequestDialogue(
                    result switch
                    {
                        0 => 0x070c,
                        1 => 0x0708,
                        2 => 0x0706,
                        _ => 0x0707
                    },
                    player);
                SetAnimation(FacingAnimation(player.Position));
                break;

            case 1:
                UpdateOscillation();
                if (_dialogueOpen())
                    return;
                _encounter.ObjectsDisabled = true;
                _angle = 0x18;
                _speedRaw = _database.Constant("departure-speed-raw");
                _substate = 2;
                SetAnimation(_vehicle * 4 + 7);
                break;

            case 2:
                UpdateOscillation();
                ApplySpeed();
                if (!OracleObjectMath.IsInsideOriginalScreenBoundary(Position))
                    EndEncounter(player);
                break;
        }
    }

    private void UpdateBookExchange(Player player)
    {
        UpdateOscillation();
        _animation.Advance();
        switch (_substate)
        {
            case 0:
                FaceToward(player.Position);
                if (_dialogueOpen())
                    return;
                _save.SetMapleState(_save.MapleState | 0x20);
                if (!_bookLanded)
                    return;
                _substate = 1;
                _targetAngle = OracleObjectMath.AngleToward(
                    _precisePosition, _bookPrecisePosition);
                break;

            case 1:
                if (!MoveDirectlyToBook())
                    return;
                _angle = 0xff;
                _bookVisible = false;
                _soundRequested(OracleSoundEngine.SndGetSeed);
                RequestDialogue(0x070e, player);
                _substate = 2;
                break;

            case 2:
                if (_dialogueOpen())
                    return;
                _bookPrecisePosition = player.Position +
                    new Vector2(player.Position.X < 0x58 ? 0x10 : -0x10, 0);
                _bookZFixed = 0;
                _bookVisible = true;
                _angle = 0;
                _substate = 3;
                break;

            case 3:
                if (MoveDirectlyToBook())
                {
                    _bookVisible = false;
                    Vector2 facing =
                        OracleObjectMath.CardinalVector(_angle ^ 0x10);
                    player.Face(new Vector2I(
                        Mathf.RoundToInt(facing.X),
                        Mathf.RoundToInt(facing.Y)));
                    RequestDialogue(0x070f, player);
                    _substate = 4;
                }
                break;

            case 4:
                if (_dialogueOpen())
                    return;
                RequestDialogue(0x0710, player);
                _substate = 5;
                break;

            case 5:
                if (_dialogueOpen())
                    return;
                TreasureObjectRecord oar =
                    _treasures.GetObject("TREASURE_OBJECT_TRADEITEM_09");
                _inventory.GiveTreasure(oar);
                int sound = _treasures.GetBehaviour(oar.TreasureId).Sound;
                if (sound != 0)
                    _soundRequested(sound);
                player.BeginGetItemTwoHandPose();
                _holdingOarPose = true;
                _soundRequested(OracleSoundEngine.SndGetItem);
                _dialogueRequested(oar.TextId, oar.Message, player);
                _substate = 6;
                break;

            case 6:
                if (_dialogueOpen())
                    return;
                if (_holdingOarPose)
                {
                    player.EndGetItemTwoHandPose();
                    _holdingOarPose = false;
                }
                _counter = 2;
                _substate = 7;
                break;

            case 7:
                _counter--;
                if (_counter != 0)
                    return;
                RequestDialogue(0x0711, player);
                _angle = 0x18;
                _speedRaw = _database.Constant("departure-speed-raw");
                Stage = MapleEncounterStage.BookDeparture;
                _substate = 0;
                break;
        }
    }

    private void UpdateBookDeparture(Player player)
    {
        UpdateOscillation();
        if (_dialogueOpen())
            return;
        ApplySpeed();
        if ((_save.MapleState & 0x10) != 0)
        {
            _save.SetMapleState(_save.MapleState & ~0x10);
            SetAnimation(_vehicle * 4 + 7);
        }
        if (!OracleObjectMath.IsInsideOriginalScreenBoundary(Position))
            EndEncounter(player);
    }

    private void UpdateBookFlight(Player player)
    {
        if (!_bookExchange || _bookLanded)
            return;
        if (!_bookFlightStarted)
        {
            if (player.KnockbackFrames > 0)
                return;
            _bookFlightStarted = true;
            _bookPrecisePosition = _precisePosition;
            _bookZFixed = _zFixed;
            _bookSpeedZ = -0x100;
            _bookAngle = OracleObjectMath.AngleToward(
                _bookPrecisePosition, new Vector2(0x50, 0x38)) & 0x1c;
            _bookVisible = player.Visible;
            _soundRequested(OracleSoundEngine.SndGainHeart);
        }
        if (OracleObjectMath.UpdateSpeedZ(
                ref _bookZFixed, ref _bookSpeedZ, 0x20))
        {
            _bookLanded = true;
            _bookZFixed = 0;
            return;
        }
        _bookPrecisePosition +=
            OracleObjectMath.VectorFromAngle32(_bookAngle);
    }

    private void GenerateDropsOrBook(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) &&
            _inventory.TradeItem == 0x08)
        {
            _bookExchange = true;
            _bookVisible = true;
            _bookPrecisePosition = _precisePosition;
            _bookZFixed = _zFixed;
            _save.SetMapleState(_save.MapleState | 0x10);
            return;
        }

        _encounter.MapleScore = 0;
        _encounter.LinkScore = 0;
        int mapleItems = 5;
        int safety = 0;
        while (mapleItems > 0)
        {
            if (++safety > 2048)
                throw new InvalidOperationException(
                    "Maple unique-drop selection failed to converge.");
            int index = ChooseDistribution(_database.Distribution(
                _dropPattern == 0
                    ? MapleDistributionKind.Rare
                    : MapleDistributionKind.Standard));
            if (!CanMapleDrop(index))
                continue;
            AddDrop(index, _precisePosition, _zFixed, spawns);
            mapleItems--;
        }

        int attempts = 0x20;
        int linkItems = 5;
        while (attempts-- > 0 && linkItems > 0)
        {
            int selected = ChooseDistribution(
                _database.Distribution(MapleDistributionKind.Link));
            if (!_inventory.TryTakeMapleDrop(selected, out int actual))
                continue;
            AddDrop(actual, player.Position, 0, spawns);
            linkItems--;
        }
    }

    private bool CanMapleDrop(int index)
    {
        int requiredTreasure = index switch
        {
            >= 5 and <= 9 => TreasureDatabase.TreasureEmberSeeds + index - 5,
            10 => TreasureDatabase.TreasureBombs,
            _ => InventoryState.TreasurePunch
        };
        if (requiredTreasure != InventoryState.TreasurePunch &&
            !_inventory.HasTreasure(requiredTreasure))
        {
            return false;
        }
        if (index >= 5)
            return true;
        if (index == 0)
        {
            if ((_save.MapleState & 0x80) != 0 || _heartPieceSpawned)
                return false;
            _heartPieceSpawned = true;
            return true;
        }

        int mask = _database.Item(index).UniqueMask;
        if ((_uniqueItemMask & mask) != 0)
            return false;
        _uniqueItemMask |= mask;
        return true;
    }

    private void AddDrop(
        int index,
        Vector2 sourcePosition,
        int sourceZFixed,
        ICollection<RoomEntitySpawn> spawns)
    {
        spawns.Add(new MapleDroppedItemSpawn(
            _database.Item(index),
            _encounter,
            _encounter.AllocateSlot(),
            sourcePosition,
            sourceZFixed,
            UpdateThisFrame: true));
    }

    private int ChooseDistribution(IReadOnlyList<int> weights)
    {
        int value = _random.Next().Value;
        for (int index = 0; index < weights.Count; index++)
        {
            value -= weights[index];
            if (value < 0)
                return index;
        }
        throw new InvalidOperationException(
            "Maple probability distribution did not sum to $100.");
    }

    private void RequestGreeting(Player player)
    {
        int meetings = _save.MapleState & 0x0f;
        if (Group == 1)
        {
            if (meetings == 0)
            {
                _save.SetGlobalFlag(OracleSaveData.GlobalFlagMapleMetInPast);
                RequestDialogue(0x0712, player);
                return;
            }
            if (!_save.HasGlobalFlag(
                    OracleSaveData.GlobalFlagMapleMetInPast))
            {
                _save.SetGlobalFlag(OracleSaveData.GlobalFlagMapleMetInPast);
                RequestDialogue(0x0713, player);
                return;
            }
        }

        int textId = meetings switch
        {
            0 => 0x0700,
            >= 5 => 0x0705,
            _ => 0x0701 + (_random.Next().Value & 3)
        };
        RequestDialogue(textId, player);
    }

    private void RequestDialogue(int textId, Player player) =>
        _dialogueRequested(textId, _database.Text(textId), player);

    private bool ChooseNextTarget()
    {
        MapleDroppedItem? target = _encounter.ChooseTarget(
            _precisePosition.Y, _precisePosition.X);
        if (target is null)
        {
            _targetItem = null;
            Stage = MapleEncounterStage.Outcome;
            _substate = 0;
            return false;
        }
        _targetItem = target;
        _targetAngle = OracleObjectMath.AngleToward(
            _precisePosition, target.Position);
        return true;
    }

    private bool UpdatePathAndMove()
    {
        if (_targetAngle != _angle)
        {
            NudgeAngleTowardTarget();
        }
        else
        {
            _pathCounter--;
            if (_pathCounter == 0)
            {
                _pathStep++;
                if (_pathStep >= _path.Steps.Count)
                    return true;
                MaplePathStep step = _path.Steps[_pathStep];
                _targetAngle = step.Angle;
                _pathCounter = step.Duration;
                DecideFlightAnimation();
            }
        }
        ApplySpeed();
        return false;
    }

    private void StartPath(MaplePathRecord path, bool setPosition)
    {
        _path = path;
        if (setPosition)
        {
            _precisePosition = new Vector2(path.StartX, path.StartY);
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        }
        _turnDelay = path.TurnDelay;
        _turnCounter = path.TurnDelay;
        _pathStep = 0;
        _angle = path.Steps[0].Angle;
        _targetAngle = path.Steps[0].Angle;
        _pathCounter = path.Steps[0].Duration;
    }

    private void NudgeAngleTowardTarget()
    {
        _turnCounter--;
        if (_turnCounter != 0)
            return;
        _turnCounter = _turnDelay;
        int difference = (_angle - _targetAngle) & 0x1f;
        if (difference == 0)
            return;
        _angle = difference < 0x10
            ? (_angle - 1) & 0x1f
            : (_angle + 1) & 0x1f;
        DecideFlightAnimation();
    }

    private void FaceToward(Vector2 target)
    {
        int next = DirectionFromAngle(
            OracleObjectMath.AngleToward(_precisePosition, target));
        SetAnimation(_vehicle * 4 + 4 + next);
    }

    private bool MoveDirectlyToBook()
    {
        Vector2 target = OracleObjectMath.ToPixelPosition(
            _bookPrecisePosition);
        if (Position == target)
            return true;
        _targetAngle = OracleObjectMath.AngleToward(
            _precisePosition, _bookPrecisePosition);
        _angle = _targetAngle;
        DecideFlightAnimation();
        ApplySpeed();
        Vector2 remaining = target - Position;
        if (Mathf.Abs(remaining.X) <= 1 &&
            Mathf.Abs(remaining.Y) <= 1)
        {
            _precisePosition = target;
            Position = target;
        }
        return false;
    }

    private int FacingAnimation(Vector2 target) =>
        _vehicle * 4 + 4 + DirectionFromAngle(
            OracleObjectMath.AngleToward(_precisePosition, target));

    private void DecideFlightAnimation()
    {
        if (!_mainFlight && Stage < MapleEncounterStage.Recoiling)
            return;
        int index = _vehicle * 4 + 4 + DirectionFromAngle(_angle);
        if (_animation.AnimationIndex != index)
            SetAnimation(index);
    }

    private static int DirectionFromAngle(int angle) =>
        (((angle + 4) & 0x1f) / 8) & 3;

    private int HitAnimation(int towardLink)
    {
        int value = ((towardLink + 4) * 4) & 0xff;
        value = ((value >> 4) | (value << 4)) & 0xff;
        value = ((value & 1) ^ 1) + 0x10;
        return value + _vehicle * 2;
    }

    private void SetAnimation(int index)
    {
        _animation.SetAnimation(index);
        QueueRedraw();
    }

    private void ApplySpeed()
    {
        Vector2 next =
            _precisePosition +
            OracleObjectMath.VectorFromAngle32(_angle) *
            (_speedRaw / 40.0f);
        // Object.y/Object.x are unsigned 8.8 coordinates. Several imported
        // flight paths intentionally begin at $f0 and cross $ff->$00 to enter
        // from the opposite screen edge.
        _precisePosition = new Vector2(
            Mathf.PosMod(next.X, 0x100),
            Mathf.PosMod(next.Y, 0x100));
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
    }

    private void KeepInBounds()
    {
        _precisePosition = new Vector2(
            Mathf.Clamp(_precisePosition.X, 8, 152.999f),
            Mathf.Clamp(_precisePosition.Y, 32, 120.999f));
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
    }

    private void UpdateOscillation()
    {
        if (_vehicle == 2)
            return;
        _zFixed += _speedZ;
        _oscillationCounter--;
        if (_oscillationCounter != 0)
            return;
        _oscillationCounter = 0x16;
        _speedZ = -_speedZ;
    }

    private bool OverlapsPlayer(Player player) =>
        Mathf.Abs(player.Position.X - Position.X) < 14 &&
        Mathf.Abs(player.Position.Y - Position.Y) < 14;

    private static bool PlayerVulnerable(Player player) =>
        !player.IsDying &&
        !player.IsDrowning &&
        !player.IsFallingInHole &&
        player.InvincibilityFrames <= 0;

    private void EndEncounter(Player player)
    {
        if (_holdingOarPose)
        {
            player.EndGetItemTwoHandPose();
            _holdingOarPose = false;
        }
        int state = _save.MapleState;
        int meetings = state & 0x0f;
        if (meetings < 0x0f)
            meetings++;
        state = (state & 0xf0) | meetings;
        state &= ~0x10;
        _save.SetMapleState(state);
        FinishCommon();
    }

    private void FinishWithoutMeeting() => FinishCommon();

    private void FinishCommon()
    {
        _encounter.ObjectsDisabled = false;
        _menusDisabled = false;
        _screenTransitionsDisabled = false;
        _roomMusicRequested(Group, _room.Id);
        Finished = true;
        Visible = false;
        QueueRedraw();
    }

    private static Texture2D LoadBookTexture(MapleBookVisualRecord visual)
    {
        AnimationFrameDefinition frame =
            OracleGraphicsCache.GetAnimationDefinition(
                visual.Animation).Frames[0];
        return NpcCharacter.BuildOamTexture(
            OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/{visual.Sprite}.png"),
            frame.EncodedOam,
            visual.TileBase,
            visual.Palette);
    }
}

internal enum MapleEncounterStage
{
    Initializing = 0,
    EntryDelay = 1,
    Flying = 2,
    Recoiling = 3,
    GroundWait = 4,
    Rising = 5,
    Racing = 6,
    Collecting = 7,
    BombStun = 8,
    Outcome = 9,
    BroomSweep = 10,
    BookExchange = 11,
    BookDeparture = 12
}
