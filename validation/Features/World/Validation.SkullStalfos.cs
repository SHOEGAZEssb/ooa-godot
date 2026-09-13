using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonStalfos()
    {
        var database = new EnemyDatabase();
        foreach (var (room, count) in new[] { (0x72, 2), (0x7e, 4) })
        {
            _saveData.SetRoomFlag(4, room, 0xff, false);
            LoadValidationRoom(4, room);
            FailIf(_entities.Entities<StalfosCharacter>().Count != count ||
                _entities.Entities<StalfosCharacter>().Any(s => s.Record.SubId != 2),
                $"Room 4:{room:x2} lost its enemyData.s $31:$02 placements.");
        }
        var record = ResolveStalfos(database, RoomEnemyPlacements(database, 4, 0x7e, 0x31, 2)[0]);
        FailIf(record.Health != 4 || record.Palette != 3 || record.SpeedRaw != 20,
            "Blue Stalfos lost its source health $04, palette $03 or SPEED_80.");
        Vector2 origin = new(120, 88);
        // An open movement fixture isolates the source distance and Z arithmetic.
        var roomData = _world.LoadRoom(4, 0x7e);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var sounds = new List<int>();
        StalfosCharacter Create(OracleRandom? random = null)
        {
            var result = new StalfosCharacter();
            result.Initialize(record, roomData, origin, random ?? new OracleRandom(), sounds.Add);
            return result;
        }
        var stalfos = Create();
        stalfos.UpdateFrame(origin + new Vector2(22, 22), true);
        FailIf(stalfos.State != StalfosState.Deciding,
            "Stalfos accepted Manhattan distance $2c; objectCheckLinkWithinDistance is strict.");
        stalfos.UpdateFrame(origin + new Vector2(21, 22), true);
        FailIf(stalfos.State != StalfosState.Jumping || stalfos.SpeedZ != -512 ||
            stalfos.ZFixed != 0 || stalfos.CollisionEnabled || stalfos.Angle != 0x1c ||
            !sounds.SequenceEqual(new[] { 0x8f }),
            $"Stalfos dodge setup lost full angle, collision suppression, speedZ or SND_ENEMY_JUMP: angle={stalfos.Angle:x2}.");
        for (int i = 0; i < 15; i++) stalfos.UpdateFrame(origin, true);
        FailIf(stalfos.ZFixed != -4320 || stalfos.SpeedZ != -32 || stalfos.CollisionEnabled,
            "Stalfos ascent must remain invulnerable through update15 without restarting from another item pulse.");
        stalfos.UpdateFrame(origin, true);
        FailIf(stalfos.ZFixed != -4352 || stalfos.SpeedZ != 0 || !stalfos.CollisionEnabled,
            "Stalfos must restore collisions at jump update16 when speedZ high becomes zero.");
        for (int i = 0; i < 16; i++) stalfos.UpdateFrame(origin);
        FailIf(stalfos.State != StalfosState.Jumping || stalfos.ZFixed != -512,
            "Stalfos jump ended before update33.");
        stalfos.UpdateFrame(origin);
        FailIf(stalfos.State != StalfosState.Deciding || stalfos.ZFixed != 0,
            "Stalfos landing must return to state $08 on update33.");
        stalfos.UpdateFrame(stalfos.Position + new Vector2(0, 20), true);
        FailIf(stalfos.State != StalfosState.Jumping || sounds.Count != 2,
            "Stalfos could not dodge again after landing.");
        stalfos.Free();

        var initialRandom = new OracleRandom();
        var initial = Create(initialRandom);
        initial.UpdateFrame(origin + Vector2.Down * 20, true);
        FailIf(initial.State != StalfosState.Jumping || initialRandom.Calls != 1,
            "State-zero Stalfos must consume common var3d RNG before the item-start dodge gate.");
        initial.Free();
        foreach (bool available in new[] { true, false })
        {
            var random = new OracleRandom();
            var firing = Create(random);
            firing.UpdateFrame(Vector2.Zero);
            // Select a source attack roll independently of the runtime decision.
            var probe = new OracleRandom();
            probe.Next();
            int skips = 0;
            while ((probe.Next().Value & 7) != 0) skips++;
            for (int i = 0; i < skips; i++) random.Next();
            firing.UpdateFrame(Vector2.Zero);
            FailIf(firing.State != StalfosState.Firing, "Stalfos's (random & 7)==0 decision did not enter separate state $0c.");
            int calls = random.Calls;
            bool spawned = firing.UpdateFrame(origin, true, () => available);
            FailIf(spawned != available || firing.State != StalfosState.Walking || random.Calls != calls + 1,
                "Firing state must ignore dodge input, attempt a part allocation, then choose a walk even when the pool is full.");
            firing.Free();
        }

        var boneRecord = StalfosBoneRecord.Load();
        FailIf(boneRecord is not { TileBase: 10, Palette: 2, RadiusX: 2, RadiusY: 2, DamageQuarters: 2, SpeedRaw: 60 } ||
            boneRecord.Animation != "4@8,0,0,0;8,8,0,96|4@8,0,0,64;8,8,0,32",
            "PART_STALFOS_BONE lost part1c properties or the aliased partAnimation5b98c/OAM stream.");
        var bone = new StalfosBoneProjectile();
        bone.Initialize(boneRecord, roomData, origin, p => p - new Vector2(40, 24));
        _player.WarpTo(origin + new Vector2(30, -30));
        bone.UpdateFrame(_player, 0);
        FailIf(bone.Angle != 4 || bone.Position != origin || !bone.Visible,
            "Bone state0 must aim at the live target with all 32 angles and defer movement.");
        FailIf(!bone.DeflectWithSword() || bone.State != HostileProjectileState.Flying,
            "Sword collision must publish pending bone status until its part dispatch.");
        bone.UpdateFrame(_player, 1);
        FailIf(bone.State != HostileProjectileState.Bouncing || bone.Counter != 32 || bone.Angle != 0x14 || bone.CollisionEnabled,
            "Bone bounce lost source reversed angle, collision disable or $20 counter.");
        for (int i = 0; i < 7; i++) bone.UpdateFrame(_player, 3);
        FailIf(bone.AnimationFrame != 0, "Bone bounce animated on odd global wFrameCounter updates.");
        for (int i = 0; i < 4; i++) bone.UpdateFrame(_player, 4);
        FailIf(bone.AnimationFrame != 1, "Bone bounce did not advance after four even global updates.");
        for (int i = 11; i < 31; i++) bone.UpdateFrame(_player, i);
        FailIf(bone.Finished || bone.Counter != 1, "Bone deleted before bounce update32.");
        bone.UpdateFrame(_player, 32);
        FailIf(!bone.Finished, "Bone failed to delete on the bounce counter's zero update.");
        bone.Free();

        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        void Step(int count, bool press = false)
        {
            input.CaptureForValidation(press ? ["attack"] : [], press ? ["attack"] : [], Vector2.Zero);
            scheduler.Advance(count / 60.0, update);
        }
        _inventory.GiveTreasure(TreasureDatabase.TreasureSword, 1);
        _inventory.EquipA(InventoryState.ItemSword);
        var snapshot = CaptureOracleRandomForValidation();
        (Vector2, int, StalfosState)[] Run(bool batched)
        {
            RestoreOracleRandomForValidation(snapshot);
            LoadValidationRoom(4, 0x7e);
            var enemies = _entities.Entities<StalfosCharacter>();
            var target = enemies[0];
            Vector2 link = target.Position + Vector2.Down * 30;
            FailIf(_currentRoom.IsSolid(link), "Stalfos gameplay fixture must use open room collision geometry.");
            _player.WarpTo(link);
            Step(1, true);
            FailIf(!_player.StartedItemAnimationThisUpdate || target.State != StalfosState.Jumping,
                $"Actual sword parent initialization did not reach the same-update enemy dodge gate: start={_player.StartedItemAnimationThisUpdate}, attack={_player.IsAttacking}, A={_inventory.EquippedA:x2}, state={target.State}, Link={_player.Position}, enemy={target.Position}.");
            Step(1);
            FailIf(_player.StartedItemAnimationThisUpdate || !_player.IsAttacking,
                "The item-start signal must clear on the next update while the sword animation continues.");
            if (batched) Step(8); else for (int i = 0; i < 8; i++) Step(1);
            return enemies.Select(e => (e.Position, e.ZFixed, e.State)).ToArray();
        }
        var singles = Run(false);
        FailIf(!singles.SequenceEqual(Run(true)), "Stalfos gameplay differs between individual updates and a batched host frame.");
        _player.WarpTo(_player.Position);
        Step(1, true);
        FailIf(!_player.StartedItemAnimationThisUpdate, "Repeat sword use did not publish a fresh item-start signal.");
        _debugFlagMenu.OpenImmediatelyForValidation();
        Step(3);
        FailIf(!_player.StartedItemAnimationThisUpdate,
            "A paused gameplay frame cleared the item-start signal without executing checkUseItems.");
        _debugFlagMenu.CloseImmediatelyForValidation();
        typeof(Player).GetMethod("CancelSwordAttack", flags)!.Invoke(_player, null);
        FailIf(_player.StartedItemAnimationThisUpdate,
            "Cancelling the parent item retained a live high-nibble item-use signal.");

        foreach (bool full in new[] { false, true })
        {
            LoadValidationRoom(4, 0x7e);
            _player.WarpTo(new Vector2(120, 102));
            Step(1);
            var shooters = _entities.Entities<StalfosCharacter>();
            if (full)
            {
                var add = typeof(RoomEntityManager).GetMethod("AddEntity", flags)!;
                for (int i = 0; i < 16; i++)
                {
                    var occupied = new StalfosBoneProjectile();
                    occupied.Initialize(boneRecord, _currentRoom, new Vector2(120, 40), p => p - new Vector2(40, 24));
                    add.Invoke(_entities, [new StalfosBoneRoomEntity(occupied)]);
                }
            }
            foreach (var shooter in shooters)
            {
                shooter.Position += new Vector2(0.75f, 0.25f);
                typeof(StalfosCharacter).GetField("_state", flags)!.SetValue(shooter, StalfosState.Firing);
            }
            Vector2[] firingPositions = shooters.Select(s => OracleObjectMath.ToPixelPosition(s.Position)).ToArray();
            Step(1);
            var children = _entities.Entities<StalfosBoneProjectile>();
            FailIf(children.Count != (full ? 16 : 4) || shooters.Any(s => s.State != StalfosState.Walking),
                "Actual Stalfos part allocation ignored shared-pool capacity or failed to resume walking.");
            if (!full)
                FailIf(!children.Select(b => b.Position).SequenceEqual(firingPositions) ||
                    children.Any(b => b.State != HostileProjectileState.Flying || !b.Visible),
                    "New bones must execute state0 in the same update's part phase, without taking their first movement step.");
        }
        LoadValidationRoom(4, 0x91);
        FailIf(_entities.Entities<StalfosBoneProjectile>().Count != 0, "Room exit retained Stalfos bones.");
        GD.Print("Validated Skull Dungeon Stalfos: source placements, Manhattan dodge boundary, initial RNG, ascent/apex/landing, repeat dodge, attack allocation, bone aim/bounce and real sword update ordering.");
    }
}
