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
        _player.WarpTo(new Vector2(0x38, 0x54));
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
        OracleRoomData exteriorRoom = _currentRoom;
        FailIf(
            exteriorRoom.GetMetatile(new Vector2(0x58, 0x28)) != 0xde ||
            exteriorRoom.GetTerrainInfo(new Vector2(0x58, 0x28)).Collision != 0x0c ||
            !RoomTransitionController.LinkWithinTileWarpBounds(
                exteriorRoom, 0, 0x25, new Vector2(0x50, 0x22)) ||
            !RoomTransitionController.LinkWithinTileWarpBounds(
                exteriorRoom, 0, 0x25, new Vector2(0x5f, 0x2b)) ||
            RoomTransitionController.LinkWithinTileWarpBounds(
                exteriorRoom, 0, 0x25, new Vector2(0x58, 0x21)) ||
            RoomTransitionController.LinkWithinTileWarpBounds(
                exteriorRoom, 0, 0x25, new Vector2(0x58, 0x2c)),
            "Exterior door 0:47/$25 did not retain collision-$0c's " +
            "X-unbounded, Y=$22-$2b warp activation window.");

        OracleRoomData multiDoorRoom = _world.LoadRoom(1, 0x0e);
        var warps = new WarpDatabase();
        byte multiLeftTile =
            multiDoorRoom.GetMetatile(new Vector2(0x38, 0x38));
        byte multiRightTile =
            multiDoorRoom.GetMetatile(new Vector2(0x48, 0x38));
        byte multiCollision =
            multiDoorRoom.GetTerrainInfo(new Vector2(0x38, 0x38)).Collision;
        bool multiWarp =
            warps.TryGetTileWarp(1, 0x0e, 0x33, 0xef, out _);
        bool multiLowerBound =
            RoomTransitionController.LinkWithinTileWarpBounds(
                multiDoorRoom, 1, 0x33, new Vector2(0x30, 0x30));
        bool multiUpperBound =
            RoomTransitionController.LinkWithinTileWarpBounds(
                multiDoorRoom, 1, 0x33, new Vector2(0x3f, 0x39));
        bool multiOutsideBound =
            RoomTransitionController.LinkWithinTileWarpBounds(
                multiDoorRoom, 1, 0x33, new Vector2(0x38, 0x3a));
        FailIf(
            multiLeftTile != 0xef ||
            multiRightTile != 0xef ||
            multiCollision != 0 ||
            !multiWarp ||
            !multiLowerBound ||
            !multiUpperBound ||
            multiOutsideBound,
            "Room 1:0e's adjacent walkable `$ef warp tiles did not retain " +
            "their X-unbounded, Y=$30-$39 activation window " +
            $"(tiles=${multiLeftTile:x2}/${multiRightTile:x2}, " +
            $"collision=${multiCollision:x2}, warp={multiWarp}, " +
            $"bounds={multiLowerBound}/{multiUpperBound}/{multiOutsideBound}).");

        for (float y = 54; y >= 43; y--)
        {
            FailIf(Collides(new Vector2(88, y)), $"The path into exterior door $25 is blocked at y={y}.");
        }
        _player.WarpTo(new Vector2(88, 43));
        FailIf(
            !CheckTileWarp(_player) || _activeGroup != 2 || _currentRoom.Id != 0xea,
            $"Expected exterior 0:47/$25 to enter house 2:ea, got {_activeGroup}:{_currentRoom.Id:x2}.");
        FailIf(
            !IsTransitioning || !Mathf.IsEqualApprox(_player.Position.Y, _currentRoom.Height),
            "House entry did not begin at the bottom edge of the interior.");
        UpdateRoomWarpTransition(WarpEnterFrames / 60.0);
        FailIf(
            !IsTransitioning || !Mathf.IsEqualApprox(_player.Position.Y, _currentRoom.Height - WarpEnterFrames),
            "Link did not perform the 28-frame interior entry walk.");
        UpdateRoomWarpTransition((WarpFadeFrames - WarpEnterFrames) / 60.0);
        FailIf(IsTransitioning, "The 32-frame room fade did not finish after entering the house.");
        FailIf(
            _saveData.RespawnGroup != 2 || _saveData.RespawnRoom != 0xea ||
            _saveData.RespawnFacing != 0 || _saveData.RespawnY != 0x64 ||
            _saveData.RespawnX != 0x50,
            "TRANSITION_DEST_ENTERSCREEN did not record house 2:ea's final entry checkpoint.");

        for (float y = _player.Position.Y; y <= _currentRoom.Height + 2; y++)
        {
            FailIf(
                Collides(new Vector2(_currentRoom.Width / 2.0f, y)),
                $"The house's bottom exit is blocked at y={y}.");
        }
        _player.WarpTo(new Vector2(_currentRoom.Width / 2.0f, _currentRoom.Height + 2));
        CheckRoomExit(_player);
        FailIf(
            !IsTransitioning || _activeGroup != 2 || _currentRoom.Id != 0xea,
            "The house exit did not begin with its scripted walk offscreen.");
        UpdateRoomWarpTransition(WarpLeaveFrames / 60.0);
        FailIf(
            _activeGroup != 0 || _currentRoom.Id != 0x47 || !IsTransitioning,
            "The exterior was not loaded after the 16-frame exit walk.");
        EraInfoDisplay eraInfo =
            _entities.Entities<EraInfoDisplay>().SingleOrDefault() ??
            throw new InvalidOperationException(
                "House 2:ea's exit did not create INTERAC_ERA_OR_SEASON_INFO $e0.");
        FailIf(
            eraInfo.SubId != 0 ||
            eraInfo.Stage != EraInfoStage.Initializing ||
            eraInfo.Visible ||
            eraInfo.ZIndex != NpcCharacter.InFrontOfLinkZIndex ||
            eraInfo.TextureSize != new Vector2I(32, 16) ||
            eraInfo.TextureOffset != new Vector2(-16, -8) ||
            eraInfo.PixelHash == 0,
            "The present-era display did not initialize from its imported four-cell OAM.");

        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            eraInfo.Stage != EraInfoStage.Entering ||
            !eraInfo.Visible ||
            eraInfo.Position != new Vector2(0xb0, 0x0a) ||
            WorldToScreen(eraInfo.Position) + eraInfo.TextureOffset !=
                new Vector2(160, 18),
            "INTERAC_ERA_OR_SEASON_INFO state 0 did not begin just off the right edge.");

        for (int update = 0; update < WarpFadeFrames; update++)
        {
            UpdateRoomWarpTransition(1.0 / 60.0);
            _entities.Update(1.0 / 60.0, _player);
        }
        FailIf(
            _activeGroup != 0 || _currentRoom.Id != 0x47 ||
            _currentRoom.GetPackedPosition(_player.Position) != 0x35,
            $"Expected house 2:ea bottom exit to step out below 0:47/$25, got " +
            $"{_activeGroup}:{_currentRoom.Id:x2}/${_currentRoom.GetPackedPosition(_player.Position):x2}.");
        FailIf(
            IsTransitioning ||
            eraInfo.Stage != EraInfoStage.Entering ||
            eraInfo.Position != new Vector2(0x30, 0x0a),
            "The era display did not advance on all 32 destination fade-in updates.");
        FailIf(
            Collides(_player.Position + Vector2.Down),
            "The exterior landing spot below 0:47/$25 is blocked.");
        FailIf(
            _saveData.RespawnGroup != 0 || _saveData.RespawnRoom != 0x47 ||
            _saveData.RespawnFacing != 2 || _saveData.RespawnY != 0x38 ||
            _saveData.RespawnX != 0x58 ||
            !OracleSaveData.TryDeserialize(_saveData.Serialize(), out OracleSaveData? exteriorSave) ||
            exteriorSave!.RespawnGroup != 0 || exteriorSave.RespawnRoom != 0x47 ||
            exteriorSave.RespawnY != 0x38 || exteriorSave.RespawnX != 0x58,
            "TRANSITION_DEST_SET_RESPAWN did not persist exterior 0:47's stepped-out checkpoint.");

        for (int update = 0; update < 8; update++)
            _entities.Update(1.0 / 60.0, _player);
        FailIf(
            eraInfo.Stage != EraInfoStage.Holding ||
            eraInfo.Counter != 40 ||
            eraInfo.Position != new Vector2(0x10, 0x0a) ||
            WorldToScreen(eraInfo.Position) + eraInfo.TextureOffset !=
                new Vector2(0, 18),
            "The era display did not finish its 40-update, four-pixel fly-in at x=$10.");
        for (int update = 0; update < 39; update++)
            _entities.Update(1.0 / 60.0, _player);
        FailIf(
            eraInfo.Stage != EraInfoStage.Holding || eraInfo.Counter != 1,
            "The era display ended its 40-update hold early.");
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            eraInfo.Stage != EraInfoStage.Exiting || eraInfo.Counter != 6,
            "The era display did not arm its six-update exit.");
        for (int update = 0; update < 5; update++)
            _entities.Update(1.0 / 60.0, _player);
        FailIf(
            eraInfo.Stage != EraInfoStage.Exiting ||
            eraInfo.Counter != 1 ||
            eraInfo.Position != new Vector2(-14, 0x0a),
            "The era display finished its six-pixel exit early.");
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            _entities.Entities<EraInfoDisplay>().Count != 0 ||
            eraInfo.Position != new Vector2(-20, 0x0a),
            "The era display did not delete on the sixth fly-out update.");
        ulong presentEraHash = eraInfo.PixelHash;

        int checkpointGroup = _saveData.RespawnGroup;
        int checkpointRoom = _saveData.RespawnRoom;
        int checkpointY = _saveData.RespawnY;
        int checkpointX = _saveData.RespawnX;

        _activeGroup = 2;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0xeb);
        _roomView.SetRoom(_currentRoom.Texture);
        _player.WarpTo(new Vector2(-2, _currentRoom.Height / 2.0f));
        _player.UpdatePushingState(Vector2.Left);
        CheckRoomExit(_player);
        FailIf(
            _activeGroup != 2 || _currentRoom.Id != 0xea,
            $"Expected room 2:eb left edge to scroll to 2:ea, got {_activeGroup}:{_currentRoom.Id:x2}.");
        ValidateLinkScrollsForOneTransitionFrame();
        FinishActiveScrollingTransitionForValidation();
        FailIf(
            _currentRoom.GetPackedPosition(_player.Position) != 0x49,
            $"Expected Link to finish 2:eb -> 2:ea near the right edge, got " +
            $"${_currentRoom.GetPackedPosition(_player.Position):x2}.");
        FailIf(
            _saveData.RespawnGroup != checkpointGroup || _saveData.RespawnRoom != checkpointRoom ||
            _saveData.RespawnY != checkpointY || _saveData.RespawnX != checkpointX,
            "An ordinary scrolling transition incorrectly replaced the death checkpoint.");

        ValidateEraInfoDisplayPredicates(presentEraHash);

        GD.Print("Validated original house entry/exit fades, destination checkpoint updates, " +
            "save-image round trip, present/past era fly-in timing and predicates, " +
            "and non-checkpoint 2:eb -> 2:ea scrolling.");
    }

    private void ValidateEraInfoDisplayPredicates(ulong presentEraHash)
    {
        LoadDebugRoom(1, 0x48);
        FailIf(
            (_currentRoom.TilesetFlags & 0x81) != 0x81 ||
            !_transitions.CheckDisplayEraInfoAfterFullRoomLoad(),
            "A full load of past overworld room 1:48 did not request its era display.");
        EraInfoDisplay past =
            _entities.Entities<EraInfoDisplay>().SingleOrDefault() ??
            throw new InvalidOperationException("The past-era display was not created.");
        FailIf(
            past.SubId != 1 ||
            past.PixelHash == 0 ||
            past.PixelHash == presentEraHash,
            "wTilesetFlags bit 7 did not select the distinct past-era OAM and palette.");

        FailIf(
            !_rooms.TryGetNeighbor(Vector2I.Right, out int scrollTarget),
            "Past overworld room 1:48 has no right neighbor.");
        _transitions.BeginScroll(_player, Vector2I.Right, scrollTarget);
        _transitions.UpdateScroll(1.0 / 60.0);
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            past.Stage != EraInfoStage.Entering ||
            past.Position != new Vector2(0xb0, 0x0a) ||
            _entities.OutgoingEntities<EraInfoDisplay>().SingleOrDefault() != past,
            "The era display's native always-update bit did not advance during scrolling.");
        for (int update = 1; update < 40; update++)
        {
            _transitions.UpdateScroll(1.0 / 60.0);
            _entities.Update(1.0 / 60.0, _player);
        }
        FailIf(_transitions.ScrollActive, "The era always-update scroll did not finish.");

        LoadDebugRoom(2, 0xea);
        FailIf(
            _transitions.CheckDisplayEraInfoAfterFullRoomLoad() ||
            _entities.Entities<EraInfoDisplay>().Count != 0,
            "An indoor full room load incorrectly created the outdoor era display.");

        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagSuppressEraInfoOnce);
        FailIf(
            _transitions.CheckDisplayEraInfoAfterFullRoomLoad() ||
            _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagSuppressEraInfoOnce),
            "GLOBALFLAG_16 did not suppress and clear one era-display check before tileset tests.");

        LoadDebugRoom(0, 0x47);
        _entities.RuntimeState.SetWramByte(
            OracleRuntimeState.SentBackByStrangeForceAddress,
            1);
        FailIf(
            _transitions.CheckDisplayEraInfoAfterFullRoomLoad() ||
            _entities.Entities<EraInfoDisplay>().Count != 0,
            "wSentBackByStrangeForce=$01 did not suppress the era display.");
        _entities.RuntimeState.SetWramByte(
            OracleRuntimeState.SentBackByStrangeForceAddress,
            0);
    }

    private void ValidateCaveWarps()
    {
        // In an unlinked game the $f0 single-tile change covers position $28
        // with non-warp metatile $64. Declare the linked-game prerequisite that
        // leaves the underlying cave exposed instead of inheriting it from
        // another case.
        _saveData.SetLinkedGame(true);
        ValidateLargeRoomCaveWarp(0x21, 0x04);
        ValidateLargeDungeonTopTransition();
        ValidateLargeRoomCaveWarp(0x28, 0xce);
        GD.Print("Validated 0:48 cave entries and dungeon00 room 4:04 -> 4:03 top transition.");
    }

    private void ValidateMakuTreeSouthExitReveal()
    {
        const int group = 0;
        const int sourceRoom = 0x38;
        const int destinationRoom = 0x48;

        LoadDebugRoom(group, sourceRoom);
        _player.WarpTo(new Vector2(0x50, 0x70));
        _player.Face(Vector2I.Down);

        var warps = new WarpDatabase();
        FailIf(
            !warps.TryGetEdgeWarp(
            group,
            sourceRoom,
            Vector2I.Down,
            _player.Position,
            new Vector2(_currentRoom.Width, _currentRoom.Height),
            out Warp warp),
            "Room 0:38 is missing its imported south edge warp.");

        _transitions.ApplyWarp(_player, warp);
        FailIf(
            !IsTransitioning ||
            !_transitions.RoomLoadColumnRevealActive ||
            _roomView.RoomLoadColumnRevealActive ||
            _scene.RoomLoadReveal.Active ||
            _scene.RoomLoadReveal.Visible ||
            _warpFade.Color.A != 0.0f,
            "Room 0:38's south edge warp did not select the full-load column reveal.");

        UpdateRoomWarpTransition(WarpLeaveFrames / 60.0);
        FailIf(
            _activeGroup != group ||
            _currentRoom.Id != destinationRoom ||
            !_transitions.RoomLoadColumnRevealActive ||
            !_roomView.RoomLoadColumnRevealActive ||
            !_scene.RoomLoadReveal.Active ||
            !_scene.RoomLoadReveal.Visible ||
            _transitions.RoomLoadRevealLoadedColumns != 0 ||
            _roomView.RoomLoadRevealLoadedColumns != 0 ||
            _scene.RoomLoadReveal.LoadedColumns != 0 ||
            !ReferenceEquals(
                _roomView.RoomLoadClearedTilemap,
                _currentRoom.ClearedTilemapTexture) ||
            !ReferenceEquals(
                _scene.RoomLoadReveal.ClearedTilemap,
                _currentRoom.ClearedTilemapTexture) ||
            _scene.RoomLoadReveal.GetParent() != _scene.InterfaceLayer ||
            _warpFade.Color.A != 0.0f ||
            _player.Position != new Vector2(0x50, -0x10),
            "Room 0:38's south exit did not load 0:48 behind the cleared VRAM map.");

        Image clearedTilemap = _currentRoom.ClearedTilemapTexture.GetImage();
        Color clearedColor = Color.Color8(255, 214, 140);
        FailIf(
            clearedTilemap.GetPixel(0, 0) != clearedColor ||
            clearedTilemap.GetPixel(7, 7) != clearedColor ||
            clearedTilemap.GetPixel(8, 0) != clearedColor ||
            clearedTilemap.GetPixel(159, 127) != clearedColor,
            "initializeVramMaps tile $00/attribute $80 did not repeat " +
            "GFXH_HUD tile 0's solid shade 2 through PALH_0f BG palette 0.");

        UpdateRoomWarpTransition(
            RoomTransitionController.RoomLoadRevealInitializationFrames / 60.0);
        FailIf(
            _transitions.RoomLoadRevealLoadedColumns != 0 ||
            _roomView.RoomLoadRevealLoadedColumns != 0 ||
            _scene.RoomLoadReveal.LoadedColumns != 0 ||
            _warpFade.Color.A != 0.0f ||
            _player.Position != new Vector2(0x50, -0x10),
            "The room-load transition did not preserve its three blank initialization updates.");

        UpdateRoomWarpTransition(1.0 / 60.0);
        FailIf(
            _transitions.RoomLoadRevealLoadedColumns != 1 ||
            _roomView.RoomLoadRevealLoadedColumns != 1 ||
            _scene.RoomLoadReveal.LoadedColumns != 1 ||
            !RoomTransitionController.RoomLoadRevealIsClearedAtPixel(71, 1) ||
            RoomTransitionController.RoomLoadRevealIsClearedAtPixel(72, 1) ||
            RoomTransitionController.RoomLoadRevealIsClearedAtPixel(79, 1) ||
            !RoomTransitionController.RoomLoadRevealIsClearedAtPixel(80, 1),
            "The first room-load update did not draw only 8-pixel column 9.");

        UpdateRoomWarpTransition(1.0 / 60.0);
        FailIf(
            _transitions.RoomLoadRevealLoadedColumns != 2 ||
            _roomView.RoomLoadRevealLoadedColumns != 2 ||
            _scene.RoomLoadReveal.LoadedColumns != 2 ||
            RoomTransitionController.RoomLoadRevealIsClearedAtPixel(80, 2) ||
            RoomTransitionController.RoomLoadRevealIsClearedAtPixel(87, 2) ||
            !RoomTransitionController.RoomLoadRevealIsClearedAtPixel(88, 2),
            "The second room-load update did not draw 8-pixel column 10.");

        UpdateRoomWarpTransition(18.0 / 60.0);
        FailIf(
            _transitions.RoomLoadRevealLoadedColumns != 20 ||
            _roomView.RoomLoadRevealLoadedColumns != 20 ||
            _scene.RoomLoadReveal.LoadedColumns != 20 ||
            RoomTransitionController.RoomLoadRevealIsClearedAtPixel(0, 20) ||
            RoomTransitionController.RoomLoadRevealIsClearedAtPixel(159, 20) ||
            _player.Position != new Vector2(0x50, -0x10),
            "The first 20 alternating column updates did not fill the visible 160-pixel room.");

        UpdateRoomWarpTransition(12.0 / 60.0);
        FailIf(
            _transitions.RoomLoadRevealLoadedColumns !=
                RoomTransitionController.RoomLoadRevealColumnUpdates ||
            !IsTransitioning ||
            _player.Position != new Vector2(0x50, -0x10),
            "The room-load transition did not finish all 32 VRAM-map column updates.");

        UpdateRoomWarpTransition(1.0 / 60.0);
        FailIf(
            !IsTransitioning ||
            _transitions.RoomLoadColumnRevealActive ||
            _roomView.RoomLoadColumnRevealActive ||
            _scene.RoomLoadReveal.Active ||
            _scene.RoomLoadReveal.Visible ||
            _warpFade.Color.A != 0.0f ||
            _transitions.TimeWarpPhaseName != "EnterScreen" ||
            _player.Position != new Vector2(0x50, -0x10),
            "The completed column load did not release destination transition $03.");

        UpdateRoomWarpTransition((WarpEnterFrames - 1.0f) / 60.0);
        FailIf(
            !IsTransitioning ||
            _player.Position != new Vector2(0x50, 0x0b),
            "Link did not begin his 28-update destination walk after the column load.");

        UpdateRoomWarpTransition(1.0 / 60.0);
        FailIf(
            IsTransitioning ||
            _player.Position != new Vector2(0x50, 0x0c),
            "Room 0:38's destination transition did not finish after Link's 28th walk update.");

        LoadValidationRoom(0, 0x11);
        GD.Print(
            "Validated room 0:38 south exit exact HUD tile-$00/palette-0 priority map " +
            "and alternating 8-pixel VRAM-column reveal.");
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
        FailIf(exitX < 0.0f, "Could not find 4:04's open northern dungeon exit.");

        _player.WarpTo(new Vector2(exitX, -2.0f));
        _player.UpdatePushingState(Vector2.Up);
        CheckRoomExit(_player);
        FailIf(
            _activeGroup != 4 || _currentRoom.Id != 0x03 || !_scrollTransitionActive,
            $"Expected dungeon00 room 4:04 north to lead to 4:03, got {_activeGroup}:{_currentRoom.Id:x2}.");
        FailIf(
            _scrollTransitionFrames != 32 || !Mathf.IsEqualApprox(_scrollTransitionDistance, 128.0f),
            "Large-room vertical scrolling did not use the 128px playfield distance.");

        FinishActiveScrollingTransitionForValidation();
        FailIf(
            Mathf.Abs(WorldToScreen(_player.Position).Y - 134.0f) > 0.01f,
            "Link did not finish 4:04 -> 4:03 at the lower playfield edge.");
    }

    private void ValidateLargeRoomCaveWarp(int sourcePosition, int destinationRoom)
    {
        LoadNpcValidationRoom();
        int tileX = sourcePosition & 0x0f;
        int tileY = (sourcePosition >> 4) & 0x0f;
        _player.WarpTo(new Vector2(
            tileX * OracleRoomData.MetatileSize + 8,
            tileY * OracleRoomData.MetatileSize + 8));
        FailIf(
            !CheckTileWarp(_player) || _activeGroup != 4 || _currentRoom.Id != destinationRoom,
            $"Expected 0:48/${sourcePosition:x2} to enter 4:{destinationRoom:x2}, got " +
            $"{_activeGroup}:{_currentRoom.Id:x2}.");
        int expectedWidth = OracleRoomData.LargeRoomWidthInTiles * OracleRoomData.MetatileSize;
        int expectedHeight = OracleRoomData.LargeRoomHeightInTiles * OracleRoomData.MetatileSize;
        FailIf(
            _currentRoom.Width != expectedWidth || _currentRoom.Height != expectedHeight ||
            _currentRoom.Texture.GetWidth() != expectedWidth ||
            _currentRoom.Texture.GetHeight() != expectedHeight,
            $"Expected 4:{destinationRoom:x2} to use the original 240x176 playable large-room dimensions.");
        FailIf(
            _player.Position != new Vector2(0x78, 0xb0),
            $"Expected the original large-room entry coordinate $b0/$78, got {_player.Position}.");

        UpdateRoomCamera();
        FailIf(
            WorldToScreen(_player.Position).DistanceSquaredTo(new Vector2(80, 144)) > 0.01f,
            $"Link did not begin the 4:{destinationRoom:x2} cave entry at screen position (80,144).");
        UpdateRoomWarpTransition(WarpEnterFrames / 60.0);
        UpdateRoomCamera();
        FailIf(
            WorldToScreen(_player.Position).DistanceSquaredTo(new Vector2(80, 116)) > 0.01f,
            $"Link did not finish the 28-frame 4:{destinationRoom:x2} cave entry at screen position (80,116).");
        UpdateRoomWarpTransition((WarpFadeFrames - WarpEnterFrames) / 60.0);
        FailIf(IsTransitioning, $"The 4:{destinationRoom:x2} cave fade did not finish.");

        _player.WarpTo(new Vector2(_currentRoom.Width - 1, _currentRoom.Height / 2.0f));
        UpdateRoomCamera();
        FailIf(
            Mathf.Abs(WorldToScreen(new Vector2(_currentRoom.Width, 0)).X -
            OracleRoomData.ViewportWidth) > 0.01f,
            $"The 4:{destinationRoom:x2} camera exposed the padded 16th large-room column.");
        FailIf(
            !_collision.Collides(new Vector2(
            OracleRoomData.LargeRoomWidthInTiles * OracleRoomData.MetatileSize + 5,
            _currentRoom.Height / 2.0f)),
            $"The 4:{destinationRoom:x2} padded 16th large-room column allowed Link out of bounds.");
    }

    private void ValidateStartupTransition()
    {
        FailIf(_currentRoom.Id != 0x11, "The transition validation expects startup room 11.");

        // Room 11's top staircase is metatile $d0 at column 4. This position
        // crosses the same collision samples and room-exit code as player input.
        Vector2 exitPosition = new(4 * OracleRoomData.MetatileSize + 8, -2);
        for (float y = _player.Position.Y; y >= exitPosition.Y; y -= 2)
        {
            FailIf(
                Collides(new Vector2(exitPosition.X, y)),
                $"Room 11's path to the top staircase is blocked at y={y}.");
        }

        _player.WarpTo(exitPosition);
        _player.UpdatePushingState(Vector2.Up);
        CheckRoomExit(_player);
        FailIf(
            _currentRoom.Id != 0x01,
            $"Expected room 01 after the startup transition, got {_currentRoom.Id:x2}.");
        ValidateLinkScrollsForOneTransitionFrame();
        FinishActiveScrollingTransitionForValidation();
        FailIf(
            _currentRoom.GetPackedPosition(_player.Position) != 0x74,
            $"Expected Link to finish the 11 -> 01 transition near $74, got " +
            $"${_currentRoom.GetPackedPosition(_player.Position):x2}.");
        GD.Print("Validated original-style transition 11 -> 01 through staircase collision $18.");
    }

    private void ValidateSymmetryTransition()
    {
        FailIf(_currentRoom.Id != 0x22, "The Symmetry transition validation expects room 22.");

        int oldTileset = _currentRoom.TilesetId;
        Vector2 exitPosition = new(3 * OracleRoomData.MetatileSize + 8, -2);
        FailIf(Collides(exitPosition), "Room 22's north staircase is blocked.");

        _player.WarpTo(exitPosition);
        _player.UpdatePushingState(Vector2.Up);
        CheckRoomExit(_player);
        FailIf(
            _currentRoom.Id != 0x12 || _currentRoom.TilesetId == oldTileset,
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
        FailIf(
            !Mathf.IsEqualApprox((float)_animationTicks, (float)animationTickBefore),
            "Animated tiles advanced during a room transition.");

        Vector2 position = _player.Position;
        UpdateScrollingTransition(1.0 / 60.0);
        Vector2 moved = _player.Position - position;
        Vector2 scrollDirection = -(Vector2)_scrollTransitionDirection;
        FailIf(moved.Dot(scrollDirection) <= 0.0f, "Link did not scroll with the screen transition.");
    }

    private void FinishActiveScrollingTransitionForValidation()
    {
        for (int i = 0; i < 80 && IsTransitioning; i++)
            UpdateScrollingTransition(1.0 / 60.0);
        FailIf(IsTransitioning, "Scrolling transition did not finish within 80 frames.");
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
        FailIf(IsTransitioning, "Scrolling transition did not finish within 80 frames.");
        return frames;
    }
}
