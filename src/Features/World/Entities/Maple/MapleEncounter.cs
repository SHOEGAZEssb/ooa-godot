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
    private Func<MapleDroppedItemSpawn, bool>? _createPart;
    internal void BindPartCreation(Func<MapleDroppedItemSpawn, bool> create) => _createPart = create;
    private Func<MapleTouchingBookSpawn, MapleTouchingBookRoomEntity?>? _createBook;
    internal void BindBookCreation(Func<MapleTouchingBookSpawn, MapleTouchingBookRoomEntity?> create) => _createBook = create;
    private MapleTouchingBookRoomEntity? _book;
    internal MapleTouchingBookRoomEntity? TouchingBook => _book;
    private OracleRuntimeState? _nativeMemory;
    internal void BindNativeMemory(OracleRuntimeState memory) => _nativeMemory = memory;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private OracleSaveData _save = null!;
    private InventoryState _inventory = null!;
    private Action<int, string, Player> _dialogueRequested = null!;
    private Func<bool> _dialogueOpen = null!;
    private Action<int> _soundRequested = null!;
    private Action<int> _horizontalShakeRequested = null!;
    private Action<int, int> _roomMusicRequested = null!;
    private EnemyAnimationPlayer _animation = null!;
    private Texture2D _shadowTexture = null!;
    private Vector2 _shadowOffset;
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
    private int _bookDirection;
    private MapleDroppedItem? _targetItem;
    private int _targetItemIndex;
    private int _recoilBounce;
    private bool _risingInitialized;
    private bool _bombPullActive;
    private Action? _updateBombPull;
    internal void BindBombPull(Action update) => _updateBombPull = update;
    private int _globalFrameCounter;

    internal MapleEncounterStage Stage { get; private set; }
    internal int Substate => _substate;
    internal int Vehicle => _vehicle;
    internal int Variation => _variation;
    internal int DropPattern => _dropPattern;
    internal int Angle => _angle;
    internal int TargetAngle => _targetAngle;
    internal int AnimationIndex => _animation.AnimationIndex;
    internal Texture2D CurrentTexture => _animation.CurrentTexture;
    internal Texture2D ShadowTexture => _shadowTexture;
    internal Vector2 ShadowOffset => _shadowOffset;
    internal bool ShadowDrawn =>
        _zFixed < 0 && (_globalFrameCounter & 1) == 0;
    internal int MapleScore => _encounter.MapleScore;
    internal int LinkScore => _encounter.LinkScore;
    internal bool MainFlight => _mainFlight;
    internal Vector2 BookTargetPosition => _precisePosition;
    internal bool BookTradeAccepted => (_save.MapleState & 0x20) != 0;
    internal bool BookTextActive => _dialogueOpen();
    internal int BookDirection { get => _bookDirection; set => _bookDirection = value & 0xff; }
    internal void SetBookMovementAngle(int angle) => _angle = angle & 0xff;
    internal void ShowBookText(int textId, Player player) => RequestDialogue(textId, player);
    internal void SignalBookDeparture() => _substate = 2;
    internal bool ScreenTransitionsDisabled => (_nativeMemory?.ReadWramByte(WramAddress.wDisableScreenTransitions) ?? 0) != 0;
    internal bool MenusDisabled => (_nativeMemory?.ReadWramByte(WramAddress.wMenuDisabled) ?? 0) != 0;
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
        Action<int, string, Player> dialogueRequested,
        Func<bool> dialogueOpen,
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
        _dialogueRequested = dialogueRequested;
        _dialogueOpen = dialogueOpen;
        _soundRequested = soundRequested;
        _horizontalShakeRequested = horizontalShakeRequested;
        _roomMusicRequested = roomMusicRequested;

        InitializeVisuals();
        Stage = MapleEncounterStage.Initializing;
        // checkAndSpawnMaple does not set visible. Unlike ordinary companion
        // initialization, Maple's state0 is gated by scrolling too.
        Visible = false;
        QueueRedraw();
    }

    private void InitializeVisuals()
    {
        MapleVisualRecord visual = _database.Visual;
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
    }

    private int Group { get; set; }

    internal void UpdateFrame(
        Player player,
        ICollection<RoomEntitySpawn> spawns,
        int globalFrameCounter,
        bool menusDisabled = false)
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
                UpdateEntryDelay(menusDisabled);
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
        _speedRaw = ObjectSpeed.Speed140;
        _counter = _database.Constant("entry-delay");
        _dropPattern = (_random.Next().Value & 7) == 0 ? 0 : 1;
        StartPath(
            _database.Path(MaplePathKind.Shadow, _dropPattern),
            setPosition: false);
        SetAnimation(0x19);
        Stage = MapleEncounterStage.EntryDelay;
        Visible = true; // mapleState0: objectSetVisiblec0, after setting XYZ.
    }

    private void UpdateEntryDelay(bool menusDisabled)
    {
        _counter--;
        if (_counter != 0)
            return;
        Stage = MapleEncounterStage.Flying;
        // maple.s: mapleState1 checks wMenuDisabled only after mapleState4's
        // third decrement, then deletes without incrementing the meeting count.
        if (menusDisabled)
        {
            FinishWithoutMeeting();
            return;
        }
        _soundRequested(SoundId.MusMapleTheme);
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

        if (PlayerVulnerable(player) && OverlapsPlayer(player))
        {
            CollideWithPlayer(player, spawns);
            return; // mapleCollideWithLink skips oscillation on this visit.
        }
        UpdateOscillation();
        _animation.Advance();
    }

    private void BeginMainFlight()
    {
        _mainFlight = true;
        _zFixed = (-8 << 8) | (_zFixed & 0xff);
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
        _nativeMemory?.SetWramByte(WramAddress.wDisableScreenTransitions, 1);
        _nativeMemory?.SetWramByte(WramAddress.wMenuDisabled, 1);
        int towardLink =
            OracleObjectMovement.Shared.RelativeAngle(
                _precisePosition, player.Position) & ObjectAngle.CardinalMask;
        player.ApplyMapleKnockback(_precisePosition);
        _angle = towardLink ^ 0x10;
        _speedRaw = _vehicle switch { 0 => ObjectSpeed.Speed100, 1 => ObjectSpeed.Speed140, _ => ObjectSpeed.Speed180 };
        _recoilBounce = -4;
        _horizontalShakeRequested(
            _database.Constant("horizontal-shake-updates"));
        Stage = MapleEncounterStage.Recoiling;
        SetAnimation(HitAnimation(towardLink));
        _soundRequested(SoundId.SndScentSeed);
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
                // mapleState3 replaces speedZ's high byte only; the impact
                // fraction survives into the next bounce.
                _speedZ = unchecked((short)((_recoilBounce << 8) | (_speedZ & 0xff)));
            }
            OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, 0x40);
            ApplySpeed();
            KeepInBounds();
            if (_room.IsSolid(Position))
            {
                _precisePosition +=
                    OracleObjectMath.StrictCardinalVector(_angle ^ ObjectAngle.HalfTurn) * 4;
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
            _zFixed = -0x100 | (_zFixed & 0xff);
            _speedZ = 0x40;
            _oscillationCounter = 0x16;
            _turnDelay = 1;
            _turnCounter = 1;
            _angle ^= 0x10;
            DecideFlightAnimation();
        }

        _zFixed = unchecked((short)(_zFixed - 0x100));
        // mapleState5 yields for zh >= $f9/$e9, then completes on
        // $f8/$e8. Comparing against -8/-24 delayed the handoff one visit.
        int waitHeight = _vehicle == 0 ? 0xf9 : 0xe9;
        if (((_zFixed >> 8) & 0xff) >= waitHeight)
            return;

        if ((_save.MapleState & 0x10) != 0)
        {
            Stage = MapleEncounterStage.BookExchange;
            _substate = 0;
            _speedRaw = ObjectSpeed.Speed100;
            _angle = 0xff;
            _bookDirection = 0x0b;
            RequestDialogue(0x070d, player);
            return;
        }

        Stage = MapleEncounterStage.Racing;
        _nativeMemory?.SetWramByte(WramAddress.wMenuDisabled, 0);
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
            _soundRequested(SoundId.MusMapleGame);
        }
        if (_targetAngle != _angle)
            NudgeAngleTowardTarget();
        // mapleState6 continues after the selector changes state to $09.
        ChooseNextTarget();
        ApplySpeed();
        if (_targetItem is null || _targetItem.Finished)
        {
            KeepInBounds();
            return;
        }
        int radius = _vehicle == 2 ? 4 : 2;
        if (!RoomEntityManager.ObjectCollisionXYOverlaps(
            new Rect2(Position - Vector2.One * radius, Vector2.One * (radius * 2)), _targetItem.CollisionBounds))
        {
            KeepInBounds();
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
        // mapleState7 runs vacuum/bomb work in the companion pass, before
        // Link and ITEM/PART. Its ordinary item handoff still runs while a
        // bomb approaches; only the bomb's actual stun releases the target.
        _bombPullActive = false;
        if (_vehicle == 1) _updateBombPull?.Invoke();
        if (Stage == MapleEncounterStage.BombStun) return;
        if (_targetItem is null ||
            _targetItem.Finished && !_targetItem.MapleCollectionReady || _targetItem.AttachedToItem)
        {
            if (_bombPullActive) return;
            Stage = MapleEncounterStage.Racing;
            _angle = _targetAngle;
            return;
        }
        if (!_targetItem.MapleCollectionReady)
            return;

        _encounter.MapleScore =
            (_encounter.MapleScore +
             _database.Item(_targetItemIndex).Value) & 0xff;
        _targetItem.CompleteMapleCollection();
        if (_bombPullActive) return;
        _angle = _targetAngle;
        Stage = _vehicle == 0
            ? MapleEncounterStage.BroomSweep
            : MapleEncounterStage.Racing;
    }

    internal bool OverlapsBomb(BombEffect bomb) =>
        _bombPullActive ||
        (Mathf.Abs(bomb.Position.X - Position.X) < 14 &&
         Mathf.Abs(bomb.Position.Y - Position.Y) < 12);

    internal void BeginBombPull()
    {
        if (_bombPullActive)
            return;
        _bombPullActive = true;
    }

    internal void BeginBombStun()
    {
        if (!_bombPullActive)
            return;
        _bombPullActive = false;
        _targetItem?.ReleaseFromMaple();
        _targetItem = null;
        Stage = MapleEncounterStage.BombStun;
        _substate = 0;
        _counter = 0x20;
        SetAnimation(0x1a);
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
                _nativeMemory?.SetWramByte(WramAddress.wTextIsActive, 0x80);
                _nativeMemory?.SetWramByte(WramAddress.wDisabledObjects, 0x1f);
                _angle = ObjectAngle.Left;
                _speedRaw = _database.Constant("departure-speed-raw");
                _substate = 2;
                SetAnimation(_vehicle * 4 + 7);
                break;

            case 2:
                UpdateOscillation();
                ApplySpeed();
                if (!OracleObjectMath.IsInsideOriginalScreenBoundary(
                        OracleObjectMath.NormalizeSourceScreenPosition(Position)))
                    EndEncounter(player);
                break;
        }
    }

    private void UpdateBookExchange(Player player)
    {
        // The INTERAC$a5 owner drives approach angles and trade states.
        // mapleStateB owns facing/oscillation and its two-visit departure wait.
        if (_substate != 0)
        {
            if (--_substate != 0) return;
            RequestDialogue(0x0711, player);
            _angle = ObjectAngle.Left;
            _speedRaw = _database.Constant("departure-speed-raw");
            Stage = MapleEncounterStage.BookDeparture;
            UpdateBookDeparture(player); // Native stateB falls into stateC.
            return;
        }
        UpdateOscillation();
        int direction = (_bookDirection & 0x80) != 0 ? _bookDirection & 3 :
            DirectionFromAngle(OracleObjectMovement.Shared.RelativeAngle(_precisePosition, player.Position));
        if ((_bookDirection & 0x80) != 0 || direction != _bookDirection)
        {
            if ((_bookDirection & 0x80) == 0) _bookDirection = direction;
            SetAnimation(_vehicle * 4 + 4 + direction);
        }
        if (_dialogueOpen()) return;
        _save.SetMapleState(_save.MapleState | 0x20);
        if ((_angle & 0x80) == 0) ApplySpeed();
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
        if (!OracleObjectMath.IsInsideOriginalScreenBoundary(
                OracleObjectMath.NormalizeSourceScreenPosition(Position)))
            EndEncounter(player);
    }

    private void GenerateDropsOrBook(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_inventory.HasTreasure(TreasureId.TradeItem) &&
            _inventory.TradeItem == 0x08)
        {
            // mapleSpawnItemDrops returns on failed checked INTERAC$a5
            // creation; bit4 and the book continuation require success.
            var create = _createBook ?? throw new InvalidOperationException(
                "mapleSpawnItemDrops INTERAC $a5 requires the shared interaction allocator.");
            _book = create(new(this, _precisePosition, _zFixed, _database.BookVisual));
            if (_book is not null)
            {
                _save.SetMapleState(_save.MapleState | 0x10);
            }
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
            bool admitted = AddDrop(index, _precisePosition, _zFixed, spawns);
            // mapleSpawnItemDrop_variant returns the copied Z high byte's
            // flags on success. With nonzero airborne Z, a unique item does
            // not decrement counter1; a failed allocation sets carry and does.
            if (!admitted || index >= 5 || unchecked((byte)(_zFixed >> 8)) == 0)
                mapleItems--;
        }

        int attempts = 0x20;
        int linkItems = 5;
        // mapleSpawnItemDrops decrements hFF8C before the zero test, so
        // an exhausted $20 counter permits $1f selections.
        while (--attempts > 0 && linkItems > 0)
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
            >= 5 and <= 9 => TreasureId.EmberSeeds + index - 5,
            10 => TreasureId.Bombs,
            _ => TreasureId.Punch
        };
        if (requiredTreasure != TreasureId.Punch &&
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

    private bool AddDrop(
        int index,
        Vector2 sourcePosition,
        int sourceZFixed,
        ICollection<RoomEntitySpawn> spawns)
    {
        var spawn = new MapleDroppedItemSpawn(
            _database.Item(index),
            _encounter,
            0, // Replaced by the authoritative shared PART slot at admission.
            sourcePosition,
            sourceZFixed,
            UpdateThisFrame: true);
        if (_createPart is not null) return _createPart(spawn);
        spawns.Add(spawn);
        return true;
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
                _save.SetGlobalFlag(GlobalFlag.MapleMetInPast);
                RequestDialogue(0x0712, player);
                return;
            }
            if (!_save.HasGlobalFlag(
                    GlobalFlag.MapleMetInPast))
            {
                _save.SetGlobalFlag(GlobalFlag.MapleMetInPast);
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
            // The selector changes state but preserves relatedObj2. State6
            // still moves and checks that retained object's collision, even
            // when its subid bit7 excluded it from target selection.
            Stage = MapleEncounterStage.Outcome;
            _substate = 0;
            return false;
        }
        _targetItem = target;
        _targetAngle = OracleObjectMovement.Shared.RelativeAngle(
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
            // Source path changes copy only the coordinate high bytes.
            _precisePosition = new Vector2(path.StartX, path.StartY) +
                (_precisePosition - _precisePosition.Floor());
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
        int difference = (_angle - _targetAngle) & ObjectAngle.Mask;
        if (difference == 0)
            return;
        _angle = difference < 0x10
            ? (_angle - 1) & ObjectAngle.Mask
            : (_angle + 1) & ObjectAngle.Mask;
        DecideFlightAnimation();
    }

    private int FacingAnimation(Vector2 target) =>
        _vehicle * 4 + 4 + DirectionFromAngle(
            OracleObjectMovement.Shared.RelativeAngle(_precisePosition, target));

    private void DecideFlightAnimation()
    {
        if (!_mainFlight && Stage < MapleEncounterStage.Recoiling)
            return;
        int index = _vehicle * 4 + 4 + DirectionFromAngle(_angle);
        if (_animation.AnimationIndex != index)
            SetAnimation(index);
    }

    private static int DirectionFromAngle(int angle) =>
        (((angle + 4) & ObjectAngle.Mask) / 8) & 3;

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
        // objectApplySpeed performs wrapping 16-bit adds. Several imported
        // flight paths intentionally begin at $f0 and cross $ff->$00.
        Position = NativeObjectMovement.ApplySpeed(_nativeMemory,
            ref _precisePosition, _speedRaw, _angle);
    }

    private void KeepInBounds()
    {
        _precisePosition = MapleScreenBounds.Clamp(_precisePosition);
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
        // objectCheckCollidedWithLink_ignoreZ includes the negative summed-
        // radius edge and excludes the positive edge, with byte arithmetic.
        Player.EnemyCollisionOverlaps(player.Position,
            new Rect2(Position - Vector2.One * 8, Vector2.One * 16));

    private bool PlayerVulnerable(Player player) =>
        // Ages calls checkLinkVulnerableAndIDZero before the ignore-Z scan.
        // In particular, negative shield-recoil invincibility is nonzero too.
        player.NativePuzzleResetVulnerable && player.ActiveTransformation == 0 && !player.BabyRequested &&
        !player.CompanionRideActive && !player.MinecartRideActive && !player.RaftRideActive &&
        !player.IsDrowning &&
        !player.IsFallingInHole &&
        (_nativeMemory?.ReadWramByte(WramAddress.wMenuDisabled) ?? 0) == 0 &&
        (_nativeMemory?.ReadWramByte(WramAddress.wDisableLinkCollisionsAndMenu) ?? 0) == 0;

    private void EndEncounter(Player player)
    {
        int state = _save.MapleState;
        int meetings = state & 0x0f;
        if (meetings < 0x0f)
            meetings++;
        state = (state & 0xf0) | meetings;
        state &= ~0x10;
        _save.SetMapleState(state);
        // mapleEndEncounter clears the menu byte also written by INTERAC$a5.
        // The entrance abort jumps straight to mapleDeleteSelf and preserves it.
        _nativeMemory?.SetWramByte(WramAddress.wMenuDisabled, 0);
        _nativeMemory?.SetWramByte(WramAddress.wTextIsActive, 0);
        _nativeMemory?.SetWramByte(WramAddress.wDisabledObjects, 0);
        _nativeMemory?.SetWramByte(WramAddress.wDisableScreenTransitions, 0);
        FinishCommon();
    }

    private void FinishWithoutMeeting() => FinishCommon();

    private void FinishCommon()
    {
        _roomMusicRequested(Group, _room.Id);
        Finished = true;
        Visible = false;
        QueueRedraw();
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
