using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOctogonDeathRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers,0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit,0);
            byte[] shared = [1,0,0xff,0x28,0x28,0x78,0xff,0];
            for (int i = 0; i < shared.Length; i++) _runtimeState.SetWramByte(0xcfd0+i,shared[i]);
            LoadValidationRoom(5,0x36); _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(0x78,0x98));
            FailIf(_collision.Collides(_player.Position),"Octogon death fixture must start on the room's reachable lower floor.");
            var body = _entities.Entities<OctogonCharacter>().Single();
            var reward = _entities.Entities<DungeonRewardRoomEntity>().Single();
            var active = SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!;
            foreach (var entity in active.Where(entity => entity.Node != body && entity != reward).ToArray())
            { active.Remove(entity); free.Invoke(_entities,[entity]); }
            var slots = SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
            int slot = slots.Single().Value, p = 0xd080+slot*256;
            var rom = new EnemyStatusRom(_currentRoom,_saveData,_random.Calls);
            // Heart-container graphic subid$3b uses object header$79.
            byte[] headers = [0xc8,0xc9,0xca,0x8e,0xa7,0x78,0x79];
            for (int i = 0; i < headers.Length; i++) rom[0xcc08+i*2] = headers[i];
            rom[0xcc39] = 0x0c;
            for (int i = 0; i < shared.Length; i++) rom[0xcfd0+i] = shared[i];
            rom[p] = 1; rom[p+1] = 0x7d;
            rom[p+11] = (byte)body.Position.Y; rom[p+13] = (byte)body.Position.X;
            int watcher = 0xd040+_entities.InteractionSlot(reward)*256;
            rom[watcher] = 1; rom[watcher+1] = 0x20;
            rom[watcher+11] = 0x58; rom[watcher+13] = 0x78;
            var seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            int update = 0, heartUpdate = -1, zeroUpdate = -1, unlockUpdate = -1;
            bool died = false;
            void Compare()
            {
                try { rom.Update(_entities.FrameCounter,_player.Position); }
                catch (System.IO.InvalidDataException error)
                {
                    string children = string.Join(",",Enumerable.Range(0,16).SelectMany(i =>
                        new[] { 0xd040+i*256,0xd0c0+i*256 }).Where(a => rom[a] != 0)
                        .Select(a => $"${a:x4}:${rom[a+1]:x2}/${rom[a+4]:x2}"));
                    throw new System.IO.InvalidDataException($"Octogon death update{update}, native children {children}: {error.Message}",error);
                }
                FailIf(_entities.RoomEnemyCount != rom[0xcdd1],
                    $"Octogon death update{update}: counted boss/explosion ownership differs: runtime={_entities.RoomEnemyCount}, ROM={rom[0xcdd1]}.");
                foreach (int room in new[] { 0x2d,0x36 })
                    FailIf(_saveData.HasRoomFlag(5,room,OracleSaveData.RoomFlag80) != ((rom[0xca00+room]&0x80) != 0),
                        $"Octogon death update{update}: room$5:${room:x2} flag$80 differs.");
                FailIf(_entities.LinkCollisionsAndMenuDisabled != (rom[0xcbca] != 0),
                    $"Octogon death update{update}: Link/menu lock differs: runtime={_entities.LinkCollisionsAndMenuDisabled}, ROM=${rom[0xcbca]:x2}.");
                bool heart = _entities.Entities<GroundTreasurePickup>().Any();
                bool nativeHeart = Enumerable.Range(0,16).Any(i => rom[0xd040+i*256] != 0 && rom[0xd041+i*256] == 0x60);
                FailIf(heart != nativeHeart,$"Octogon death update{update}: native heart allocation differs.");
                if (heart)
                {
                    var pickup = _entities.Entities<GroundTreasurePickup>().Single();
                    int native = Enumerable.Range(0,16).Select(i => 0xd040+i*256)
                        .Single(a => rom[a] != 0 && rom[a+1] == 0x60);
                    FailIf((int)pickup.State != rom[native+4] || pickup.SpawnCounter != rom[native+6] ||
                        pickup.Visible != ((rom[native+26]&0x80) != 0) || pickup.Position != new Vector2(0x78,0x58),
                        $"Octogon heart update{update}: puff/collectibility boundary differs: runtime={pickup.State}/{pickup.SpawnCounter}/{pickup.Visible}, ROM={rom[native+4]}/{rom[native+6]}/${rom[native+26]:x2}.");
                }
                if (died && _entities.RoomEnemyCount == 0 && zeroUpdate < 0) zeroUpdate = update;
                if (heart && heartUpdate < 0) heartUpdate = update;
                if (died && reward.Finished && unlockUpdate < 0) unlockUpdate = update;
                if (rom[p] != 0 && died)
                    FailIf(body.Counter1 != rom[p+6] || body.Visible != ((rom[p+26]&0x80) != 0),
                        $"Octogon death update{update}: 120-update blink/count boundary differs.");
                update++;
            }
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            Step(2);
            FailIf(rom.Word(watcher+0x18) is < 0x4000 or >= 0x8000,
                "The clean-US boss reward must execute its native ROM script, whose jump continues into enableLinkAndMenu.");
            var bodyEntity = slots.Single(pair => pair.Key.Node == body).Key;
            rom.HitWithSword(ItemCollisionType.L1Sword,40,slot);
            ((ILinkSwordStateAwareRoomEntity)bodyEntity).SetLinkSwordState(SwordActionState.Swing,1);
            FailIf(!((ISwordHittableRoomEntity)bodyEntity).ApplySwordHit(body.CollisionBounds,body.Position-Vector2.Right,40,
                EnemyKnockbackStrength.Normal,new List<RoomEntitySpawn>()),"Octogon must accept the declared fatal sword contact.");
            died = true; Step();
            FailIf(!_saveData.HasRoomFlag(5,0x2d,OracleSaveData.RoomFlag80) || reward.Finished ||
                _entities.Entities<GroundTreasurePickup>().Any(),
                "Fatal JUST_HIT must set both layer flags while the entered reward script continues waiting for enemies.");
            Step(240);
            FailIf(zeroUpdate < 0 || heartUpdate != zeroUpdate+3 || unlockUpdate != heartUpdate+1,
                $"Boss reward must yield through zero count, flag, coordinates and spawn, then continue the ROM jump into Link unlock: zero={zeroUpdate}, heart={heartUpdate}, unlock={unlockUpdate}.");
            FailIf(!body.IsDead || _entities.Entities<OctogonCharacter>().Any() || _entities.Entities<BossDeathExplosionEffect>().Any(),
                "Octogon and its uncounted shell must delete and its explosion must finish without replaying the encounter.");
            // Re-entry takes the completed-room branch and restores only an
            // uncollected heart, never either body or a second death effect.
            _entities.LoadRoom(5,_currentRoom);
            FailIf(_entities.Entities<OctogonCharacter>().Any(),"Both-room completion must suppress Octogon on repeat entry.");
            StepGameplayUpdates(2,Vector2.Zero,batched:batched);
            FailIf(_entities.Entities<GroundTreasurePickup>().Count != 1,
                "Completed boss-room re-entry must restore its one uncollected heart after setcoords/spawn yields.");
        }
        GD.Print("Validated clean-US Octogon fatal contact, both layer flags, 120-update death, counted explosion, heart allocation and deferred Link unlock through split/batched gameplay, with completed-room re-entry.");
    }
}
