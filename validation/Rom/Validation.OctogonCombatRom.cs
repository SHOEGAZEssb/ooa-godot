using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOctogonCombatRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false,true })
        foreach (int room in new[] { 0x36,0x2d })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers,0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit,0);
            // Declared encounter handoff: graphics resident, body alive in
            // this layer. Native state-zero initialization still allocates
            // and updates its shell through the actual object walk.
            byte[] shared = [1,(byte)(room == 0x36 ? 0 : 1),0xff,0x28,0x28,0x78,0xff,0];
            for (int index = 0; index < shared.Length; index++) _runtimeState.SetWramByte(0xcfd0+index,shared[index]);
            LoadValidationRoom(5,room); _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(0x78,0x98));
            FailIf(_collision.Collides(_player.Position),$"Octogon room$5:${room:x2} fixture must start on reachable lower floor.");
            var main = _entities.Entities<OctogonCharacter>().Single();
            var active = SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!;
            foreach (var entity in active.Where(entity => entity.Node != main).ToArray())
            { active.Remove(entity); free.Invoke(_entities,[entity]); }
            var slots = SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
            var parts = SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots");
            typeof(Player).GetField("_enemyInvincibilityFrames",flags)!.SetValue(_player,100000f);
            var rom = new EnemyStatusRom(_currentRoom,_saveData,_random.Calls);
            rom[0xcc08] = 0xc8; rom[0xcc0a] = 0xc9; rom[0xcc0c] = 0xca;
            rom[0xcc0e] = 0x8e; rom[0xcc10] = 0xa5;
            rom[0xcc12] = 0x8f; rom[0xcc14] = 0xa7;
            rom[0xcc39] = 0x0c; // Both boss layers belong to present Mermaid's Cave.
            for (int index = 0; index < shared.Length; index++) rom[0xcfd0+index] = shared[index];
            int p0 = 0xd080+slots.Single().Value*256;
            rom[p0] = 1; rom[p0+1] = 0x7d; rom[p0+2] = (byte)main.Record.SubId;
            rom[p0+11] = (byte)main.Position.Y; rom[p0+13] = (byte)main.Position.X;
            var seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            int update = 0;
            bool changedLayer = false;
            var emitted = new HashSet<int>();
            void Compare()
            {
                rom.Update(_entities.FrameCounter,_player.Position);
                foreach (var pair in slots.Where(pair => pair.Key.Node is OctogonCharacter))
                {
                    var actor = (OctogonCharacter)pair.Key.Node; int p = 0xd080+pair.Value*256;
                    Vector2 xy = new(rom.Word(p+12)/256f,rom.Word(p+10)/256f);
                    FailIf(rom[p] == 0 || actor.State != rom[p+4] || actor.Depth != rom[p+3] ||
                        actor.Counter1 != rom[p+6] || actor.Counter2 != rom[p+7] || actor.Direction != rom[p+8] ||
                        actor.Angle != rom[p+9] || actor.Position != xy || (actor.ZFixed&0xffff) != rom.Word(p+14) ||
                        actor.Speed != rom[p+16] || actor.State != 0 && actor.Health != rom[p+41] ||
                        actor.CollisionType != rom[p+36] ||
                        (actor.InvincibilityCounter&255) != rom[p+43] || actor.TargetIndex != rom[p+48] ||
                        actor.SwimCounter != rom[p+53] || actor.AttackCounter != rom[p+54] ||
                        actor.Visible != ((rom[p+26]&128) != 0) || actor.AnimationParameter != rom[p+33],
                        $"Octogon room$5:${room:x2} slot${pair.Value:x2} update{update} batch={batched}: " +
                        $"runtime={actor.State:x2}/{actor.Depth}, c={actor.Counter1}/{actor.Counter2}, dir/angle={actor.Direction}/{actor.Angle}, XY={actor.Position}, Z={actor.ZFixed}, speed={actor.Speed}, HP={actor.Health}, target={actor.TargetIndex}, swim/attack={actor.SwimCounter}/{actor.AttackCounter}, anim={actor.AnimationParameter}, visible={actor.Visible}; " +
                        $"ROM={rom[p+4]:x2}/{rom[p+3]}, c={rom[p+6]}/{rom[p+7]}, dir/angle={rom[p+8]}/{rom[p+9]}, XY={xy}, Z={rom.Word(p+14)}, speed={rom[p+16]}, HP={rom[p+41]}, target={rom[p+48]}, swim/attack={rom[p+53]}/{rom[p+54]}, anim={rom[p+33]}, visible=${rom[p+26]:x2}; Link={_player.Position}, health={_inventory.HealthQuarters}, dying={_player.IsDying}, transition={IsTransitioning}, freeze={_entities.RoomEntityFreezeActive()}, text={_dialogue.IsOpen}, disabled={_entities.InitializedObjectsDisabledSource()}, palette={_entities.PaletteFadeActiveSource()}, frame={_entities.FrameCounter}.");
                }
                foreach (var pair in parts.Where(pair => pair.Key.Node is OctogonPart))
                {
                    var part = (OctogonPart)pair.Key.Node; int p = 0xd0c0+pair.Value*256; emitted.Add(part.Id);
                    FailIf(rom[p] == 0 || part.State != rom[p+4] || part.Counter != rom[p+6] || part.Angle != rom[p+9] ||
                        part.Position != new Vector2(rom.Word(p+12)/256f,rom.Word(p+10)/256f) || (part.ZFixed&0xffff) != rom.Word(p+14) ||
                        part.Speed != rom[p+16] || part.SpeedZ != unchecked((short)rom.Word(p+0x14)) || part.Visible != ((rom[p+26]&128) != 0),
                        $"Octogon PART${part.Id:x2}:${part.SubId:x2} update{update}: runtime state={part.State}, c={part.Counter}, angle={part.Angle}, XY={part.Position}, Z={part.ZFixed}, speedZ={part.SpeedZ}, visible={part.Visible}; ROM={rom[p+4]}, c={rom[p+6]}, angle={rom[p+9]}, XY={rom.Word(p+12)/256f},{rom.Word(p+10)/256f}, Z={rom.Word(p+14)}, speedZ={unchecked((short)rom.Word(p+0x14))}, visible=${rom[p+26]:x2}.");
                }
                for (int index = 0; index < shared.Length; index++)
                    FailIf(_runtimeState.ReadWramByte(0xcfd0+index) != rom[0xcfd0+index],$"Octogon update{update}: shared byte${0xcfd0+index:x4} differs.");
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls-seed.Calls != rom.RandomCalls,
                    $"Octogon update{update}: ordered global RNG differs, runtime={rng.Calls-seed.Calls}, ROM={rom.RandomCalls}.");
                changedLayer |= main.Depth != shared[1]; update++;
            }
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            if (room == 0x36) Step(1600);
            else
            {
                for (int cycle = 0; cycle < 10; cycle++)
                {
                    StepGameplayUpdates(80,Vector2.Left,["move_left"],["move_left"],batched,Compare);
                    StepGameplayUpdates(80,Vector2.Right,["move_right"],["move_right"],batched,Compare);
                }
                // Native JUST_HIT accelerates the next phase after ten HP of
                // damage. Resolve the declared sword through its table scan;
                // the gameplay loop owns the following shell/phase handoff.
                var body = slots.Single(pair => pair.Key.Node == main);
                rom.HitWithSword(ItemCollisionType.L1Sword,10,body.Value);
                ((ILinkSwordStateAwareRoomEntity)body.Key).SetLinkSwordState(SwordActionState.Swing,1);
                FailIf(!((ISwordHittableRoomEntity)body.Key).ApplySwordHit(main.CollisionBounds,main.Position-Vector2.Right,10,
                    EnemyKnockbackStrength.Normal,new List<RoomEntitySpawn>()),"Underwater Octogon must accept the declared ten-HP contact.");
                Step(400);
            }
            FailIf(!changedLayer || !emitted.Contains(0x48),$"Octogon room$5:${room:x2} must complete its timed layer change and produce native depth charges: changed={changedLayer}, depth={main.Depth}, state=${main.State:x2}, phaseCounter={main.Counter2}, emitted={string.Join(',',emitted)}.");
            if (room == 0x2d) FailIf(!emitted.Contains(0x55),"Underwater Octogon must produce its trapping bubble.");
        }
        GD.Print("Validated executed clean-US Octogon body/shell initialization, timed layer changes, attacks, fixed movement, shared handoff bytes and ordered native child/RNG updates through split/batched gameplay.");
    }
}
