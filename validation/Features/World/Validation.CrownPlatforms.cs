using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownPlatforms()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        LoadValidationRoom(6,0x95);
        // getTileCollisionsAtPosition returns the whole raw byte, including
        // partially solid tiles and special collision $18 (open to Link).
        foreach (byte collision in new byte[] { 0x00,0x01,0x18,0xff })
        {
            _currentRoom.SetPositionTileAndCollision(new(24,24),0xa3,collision,0);
            foreach (var point in new[] { new Vector2(18,18),new Vector2(26,18),new Vector2(18,26),new Vector2(26,26) })
            {
                FailIf(_collision.TileBlocksPointForSidePlatform(point) != (collision != 0) ||
                    _collision.TileBlocksPointForSidePlatform(point + new Vector2(256,-256)) != (collision != 0),
                    $"Crown platform probes must preserve raw collision ${collision:x2} in every quadrant with byte-wrapped coordinates.");
            }
            if (collision == 0x18)
                FailIf(_currentRoom.IsSolid(new(24,24)),"The $18 probe fixture must remain open to ordinary Link movement.");
        }
        // loadRoomCollisions writes $ff to the immediate room borders,
        // irrespective of whether the transition system supplies a neighbor.
        FailIf(!_collision.TileBlocksPointForSidePlatform(new(-1,24)) ||
            !_collision.TileBlocksPointForSidePlatform(new(240,24)) ||
            !_collision.TileBlocksPointForSidePlatform(new(24,176)),
            "Crown platform probes must retain native $ff collision borders.");
        // Native byte comparisons: radiusY=$00 gives the close-test limit
        // $fd; initialized radiusY=$09 gives $0f. The above-platform limit
        // wraps independently (platform Y=$05 yields $fa).
        foreach (var contact in new[] {
            (Y:76, X:120, PlatformY:80, RadiusY:0, RadiusX:0, Air:false, Ride:false),
            (Y:68, X:120, PlatformY:80, RadiusY:9, RadiusX:15, Air:false, Ride:true),
            (Y:69, X:120, PlatformY:80, RadiusY:9, RadiusX:15, Air:false, Ride:false),
            (Y:65, X:120, PlatformY:80, RadiusY:9, RadiusX:15, Air:false, Ride:true),
            (Y:64, X:120, PlatformY:80, RadiusY:9, RadiusX:15, Air:false, Ride:false),
            (Y:68, X:140, PlatformY:80, RadiusY:9, RadiusX:15, Air:false, Ride:true),
            (Y:68, X:140, PlatformY:80, RadiusY:9, RadiusX:15, Air:true, Ride:false),
            (Y:68, X:139, PlatformY:80, RadiusY:9, RadiusX:15, Air:true, Ride:true),
            (Y:249, X:120, PlatformY:5, RadiusY:9, RadiusX:15, Air:false, Ride:true),
            (Y:250, X:120, PlatformY:5, RadiusY:9, RadiusX:15, Air:false, Ride:false) })
        {
            _player.WarpTo(new(contact.X,contact.Y));
            typeof(Player).GetField("_sideScrollAirborne",flags)!.SetValue(_player,contact.Air);
            FailIf(_player.CheckSideScrollPlatformRide(new(120,contact.PlatformY),contact.RadiusY,contact.RadiusX) != contact.Ride,
                $"Crown $a1 riding byte comparison failed at Link Y=${contact.Y:x2}, X=${contact.X:x2}, radiusY=${contact.RadiusY:x2}, airborne={contact.Air}.");
        }
        LoadValidationRoom(6,0x95);
        _player.WarpTo(new(136,92));
        // Isolated state0 overlap fixture: clear only the two obstruction
        // probes above Link. This asserts contact dispatch, not reachability.
        _currentRoom.SetPositionTileAndCollision(new(133,86),0xa3,0,0);
        _entities.BeginScreenTransition(6,_currentRoom,new(240,0),_player);
        var incoming = _entities.Entities<MovingSideScrollPlatformRoomEntity>().Single();
        FailIf(incoming.UpdatesDuringDialogue || incoming.LinkRiding ||
            incoming.PrecisePosition != new Vector2(136,104) || _player.Position != new Vector2(136,89),
            "Crown $a1:$0a preload must check zero-radius riding, initialize without moving, then push Link using initialized radii.");
        for (int i = 0; i < 16; i++) _entities.Update(1.0 / 60.0,_player);
        FailIf(incoming.PrecisePosition != new Vector2(136,104) || incoming.CommandIndex != 0,
            "Initialized Crown platform must remain frozen throughout scrolling.");
        _entities.FinishScreenTransition();
        _entities.Update(1.0 / 60.0,_player);
        FailIf(incoming.PrecisePosition != new Vector2(136,103.5f),
            "Crown platform must move on the first post-scroll update, without repeating state0.");
        foreach (bool batch in new[] { false,true })
        {
            LoadValidationRoom(6,0x96);
            _player.WarpTo(new(136,5));
            _player.SetScriptedPosition(new(136.5f,5.25f));
            // Isolate a single edge transition, including the source clamp
            // and fractional scrolling arithmetic; no dungeon progression.
            _transitions.BeginScroll(_player,Vector2I.Up,0x95);
            var scrollingPlatform = _entities.Entities<MovingSideScrollPlatformRoomEntity>().Single();
            FailIf(_player.PrecisePosition != new Vector2(136.5f,6.25f) || scrollingPlatform.UpdatesDuringDialogue,
                "Crown scroll must retain low coordinate bytes and initialize destination $a1 before scrolling.");
            Application.Capture([],[],Vector2.Zero);
            if (batch) Application.Advance(4.0 / 60.0);
            else for (int i = 0; i < 4; i++) Application.Advance(1.0 / 60.0);
            for (int i = 0; _transitions.ScrollActive && i < 200; i++)
            {
                FailIf(scrollingPlatform.PrecisePosition != new Vector2(136,104),"Crown destination platform moved during scrolling.");
                Application.Advance(1.0 / 60.0);
            }
            FailIf(_transitions.ScrollActive || _player.PrecisePosition != new Vector2(136.5f,166.25f) ||
                scrollingPlatform.PrecisePosition != new Vector2(136,104),
                "Crown upward scroll must apply 32 half-pixel steps and the source-height offset while keeping destination platforms frozen.");
            Application.Advance(1.0 / 60.0);
            FailIf(scrollingPlatform.PrecisePosition != new Vector2(136,103.5f),
                "Crown platform must resume immediately after the gameplay scroll completes.");
        }
        LoadValidationRoom(0,0x60);
        GD.Print("Validated side-platform raw collision probes/riding byte bounds, state0 overlap preload and destination freeze/resume through Crown scrolling.");
    }
}
