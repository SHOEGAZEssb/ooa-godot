using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullSideViewTraversal()
    {
        bool batch = false;
        void Step(int count = 1, Vector2 move = default, bool jump = false) =>
            StepGameplayUpdates(count, move, jump ? ["attack"] : [], jump ? ["attack"] : [], batched: batch);
        var placements = new MovingSideScrollPlatformDatabase().GetRoomRecords(4, 0x68);
        var script = new DungeonInteractionDatabase().SidePlatform(1);
        FailIf(placements is not [{ Order: 0, Id: 0xa1, SubId: 1, Y: 0x48, X: 0x58 },
                { Order: 1, Id: 0xa1, SubId: 1, Y: 0x48, X: 0x98 }] ||
            script is not { Speed: 20, Direction: 4, RadiusY: 9, RadiusX: 7 } ||
            script.Commands is not [{ Direction: MovingSideScrollPlatformDirection.Up, Endpoint: 0x28 },
                { Direction: MovingSideScrollPlatformDirection.Down, Endpoint: 0x68 }],
            "6:68 lost its ordered a1:01 objects or SPEED_80/up28/down68 script.");
        var random = CaptureOracleRandomForValidation();
        var checkpoints = new List<(Vector2, Vector2, Vector2, int, int)>();
        foreach (bool batched in new[] { false, true })
        {
            batch = batched;
            RestoreOracleRandomForValidation(random);
            LoadValidationRoom(4, 0x86);
            _inventory.RefillHealth();
            _player.WarpTo(new Vector2(56,56));
            FailIf(_currentRoom.IsSolid(_player.Position), "4:86's stair approach is blocked.");
            for (int i = 0; !IsTransitioning && i < 50; i++) Step(move: Vector2.Up);
            FailIf(!IsTransitioning, "4:86 stairs must be reachable by walking from their actual south approach.");
            for (int i = 0; IsTransitioning && i < 200; i++) Step();
            FailIf(IsTransitioning || _activeGroup != 6 || _currentRoom.Id != 0x68 || _player.Position != new Vector2(24,8),
                "4:86 stairs did not warp to 6:68.");
            var platforms = _entities.Entities<MovingSideScrollPlatformRoomEntity>().ToArray();
            FailIf(platforms.Length != 2 || platforms[0].Position.X != 88 || platforms[1].Position.X != 152 ||
                platforms[0].Position.Y != platforms[1].Position.Y,
                "6:68 must instantiate the two a1:01 platforms in source order.");
            // Combat fixture isolates the platform route from Keese knockback.
            // All bats pass through the combat/death owner before the crossing;
            // Link then uses only ordinary movement and Feather input.
            for (int i = 0; _entities.Entities<KeeseCharacter>().Count != 0 && i < 8; i++)
            {
                _entities.ApplySwordHit(new Rect2(Vector2.Zero, new Vector2(240,176)), _player.Position, damage: 0x7f);
                Step(40);
            }
            FailIf(_entities.Entities<KeeseCharacter>().Count != 0, "Side-view Keese combat fixture did not finish.");
            _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
            _inventory.EquipA(InventoryState.ItemFeather);
            int saved = 0;
            void Checkpoint()
            {
                FailIf(_activeGroup != 6 || _currentRoom.Id != 0x68 || _player.IsDying || _player.IsDrowning,
                    "Link left the side-view room or entered a hazard during the crossing.");
                var value = (_player.PrecisePosition, platforms[0].PrecisePosition, platforms[1].PrecisePosition,
                    platforms[0].CommandIndex, platforms[1].CommandIndex);
                if (!batch) checkpoints.Add(value);
                else FailIf(checkpoints[saved++] != value, "Side-view crossing diverged under batched host updates.");
            }
            Step(32, Vector2.Down);
            FailIf(_player.Position != new Vector2(24,40) || !_player.SideScrollClimbing,
                "Link must descend the actual entrance ladder before the first jump.");
            // Upward jumps retain the preceding angle; establish rightward
            // movement before pressing the Feather while climbing.
            Step(move: Vector2.Right);
            Step(move: Vector2.Right, jump: true);
            Step(25, Vector2.Right);
            Step(20);
            FailIf(_player.Position != new Vector2(50,25) || _player.SideScrollAirborne,
                "The first Feather jump did not land on the native left pillar.");
            Checkpoint();
            Step(10, Vector2.Right);
            Step(move: Vector2.Right, jump: true);
            Step(25, Vector2.Right);
            // Source airborne momentum persists on release; opposite input
            // brakes Link before he falls onto the narrow platform.
            Step(8, Vector2.Left);
            Step(50);
            FailIf(!platforms[0].LinkRiding, "Feather input did not board the first platform.");
            Checkpoint();
            int wait = 0;
            while ((!platforms[0].LinkRiding || platforms[0].Position.Y > 42) && wait++ < 300) Step();
            FailIf(wait >= 300 || !platforms[0].LinkRiding || _player.SideScrollAirborne,
                "Link did not remain supported through the first platform's upward return.");
            Checkpoint();
            Step(move: Vector2.Right);
            Step(move: Vector2.Right, jump: true);
            Step(25, Vector2.Right);
            Step(30);
            FailIf(_player.Position != new Vector2(116,25) || _player.SideScrollAirborne || platforms[0].LinkRiding,
                "The first platform jump did not reach the middle pillar.");
            Checkpoint();
            Step(8, Vector2.Right);
            Step(move: Vector2.Right, jump: true);
            Step(25, Vector2.Right);
            Step(8, Vector2.Left);
            Step(50);
            FailIf(!platforms[1].LinkRiding, "Feather input did not board the second platform.");
            Checkpoint();
            wait = 0;
            while ((!platforms[1].LinkRiding || platforms[1].Position.Y > 42) && wait++ < 300) Step();
            FailIf(wait >= 300 || !platforms[1].LinkRiding || _player.SideScrollAirborne,
                "Link did not remain supported through the second platform's upward return.");
            Checkpoint();
            Step(move: Vector2.Right);
            Step(move: Vector2.Right, jump: true);
            Step(59, Vector2.Right);
            Step(60);
            for (int i = 0; _player.Position.X < 216 && i < 40; i++) Step(move: Vector2.Right);
            for (int i = 0; _player.Position.X > 216 && i < 40; i++) Step(move: Vector2.Left);
            FailIf(_player.Position.X != 216 || platforms.Any(p => p.LinkRiding),
                "The final platform jump did not reach the native exit stairs and ladder.");
            for (int i = 0; !IsTransitioning && i < 50; i++) Step(move: Vector2.Down);
            FailIf(!IsTransitioning, "The bottom ladder did not trigger 6:68's edge warp.");
            for (int i = 0; IsTransitioning && i < 200; i++) Step();
            FailIf(IsTransitioning || _activeGroup != 4 || _currentRoom.Id != 0x6c || _player.Position != new Vector2(56,40),
                "6:68's bottom exit must arrive at 4:6c destination23.");
            FailIf(_entities.Entities<MovingSideScrollPlatformRoomEntity>().Count != 0,
                "Side-view platforms survived their room exit.");
        }
    }
}
