using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullMinibossPortal()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var advance = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        bool batch = false;
        void Step(int count = 1, Vector2 movement = default, bool jump = false)
        {
            input.CaptureForValidation(jump ? ["attack"] : [], jump ? ["attack"] : [], movement);
            if (batch) scheduler.Advance(count / 60.0, advance);
            else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60, advance);
        }
        var data = new DungeonEntranceInteractionDatabase();
        var pair = data.PortalPairFor(4);
        FailIf(pair.MinibossRoom != 0x80 || pair.EntranceRoom != 0x91 || data.PortalPosition != 0x57 ||
            data.PortalRadius != 3 || data.PortalSpinUpdates != 48,
            "D4 portal must retain source pair80/91, position57, radius3, spin48.");
        _saveData.SetRoomFlag(4, 0x80, OracleSaveData.RoomFlag80);
        foreach (bool batched in new[] { false, true })
        {
            batch = batched;
            LoadValidationRoom(4, 0x80);
            _player.WarpTo(new Vector2(120.75f,112.25f));
            Step();
            int room = 0x80;
            for (int trip = 0; trip < 3; trip++)
            {
                if (trip != 0)
                {
                    Step(24);
                    FailIf(IsTransitioning || _player.CutsceneControlled || _entities.PlayerPassesNpcs,
                        "The arrival portal must wait for Link to leave without bouncing him back.");
                    for (int i = 0; _player.Position.Y < 112 && i < 40; i++) Step(movement: Vector2.Down);
                }
                FailIf(_currentRoom.IsSolid(_player.Position), "Portal approach must use actual clear floor.");
                _sound.ClearPlayRequestAudit();
                for (int i = 0; !_player.CutsceneControlled && i < 40; i++) Step(movement: Vector2.Up);
                FailIf(!_player.CutsceneControlled || IsTransitioning || _player.Position != new Vector2(120,88) ||
                    !_entities.PlayerPassesNpcs || !_entities.PlayerContactDisabled ||
                    _sound.PlayRequestsFor(OracleSoundEngine.SndTeleport) != 1,
                    $"Walking into 4:{room:x2}'s portal did not begin its source spin/contact handoff.");
                Vector2 pinned = _player.PrecisePosition;
                if (trip == 0)
                    FailIf(pinned != new Vector2(120.75f,88.25f), "Portal objectCopyPosition discarded Link's low XY bytes.");
                Vector2I facing = _player.FacingVector;
                int frame = _entities.FrameCounter;
                if (trip == 0)
                {
                    var owner = ((System.Collections.Generic.IEnumerable<IRoomEntity>)typeof(RoomEntityManager)
                        .GetField("_activeEntities", flags)!.GetValue(_entities)!).OfType<MinibossPortalRoomEntity>().Single();
                    var counter = typeof(MinibossPortalRoomEntity).GetField("_counter", flags)!;
                    typeof(Player).GetField("_deathPending", flags)!.SetValue(_player, true);
                    Step(4);
                    FailIf((int)counter.GetValue(owner)! != 48 || _player.FacingVector != facing || IsTransitioning,
                        "wLinkDeathTrigger must hold portal rotation and the spin counter while interactions still update.");
                    typeof(Player).GetField("_deathPending", flags)!.SetValue(_player, false);
                    typeof(Player).GetField("_deathAnimationActive", flags)!.SetValue(_player, false);
                }
                typeof(Player).GetField("_enemyInvincibilityFrames", flags)!.SetValue(_player, 12f);
                typeof(Player).GetField("_enemyKnockbackFrames", flags)!.SetValue(_player, 5f);
                Step();
                FailIf(_player.PrecisePosition != pinned || _player.InvincibilityFrames != 0 || _player.KnockbackFrames != 0 ||
                    _player.ScriptedLinkAnimationMode is not null || _player.ZIndex != 9,
                    "Portal spin must pin high XY and reset damage/recoil while Link adopts state08 before its initialization.");
                Step();
                FailIf(_player.ScriptedLinkAnimationMode != 0x10 || _player.ZIndex != 9,
                    "The second Link update must initialize state08 with LINK_ANIM_MODE_WALK and visible=$82.");
                Step(45);
                FailIf(IsTransitioning || !_player.CutsceneControlled || _player.PrecisePosition != pinned ||
                    _player.FacingVector != Rotate(facing, ((frame & 3) + 47) / 4),
                    "Portal must hold through counter1 and rotate only on global frame multiples of4.");
                Step();
                FailIf(!IsTransitioning || _player.FacingVector != facing || _player.ZIndex != 9 ||
                    _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != 0,
                    "The 48th spin update must request direct fadeout without entering-cave sound.");
                for (int i = 0; IsTransitioning && i < 200; i++) Step();
                room = room == 0x80 ? 0x91 : 0x80;
                FailIf(IsTransitioning || _activeGroup != 4 || _currentRoom.Id != room ||
                    _player.Position != new Vector2(120,88) || _player.CutsceneControlled || _entities.PlayerPassesNpcs || _player.ZIndex != Player.NormalZIndex ||
                    _entities.Entities<MinibossPortal>().Count != 1 ||
                    !_saveData.HasRoomFlag(4, 0x80, OracleSaveData.RoomFlag80),
                    "D4 portal travel lost destination57, control restoration, arrival portal, or miniboss completion.");
            }
        }

        foreach (bool batched in new[] { false, true })
        {
            batch = batched;
            LoadValidationRoom(4, 0x80);
            _inventory.GiveTreasure(TreasureDatabase.TreasureSword, 1);
            _inventory.EquipA(InventoryState.ItemSword);
            _player.WarpTo(new Vector2(110, 88));
            Step();
            FailIf(_collision.Collides(_player.Position), "Portal sword approach must begin on clear room floor.");
            Step(movement: Vector2.Right, jump: true);
            for (int i = 0; !_player.CutsceneControlled && i < 40; i++)
            {
                input.CaptureForValidation(["attack"], [], Vector2.Right);
                scheduler.Advance(1.0 / 60, advance);
            }
            FailIf(!_player.CutsceneControlled || !_player.IsAttacking,
                $"Walking into the portal with a sword parent must retain that parent on activation: controlled={_player.CutsceneControlled}, sword={_player.IsAttacking}, position={_player.Position}.");
            // An existing ITEM child remains eligible while state08 clears
            // parent items. Keep its trajectory on the room's open floor.
            new SeedSatchelDatabase().TryGet(0x20, out var ember);
            var seed = _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
                new Vector2(80, 88), Vector2I.Up, ember, 4, SeedLaunchKind.Shooter));
            Vector2 seedStart = seed.PrecisePosition;
            Step();
            FailIf(!_player.IsAttacking || _player.ScriptedLinkAnimationMode is not null,
                "checkLinkForceState must adopt state08 without prematurely clearing the sword parent.");
            Vector2 seedAfterAdoption = seed.PrecisePosition;
            FailIf(seed.ElapsedFrames != 1 || seed.State != EmberState.Flying || seedAfterAdoption != seedStart,
                "The ITEM child's initialization must run during force-state adoption without moving yet.");
            Step();
            FailIf(_player.IsAttacking || _player.ScriptedLinkAnimationMode != 0x10,
                "state08 substate0 must cancel the sword parent on the second Link update.");
            FailIf(seed.Finished || seed.ElapsedFrames != 2 || seed.PrecisePosition == seedAfterAdoption,
                "Portal parent cancellation must preserve the existing shooter child and its ITEM updates.");
        }

        // The overlap helper permits ordinary low jumps, but the signed
        // [-7,6] Z window must reject passing above the portal at the apex.
        LoadValidationRoom(4, 0x80);
        _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
        _inventory.EquipA(InventoryState.ItemFeather);
        _player.WarpTo(new Vector2(102,88));
        Step(movement: Vector2.Right);
        Step(movement: Vector2.Right, jump: true);
        bool crossedAbove = false;
        for (int i = 0; _player.TopDownAirborne && i < 70; i++)
        {
            Vector2 before = _player.Position;
            int beforeZ = _player.TopDownAirZ;
            Step(movement: Vector2.Right);
            crossedAbove |= _player.Position.X >= 112 && _player.Position.X < 129 && _player.TopDownAirZ < -7;
            FailIf(_player.CutsceneControlled || IsTransitioning, $"Portal activated during jump after {i}: {before}, Z={beforeZ}, now {_player.Position}.");
        }
        FailIf(!crossedAbove || _player.TopDownAirborne, "Actual Feather trajectory did not cross the portal above Z=-7.");

        LoadValidationRoom(4, 0x80);
        _player.WarpTo(new Vector2(110.75f,88.25f)); Step();
        FailIf(_player.CutsceneControlled, "Portal accepted the high-byte X=-10 boundary.");
        Step(movement: Vector2.Right);
        FailIf(!_player.CutsceneControlled || _player.PrecisePosition != new Vector2(120.75f,88.25f),
            "Walking across the inclusive high-byte X=-9 boundary must activate and preserve coordinate fractions.");

        // Boundary fixtures supplement the physical approaches above. Menu
        // and lift masks must not activate/re-arm a ready portal incorrectly.
        LoadValidationRoom(4, 0x80);
        _player.WarpTo(new Vector2(120,112)); Step();
        _player.SetBraceletLiftCollisionsDisabled(true);
        _player.WarpTo(new Vector2(120,88));
        _player.SetBraceletLiftCollisionsDisabled(true);
        Step(4);
        FailIf(_player.CutsceneControlled, "A lifting Link activated the portal despite grab state$c2.");
        _player.SetBraceletLiftCollisionsDisabled(false);
        var menu = typeof(RoomEntityManager).GetField("_linkCollisionsAndMenuDisabled", flags)!;
        menu.SetValue(_entities, true);
        Step(4);
        FailIf(_player.CutsceneControlled, "Portal ignored wDisableLinkCollisionsAndMenu.");
        menu.SetValue(_entities, false);
        Step();
        FailIf(!_player.CutsceneControlled, "Clearing the collision/menu gate did not allow the ready portal to activate.");
        LoadValidationRoom(4, 0x91);
        _player.WarpTo(new Vector2(120,112));
        Step(60);
        FailIf(IsTransitioning || _player.CutsceneControlled || _entities.PlayerPassesNpcs || _currentRoom.Id != 0x91,
            "Canceling the spin by changing rooms retained portal control or a pending warp.");

        static Vector2I Rotate(Vector2I facing, int count)
        {
            for (int i = 0; i < count; i++) facing = new Vector2I(-facing.Y, facing.X);
            return facing;
        }
    }
}
