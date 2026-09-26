using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonMinecarts()
    {
        void Step(int count = 1, Vector2 move = default) =>
            StepGameplayUpdates(count, move, [], [], batched: true);
        static Vector2 Point(int p) => new((p & 15) * 16 + 8, (p >> 4) * 16 + 8);
        var data = new StaticDungeonObjectDatabase();
        var runtime = _entities.RuntimeState;
        var records = data.Minecarts(4);
        // Independently transcribed from dungeon4StaticObjects, including slot order.
        FailIf(records.Select(r => (r.Slot, r.Room, r.Y, r.X)).ToArray() is not
            [(0, 0x73, 0x58, 0x48), (1, 0x75, 0x58, 0xa8), (2, 0x78, 0x88, 0x78), (3, 0x89, 0x88, 0x48)],
            "Dungeon 4 lost its four source static minecart records or ordering.");
        FailIf(data.Minecarts(1).Count != 0 || data.Minecarts(3).Count != 0 ||
            data.Minecarts(2).Count != 3 || data.Minecarts(8).Count != 2 || data.Minecarts(11).Count != 1,
            "Static object pointer aliases or other dungeon lists changed.");
        void AssertInitialBuffer()
        {
            for (int slot = 0; slot < 8; slot++)
            {
                byte[] expected = slot < 4
                    ? [3, (byte)records[slot].Room, 0x16, 0, (byte)records[slot].Y, (byte)records[slot].X, 0, 0]
                    : new byte[8];
                for (int offset = 0; offset < 8; offset++)
                    FailIf(runtime.ReadWramByte(0xcd80 + slot * 8 + offset) != expected[offset],
                        $"Dungeon entry did not replace static slot ${slot:x2} byte ${offset:x2}.");
            }
        }
        MinecartRuntimeState.Reset(runtime, data.Minecarts(2));
        LoadValidationRoom(4, 0x91);
        _player.WarpTo(new Vector2(120, 152));
        _entities.LoadRoom(4, _currentRoom, EnemyPlacementContext.FromWarpDestination(0xff));
        Step();
        AssertInitialBuffer();
        _dialogue.Close();

        for (int i = 0; i < 11; i++) _inventory.GiveTreasure(TreasureId.HeartContainer, 4);
        // The room transitions/endpoints follow dungeon04Layout and the native
        // track bytes, not the runtime's track routing table.
        var cases = new[] {
            (Room: 0x73, Platform: 0x55, Move: Vector2.Left, Route: new[] { 0x73, 0x72, 0x73 }, End: 0x54, Blue: false),
            (Room: 0x73, Platform: 0x55, Move: Vector2.Left, Route: new[] { 0x73, 0x72, 0x76 }, End: 0x32, Blue: true),
            (Room: 0x75, Platform: 0x59, Move: Vector2.Right, Route: new[] { 0x75, 0x76, 0x77, 0x7c, 0x81 }, End: 0x47, Blue: false),
            (Room: 0x78, Platform: 0x88, Move: Vector2.Left, Route: new[] { 0x78 }, End: 0x87, Blue: false),
            (Room: 0x78, Platform: 0x88, Move: Vector2.Left, Route: new[] { 0x78, 0x77, 0x73 }, End: 0x37, Blue: true),
            (Room: 0x89, Platform: 0x83, Move: Vector2.Right, Route: new[] { 0x89, 0x8f, 0x92 }, End: 0x32, Blue: false) };
        foreach (var c in cases)
        foreach (bool batched in new[] { false, true })
        {
            MinecartRuntimeState.Reset(runtime, records);
            runtime.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0);
            // Set up both source gate inputs. Actual color-puzzle controls
            // are exercised separately; these cases isolate the rail routes.
            LoadValidationRoom(4, 0x72);
            _currentRoom.SetPositionTileAndCollision(Point(0x8d), c.Blue ? (byte)0xaf : (byte)0xad, null, 0);
            _currentRoom.SetUnderlyingMetatile(Point(0x8d), c.Blue ? (byte)0xaf : (byte)0xad);
            LoadValidationRoom(4, c.Room);
            _player.WarpTo(Point(c.Platform));
            _inventory.RefillHealth();
            FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                $"4:{c.Room:x2} boarding must approach from its real platform tile.");
            // Isolate cart handoffs from combat; the native room geometry and
            // mechanisms remain active, including platforms, cube and gates.
            _player.SetBraceletLiftCollisionsDisabled(true);
            if (c.Room == 0x78 && c.Blue)
                _entities.Entities<ColoredCubeRoomEntity>().Single().ColoredCubePuzzleState.CubeColor = 0x82;
            var cart = _entities.Entities<MinecartRoomEntity>().Single();
            for (int n = 0; !cart.Mounting && n < 40; n++) Step(move: c.Move);
            FailIf(!cart.Mounting || !_player.MinecartJumpActive,
                $"4:{c.Room:x2} cart could not be boarded through its platform: Link {_player.Position}, cart {cart.Position}.");
            for (int n = 0; !cart.Riding && n < 80; n++) Step();
            FailIf(!cart.Riding || !_player.MinecartRideActive || MinecartRuntimeState.StationaryInRoom(runtime, c.Room).Any(),
                "Boarding did not transfer the static cart into the active companion slot.");
            var route = new List<int> { c.Room };
            for (int n = 0; !cart.Dismounting && n < 4000; n++)
            {
                bool scrolling = _transitions.ScrollActive;
                var ropes = _entities.Entities<RopeCharacter>().Select(r => (Entity: r, r.Position, r.State, r.Counter, r.ZFixed)).ToArray();
                var platforms = _entities.Entities<MovingPlatformRoomEntity>().Select(p => (Entity: p, p.Position, p.Counter, p.Command)).ToArray();
                Step(batched ? 3 : 1);
                if (route[^1] != _currentRoom.Id) route.Add(_currentRoom.Id);
                if (scrolling && _transitions.ScrollActive)
                {
                    foreach (var rope in ropes)
                        FailIf(rope.Entity.Position != rope.Position || rope.Entity.State != rope.State ||
                            rope.Entity.Counter != rope.Counter || rope.Entity.ZFixed != rope.ZFixed,
                            "Destination Rope advanced its fall delay, movement or Z while scrolling.");
                    foreach (var platform in platforms)
                        FailIf(platform.Entity.Position != platform.Position || platform.Entity.Counter != platform.Counter ||
                            platform.Entity.Command != platform.Command,
                            "Destination moving platform advanced its mini-script during scrolling.");
                }
                if (!scrolling && _transitions.ScrollActive)
                {
                    foreach (var rope in _entities.Entities<RopeCharacter>())
                        FailIf(rope.Record.SubId == 1 && (rope.Visible || rope.State != RopeState.FallSetup || rope.Counter != 0),
                            "Falling Rope preload must perform state0 but leave fall-delay state8 untouched and hidden.");
                    foreach (var platform in _entities.Entities<MovingPlatformRoomEntity>())
                        FailIf(!platform.Visible || platform.Counter != 8 || platform.Command != 1 || platform.LinkRiding,
                            "Moving-platform preload must finish state0 with visible graphics and a full wait08, without claiming Link.");
                }
                _inventory.RefillHealth();
            }
            FailIf(!cart.Dismounting || !route.SequenceEqual(c.Route) || cart.Position != Point(c.End),
                $"4:{c.Room:x2} cart route/end diverged: {string.Join(',', route.Select(r => r.ToString("x2")))} " +
                $"at {cart.Position}, direction {cart.Direction}, riding {cart.Riding}, Link {_player.Position}.");
            for (int n = 0; cart.Dismounting && n < 80; n++) Step();
            FailIf(cart.Dismounting || _player.MinecartRideActive || _player.MinecartJumpActive ||
                !MinecartRuntimeState.StationaryInRoom(runtime, c.Route[^1]).Any(r => r.Position == Point(c.End)),
                "Dismount did not restore the cart in the static buffer and release Link.");
            Vector2 landing = _player.PrecisePosition;
            FailIf(_currentRoom.IsSolid(landing) || _currentRoom.GetTerrainInfo(landing).Hazard != HazardType.None,
                "The native dismount jump did not reach safe floor.");
            int destination = _currentRoom.Id;
            LoadValidationRoom(4, 0x91);
            LoadValidationRoom(4, destination);
            _player.WarpTo(landing);
            cart = _entities.Entities<MinecartRoomEntity>().Single(r => r.Position == Point(c.End));
            Vector2 difference = cart.Position - landing;
            Vector2 approach = Math.Abs(difference.X) > Math.Abs(difference.Y)
                ? new Vector2(Math.Sign(difference.X), 0) : new Vector2(0, Math.Sign(difference.Y));
            for (int n = 0; !cart.Mounting && n < 60; n++) Step(move: approach);
            FailIf(!cart.Mounting, $"Parked cart in 4:{destination:x2} could not be boarded again after room reload: " +
                $"landing {landing}, Link {_player.PrecisePosition}, cart {cart.Position}, counter {cart.PushCounter}, " +
                $"facing {_player.FacingVector}, air {_player.TopDownAirborne}, approach {approach}.");
            _player.SetBraceletLiftCollisionsDisabled(false);
            LoadValidationRoom(4, 0x91);
        }
        GD.Print("Validated Skull Dungeon's static cart entry buffer, four boarding approaches, native rail routes, parking/re-entry and batched gameplay updates.");
    }
}
