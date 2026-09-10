using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    // Feed explicit host samples to the actual application scheduler and
    // complete gameplay loop. Godot's native just-pressed flags otherwise
    // persist across synchronous validation calls within one host frame.
    private void StepSwimmingGameplay(int updates, Vector2 movement,
        string[]? held = null, string[]? pressed = null, bool batched = false)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot)
            .GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot)
            .GetField("_applicationUpdates", flags)!.GetValue(this)!;
        Action advance = typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!
            .CreateDelegate<Action>(this);
        input.CaptureForValidation(held ?? [], pressed ?? [], movement);
        if (batched)
            scheduler.Advance(updates / 60.0, advance);
        else
            for (int i = 0; i < updates; i++) scheduler.Advance(1.0 / 60.0, advance);
    }

    private void PrepareSwimmingRoom(int room = 0x05, bool mermaid = false)
    {
        ReinitializeGameplayForValidation();
        ResetValidationInput();
        _inventory.GiveTreasure(TreasureDatabase.TreasureFlippers, 0);
        _inventory.GiveTreasure(TreasureDatabase.TreasureSword, 0);
        if (mermaid) _inventory.GiveTreasure(TreasureDatabase.TreasureMermaidSuit, 0);
        LoadValidationRoom(7, room);
        // Room 7:05's actual left shaft contains water $1b from (24,8)
        // through (40,72). The room's right shaft contains a dry ladder.
        _player.WarpTo(new Vector2(40, 56));
        FailIf(_rooms.CurrentRoom.GetMetatile(_player.Position) != 0x1b ||
            _collision.Collides(_player.Position),
            "Room 7:05 swimming fixture is not in its reachable water shaft.");
        StepSwimmingGameplay(1, Vector2.Zero);
        FailIf(_player.SideScrollSwimmingState != 2,
            "Room 7:05 did not enter swimming through the gameplay update.");
    }

    private void ValidateSideScrollSwimmingGameplay()
    {
        PrepareSwimmingRoom();
        _inventory.SetScriptedEquippedItems(InventoryState.ItemNone, InventoryState.ItemSword);
        Vector2 start = _player.PrecisePosition;
        StepSwimmingGameplay(1, Vector2.Up, ["move_up", "attack"], ["move_up", "attack"]);
        FailIf(_player.IsAttacking || _player.SideScrollSwimBurstState != 1 ||
            _player.SideScrollSwimBurstCounter != 0x0c || _player.FacingVector != Vector2I.Left,
            "Room 7:05 Flippers A+Up with sword equipped did not use the swim burst and retain horizontal facing.");
        StepSwimmingGameplay(24, Vector2.Up, ["move_up"]);
        FailIf(_player.SideScrollSwimBurstState != 0 || _player.IsAttacking,
            "Room 7:05 A-button Flippers burst did not complete after 25 updates.");

        PrepareSwimmingRoom();
        _inventory.SetScriptedEquippedItems(InventoryState.ItemSword, InventoryState.ItemNone);
        start = _player.PrecisePosition;
        StepSwimmingGameplay(1, Vector2.Up, ["move_up", "item"], ["move_up", "item"]);
        FailIf(!_player.IsAttacking || _player.SwordUsesUnderwaterAnimation ||
            _player.PrecisePosition != start || _player.FacingVector != Vector2I.Up ||
            _player.GetSwordHitbox().Size == Vector2.Zero,
            "Room 7:05 B sword with Flippers did not select normal $22 graphics, lock movement, and keep the sword's upward facing.");
        StepSwimmingGameplay(16, Vector2.Up, ["move_up", "item"]);
        FailIf(_player.SwordState != SwordActionState.Swing || _player.PrecisePosition != start,
            "Room 7:05 sword released its movement lock before the 17-update animation ended.");
        StepSwimmingGameplay(1, Vector2.Up, ["move_up", "item"]);
        FailIf(_player.SwordState != SwordActionState.Held ||
            _player.FacingVector != Vector2I.Right || _player.PrecisePosition.Y >= start.Y,
            "Room 7:05 held sword did not release movement and restore the swim body on the terminal update.");
        StepSwimmingGameplay(1, Vector2.Zero);
        FailIf(_player.IsAttacking, "Room 7:05 B release left a held sword active.");
        StepSwimmingGameplay(1, Vector2.Zero, ["item"], ["item"]);
        FailIf(_player.SwordState != SwordActionState.Swing || _player.SwordUsesUnderwaterAnimation,
            "Room 7:05 repeated B attack lost the Flippers sword animation.");

        PrepareSwimmingRoom(mermaid: true);
        _inventory.SetScriptedEquippedItems(InventoryState.ItemNone, InventoryState.ItemSword);
        StepSwimmingGameplay(1, Vector2.Zero, ["attack"], ["attack"]);
        FailIf(!_player.SwordUsesUnderwaterAnimation || _player.SideScrollSwimBurstState != 0,
            "Room 7:05 Mermaid Suit A sword did not use animation $2d without a Flippers burst.");

        string Cadence(bool batched, bool mermaid)
        {
            PrepareSwimmingRoom(mermaid: mermaid);
            _inventory.SetScriptedEquippedItems(InventoryState.ItemSword, InventoryState.ItemNone);
            StepSwimmingGameplay(19, Vector2.Up, ["move_up", "item"], ["move_up", "item"], batched);
            StepSwimmingGameplay(25, Vector2.Zero, batched: batched);
            return $"{_player.PrecisePosition}:{_player.SideScrollAngle}:{_player.SideScrollSpeedRaw}:" +
                $"{_player.SideScrollSwimAnimationFrame}:{_player.SideScrollSwimAnimationCounter}:" +
                $"{_player.SwordState}:{_entities.RandomCalls}:" +
                string.Join(";", _entities.Entities<SideScrollBubbleRoomEntity>()
                    .Select(b => $"{b.PrecisePosition}:{b.Angle}:{b.TurnCounter}:{b.TurnsRemaining}"));
        }
        foreach (bool mermaid in new[] { false, true })
            FailIf(Cadence(false, mermaid) != Cadence(true, mermaid),
                $"Room 7:05 swim/sword/bubble gameplay differs between individual and batched updates (Mermaid={mermaid}).");

        PrepareSwimmingRoom();
        StepSwimmingGameplay(1, Vector2.Right, ["move_right"], ["move_right"]);
        Vector2 recoilStart = _player.PrecisePosition;
        FailIf(!_player.ApplyEnemyContactDamage(_player.Position + new Vector2(16, 0), 2),
            "Room 7:05 swimming recoil fixture did not accept contact damage.");
        StepSwimmingGameplay(1, Vector2.Right, ["move_right"]);
        FailIf(_player.PrecisePosition != recoilStart + new Vector2(-0.75f, 0) ||
            !_player.SideScrollSwimming || _player.SideScrollSpeedRaw != 0x14,
            "Room 7:05 recoil did not combine leftward SPEED_140 with rightward SPEED_80 swimming in source order.");

        ReinitializeGameplayForValidation();
        LoadValidationRoom(7, 0x05);
        Vector2 safe = new(136, 56);
        _player.WarpTo(safe);
        _player.WarpTo(new Vector2(40, 56), recordSafe: false);
        int health = _inventory.HealthQuarters;
        StepSwimmingGameplay(1, Vector2.Zero);
        FailIf(!_player.IsDrowning, "Room 7:05 without Flippers failed to enter swimming state $03.");
        FailIf(!_player.AcceptsRoomEntityContact,
            "Side-view water entry disabled collisions before the first linkUpdateDrowning update.");
        StepSwimmingGameplay(22, Vector2.Zero);
        FailIf(_player.Position != new Vector2(40, 56) || _inventory.HealthQuarters != health,
            "Side-view drowning moved or damaged Link before the next-update respawn initializer.");
        FailIf(_player.AcceptsRoomEntityContact || _player.AcceptsGroundInteractionContact,
            "linkUpdateDrowning did not disable enemy and interaction collision eligibility.");
        StepSwimmingGameplay(1, Vector2.Zero);
        FailIf(_player.Position != safe || _player.Visible || _inventory.HealthQuarters != health,
            "Side-view drowning did not enter its two-update invisible respawn after animation completion.");
        StepSwimmingGameplay(1, Vector2.Zero);
        FailIf(_player.Visible || _inventory.HealthQuarters != health,
            "Side-view drowning applied damage before its invisible counter reached zero.");
        StepSwimmingGameplay(1, Vector2.Zero);
        // linkApplyDamage halves damageToApply=$fc: two quarter-hearts.
        FailIf(!_player.Visible || _inventory.HealthQuarters != health - 2 || _player.SideScrollSwimming,
            $"Side-view drowning zero update: visible={_player.Visible}, health={_inventory.HealthQuarters}/{health - 2}, swimming={_player.SideScrollSwimmingState}.");
        GD.Print("Validated room 7:05 Flippers/Mermaid A/B routing, sword start/lock/held/release/repeat, and complete gameplay cadence.");
    }

    private void ValidateSideScrollSwimmingExits()
    {
        foreach (bool mermaid in new[] { false, true })
        foreach (Vector2I facing in new[] { Vector2I.Left, Vector2I.Right })
        {
            PrepareSwimmingRoom(mermaid: mermaid);
            _player.Face(facing);
            // Swim through the real collision shaft from well below the edge.
            for (int i = 0; i < 200 && !IsTransitioning; i++)
            {
                // Mermaid Suit uses strokes on fresh directional edges.
                bool release = mermaid && i % 12 == 11;
                StepSwimmingGameplay(1, release ? Vector2.Zero : Vector2.Up,
                    release ? [] : ["move_up"],
                    i == 0 || mermaid && i % 12 == 0 ? ["move_up"] : []);
            }
            FailIf(!IsTransitioning || _activeGroup != 7 || _rooms.CurrentRoom.Id != 0x05 ||
                _player.FacingVector != Vector2I.Up || _player.SideScrollSwimming,
                $"Room 7:05 top water exit did not select upward source transition $03 (Mermaid={mermaid}, facing={facing}, position={_player.Position}).");
            float exitX = _player.PrecisePosition.X;
            float exitY = _player.PrecisePosition.Y;
            // The update that began the warp also runs its first controller tick.
            StepSwimmingGameplay(13, Vector2.Zero);
            FailIf(_activeGroup != 7 || _rooms.CurrentRoom.Id != 0x05 ||
                _player.PrecisePosition.X != exitX || _player.PrecisePosition.Y != exitY - 13,
                "Room 7:05 source exit drifted horizontally or lost its SPEED_100 update count.");
            for (int i = 0; i < 90 && IsTransitioning; i++) StepSwimmingGameplay(1, Vector2.Zero);
            FailIf(IsTransitioning || _activeGroup != 5 || _rooms.CurrentRoom.Id != 0xcc ||
                _player.Position != new Vector2(0x28, 0x18) || _player.SideScrollSwimming,
                $"Room 7:05 exit did not finish at source destination 5:cc/$12: {_activeGroup:x1}:{_rooms.CurrentRoom.Id:x2} {_player.Position}.");
            Vector2 destination = _player.Position;
            StepSwimmingGameplay(1, Vector2.Zero);
            FailIf(_player.PrecisePosition != destination,
                "The first 5:cc destination update retained side-view swim momentum or exit walking.");
        }

        // findScreenEdgeWarpSource copies the DOWN bit as well; the initial
        // facing must not steer a lower-edge transition either.
        LoadValidationRoom(6, 0x27);
        _player.WarpTo(new Vector2(24, 152));
        _player.Face(Vector2I.Left);
        for (int i = 0; i < 40 && !IsTransitioning; i++)
            StepSwimmingGameplay(1, Vector2.Down, ["move_down"]);
        FailIf(!IsTransitioning || _player.FacingVector != Vector2I.Down,
            "Room 6:27 lower ladder exit did not derive DOWN from the edge warp.");
        GD.Print("Validated actual 7:05 underwater top exits with both equipment/facing states, arrival cleanup and 6:27 downward edge direction.");
    }

    private void ValidateSideScrollSwimmingBubbles()
    {
        PrepareSwimmingRoom();
        int calls = _entities.RandomCalls;
        StepSwimmingGameplay(1, Vector2.Zero);
        var bubble = _entities.Entities<SideScrollBubbleRoomEntity>().Single();
        FailIf(_entities.RandomCalls != calls + 2 || bubble.Position != _player.Position ||
            bubble.Angle != 0 || bubble.TurnCounter != 4 || bubble.TurnsRemaining != 5,
            "Room 7:05 bubble did not consume Link's interval draw before the interaction's direction draw, or moved during initialization.");
        Vector2 start = bubble.PrecisePosition;
        StepSwimmingGameplay(3, Vector2.Zero);
        FailIf(bubble.PrecisePosition != start + new Vector2(0, -1.5f) ||
            bubble.Angle != 0 || bubble.TurnCounter != 1,
            "INTERAC_BUBBLE $91:$00 did not apply three exact SPEED_80/$00 displacements before turning.");
        StepSwimmingGameplay(1, Vector2.Zero);
        FailIf(bubble.Angle is not (1 or 0x1f) || bubble.TurnCounter != 4 || bubble.TurnsRemaining != 4,
            "INTERAC_BUBBLE $91:$00 did not turn on update four.");
        int firstAngle = bubble.Angle;
        StepSwimmingGameplay(16, Vector2.Zero);
        FailIf(bubble.TurnsRemaining != 8 || bubble.Angle != (firstAngle == 1 ? 3 : 0x1d),
            "INTERAC_BUBBLE $91:$00 did not reverse before its fifth angle addition.");

        // Constructor and water deletion consume no RNG. The source only
        // copies coordinate high bytes and checks hazardCollisionTable first.
        OracleRandom random = new();
        var invalid = new SideScrollBubbleRoomEntity(new Vector2(88.75f, 56.5f), _rooms.CurrentRoom, random);
        AddChild(invalid);
        invalid.UpdateFrame(new RoomEntityFrame(_player, 0, false), new List<RoomEntitySpawn>());
        FailIf(!invalid.Finished || random.Calls != 0 || invalid.PrecisePosition != new Vector2(88, 56),
            "INTERAC_BUBBLE $91:$00 dry initialization consumed RNG or copied fractional coordinates.");
        invalid.Free();
        GD.Print("Validated swimming bubble source RNG order, initialization, fixed-point movement, 4/5/8 turning boundaries, and dry deletion.");
    }

    private void ValidateSideScrollSwimmingKinematics()
    {
        var inventory = new InventoryState(_treasures, OracleSaveData.CreateStandardGame());
        inventory.GiveTreasure(TreasureDatabase.TreasureFlippers, 0);
        SideScrollTerrainState water = new(0x1b, 0x1b,
            SideScrollTileType.Water, SideScrollTileType.Water);
        var world = new ValidationRingPlayerWorld { SideScrolling = true, SideScrollTerrain = water };
        var swimmer = new Player();
        AddChild(swimmer);
        swimmer.Initialize(world, inventory, new Vector2(80, 80), new OracleRandom());
        try
        {
            swimmer.AdvanceSideScrollUpdateForValidation(Vector2.Zero);
            FailIf(world.DrowningSplashes.Single().Position != new Vector2(80, 77),
                "linkCreateSplash lost its side-view Y-$03 high-byte offset.");
            swimmer.AdvanceSideScrollUpdateForValidation(Vector2.Right);
            Vector2 beforeTurn = swimmer.PrecisePosition;
            swimmer.AdvanceSideScrollUpdateForValidation(Vector2.Left);
            FailIf(swimmer.FacingVector != Vector2I.Left || swimmer.SideScrollAngle != 8 ||
                swimmer.SideScrollSpeedRaw != 0x0f || swimmer.PrecisePosition.X <= beforeTurn.X,
                "Swim facing did not reverse before func_5933's still-rightward braking velocity.");
            // With speed $0f, eight turns reach the leftward target; state 1
            // immediately decrements $0d to $0c and adds five to speedTmp.
            swimmer.AdvanceSideScrollUpdateForValidation(Vector2.Left, attackJustPressed: true);
            FailIf(swimmer.SideScrollAngle != 0x18 || swimmer.SideScrollSpeedRaw != 0x19 ||
                swimmer.SideScrollSwimBurstCounter != 0x0c,
                "A+Left did not turn the burst using this update's facing before its eight func_5933 calls.");
            Vector2 lockedPosition = swimmer.PrecisePosition;
            int animationCounter = swimmer.SideScrollSwimAnimationCounter;
            world.MovementDisabled = true;
            swimmer.AdvanceSideScrollUpdateForValidation(Vector2.Right, attackJustPressed: true);
            FailIf(swimmer.PrecisePosition != lockedPosition || swimmer.SideScrollAngle != 0x18 ||
                swimmer.SideScrollSwimBurstCounter != 0x0c ||
                swimmer.SideScrollSwimAnimationCounter != animationCounter - 1,
                "wLinkImmobilized failed to freeze swim velocity/burst while still animating.");
            world.MovementDisabled = false;

            swimmer.WarpTo(new Vector2(80, 80));
            world.SideScrollTerrain = water;
            swimmer.AdvanceSideScrollUpdateForValidation(Vector2.Zero);
            world.SideScrollTerrain = new SideScrollTerrainState(0x01, 0x1a,
                SideScrollTileType.None, SideScrollTileType.Ladder | SideScrollTileType.Water);
            int splashes = world.DrowningSplashes.Count;
            swimmer.AdvanceSideScrollUpdateForValidation(Vector2.Up);
            FailIf(swimmer.SideScrollSwimming || swimmer.SideScrollAirborne ||
                world.DrowningSplashes.Count != splashes,
                "The CURRENT y+$08 underwater-ladder probe did not suppress the water-exit hop.");

            swimmer.WarpTo(new Vector2(80, 80));
            world.SideScrollTerrain = new SideScrollTerrainState(0x1a, 0x1a,
                SideScrollTileType.Ladder | SideScrollTileType.Water,
                SideScrollTileType.Ladder | SideScrollTileType.Water);
            swimmer.AdvanceSideScrollUpdateForValidation(Vector2.Zero);
            world.SideScrollTerrain = default;
            swimmer.AdvanceSideScrollUpdateForValidation(Vector2.Right);
            FailIf(!swimmer.SideScrollAirborne || swimmer.SideScrollSwimming ||
                swimmer.AirborneLinkUsesJumpAnimation || swimmer.SideScrollSpeedRaw != 0x14 ||
                swimmer.SideScrollSpeedZ != -0x17c ||
                swimmer.PrecisePosition != new Vector2(80.5f, 78.375f) ||
                swimmer.SideScrollSwimAnimationCounter != 8 ||
                world.DrowningSplashes.Count != splashes + 2,
                "Water exit used the previous tile, reset speed/animation, or lost the -$01a0 hop plus $24 gravity.");
        }
        finally { swimmer.Free(); }
        GD.Print("Validated source swim facing/braking/A-turn order, immobilization, Y-$03 splash, current below-ladder probe, and exact retained-speed water hop.");
    }
}
