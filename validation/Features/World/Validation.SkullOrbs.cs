using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullStationaryOrb()
    {
        CompareSkullStationaryOrbRom();
        var data = PartOrbDatabase.Shared;
        FailIf(data.BackgroundTile != 0x0a || data.TileCollision != 0x0f || data.SubidMask != 7 || data.RadiusX != 4 || data.RadiusY != 4,
            "partCode03 state0 must write cfXX=$0a, ceXX=$0f, preserve Zh=0, and derive its mask from subid&7.");
        var random = CaptureOracleRandomForValidation();
        _inventory.GiveTreasure(TreasureId.Sword, 0);
        _inventory.EquipA(TreasureId.Sword);
        foreach (bool batch in new[] { false, true })
        {
            RestoreOracleRandomForValidation(random);
            _inventory.RefillHealth();
            void Step(int count = 1, Vector2 move = default, bool attack = false) =>
                StepGameplayUpdates(count, move, attack ? ["attack"] : [], attack ? ["attack"] : [], batched: batch);
            var seeds = new SeedSatchelDatabase();
            var shooter = SeedShooterRecord.Load();
            foreach (int item in new[] { 0x20, 0x21, 0x22, 0x23, 0x24 })
            {
                // Shared orb receiver in the enemy-free bridge room; Skull's
                // live Keese can intercept this otherwise controlled shot.
                _saveData.SetRoomFlag(2, 0x9e, 0x40, false);
                _entities.RuntimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
                LoadValidationRoom(2, 0x9e);
                _player.WarpTo(new Vector2(40, 120));
                Step();
                var orb = _entities.Entities<DungeonOrbRoomEntity>().Single();
                FailIf(!seeds.TryGet(item, out var record), $"Missing source seed${item:x2}.");
                // Real ITEM child, with a controlled clear eastern approach.
                // SPEED_180 moves12->9->6 pixels from the orb; only the second
                // movement update enters the source collision radii.
                var seed = _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
                    orb.Position + Vector2.Right * 12 - shooter.Offsets[6], Vector2I.Up, record, 2, SeedLaunchKind.Shooter, 6));
                int expectedItem = item == 0x24 ? 0x20 + seed.MysteryEffect : item;
                Step();
                FailIf(seed.HasPendingNativeCollision || orb.PendingHit || seed.State != EmberState.Flying,
                    $"Seed${item:x2} setup must remain outside the orb: seed={seed.Position}/{seed.State}/{seed.HasPendingNativeCollision},orb={orb.PendingHit}/{orb.HitLockout},Link={_player.Position}.");
                Step();
                FailIf(seed.HasPendingNativeCollision || orb.PendingHit || orb.IsOn,
                    "The first three-pixel approach update remains outside the stationary orb's native collision box.");
                Step();
                FailIf(!seed.HasPendingNativeCollision || seed.CollisionEnabled || seed.State != EmberState.Flying ||
                    !orb.PendingHit || orb.IsOn || orb.HitLockout != 0 || orb.Palette != 1,
                    $"Seed${item:x2} must pass logical tile$0a, disable collision in effect20 and defer ITEM/PART handlers until the next update.");
                Step();
                FailIf(seed.HasPendingNativeCollision || orb.PendingHit || !orb.IsOn || orb.Palette != 2 || orb.HitLockout != 0 ||
                    seed.SeedItem != expectedItem || seed.State == EmberState.Flying,
                    $"Seed${item:x2} and stationary orb must consume their pending collision exactly once on the next ITEM/PART passes.");
                LoadValidationRoom(4, 0x73);
            }
        }
        GD.Print("Validated stationary orb source buffers, full solid collision, real Sword repeat, native PART/chest handoff, text freeze and local palette.");
    }

    private void ValidateSkullOrbScripts()
    {
        CompareSkullMovingOrbRom();
        CompareOrbChestInitializationRom();
        void Step(int count = 1) =>
            StepGameplayUpdates(count, Vector2.Zero, [], [], batched: true);
        var database = new SkullDungeonDatabase();
        FailIf(database.GetRoomRecords(4,0x92) is not [{ Id:InteractionId.DungeonScript, SubId:3, Order:0, X:40, Y:104 },
                { Id:InteractionId.Id0b, SubId:0, Order:1, X:128, Y:72, Var03:2 }] ||
            database.GetRoomRecords(4,0x74)[1] is not { Id:InteractionId.DungeonScript, SubId:2, Order:2, X:72, Y:88 },
            "Orb placements must retain source order, pixel coordinates, script dispatch, and raw var03 mask.");
        FailIf(string.Concat(Enumerable.Range(0,32).Select(i => PartOrbDatabase.Shared.HitLockout(i) >= 0 ? '1':'0')) !=
            "00001111111101100000001111111110" || PartOrbDatabase.Shared.Speed != 0x14 ||
            PartOrbDatabase.Shared.Right != 0x98 || PartOrbDatabase.Shared.Left != 0x68,
            "Moving orb lost its source active mask or SPEED_80/right98/left68 script.");
        foreach (int mask in new[] { 2, 1 })
        {
            _saveData.SetRoomFlag(4,0x74,0xff,false);
            _entities.RuntimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, (byte)mask);
            LoadValidationRoom(4,0x74);
            _player.WarpTo(new Vector2(40,88));
            Step(3);
            var other = _entities.Entities<DungeonOrbChestRoomEntity>().Single();
            FailIf(other.Counter != (mask == 1 ? 15 : -1), "Room4:74 script02 must test only toggle bit0.");
            if (mask == 1)
            {
                Step(15);
                FailIf(!other.Finished || _currentRoom.GetMetatile(new Vector2(72,88)) != 0xf1,
                    "Room4:74 must create its chest at the source58/48 pixel position after15 updates.");
            }
        }
        _entities.RuntimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
        LoadValidationRoom(4,0x92);
        _player.WarpTo(new Vector2(40,88));
        var orb = _entities.Entities<MovingOrbRoomEntity>().Single();
        Step();
        // Controlled item allocation isolates the beam/orb collision handoff.
        FailIf(!_entities.TrySpawnSwordBeam(orb.Position + Vector2.Down * 16, 0), "Orb beam fixture failed to allocate its real item.");
        var beam = _entities.Entities<SwordBeamEffect>().Single();
        for (int i = 0; !beam.PendingNativeCollision && !beam.Finished && i < 30; i++) Step();
        FailIf(!beam.PendingNativeCollision || beam.Finished || beam.CollisionEnabled || !orb.PendingHit || orb.Palette != 1,
            "Native effect20 must disable the beam but defer both item completion and orb toggle until their next updates.");
        Step();
        FailIf(!beam.Finished || beam.PendingNativeCollision || orb.PendingHit || orb.Palette != 2,
            "The next ITEM/PART dispatch must consume the native beam/orb signals once.");
        _entities.ApplyThrownObjectHit(orb.CollisionBounds, 0, 7, 3);
        FailIf(orb.PendingHit, "Thrown-object geometry must remain queued until the post-object scan.");
        Step();
        FailIf(!orb.PendingHit || orb.HitLockout != 28 || orb.Palette != 2,
            "Post-object thrown collision must use effect26's pending hit and28-update lockout.");
        Step();
        FailIf(orb.PendingHit || orb.HitLockout != 27 || orb.Palette != 1,
            "The thrown hit must toggle the orb on the following PART update.");
        orb.ClearHealthAndCollision();
        Vector2 beforeClear = orb.Position;
        Step(2);
        FailIf(orb.Position == beforeClear || orb.Palette != 1 || orb.PendingHit,
            "PARTSTATUS_DEAD follows the moving orb's normal movement path without inventing a JUST_HIT toggle.");
        LoadValidationRoom(4,0x91);
        FailIf(_entities.Entities<MovingOrbRoomEntity>().Count != 0 || _entities.Entities<SwordBeamEffect>().Count != 0,
            "Room replacement must release orb and native projectile lifetimes.");
    }
}
