using Godot;
using System;

namespace oracleofages;

public partial class GameRoot
{
    private void ValidateChests()
    {
        WarpToChestTest();
        const int chestPosition = 0x51;
        Vector2 chestPoint = new(24, 88);
        if (_activeGroup != 0 || _currentRoom.Id != 0x49 ||
            _currentRoom.GetPackedPosition(chestPoint) != chestPosition ||
            _currentRoom.GetMetatile(chestPoint) != 0xf1)
        {
            throw new InvalidOperationException(
                "The canonical 0:49/$51 30-rupee chest was not available for testing.");
        }

        Image roomImage = _currentRoom.Texture.GetImage();
        int redChestPixels = 0;
        for (int y = 80; y < 96; y++)
        for (int x = 16; x < 32; x++)
        {
            Color pixel = roomImage.GetPixel(x, y);
            if (pixel.R > 0.5f && pixel.G < 0.2f && pixel.B < 0.25f)
                redChestPixels++;
        }
        if (redChestPixels == 0)
        {
            throw new InvalidOperationException(
                "Chest $51 did not render with PALH_0f background palette 0.");
        }

        _player.WarpTo(new Vector2(24, 74));
        _player.Face(Vector2I.Down);
        if (!TryInteract(_player) || !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "It won't open\nfrom this side!")
        {
            throw new InvalidOperationException("Chest $51 did not use TX_510d from the wrong side.");
        }
        _dialogue.Close();

        _player.WarpTo(new Vector2(24, 100));
        _player.Face(Vector2I.Up);
        int rupeesBefore = _player.Rupees;
        if (!TryInteract(_player) || !_interactions.ChestRewardActive ||
            _currentRoom.GetMetatile(chestPoint) != 0xf0)
        {
            throw new InvalidOperationException("Chest $51 did not open from below into tile $f0.");
        }

        _interactions.Update(31.0 / 60.0, _player);
        if (!_interactions.ChestRewardActive || _player.Rupees != rupeesBefore)
            throw new InvalidOperationException("The chest reward completed before its 32-frame rise.");
        _interactions.Update(1.0 / 60.0, _player);
        if (!_interactions.ChestRewardActive || _player.Rupees != rupeesBefore + 30 ||
            !_dialogue.IsOpen || _dialogue.CurrentMessage != "You got\n30 Rupees!\nThat's nice.")
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_RUPEES_04 did not remain visible while showing TX_0005.");
        }

        _dialogue.Close();
        if (!_interactions.ChestRewardActive)
            throw new InvalidOperationException("The chest reward disappeared before its textbox closed.");
        _interactions.Update(0.0, _player);
        if (_interactions.ChestRewardActive)
            throw new InvalidOperationException("The chest reward remained after its textbox closed.");

        _currentRoom = _world.LoadRoom(0, 0x49);
        _roomView.SetRoom(_currentRoom.Texture);
        if (_currentRoom.GetMetatile(chestPoint) != 0xf0 || TryInteract(_player))
            throw new InvalidOperationException("The opened chest room flag did not persist for the session.");

        GD.Print("Validated 0:49/$51 red chest palette, direction, 32-frame reward rise, " +
            "reward visibility through TX_0005, 30 rupees, and opened state.");
    }

    private void ValidateHouseWarp()
    {
        WarpToHouseTest();
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

        GD.Print("Validated original house entry/exit fades, scripted walks, and 2:eb -> 2:ea screen transition.");
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
        if (Mathf.Abs(WorldToScreen(_player.Position).Y - 118.0f) > 0.01f)
            throw new InvalidOperationException("Link did not finish 4:04 -> 4:03 at the lower playfield edge.");
    }

    private void ValidateLargeRoomCaveWarp(int sourcePosition, int destinationRoom)
    {
        WarpToNpcTest();
        int tileX = sourcePosition & 0x0f;
        int tileY = (sourcePosition >> 4) & 0x0f;
        _player.WarpTo(new Vector2(
            tileX * OracleRoomData.MetatileSize + 8,
            tileY * OracleRoomData.MetatileSize + 8));
        if (!CheckTileWarp(_player) || _activeGroup != 4 || _currentRoom.Id != destinationRoom)
            throw new InvalidOperationException(
                $"Expected 0:48/${sourcePosition:x2} to enter 4:{destinationRoom:x2}, got " +
                $"{_activeGroup}:{_currentRoom.Id:x2}.");
        if (_currentRoom.Width != 256 || _currentRoom.Height != 176)
            throw new InvalidOperationException(
                $"Expected 4:{destinationRoom:x2} to use the original 256x176 large-room dimensions.");
        if (_player.Position != new Vector2(0x78, 0xb0))
            throw new InvalidOperationException(
                $"Expected the original large-room entry coordinate $b0/$78, got {_player.Position}.");

        UpdateRoomCamera();
        if (WorldToScreen(_player.Position).DistanceSquaredTo(new Vector2(80, 128)) > 0.01f)
            throw new InvalidOperationException(
                $"Link did not begin the 4:{destinationRoom:x2} cave entry at screen position (80,128).");
        UpdateRoomWarpTransition(WarpEnterFrames / 60.0);
        UpdateRoomCamera();
        if (WorldToScreen(_player.Position).DistanceSquaredTo(new Vector2(80, 100)) > 0.01f)
            throw new InvalidOperationException(
                $"Link did not finish the 28-frame 4:{destinationRoom:x2} cave entry at screen position (80,100).");
        UpdateRoomWarpTransition((WarpFadeFrames - WarpEnterFrames) / 60.0);
        if (IsTransitioning)
            throw new InvalidOperationException($"The 4:{destinationRoom:x2} cave fade did not finish.");
    }

    private void ValidateSwordBush()
    {
        WarpToBushTest();
        Vector2 bushPoint = new(24, 56);
        if (_currentRoom.GetMetatile(bushPoint) != 0xc5)
            throw new InvalidOperationException("Expected overworld bush $c5 in room 69 at $31.");
        Vector2 objectPosition = _player.Position;
        _player.StartSwordAttack();
        if (_player.AttackSpriteOrigin != new Vector2(-8, -8))
            throw new InvalidOperationException(
                $"Sword frame $ac displaced Link from the standard OAM origin: {_player.AttackSpriteOrigin}.");
        if (_player.SwordSpritePosition != new Vector2(16, -2))
            throw new InvalidOperationException(
                $"Sword arc phase $00 was not relative to Link's object position: {_player.SwordSpritePosition}.");
        _player._Process(7.0 / 60.0);
        if (_player.Position != objectPosition)
            throw new InvalidOperationException("Swinging the sword changed Link's object position.");
        if (_player.AttackSpriteOrigin != new Vector2(-8, -11))
            throw new InvalidOperationException(
                $"Sword frame $b4 did not apply only its original OAM $08 pose offset: {_player.AttackSpriteOrigin}.");
        if (_player.SwordSpritePosition != new Vector2(-4, -17))
            throw new InvalidOperationException(
                $"Sword arc phase $08 was not relative to Link's object position: {_player.SwordSpritePosition}.");
        if (_currentRoom.GetMetatile(bushPoint) != 0x3a)
            throw new InvalidOperationException("The level-1 sword did not replace bush $c5 with ground $3a.");
        if (_currentRoom.IsSolid(bushPoint))
            throw new InvalidOperationException("The cut bush's replacement tile remained solid.");
        GD.Print("Validated level-1 sword OAM anchoring, hit, and bush substitution c5 -> 3a in room 69.");
    }

    private void ValidateTerrain()
    {
        _dialogue.Close();
        _player.RefillHealth();
        _activeGroup = 0;
        ClearDeactivatedWarp();

        _currentRoom = _world.LoadRoom(_activeGroup, 0xb8);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 waterSafe = new(40, 8);
        _player.WarpTo(waterSafe);
        _player.WarpTo(new Vector2(8, 8), recordSafe: false);
        if (GetTerrainInfo(_player.Position).Hazard != OracleRoomData.HazardType.Water)
            throw new InvalidOperationException("Expected room b8/$00 to be water terrain.");
        ValidateDrowningSequence(waterSafe, OracleRoomData.HazardType.Water);

        _currentRoom = _world.LoadRoom(_activeGroup, 0x03);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 lavaSafe = new(56, 8);
        _player.WarpTo(lavaSafe);
        _player.WarpTo(new Vector2(8, 24), recordSafe: false);
        if (GetTerrainInfo(_player.Position).Hazard != OracleRoomData.HazardType.Lava)
            throw new InvalidOperationException("Expected room 03/$10 to be lava terrain.");
        ValidateDrowningSequence(lavaSafe, OracleRoomData.HazardType.Lava);

        if (!TryFindTerrainSample(
            OracleRoomData.HazardType.Hole,
            out int holeGroup,
            out int holeRoom,
            out Vector2 holeCenter,
            out Vector2 holeSafe))
        {
            throw new InvalidOperationException("Could not find a testable hole terrain tile.");
        }

        _player.RefillHealth();
        _activeGroup = holeGroup;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, holeRoom);
        _roomView.SetRoom(_currentRoom.Texture);
        _player.WarpTo(holeSafe);
        Vector2 offCenterHoleEntry = holeCenter + new Vector2(3, -2);
        int beforeHoleHealth = _player.HealthQuarters;
        _player.WarpTo(offCenterHoleEntry, recordSafe: false);
        Vector2 expectedHoleCenter = GetActiveTerrain(_player.Position).TileCenter;
        _player._PhysicsProcess(1.0 / 60.0);
        if (!_player.IsPullingIntoHole && !_player.IsFallingInHole)
            throw new InvalidOperationException(
                $"Room {holeGroup:x1}:{holeRoom:x2} hole terrain did not start Link's pull-in state.");
        if (_player.HealthQuarters != beforeHoleHealth)
            throw new InvalidOperationException("Hole damage was applied before the pull/fall animation finished.");

        AdvanceHolePullUntilFall(expectedHoleCenter);
        AdvanceHoleFallUntilRespawn(holeSafe);
        if (!_player.Visible || _player.Position.DistanceSquaredTo(holeSafe) > 1.0f)
            throw new InvalidOperationException("Hole terrain did not return Link to the last safe tile.");
        if (_player.HealthQuarters != beforeHoleHealth - 2)
            throw new InvalidOperationException("Hole terrain did not apply half-heart damage after respawn.");

        ValidateRoom01HoleBoundaryCase();

        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(_activeGroup, 0x11);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 ledgeStart = new(24, 56);
        _player.WarpTo(ledgeStart);
        _player.Face(Vector2I.Down);
        if (!TryStartLedgeHop(_player, _player.Position, Vector2.Down))
            throw new InvalidOperationException("Room 11's south cliff did not start a ledge hop.");
        for (int i = 0; i < 30; i++)
            _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Position.Y <= ledgeStart.Y + OracleRoomData.MetatileSize)
            throw new InvalidOperationException("The ledge hop did not carry Link down across the cliff.");

        ValidateRoom56TileEdgeSlide();

        GD.Print("Validated terrain hazards, hole fall/respawn, ledge hop, and original tile-edge sliding.");
    }

    private void ValidateDrowningSequence(
        Vector2 safePosition,
        OracleRoomData.HazardType hazard)
    {
        string terrainName = hazard.ToString();
        Vector2 hazardPosition = _player.Position;
        int healthBeforeDrowning = _player.HealthQuarters;
        int worldChildCount = GetChildCount();

        _player._PhysicsProcess(1.0 / 60.0);
        SplashEffect? splash = _terrain.ActiveSplash;
        if (GetChildCount() != worldChildCount + 1 || splash is null ||
            splash.Position != hazardPosition ||
            splash.IsLava != (hazard == OracleRoomData.HazardType.Lava))
        {
            throw new InvalidOperationException(
                $"{terrainName} drowning did not create its original splash interaction at Link's position.");
        }
        int splashFrameDuration = splash.IsLava ? 2 : 4;
        int splashFrameCount = splash.IsLava ? 10 : 3;
        if (splash.DurationFrames != splashFrameDuration * splashFrameCount ||
            splash.AnimationFrame != 0)
        {
            throw new InvalidOperationException(
                $"{terrainName} splash did not start with the original interaction timing.");
        }
        splash.Advance((splashFrameDuration - 1.0) / 60.0);
        if (splash.AnimationFrame != 0)
            throw new InvalidOperationException(
                $"{terrainName} splash did not hold its first OAM record for {splashFrameDuration} updates.");
        splash.Advance(1.0 / 60.0);
        if (splash.AnimationFrame != 1)
            throw new InvalidOperationException(
                $"{terrainName} splash did not advance to its second OAM record.");
        splash.Advance((splash.DurationFrames - splashFrameDuration - 1.0) / 60.0);
        if (splash.AnimationFrame != splashFrameCount - 1 || splash.IsQueuedForDeletion())
            throw new InvalidOperationException(
                $"{terrainName} splash did not reach and hold its final OAM record.");
        splash.Advance(1.0 / 60.0);
        if (!splash.IsQueuedForDeletion())
            throw new InvalidOperationException(
                $"{terrainName} splash did not delete after {splash.DurationFrames} updates.");
        if (!_player.IsDrowning || !_player.Visible || _player.DrownAnimationFrame != 0)
            throw new InvalidOperationException(
                $"{terrainName} terrain did not begin visible LINK_ANIM_MODE_DROWN frame $d4.");
        if (_player.HealthQuarters != healthBeforeDrowning)
            throw new InvalidOperationException(
                $"{terrainName} damage was applied before the drowning animation finished.");

        _player._PhysicsProcess(5.0 / 60.0);
        if (!_player.Visible || _player.DrownAnimationFrame != 0)
            throw new InvalidOperationException(
                $"{terrainName} did not hold directional drowning frame $d4 for six updates.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (!_player.Visible || _player.DrownAnimationFrame != 1)
            throw new InvalidOperationException(
                $"{terrainName} did not advance to drowning frame $0b after six updates.");

        _player._PhysicsProcess(15.0 / 60.0);
        if (!_player.Visible || _player.Position != hazardPosition)
            throw new InvalidOperationException(
                $"{terrainName} moved or hid Link before the 22-update drowning animation finished.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Visible || !_player.IsDrowning)
            throw new InvalidOperationException(
                $"{terrainName} did not hide Link after the 22-update drowning animation.");
        if (_player.HealthQuarters != healthBeforeDrowning)
            throw new InvalidOperationException(
                $"{terrainName} damage was applied before the two-update respawn delay.");

        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Visible)
            throw new InvalidOperationException(
                $"{terrainName} did not preserve the first invisible respawn update.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.IsDrowning || !_player.Visible ||
            _player.Position.DistanceSquaredTo(safePosition) > 1.0f)
        {
            throw new InvalidOperationException(
                $"{terrainName} did not return Link to the last safe tile after drowning.");
        }
        if (_player.HealthQuarters != healthBeforeDrowning - 2)
            throw new InvalidOperationException(
                $"{terrainName} did not apply one half-heart after Link reappeared.");
    }

    private void ValidateRoom56TileEdgeSlide()
    {
        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(_activeGroup, 0x56);
        _roomView.SetRoom(_currentRoom.Texture);
        RefreshRoomObjects();

        // The left end of bridge metatiles $1d/$1e starts at x=$30. These two
        // positions reproduce approaching its upper and lower rails from the
        // west, including the lower corner shown in the reported stuck case.
        ValidateBridgeSlidePath(new Vector2(44, 62), expectUp: true);
        ValidateBridgeSlidePath(new Vector2(44, 92), expectUp: false);

        // A fractional fixed-point remainder is possible when Link walks onto
        // the bridge after moving over slower terrain. At y=70.5 the upper
        // rail correctly blocks another upward step, while the original yh
        // coordinate (and therefore the rendered sprite) remains row 70.
        Vector2 fractionalStop = new(56, 70.5f);
        if (_collision.ResolveMovement(
                fractionalStop, Vector2.Up, allowWallSlide: true) != Vector2.Zero)
        {
            throw new InvalidOperationException(
                "Room 0:56's upper bridge rail did not block the fractional direct approach.");
        }

        _player.WarpTo(fractionalStop, recordSafe: false);
        if (_player.Position != new Vector2(56, 70))
        {
            throw new InvalidOperationException(
                "Link's fixed-point bridge position was rounded instead of rendered from yh/xh.");
        }
    }

    private void ValidateBridgeSlidePath(Vector2 start, bool expectUp)
    {
        Vector2 position = start;
        bool deflected = false;
        for (int frame = 0; frame < 16; frame++)
        {
            Vector2 resolved = _collision.ResolveMovement(
                position, Vector2.Right, allowWallSlide: true);
            if (resolved == Vector2.Zero)
                throw new InvalidOperationException(
                    $"Room 0:56 bridge slide became stuck at {position}.");
            deflected |= expectUp ? resolved.Y < 0.0f : resolved.Y > 0.0f;
            position += resolved;
        }

        Vector2 expected = start + new Vector2(12.0f, expectUp ? -4.0f : 4.0f);
        if (!deflected || position.DistanceSquaredTo(expected) > 0.01f)
            throw new InvalidOperationException(
                $"Room 0:56 bridge did not apply the symmetric four-frame " +
                $"{(expectUp ? "upward" : "downward")} deflection; " +
                $"start={start}, expected={expected}, end={position}.");
    }

    private void ValidateRoom01HoleBoundaryCase()
    {
        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x01);
        _roomView.SetRoom(_currentRoom.Texture);

        if (!TryFindHoleWithSafeNeighbor(_currentRoom, out Vector2 holeCenter, out Vector2 safePosition))
            throw new InvalidOperationException("Room 0:01 did not have a testable hole with a safe neighbor.");

        _player.RefillHealth();
        _player.WarpTo(safePosition);
        int beforeHealth = _player.HealthQuarters;

        float tileTop = Mathf.FloorToInt(holeCenter.Y / OracleRoomData.MetatileSize) *
            OracleRoomData.MetatileSize;
        Vector2 boundaryEntry = new(holeCenter.X, tileTop - 5.0f + 0.6f);
        _player.WarpTo(boundaryEntry, recordSafe: false);
        Vector2 expectedCenter = GetActiveTerrain(_player.Position).TileCenter;
        if (expectedCenter.DistanceSquaredTo(holeCenter) > 1.0f)
            throw new InvalidOperationException("Room 0:01 boundary setup did not sample the hole tile.");

        _player._PhysicsProcess(1.0 / 60.0);
        if (!_player.IsPullingIntoHole && !_player.IsFallingInHole)
            throw new InvalidOperationException(
                "Room 0:01 boundary hole entry did not start the pull-in state.");

        AdvanceHolePullUntilFall(holeCenter);
        AdvanceHoleFallUntilRespawn(safePosition);
        if (_player.IsFallingInHole)
            throw new InvalidOperationException("Room 0:01 boundary hole fall did not complete.");
        if (_player.Position.DistanceSquaredTo(safePosition) > 1.0f)
            throw new InvalidOperationException("Room 0:01 hole respawn did not return to the room entry anchor.");
        if (_player.HealthQuarters != beforeHealth - 2)
            throw new InvalidOperationException("Room 0:01 hole fall did not apply half-heart damage.");
    }

    private void AdvanceHolePullUntilFall(Vector2 expectedCenter)
    {
        for (int i = 0; i < 120 && !_player.IsFallingInHole; i++)
            _player._PhysicsProcess(1.0 / 60.0);

        if (!_player.IsFallingInHole)
            throw new InvalidOperationException("Hole pull-in did not transition to the fall animation.");
        if (_player.Position.DistanceSquaredTo(expectedCenter) > 1.0f)
            throw new InvalidOperationException("Hole pull-in did not center Link on the sampled hole tile.");
    }

    private void AdvanceHoleFallUntilRespawn(Vector2 expectedRespawn)
    {
        for (int i = 0; i < 80 && _player.IsFallingInHole; i++)
            _player._PhysicsProcess(1.0 / 60.0);

        if (_player.IsFallingInHole)
            throw new InvalidOperationException("The falling-in-hole animation did not finish.");
        if (_player.Position.DistanceSquaredTo(expectedRespawn) > 1.0f)
            throw new InvalidOperationException("Hole terrain did not return Link to the stored respawn anchor.");
    }

    private static bool TryFindHoleWithSafeNeighbor(
        OracleRoomData room,
        out Vector2 holeCenter,
        out Vector2 safePosition)
    {
        for (int tileY = 1; tileY < room.HeightInTiles; tileY++)
        for (int tileX = 0; tileX < room.WidthInTiles; tileX++)
        {
            Vector2 center = new(
                tileX * OracleRoomData.MetatileSize + 8,
                tileY * OracleRoomData.MetatileSize + 8);
            OracleRoomData.TerrainInfo terrain = room.GetTerrainInfo(center);
            if (terrain.Hazard != OracleRoomData.HazardType.Hole)
                continue;

            foreach (Vector2I direction in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
            {
                Vector2 candidate = center + (Vector2)direction * OracleRoomData.MetatileSize;
                if (candidate.X < 0 || candidate.X >= room.Width ||
                    candidate.Y < 0 || candidate.Y >= room.Height)
                {
                    continue;
                }

                OracleRoomData.TerrainInfo safeTerrain = room.GetTerrainInfo(candidate);
                if (safeTerrain.Hazard == OracleRoomData.HazardType.None &&
                    !RoomCollides(room, candidate))
                {
                    holeCenter = center;
                    safePosition = candidate;
                    return true;
                }
            }
        }

        holeCenter = Vector2.Zero;
        safePosition = Vector2.Zero;
        return false;
    }

    private bool TryFindTerrainSample(
        OracleRoomData.HazardType hazard,
        out int group,
        out int room,
        out Vector2 hazardCenter,
        out Vector2 safePosition)
    {
        for (int candidateGroup = 0; candidateGroup <= 5; candidateGroup++)
        for (int candidateRoom = 0; candidateRoom <= 0xff; candidateRoom++)
        {
            if (!_world.HasRoom(candidateGroup, candidateRoom))
                continue;

            OracleRoomData data = _world.LoadRoom(candidateGroup, candidateRoom);
            Vector2? safe = null;
            Vector2? target = null;

            for (int tileY = 0; tileY < data.HeightInTiles; tileY++)
            for (int tileX = 0; tileX < data.WidthInTiles; tileX++)
            {
                Vector2 center = new(
                    tileX * OracleRoomData.MetatileSize + 8,
                    tileY * OracleRoomData.MetatileSize + 8);
                OracleRoomData.TerrainInfo terrain = data.GetTerrainInfo(center);

                if (terrain.Hazard == OracleRoomData.HazardType.None &&
                    safe == null &&
                    !RoomCollides(data, center))
                {
                    safe = center;
                }
                if (terrain.Hazard == hazard &&
                    terrain.Type == OracleRoomData.TerrainType.Hole &&
                    !RoomCollides(data, center))
                {
                    target = center;
                }
            }

            if (safe != null && target != null)
            {
                group = candidateGroup;
                room = candidateRoom;
                hazardCenter = target.Value;
                safePosition = safe.Value;
                return true;
            }
        }

        group = -1;
        room = -1;
        hazardCenter = Vector2.Zero;
        safePosition = Vector2.Zero;
        return false;
    }

    private static bool RoomCollides(OracleRoomData room, Vector2 playerPosition)
    {
        foreach (Vector2 offset in new[]
        {
            new Vector2(-5, -2), new Vector2(5, -2),
            new Vector2(-5, 5), new Vector2(5, 5)
        })
        {
            Vector2 sample = playerPosition + offset;
            if (sample.X < 0 || sample.X >= room.Width ||
                sample.Y < 0 || sample.Y >= room.Height ||
                room.IsSolid(sample))
            {
                return true;
            }
        }
        return false;
    }

    private void ValidateHealth()
    {
        _dialogue.Close();
        _player.RefillHealth();
        SyncHudToPlayer();

        if (_player.HealthQuarters != 12 || _hud.HealthQuarters != 12 ||
            _hud.MaxHealthQuarters != _player.MaxHealthQuarters)
            throw new InvalidOperationException("Expected Link and the HUD to start with three full hearts.");

        _player.ApplyDamage(1);
        if (_player.HealthQuarters != 11 || _hud.HealthQuarters != 11)
            throw new InvalidOperationException("Direct quarter-heart damage did not synchronize to the HUD.");

        _player.Heal(1);
        if (_player.HealthQuarters != 12 || _hud.HealthQuarters != 12)
            throw new InvalidOperationException("Direct quarter-heart healing did not synchronize to the HUD.");

        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x03);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 safe = new(56, 8);
        _player.WarpTo(safe);
        _player.WarpTo(new Vector2(8, 24), recordSafe: false);

        ValidateDrowningSequence(safe, OracleRoomData.HazardType.Lava);
        if (_player.HealthQuarters != 10 || _hud.HealthQuarters != 10)
            throw new InvalidOperationException(
                "Lava hazard did not synchronize its delayed half-heart damage to the HUD.");

        GD.Print("Validated quarter-heart health, HUD synchronization, and delayed half-heart terrain damage.");
    }

    private void ValidateAnimations()
    {
        OracleRoomData water = _world.LoadRoom(0, 0xb8);
        ulong waterStart = water.GetAnimationChecksum(0);
        bool waterChanged = false;
        for (int tick = 1; tick <= 120 && !waterChanged; tick++)
            waterChanged = water.GetAnimationChecksum(tick) != waterStart;

        OracleRoomData lava = _world.LoadRoom(0, 0x03);
        ulong lavaStart = lava.GetAnimationChecksum(0);
        bool lavaChanged = false;
        for (int tick = 1; tick <= 60 && !lavaChanged; tick++)
            lavaChanged = lava.GetAnimationChecksum(tick) != lavaStart;

        if (!waterChanged || !lavaChanged)
            throw new InvalidOperationException(
                $"Expected animated water and lava frames; water={waterChanged}, lava={lavaChanged}.");

        OracleRoomData waterfall = _world.LoadRoom(0, 0x45);
        const int settledWaterfallTick = 32;
        if (waterfall.AnimationGroup != 0 ||
            !waterfall.HasAnimationOverride(4, settledWaterfallTick) ||
            !waterfall.HasAnimationOverride(236, settledWaterfallTick) ||
            !waterfall.HasAnimationOverride(238, settledWaterfallTick))
        {
            throw new InvalidOperationException(
                "Waterfall animation did not preserve its three alternating VRAM destination writes.");
        }

        // Room textures are cached independently, but the original engine has
        // one shared animated-tile VRAM state. Prime 0:56 with a stale phase,
        // then verify that beginning the 0:55 -> 0:56 scroll synchronizes it
        // to the outgoing room before both textures are shown together.
        OracleRoomData target = _world.LoadRoom(0, 0x56);
        target.UpdateAnimation(0);
        int staleTargetSignature = target.CurrentAnimationSignature;
        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(0, 0x55);
        OracleRoomData source = _currentRoom;
        if (!Mathf.IsZeroApprox((float)_animationTicks))
            throw new InvalidOperationException("Changing animation groups did not reset their shared clock.");
        _animationTicks = 13.0;
        source.UpdateAnimation((long)_animationTicks);
        if (source.CurrentAnimationSignature == staleTargetSignature)
            throw new InvalidOperationException("Animation phase-lock validation chose indistinguishable ticks.");

        _roomView.SetRoom(source.Texture);
        _entities.LoadRoom(_activeGroup, source);
        _player.WarpTo(new Vector2(source.Width + 2.0f, source.Height / 2.0f));
        CheckRoomExit(_player);
        if (!IsTransitioning || _activeGroup != 0 || _currentRoom.Id != 0x56)
            throw new InvalidOperationException("Room 0:55 did not begin its rightward scroll into 0:56.");
        if (source.AnimationGroup != _currentRoom.AnimationGroup ||
            source.CurrentAnimationSignature != _currentRoom.CurrentAnimationSignature)
        {
            throw new InvalidOperationException(
                "Outgoing and incoming water/waterfall tiles began the scroll in different phases.");
        }

        ValidateLinkScrollsForOneTransitionFrame();
        if (source.CurrentAnimationSignature != _currentRoom.CurrentAnimationSignature)
            throw new InvalidOperationException("Animated-tile phases diverged during the frozen scroll.");
        FinishActiveScrollingTransitionForValidation();

        GD.Print("Validated disassembly-driven water and lava animation plus persistent " +
            "three-range waterfall VRAM updates and 0:55 -> 0:56 phase locking.");
    }

    private void ValidateSigns()
    {
        WarpToSignTest();
        if (_dialogue.VisibleLinesPerPage != 2 || _dialogue.TextLineSpacing != 16)
            throw new InvalidOperationException(
                "The textbox does not use the original two 8x16 text rows.");
        if (_currentRoom.GetMetatile(new Vector2(88, 58)) != 0xf2)
            throw new InvalidOperationException("Expected sign metatile $f2 in room 2a at $35.");
        if (!TryInteract(_player) || !_dialogue.IsOpen)
            throw new InvalidOperationException("The room 2a test sign did not open its dialogue.");

        bool arrowBefore = _dialogue.ArrowVisible;
        _dialogue.AdvanceArrowClockForValidation(16.0 / 60.0);
        if (_dialogue.ArrowVisible == arrowBefore)
            throw new InvalidOperationException("The textbox arrow did not toggle after 16 original-engine frames.");
        _dialogue.AdvanceArrowClockForValidation(16.0 / 60.0);
        if (_dialogue.ArrowVisible != arrowBefore)
            throw new InvalidOperationException("The textbox arrow did not complete its 32-frame blink cycle.");

        _dialogue.ShowMessage("First.\nSecond.\nThird.\nFourth.", _player.Position.Y);
        _dialogue.AdvanceOrClose();
        if (!_dialogue.IsScrollingText ||
            !Mathf.IsEqualApprox(_dialogue.TextScrollOffset, 8.0f))
        {
            throw new InvalidOperationException(
                "The button frame did not perform standardTextStateb's first 8px shift.");
        }
        _dialogue.AdvanceTextScrollForValidation(1.0 / 60.0);
        if (!_dialogue.IsScrollingText ||
            !Mathf.IsEqualApprox(_dialogue.TextScrollOffset, 8.0f))
        {
            throw new InvalidOperationException(
                "The standard textbox DMA state did not hold the first 8px shift for one frame.");
        }
        _dialogue.AdvanceTextScrollForValidation(1.0 / 60.0);
        if (!Mathf.IsEqualApprox(_dialogue.TextScrollOffset, 16.0f))
            throw new InvalidOperationException("standardTextState7 did not perform the second 8px shift.");
        _dialogue.AdvanceTextScrollForValidation(5.0 / 60.0);
        if (_dialogue.IsScrollingText || !Mathf.IsZeroApprox(_dialogue.TextScrollOffset))
            throw new InvalidOperationException(
                "The two discrete tile-row scroll sequences did not finish in seven frames.");

        _dialogue.ShowMessage("Last line.", _player.Position.Y);
        _dialogue.AdvanceOrClose();
        if (_dialogue.IsOpen || !_dialogue.BlocksPlayerInput)
            throw new InvalidOperationException("Closing the final textbox did not consume its button press.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (_dialogue.IsOpen)
            throw new InvalidOperationException("The final textbox press immediately restarted the interaction.");

        GD.Print("Validated dialogue spacing, discrete tile-row text scroll, 32-frame arrow blink, and final-page input consumption.");
    }

    private void ValidateNpcs()
    {
        WarpToNpcTest();
        NpcCharacter? villager = _npcNodes.Find(npc => npc.Record.Id == 0x3a && npc.Record.SubId == 0x03);
        if (villager is null)
            throw new InvalidOperationException($"Expected the room 0:48 villager among {_npcNodes.Count} extracted NPCs.");
        if (villager.TextId != 0x1420)
            throw new InvalidOperationException($"Expected room 0:48 villager to resolve TX_1420, got TX_{villager.TextId:x4}.");
        if (villager.Position != new Vector2(0x38, 0x48) ||
            villager.SpriteBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager sprite is not centered on its object tile.");
        }
        if (villager.ObjectCollisionBounds.Size != new Vector2(12.0f, 12.0f) ||
            villager.ObjectCollisionBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager object hitbox does not match the original $06/$06 collision radii.");
        }
        if (villager.LinkBlockingBounds.Size != new Vector2(24.0f, 24.0f) ||
            villager.LinkBlockingBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager does not combine NPC and Link $06 radii into a 24px blocking region.");
        }
        if (!villager.BlocksLinkCenter(villager.Position) ||
            villager.BlocksLinkCenter(villager.Position + new Vector2(0.0f, 12.0f)))
        {
            throw new InvalidOperationException("The room 0:48 villager's strict radius collision boundary is not centered correctly.");
        }
        if (!Collides(villager.Position + new Vector2(0.0f, 11.9f)) ||
            Collides(villager.Position + new Vector2(0.0f, 12.1f)))
        {
            throw new InvalidOperationException("The room 0:48 villager did not stop Link at the original bottom collision radius.");
        }
        if (!TryInteract(_player) || !_dialogue.IsOpen)
            throw new InvalidOperationException("The room 0:48 villager did not open dialogue.");
        int frameBase = villager.Record.TileBase / 4;
        if (villager.CurrentFrameColumn != frameBase)
            throw new InvalidOperationException("The room 0:48 villager did not face down toward Link after talking.");
        villager.UpdateNpc(16.0 / 60.0, _player.Position);
        if (villager.CurrentAnimationFrame != 1)
            throw new InvalidOperationException("The room 0:48 villager did not advance its original 16-frame idle animation.");
        villager.UpdateNpc(1.0 / 60.0, villager.Position + Vector2.Left * 20.0f);
        if (villager.FacingVector != Vector2I.Left || villager.CurrentAnimationFrame != 0)
            throw new InvalidOperationException("The room 0:48 villager did not face nearby Link and reset its animation.");
        villager.UpdateNpc(1.0 / 60.0, villager.Position + Vector2.Right * 20.0f);
        if (villager.FacingVector != Vector2I.Left)
            throw new InvalidOperationException("The NPC facing cooldown did not preserve the current direction.");
        villager.UpdateNpc(30.0 / 60.0, villager.Position + Vector2.Right * 20.0f);
        if (villager.FacingVector != Vector2I.Right || villager.CurrentFrameColumn != frameBase + 2)
            throw new InvalidOperationException("The villager did not use the mirrored side OAM after the 30-frame facing delay.");
        villager.UpdateNpc(30.0 / 60.0, villager.Position + Vector2.Right * 80.0f);
        if (villager.FacingVector != Vector2I.Down)
            throw new InvalidOperationException("The villager did not return to facing down when Link left the $28 awareness radius.");
        GD.Print("Validated villager idle animation, $28 Link awareness, 30-frame facing delay, and TX_1420 dialogue.");
    }

    private static int GetStartingRoom()
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (!argument.StartsWith("--room=", StringComparison.OrdinalIgnoreCase))
                continue;
            string value = argument[7..].Replace("0x", "", StringComparison.OrdinalIgnoreCase);
            if (int.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out int room)
                && room is >= 0 and <= 0xff)
                return room;
        }
        return 0x11;
    }

    private static int GetStartingGroup()
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (!argument.StartsWith("--group=", StringComparison.OrdinalIgnoreCase))
                continue;
            string value = argument[8..].Replace("0x", "", StringComparison.OrdinalIgnoreCase);
            if (int.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out int group)
                && group is >= 0 and <= 5)
                return group;
        }
        return 0;
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
}
