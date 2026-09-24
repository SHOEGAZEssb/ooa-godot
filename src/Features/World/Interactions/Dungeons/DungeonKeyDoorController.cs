using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Implements nextToKeyBlock for imported dungeon tile $1e and nextToKeyDoor
/// for small-key tiles $70-$73 and boss-key tiles $74-$77. Both paths check
/// the active dungeon's corresponding key. Doors record both sides of the
/// dungeon-layout adjacency and use the ordinary six-update interleaved-door
/// frame; key blocks immediately become standard floor and set room flag $80.
/// Only a small key is consumed.
/// </summary>
public partial class DungeonKeyDoorController : Node
{
    private readonly RoomSession _rooms;
    private readonly InventoryState _inventory;
    private readonly RoomEntityManager _entities;
    private readonly TreasureDatabase _treasures;
    private readonly DungeonKeyBlockDatabase _keyBlocks = new();
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private int _pushCounter;
    private int _candidatePosition = -1;
    private Vector2I _candidateDirection;
    private bool _opening;
    private bool _outgoing;
    private int _openingCounter;
    private double _openingTicks;
    private Vector2 _doorCenter;
    private DungeonKeyDoorDatabaseRecord _door;
    private OpeningState _openingState;
    private enum OpeningState { Initialize, Script, Ready, Animate, ScriptEnd }

    public event Action<string>? MessageRequested;

    internal bool Opening => _opening;
    internal int RemainingPushFrames => _pushCounter;
    internal int OpeningCounter => _openingCounter;

    public DungeonKeyDoorController(
        RoomSession rooms,
        InventoryState inventory,
        RoomEntityManager entities,
        TreasureDatabase treasures,
        Func<long> animationTick,
        Action<int> playSound)
    {
        _rooms = rooms;
        _inventory = inventory;
        _entities = entities;
        _treasures = treasures;
        _animationTick = animationTick;
        _playSound = playSound;
        _pushCounter = DefaultPushCounter;
        _entities.ReservedKeyDoor = this;
        _rooms.RoomChanged += (_, _) =>
        {
            if (!_outgoing) Cancel();
        };
    }

    private int DefaultPushCounter => _keyBlocks.Record.PushCounter;

    public void UpdatePushAttempt(
        Vector2 linkPosition,
        Vector2I facing,
        Vector2 movementInput)
    {
        if (!InteractableTilePushGeometry.TryGetCardinalInput(
                movementInput, out Vector2I direction) ||
            direction != facing ||
            !InteractableTilePushGeometry.IsAlignedForPush(linkPosition) ||
            !TryGetFrontTile(
                linkPosition, direction, out int position,
                out Vector2 center, out byte tile))
        {
            ResetPushCounter();
            return;
        }

        DungeonKeyBlockDatabaseRecord keyBlock = _keyBlocks.Record;
        int activeCollisions = _rooms.CurrentRoom.ActiveCollisions;
        if (tile == keyBlock.ClosedTile &&
            _keyBlocks.SupportsActiveCollisions(activeCollisions))
        {
            if (_candidatePosition != position ||
                _candidateDirection != direction)
            {
                _candidatePosition = position;
                _candidateDirection = direction;
                _pushCounter = keyBlock.PushCounter;
            }

            _pushCounter--;
            if (_pushCounter == 0)
                TryOpenKeyBlock(center, keyBlock);
            return;
        }

        if (!_rooms.KeyDoors.TryGet(
                activeCollisions, tile,
                out DungeonKeyDoorDatabaseRecord door) ||
            door.Direction != direction)
        {
            ResetPushCounter();
            return;
        }

        if (_candidatePosition != position || _candidateDirection != direction)
        {
            _candidatePosition = position;
            _candidateDirection = direction;
            _pushCounter = door.PushCounter;
        }

        // nextToKeyDoor calls decPushingAgainstTileCounter, then decrements
        // the same byte once more when it is still nonzero. The imported
        // initial value is 20, so a continuously pushed door activates on
        // the tenth original update.
        if (!PushingAgainstTileCounter.DecrementTwiceToZero(
                ref _pushCounter))
            return;

        TryOpen(center, door);
    }

    public override void _PhysicsProcess(double delta) => Advance(delta);

    internal void Advance(double delta, Player? player = null, bool interactionsDisabled = false)
    {
        if (!_opening || _outgoing || delta <= 0.0)
            return;

        _openingTicks += delta * OracleSoundEngine.UpdatesPerSecond;
        while (_opening && _openingTicks >= 1.0)
        {
            _openingTicks -= 1.0;
            bool text = _entities.TextActiveSource();
            if (_entities.SwitchHook?.ExchangeState == 2 ||
                _openingState != OpeningState.Initialize && (text || interactionsDisabled))
                continue;
            bool scriptPaused = text || player?.IsDying == true;
            if (_openingState is OpeningState.Initialize or OpeningState.Script)
            {
                // INTERAC$1e:$00 initializes, then executes incstate. That
                // script command yields; state2 starts on the next update.
                _openingState = scriptPaused ? OpeningState.Script : OpeningState.Ready;
                continue;
            }
            if (_openingState == OpeningState.ScriptEnd)
            {
                if (!scriptPaused) _opening = false;
                continue;
            }
            if (_entities.DoorPaletteFadeActive) continue;
            if (_openingState == OpeningState.Ready)
            {
                // @state2Substate0 skips animation if another interaction has
                // already cleared collision. Holes also count as passable.
                if (!_rooms.CurrentRoom.IsSolid(_doorCenter))
                {
                    _openingState = OpeningState.ScriptEnd;
                    if (!scriptPaused) _opening = false;
                    continue;
                }
                _rooms.CurrentRoom.SetInterleavedMetatile(
                    _doorCenter, _door.OpenTile, _door.ClosedTile,
                    InteractableTilePushGeometry.DirectionIndex(_door.Direction), _animationTick());
                PlayDoorSoundIfVisible();
                _openingCounter = _door.DoorFrameWait;
                _openingState = OpeningState.Animate;
                continue;
            }
            _openingCounter--;
            if (_openingCounter != 0)
                continue;

            _rooms.CurrentRoom.SetPositionTileAndCollision(
                _doorCenter, _door.OpenTile, null, _animationTick());
            PlayDoorSoundIfVisible();
            _openingState = OpeningState.ScriptEnd;
            if (!scriptPaused) _opening = false;
        }
    }

    internal void Cancel()
    {
        _opening = false;
        _outgoing = false;
        _openingCounter = 0;
        _openingTicks = 0.0;
        ResetPushCounter();
    }

    internal void BeginScreenTransition() => _outgoing = _opening;

    internal void UpdateDuringScreenTransition()
    {
        // setInteractionsEnabledTo2 includes reserved0. Scroll mode$08
        // dispatches only state0, whose handler deletes enabled02 first.
        if (_outgoing && _openingState == OpeningState.Initialize) Cancel();
    }

    internal void FinishScreenTransition()
    {
        if (_outgoing) Cancel();
    }

    private void PlayDoorSoundIfVisible()
    {
        if (OracleObjectMath.IsInsideOriginalScreenBoundary(_entities.WorldToScreen(_doorCenter)))
            _playSound(_door.DoorSound);
    }

    private bool TryGetFrontTile(
        Vector2 linkPosition,
        Vector2I direction,
        out int position,
        out Vector2 center,
        out byte tile)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 frontPoint = linkPosition +
            InteractableTilePushGeometry.FrontTileOffset(direction);
        position = room.GetPackedPosition(frontPoint);
        int tileX = position & 0x0f;
        int tileY = position >> 4;
        center = new Vector2(
            tileX * OracleRoomData.MetatileSize + 8,
            tileY * OracleRoomData.MetatileSize + 8);
        tile = room.GetMetatile(frontPoint);
        return tile != 0xff;
    }

    private void TryOpenKeyBlock(
        Vector2 center,
        DungeonKeyBlockDatabaseRecord keyBlock)
    {
        int dungeon = _rooms.CurrentDungeonIndex;
        if (!_inventory.TryUseDungeonSmallKey(dungeon))
        {
            MessageRequested?.Invoke(keyBlock.NoKeyMessage);
            ResetPushCounter();
            return;
        }

        TryCreateKeySprite(center, keyBlock.KeyGraphic);
        _rooms.CurrentRoom.SetPositionTileAndCollision(
            center, keyBlock.OpenTile, null, _animationTick());
        _playSound(keyBlock.OpenSound);
        _rooms.SaveData.SetRoomFlag(
            _rooms.ActiveGroup,
            _rooms.CurrentRoom.Id,
            keyBlock.RoomFlag);
        // nextToKeyBlock tries these allocations independently, key first.
        // Failure does not undo the key debit, floor tile or room flag.
        if (_entities.InteractionSlotAvailable)
            _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(center, keyBlock.PuffSound));
        ResetPushCounter();
    }

    private void TryOpen(
        Vector2 center,
        DungeonKeyDoorDatabaseRecord door)
    {
        int dungeon = _rooms.CurrentDungeonIndex;
        bool hasKey = door.UsesBossKey
            ? _inventory.HasDungeonBossKey(dungeon)
            : _inventory.TryUseDungeonSmallKey(dungeon);
        if (!hasKey)
        {
            MessageRequested?.Invoke(door.NoKeyMessage);
            ResetPushCounter();
            return;
        }

        // nextToKeyDoor checks reserved INTERACTION0 only after key debit.
        // A busy opener skips new effects/flags but still resets the push timer.
        if (_opening)
        {
            ResetPushCounter();
            return;
        }

        int group = _rooms.ActiveGroup;
        int room = _rooms.CurrentRoom.Id;
        if (!_rooms.TryGetNeighbor(door.Direction, out int neighbor))
        {
            throw new InvalidOperationException(
                $"Dungeon-key door ${door.ClosedTile:x2} in room {group:x1}:{room:x2} " +
                "has no matching dungeon-layout neighbor.");
        }

        _rooms.SaveData.SetRoomFlag(group, room, door.RoomFlag);
        _rooms.SaveData.SetRoomFlag(group, neighbor, door.OppositeRoomFlag);
        TryCreateKeySprite(center, door.KeyGraphic);

        _doorCenter = center;
        _door = door;
        _openingState = OpeningState.Initialize;
        _openingCounter = 0;
        _openingTicks = 0.0;
        _opening = true;
        ResetPushCounter();
    }

    private void TryCreateKeySprite(Vector2 center, int graphic)
    {
        // createKeySpriteInteraction returns on getFreeInteractionSlot failure.
        if (_entities.InteractionSlotAvailable)
            _entities.Spawn<DungeonKeyUseEffect>(new DungeonKeyUseSpawn(center, _treasures.GetObjectVisual(graphic)));
    }

    private void ResetPushCounter()
    {
        _pushCounter = DefaultPushCounter;
        _candidatePosition = -1;
        _candidateDirection = Vector2I.Zero;
    }

}
