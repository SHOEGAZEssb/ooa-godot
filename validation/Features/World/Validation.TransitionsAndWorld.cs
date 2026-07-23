using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void LoadValidationRoom(int group, int room)
    {
        LoadDebugRoom(group, room);
        _player.WarpTo(FindSpawn());
        _player.Face(Vector2I.Down);
    }

    private void LoadSignValidationRoom()
    {
        LoadDebugRoom(0, 0x2a);
        _player.WarpTo(new Vector2(5 * OracleRoomData.MetatileSize + 8, 70));
        _player.Face(Vector2I.Up);
    }

    private void LoadBushValidationRoom()
    {
        LoadDebugRoom(0, 0x69);
        Vector2 bushPoint = new(24, 56);
        _rooms.CurrentRoom.ReplaceMetatile(
            bushPoint, 0x3a, 0xc5, (long)_animationTicks);
        _player.WarpTo(new Vector2(bushPoint.X, 70));
        _player.Face(Vector2I.Up);
    }

    private void LoadHouseValidationRoom()
    {
        LoadDebugRoom(0, 0x47);
        _player.WarpTo(new Vector2(5 * OracleRoomData.MetatileSize + 8, 54));
        _player.Face(Vector2I.Up);
    }

    private void LoadNpcValidationRoom()
    {
        LoadDebugRoom(0, 0x48);
        _player.WarpTo(new Vector2(0x38, 0x58));
        _player.Face(Vector2I.Up);
    }

    private void LoadChestValidationRoom()
    {
        LoadDebugRoom(0, 0x49);
        _interactions.ResetChestForTesting(0, 0x49, 0x51);
        _player.WarpTo(new Vector2(24, 100));
        _player.Face(Vector2I.Up);
    }

    private void ValidateHouseWarp()
    {
        LoadHouseValidationRoom();
        for (float y = 54; y >= 47; y--)
        {
            if (Collides(new Vector2(88, y)))
                throw new InvalidOperationException($"The path into exterior door $25 is blocked at y={y}.");
        }
        _player.WarpTo(new Vector2(88, 47));
        if (!CheckTileWarp(_player) || _activeGroup != 2 || _currentRoom.Id != 0xea)
            throw new InvalidOperationException(
                $"Expected exterior 0:47/$25 to enter house 2:ea, got {_activeGroup}:{_currentRoom.Id:x2}.");
        if (!IsTransitioning || !Mathf.IsEqualApprox(_player.Position.Y, _currentRoom.Height))
            throw new InvalidOperationException("House entry did not begin at the bottom edge of the interior.");
        UpdateRoomWarpTransition(WarpEnterFrames / 60.0);
        if (!IsTransitioning || !Mathf.IsEqualApprox(_player.Position.Y, _currentRoom.Height - WarpEnterFrames))
            throw new InvalidOperationException("Link did not perform the 28-frame interior entry walk.");
        UpdateRoomWarpTransition((WarpFadeFrames - WarpEnterFrames) / 60.0);
        if (IsTransitioning)
            throw new InvalidOperationException("The 32-frame room fade did not finish after entering the house.");
        if (_saveData.RespawnGroup != 2 || _saveData.RespawnRoom != 0xea ||
            _saveData.RespawnFacing != 0 || _saveData.RespawnY != 0x64 ||
            _saveData.RespawnX != 0x50)
        {
            throw new InvalidOperationException(
                "TRANSITION_DEST_ENTERSCREEN did not record house 2:ea's final entry checkpoint.");
        }

        for (float y = _player.Position.Y; y <= _currentRoom.Height + 2; y++)
        {
            if (Collides(new Vector2(_currentRoom.Width / 2.0f, y)))
                throw new InvalidOperationException($"The house's bottom exit is blocked at y={y}.");
        }
        _player.WarpTo(new Vector2(_currentRoom.Width / 2.0f, _currentRoom.Height + 2));
        CheckRoomExit(_player);
        if (!IsTransitioning || _activeGroup != 2 || _currentRoom.Id != 0xea)
            throw new InvalidOperationException("The house exit did not begin with its scripted walk offscreen.");
        UpdateRoomWarpTransition(WarpLeaveFrames / 60.0);
        if (_activeGroup != 0 || _currentRoom.Id != 0x47 || !IsTransitioning)
            throw new InvalidOperationException("The exterior was not loaded after the 16-frame exit walk.");
        UpdateRoomWarpTransition(WarpFadeFrames / 60.0);
        if (_activeGroup != 0 || _currentRoom.Id != 0x47 ||
            _currentRoom.GetPackedPosition(_player.Position) != 0x35)
            throw new InvalidOperationException(
                $"Expected house 2:ea bottom exit to step out below 0:47/$25, got " +
                $"{_activeGroup}:{_currentRoom.Id:x2}/${_currentRoom.GetPackedPosition(_player.Position):x2}.");
        if (Collides(_player.Position + Vector2.Down))
            throw new InvalidOperationException("The exterior landing spot below 0:47/$25 is blocked.");
        if (_saveData.RespawnGroup != 0 || _saveData.RespawnRoom != 0x47 ||
            _saveData.RespawnFacing != 2 || _saveData.RespawnY != 0x38 ||
            _saveData.RespawnX != 0x58 ||
            !OracleSaveData.TryDeserialize(_saveData.Serialize(), out OracleSaveData? exteriorSave) ||
            exteriorSave!.RespawnGroup != 0 || exteriorSave.RespawnRoom != 0x47 ||
            exteriorSave.RespawnY != 0x38 || exteriorSave.RespawnX != 0x58)
        {
            throw new InvalidOperationException(
                "TRANSITION_DEST_SET_RESPAWN did not persist exterior 0:47's stepped-out checkpoint.");
        }

        int checkpointGroup = _saveData.RespawnGroup;
        int checkpointRoom = _saveData.RespawnRoom;
        int checkpointY = _saveData.RespawnY;
        int checkpointX = _saveData.RespawnX;

        _activeGroup = 2;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0xeb);
        _roomView.SetRoom(_currentRoom.Texture);
        _player.WarpTo(new Vector2(-2, _currentRoom.Height / 2.0f));
        CheckRoomExit(_player);
        if (_activeGroup != 2 || _currentRoom.Id != 0xea)
            throw new InvalidOperationException(
                $"Expected room 2:eb left edge to scroll to 2:ea, got {_activeGroup}:{_currentRoom.Id:x2}.");
        ValidateLinkScrollsForOneTransitionFrame();
        FinishActiveScrollingTransitionForValidation();
        if (_currentRoom.GetPackedPosition(_player.Position) != 0x49)
            throw new InvalidOperationException(
                $"Expected Link to finish 2:eb -> 2:ea near the right edge, got " +
                $"${_currentRoom.GetPackedPosition(_player.Position):x2}.");
        if (_saveData.RespawnGroup != checkpointGroup || _saveData.RespawnRoom != checkpointRoom ||
            _saveData.RespawnY != checkpointY || _saveData.RespawnX != checkpointX)
        {
            throw new InvalidOperationException(
                "An ordinary scrolling transition incorrectly replaced the death checkpoint.");
        }

        GD.Print("Validated original house entry/exit fades, destination checkpoint updates, " +
            "save-image round trip, and non-checkpoint 2:eb -> 2:ea scrolling.");
    }

    private void ValidateCaveWarps()
    {
        ValidateLargeRoomCaveWarp(0x21, 0x04);
        ValidateLargeDungeonTopTransition();
        ValidateLargeRoomCaveWarp(0x28, 0xce);
        GD.Print("Validated 0:48 cave entries and dungeon00 room 4:04 -> 4:03 top transition.");
    }

    private void ValidateLargeDungeonTopTransition()
    {
        float exitX = -1.0f;
        for (float x = 8.0f; x < _currentRoom.Width; x++)
        {
            if (!Collides(new Vector2(x, -2.0f)))
            {
                exitX = x;
                break;
            }
        }
        if (exitX < 0.0f)
            throw new InvalidOperationException("Could not find 4:04's open northern dungeon exit.");

        _player.WarpTo(new Vector2(exitX, -2.0f));
        CheckRoomExit(_player);
        if (_activeGroup != 4 || _currentRoom.Id != 0x03 || !_scrollTransitionActive)
            throw new InvalidOperationException(
                $"Expected dungeon00 room 4:04 north to lead to 4:03, got {_activeGroup}:{_currentRoom.Id:x2}.");
        if (_scrollTransitionFrames != 32 || !Mathf.IsEqualApprox(_scrollTransitionDistance, 128.0f))
            throw new InvalidOperationException("Large-room vertical scrolling did not use the 128px playfield distance.");

        FinishActiveScrollingTransitionForValidation();
        if (Mathf.Abs(WorldToScreen(_player.Position).Y - 134.0f) > 0.01f)
            throw new InvalidOperationException("Link did not finish 4:04 -> 4:03 at the lower playfield edge.");
    }

    private void ValidateLargeRoomCaveWarp(int sourcePosition, int destinationRoom)
    {
        LoadNpcValidationRoom();
        int tileX = sourcePosition & 0x0f;
        int tileY = (sourcePosition >> 4) & 0x0f;
        _player.WarpTo(new Vector2(
            tileX * OracleRoomData.MetatileSize + 8,
            tileY * OracleRoomData.MetatileSize + 8));
        if (!CheckTileWarp(_player) || _activeGroup != 4 || _currentRoom.Id != destinationRoom)
            throw new InvalidOperationException(
                $"Expected 0:48/${sourcePosition:x2} to enter 4:{destinationRoom:x2}, got " +
                $"{_activeGroup}:{_currentRoom.Id:x2}.");
        int expectedWidth = OracleRoomData.LargeRoomWidthInTiles * OracleRoomData.MetatileSize;
        int expectedHeight = OracleRoomData.LargeRoomHeightInTiles * OracleRoomData.MetatileSize;
        if (_currentRoom.Width != expectedWidth || _currentRoom.Height != expectedHeight ||
            _currentRoom.Texture.GetWidth() != expectedWidth ||
            _currentRoom.Texture.GetHeight() != expectedHeight)
            throw new InvalidOperationException(
                $"Expected 4:{destinationRoom:x2} to use the original 240x176 playable large-room dimensions.");
        if (_player.Position != new Vector2(0x78, 0xb0))
            throw new InvalidOperationException(
                $"Expected the original large-room entry coordinate $b0/$78, got {_player.Position}.");

        UpdateRoomCamera();
        if (WorldToScreen(_player.Position).DistanceSquaredTo(new Vector2(80, 144)) > 0.01f)
            throw new InvalidOperationException(
                $"Link did not begin the 4:{destinationRoom:x2} cave entry at screen position (80,144).");
        UpdateRoomWarpTransition(WarpEnterFrames / 60.0);
        UpdateRoomCamera();
        if (WorldToScreen(_player.Position).DistanceSquaredTo(new Vector2(80, 116)) > 0.01f)
            throw new InvalidOperationException(
                $"Link did not finish the 28-frame 4:{destinationRoom:x2} cave entry at screen position (80,116).");
        UpdateRoomWarpTransition((WarpFadeFrames - WarpEnterFrames) / 60.0);
        if (IsTransitioning)
            throw new InvalidOperationException($"The 4:{destinationRoom:x2} cave fade did not finish.");

        _player.WarpTo(new Vector2(_currentRoom.Width - 1, _currentRoom.Height / 2.0f));
        UpdateRoomCamera();
        if (Mathf.Abs(WorldToScreen(new Vector2(_currentRoom.Width, 0)).X -
            OracleRoomData.ViewportWidth) > 0.01f)
        {
            throw new InvalidOperationException(
                $"The 4:{destinationRoom:x2} camera exposed the padded 16th large-room column.");
        }
        if (!_collision.Collides(new Vector2(
            OracleRoomData.LargeRoomWidthInTiles * OracleRoomData.MetatileSize + 5,
            _currentRoom.Height / 2.0f)))
        {
            throw new InvalidOperationException(
                $"The 4:{destinationRoom:x2} padded 16th large-room column allowed Link out of bounds.");
        }
    }

    private void ValidateStartupTransition()
    {
        if (_currentRoom.Id != 0x11)
            throw new InvalidOperationException("The transition validation expects startup room 11.");

        // Room 11's top staircase is metatile $d0 at column 4. This position
        // crosses the same collision samples and room-exit code as player input.
        Vector2 exitPosition = new(4 * OracleRoomData.MetatileSize + 8, -2);
        for (float y = _player.Position.Y; y >= exitPosition.Y; y -= 2)
        {
            if (Collides(new Vector2(exitPosition.X, y)))
                throw new InvalidOperationException(
                    $"Room 11's path to the top staircase is blocked at y={y}.");
        }

        _player.WarpTo(exitPosition);
        CheckRoomExit(_player);
        if (_currentRoom.Id != 0x01)
            throw new InvalidOperationException(
                $"Expected room 01 after the startup transition, got {_currentRoom.Id:x2}.");
        ValidateLinkScrollsForOneTransitionFrame();
        FinishActiveScrollingTransitionForValidation();
        if (_currentRoom.GetPackedPosition(_player.Position) != 0x74)
            throw new InvalidOperationException(
                $"Expected Link to finish the 11 -> 01 transition near $74, got " +
                $"${_currentRoom.GetPackedPosition(_player.Position):x2}.");
        GD.Print("Validated original-style transition 11 -> 01 through staircase collision $18.");
    }

    private void ValidateSymmetryTransition()
    {
        if (_currentRoom.Id != 0x22)
            throw new InvalidOperationException("The Symmetry transition validation expects room 22.");

        int oldTileset = _currentRoom.TilesetId;
        Vector2 exitPosition = new(3 * OracleRoomData.MetatileSize + 8, -2);
        if (Collides(exitPosition))
            throw new InvalidOperationException("Room 22's north staircase is blocked.");

        _player.WarpTo(exitPosition);
        CheckRoomExit(_player);
        if (_currentRoom.Id != 0x12 || _currentRoom.TilesetId == oldTileset)
            throw new InvalidOperationException(
                $"Expected room 12 / a new tileset, got {_currentRoom.Id:x2} / {_currentRoom.TilesetId:x2}.");
        ValidateLinkScrollsForOneTransitionFrame();
        FinishActiveScrollingTransitionForValidation();
        GD.Print($"Validated cross-tileset transition 22 ({oldTileset:x2}) -> " +
            $"12 ({_currentRoom.TilesetId:x2}).");
    }

    private void ValidateLinkScrollsForOneTransitionFrame()
    {
        if (!IsTransitioning)
            return;

        double animationTickBefore = _animationTicks;
        UpdateAnimatedTiles(1.0 / 60.0);
        if (!Mathf.IsEqualApprox((float)_animationTicks, (float)animationTickBefore))
            throw new InvalidOperationException("Animated tiles advanced during a room transition.");

        Vector2 position = _player.Position;
        UpdateScrollingTransition(1.0 / 60.0);
        Vector2 moved = _player.Position - position;
        Vector2 scrollDirection = -(Vector2)_scrollTransitionDirection;
        if (moved.Dot(scrollDirection) <= 0.0f)
            throw new InvalidOperationException("Link did not scroll with the screen transition.");
    }

    private void FinishActiveScrollingTransitionForValidation()
    {
        for (int i = 0; i < 80 && IsTransitioning; i++)
            UpdateScrollingTransition(1.0 / 60.0);
        if (IsTransitioning)
            throw new InvalidOperationException("Scrolling transition did not finish within 80 frames.");
    }

    private int FinishActiveScrollingTransitionWithRoomEventsForValidation()
    {
        int frames = 0;
        for (; frames < 80 && IsTransitioning; frames++)
        {
            UpdateScrollingTransition(1.0 / 60.0);
            _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
        }
        if (IsTransitioning)
            throw new InvalidOperationException("Scrolling transition did not finish within 80 frames.");
        return frames;
    }
}
