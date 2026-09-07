using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRaftFidelity()
    {
        RaftBehavior behavior = new RaftDatabase().Behavior;
        _saveData.SetGlobalFlag(behavior.ChangedRoomsFlag);
        LoadValidationRoom(1, 0xa7);
        RaftRoomEntity raft = _entities.Entities<RaftRoomEntity>().Single();
        OracleRoomData room = _rooms.CurrentRoom;
        for (int y = 8; y < room.Height; y += 16)
        for (int x = 8; x < room.Width; x += 16)
            room.SetPositionTileAndCollision(new Vector2(x, y), 0xfc, 0x10, 0);

        void Step()
        {
            _entities.UpdateRaftBeforePlayer(_player);
            _player.AdvanceApplicationUpdate();
            _entities.Update(1.0 / 60.0, _player);
        }
        void InputStep(params string[] actions)
        {
            foreach (string action in actions) Input.ActionPress(action);
            Step();
            foreach (string action in actions) Input.ActionRelease(action);
        }
        void PlaceRaft(Vector2 position)
        {
            typeof(RaftRoomEntity).GetField("_precisePosition",
                BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(raft, position);
            raft.Position = OracleObjectMath.ToPixelPosition(position);
            raft.UpdatePlayerForcedMovement(_player);
        }
        int InstrumentLock() => (int)typeof(Player).GetField("_instrumentsDisabledCounter",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_player)!;

        // interactionCodee6: the $09 box supplies support, but only the $05
        // box allocates w1Companion. Equality lies outside either radius.
        Vector2 origin = raft.PrecisePosition;
        foreach (int dx in new[] { 9, 8, 5 })
        {
            _player.WarpTo(origin + new Vector2(dx, -5), recordSafe: false);
            _entities.Update(1.0 / 60.0, _player);
            FailIf(raft.LinkRiding || raft.UsesSpecialObjectSlot ||
                _entities.PlayerRidingObject != (dx < 9),
                $"INTERAC_RAFT $e6 approach boundary dx=${dx:x2} confused support and mounting.");
        }
        _player.StartHarpActionForValidation();
        FailIf(_player.IsUsingHarp || InstrumentLock() != 5,
            "INTERAC_RAFT $e6 did not publish its five-update instrument lock.");
        _player.WarpTo(origin + new Vector2(4.75f, -4.75f), recordSafe: false);
        _entities.Update(1.0 / 60.0, _player);
        FailIf(raft.LinkRiding || !raft.UsesSpecialObjectSlot ||
            CompanionRuntimeState.ReadRemembered(_entities.RuntimeState).Id != 0,
            "INTERAC_RAFT $e6 performed SPECIALOBJECT_RAFT $13 initialization before its next pass.");
        Step();
        FailIf(!raft.LinkRiding || _player.PrecisePosition != origin + new Vector2(0.75f, -4.75f),
            "SPECIALOBJECT_RAFT $13 failed its following-update mount or copied Link's fractional bytes.");
        RememberedCompanion remembered = CompanionRuntimeState.ReadRemembered(_entities.RuntimeState);
        Vector2 respawn = _player.LocalRespawnPosition;
        Step(); // First idle update establishes var3e=$ff, var3f=$04.

        InputStep("move_up", "move_right");
        FailIf(raft.Direction != 0, "SPECIALOBJECT_RAFT $13 changed DIR_UP on an up-right diagonal.");
        InputStep("move_right");
        InputStep("move_right", "move_down");
        FailIf(raft.Direction != 1, "SPECIALOBJECT_RAFT $13 changed DIR_RIGHT on a down-right diagonal.");
        InputStep("move_up", "move_left");
        FailIf(raft.Direction != 3, "SPECIALOBJECT_RAFT $13 did not choose DIR_LEFT from an unrelated diagonal facing.");
        FailIf(CompanionRuntimeState.ReadRemembered(_entities.RuntimeState) != remembered ||
            _player.LocalRespawnPosition != respawn,
            "SPECIALOBJECT_RAFT $13 movement overwrote the mount checkpoint.");
        Vector2 opposedStart = raft.PrecisePosition;
        Vector2I opposedFacing = _player.FacingVector;
        InputStep("move_up", "move_down", "move_left");
        FailIf(raft.PrecisePosition != opposedStart || raft.Angle != 0xff ||
            _player.FacingVector != opposedFacing,
            "SPECIALOBJECT_RAFT $13 moved with opposing vertical keys and a third direction.");

        // parentItemCode_shooter allows the raft; parentItemCode_satchel has
        // an explicit SPECIALOBJECT_RAFT rejection instead.
        _inventory.GiveTreasure(new TreasureObjectRecord(
            "VALIDATION_RAFT_SHOOTER", InventoryState.ItemShooter, 0, 1, 0xff, 0, string.Empty));
        _inventory.GiveTreasure(TreasureDatabase.TreasureEmberSeeds, 0x20);
        _inventory.SelectShooterSeeds(0);
        _inventory.EquipA(InventoryState.ItemShooter);
        Input.ActionPress("attack");
        Step();
        FailIf(!_player.IsUsingSeedShooter, "SPECIALOBJECT_RAFT $13 incorrectly disabled the Seed Shooter.");
        opposedStart = raft.PrecisePosition;
        InputStep("move_right");
        FailIf(raft.PrecisePosition != opposedStart,
            "SPECIALOBJECT_RAFT $13 ignored the aiming Seed Shooter's immobilization.");
        Input.ActionRelease("attack");
        _seedSatchel.InterruptShooter();

        // wLinkObjectIndex points at the raft: collision is six pixels on each
        // axis around its ground center, with the original half-open byte edges.
        Vector2 center = OracleObjectMath.ToPixelPosition(raft.PrecisePosition);
        FailIf(_player.EnemyContactPosition != raft.PrecisePosition ||
            !_player.OverlapsEnemyCollision(new Rect2(center + new Vector2(-1, 6), new Vector2(2, 2))) ||
            _player.OverlapsEnemyCollision(new Rect2(center + new Vector2(-1, -8), new Vector2(2, 2))) ||
            _player.OverlapsEnemyCollision(new Rect2(center - Vector2.One, Vector2.One * 2), 7),
            "SPECIALOBJECT_RAFT $13 collision used Link's raised sprite or lost the signed boundary.");

        // One remaining recoil update must still move at SPEED_100; Link must
        // neither consume it early nor add ordinary SPEED_140 displacement.
        FailIf(!_player.ApplyEnemyContactDamage(center + Vector2.Left * 20, 1),
            "SPECIALOBJECT_RAFT $13 knockback fixture did not accept damage.");
        typeof(Player).GetField("_enemyKnockbackFrames", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_player, 1.0f);
        Vector2 before = raft.PrecisePosition;
        Step();
        FailIf(raft.PrecisePosition != before + Vector2.Right || _player.KnockbackFrames != 0,
            "SPECIALOBJECT_RAFT $13 lost its zero-counter SPEED_100 recoil update.");
        FailIf(CompanionRuntimeState.ReadRemembered(_entities.RuntimeState) != remembered,
            "SPECIALOBJECT_RAFT $13 recoil overwrote remembered coordinates.");

        // Link's own facing is not overwritten by func_410d @ridingRaft.
        _player.Face(Vector2I.Down);
        raft.UpdatePlayerForcedMovement(_player);
        FailIf(_player.FacingVector != Vector2I.Down,
            "SPECIALOBJECT_RAFT $13 forced Link to use the raft's independent direction.");
        _player.StartSwordAttack();
        before = raft.PrecisePosition;
        InputStep("move_right");
        FailIf(raft.PrecisePosition != before || raft.Direction != 1,
            "SPECIALOBJECT_RAFT $13 ignored wLinkImmobilized or stopped updating direction during a sword swing.");
        _player.AdvanceSwordForValidation(17, buttonHeld: true);
        FailIf(_player.IsAttacking,
            "SPECIALOBJECT_RAFT $13 allowed swordParent state $06 to enter held/charging state.");

        _inventory.GiveTreasure(TreasureDatabase.TreasureBombs, 0x10);
        FailIf(!_bomb.TryUse(_player), "SPECIALOBJECT_RAFT $13 incorrectly rejected ITEM_BOMB.");
        before = raft.PrecisePosition;
        InputStep("move_left");
        FailIf(raft.PrecisePosition != before || _player.FacingVector != Vector2I.Left,
            "SPECIALOBJECT_RAFT $13 bomb lifting must immobilize movement while allowing Link to turn.");
        _bomb.Interrupt(_player, discard: true);

        // Animation advances once per special-object pass, despite the later
        // interaction pass. Source frames each last $0c updates.
        InputStep("move_up");
        center = OracleObjectMath.ToPixelPosition(raft.PrecisePosition);
        for (int update = 0; update < 11; update++) Step();
        FailIf(Mathf.FloorToInt(_player.PrecisePosition.Y) != center.Y - 5,
            "SPECIALOBJECT_RAFT $13 bob advanced before animation counter $0c elapsed.");
        Step();
        FailIf(Mathf.FloorToInt(_player.PrecisePosition.Y) != center.Y - 6,
            "SPECIALOBJECT_RAFT $13 bob omitted animation parameter $04 on the zero update.");

        // A collision-$00 landing can still be blocked by the bottom samples
        // at y+7. The old y+6 approximation misses the next metatile here.
        Vector2 landing = new(72, 9);
        room.SetPositionTileAndCollision(landing, 0, 0, 0);
        room.SetPositionTileAndCollision(new Vector2(72, 17), 0, 0x0f, 0);
        FailIf(raft.CanDismountAt(landing),
            "SPECIALOBJECT_RAFT $13 omitted calculateAdjacentWallsBitset's y+$07 probes.");

        // Dock above a raft at y=$55. Holding a diagonal while both axes are
        // blocked dismounts in the preserved cardinal direction.
        PlaceRaft(new Vector2(72, 85));
        room.SetPositionTileAndCollision(new Vector2(72, 72), 0, 0, 0);
        room.SetPositionTileAndCollision(new Vector2(72, 88), 0xfc, 0x10, 0);
        Step();
        // Directly supply the source blocked-wall state without changing the
        // actual dock data that calculateAdjacentWallsBitset consumes.
        MethodInfo dismount = typeof(RaftRoomEntity).GetMethod("TryDismount",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        typeof(RaftRoomEntity).GetField("_dismountAngle", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(raft, 4);
        FailIf(!(bool)dismount.Invoke(raft, [_player])!,
            "SPECIALOBJECT_RAFT $13 incorrectly rejected a diagonal dismount request.");
        for (int update = 0; update < 11; update++)
        {
            Step();
            FailIf(!raft.DisablesMenus || !CompanionRuntimeState.IsActive(_entities.RuntimeState, 0x13),
                "SPECIALOBJECT_RAFT $13 released its slot/menu before counter $0c reached zero.");
        }
        Step();
        FailIf(raft.DisablesMenus || raft.UsesSpecialObjectSlot ||
            CompanionRuntimeState.IsActive(_entities.RuntimeState, 0x13) ||
            raft.ZIndex != NpcCharacter.FixedLowPriorityZIndex,
            "SPECIALOBJECT_RAFT $13 state $02 did not recreate INTERAC_RAFT $e6:$02 on its zero update.");

        Step();
        Step(); // Complete Link's separate 14-update forced walk.
        _player.WarpTo(raft.PrecisePosition + new Vector2(0.75f, -4.75f), recordSafe: false);
        _entities.Update(1.0 / 60.0, _player);
        Step();
        FailIf(!raft.LinkRiding, "INTERAC_RAFT $e6:$02 could not remount for scroll validation.");
        remembered = CompanionRuntimeState.ReadRemembered(_entities.RuntimeState);
        raft.BeginScreenTransition(_world.LoadRoom(1, 0xa8));
        raft.SetScreenTransitionPosition(new Vector2(144.5f, 85.25f), new Vector2(-32, 0), _player);
        FailIf(raft.PrecisePosition != new Vector2(144.5f, 85.25f) ||
            _player.PrecisePosition.X != 144.75f || _player.Position.X != 112,
            "SPECIALOBJECT_RAFT $13 scrolling mixed screen offsets or raft fractions into Link's logical position.");
        raft.SetScreenTransitionBoundaryCoordinate(true, 143, _player);
        FailIf(raft.PrecisePosition.X != 143.5f,
            "SPECIALOBJECT_RAFT $13 boundary clamp discarded the source fractional byte.");
        raft.FinishScreenTransition(new Vector2(8.5f, 85.25f), _player);
        FailIf(CompanionRuntimeState.ReadRemembered(_entities.RuntimeState) != remembered ||
            CompanionRuntimeState.Read(_entities.RuntimeState).Room != 0xa8 ||
            _player.LocalRespawnPosition != new Vector2(8, 85) ||
            CompanionRuntimeState.ReadLastAnimalMountPosition(_entities.RuntimeState) != new Vector2(8, 85),
            "SPECIALOBJECT_RAFT $13 scroll finisher lost the active room/local checkpoint or rewrote remembered state.");

        _player.BeginFloorDoorRespawn();
        Step();
        FailIf(raft.Visible || raft.PrecisePosition != new Vector2(8.5f, 85.25f),
            "SPECIALOBJECT_RAFT $13 @respawning failed to hide at the local high-byte coordinates.");
        Step();
        FailIf(!raft.Visible || !_player.RaftRideActive,
            "SPECIALOBJECT_RAFT $13 failed to restore the mounted pair after local respawn.");
        raft.BeginRaftwreckControl(_player, new Vector2(64.75f, 72.75f));
        raft.SetRaftwreckPosition(_player, new Vector2(65.75f, 73.75f), 1);
        FailIf(raft.PrecisePosition != new Vector2(65.5f, 73.25f) ||
            _player.FacingVector != Vector2I.Right,
            "INTERAC_RAFTWRECK $9b copied fractional bytes or failed setLinkDirection's two-owner write.");
        raft.CancelRaftwreckControl(_player);

        // Subid $00 falls through to the same flag-$26 gate as subid $01.
        CompanionRuntimeState.Clear(_entities.RuntimeState, 0x13);
        CompanionRuntimeState.ForgetRemembered(_entities.RuntimeState);
        _saveData.SetGlobalFlag(behavior.ChangedRoomsFlag, false);
        _saveData.WriteWramByte(behavior.DimitriStateAddress, (byte)behavior.DimitriMask);
        LoadValidationRoom(1, 0xa9);
        FailIf(_entities.Entities<RaftRoomEntity>().Count != 0,
            "Room 1:a9 INTERAC_RAFT $e6:$00 skipped GLOBALFLAG_RAFTON_CHANGED_ROOMS.");
        GD.Print("Validated INTERAC_RAFT $e6 radii, delayed mount, instrument lock, " +
            "fractional Link coordinates, source-priority $03, diagonal direction, " +
            "mount checkpoint, ground-center collision, zero-counter recoil, " +
            "item immobilization, animation cadence, dismount probes and $0c boundary, and subid $00 gates.");
    }
}
