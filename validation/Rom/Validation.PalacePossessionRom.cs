using Godot;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePalacePossessionRom()
    {
        foreach (bool batch in new[] {false,true})
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(5,0xce); _inventory.EquipA(0); _inventory.EquipB(0);
            var source=_entities.EntityAdapters<PalacePossessionRoomEntity>().Single(actor=>actor.Id==0x6d && actor.SubId==0);
            var floor=new Vector2(0x78,0x98);
            FailIf(_collision.Collides(floor),"Possessed Nayru introduction requires a reachable palace floor.");
            _player.WarpTo(floor); _player.Face(Vector2I.Up);
            var random=_random.CaptureState();
            var rom=new SomariaRom(_saveData,random,_currentRoom,0,0x78,0x98)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();
            const int main=0xd240;
            rom[main]=1; rom[main+1]=0x6d; rom[main+11]=(byte)source.Node.Position.Y; rom[main+13]=(byte)source.Node.Position.X;
            rom[0xcc08]=0xd0; rom[0xcc0a]=0;
            int update=0, generation=0, texts=0,hold=0, projectileUpdates=0;
            void Compare()
            {
                rom.AdvanceWarpPalette(); rom.UpdateGameplay(0,0,0xff,++update);
                FailIf(RandomDiffers(random, rom[0xff94], rom[0xff95], rom.RandomCalls),
                    $"Possession shared RNG differs update={update}.");
                foreach (var actor in _entities.EntityAdapters<PalacePossessionRoomEntity>())
                {
                    int address=Enumerable.Range(2,14).Select(slot=>0xd040+slot*256).FirstOrDefault(slot=>rom[slot]!=0&&rom[slot+1]==actor.Id&&rom[slot+2]==actor.SubId);
                    FailIf(address==0 || actor.State!=rom[address+4] || actor.PrecisePosition!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f),
                        $"Possession intro INTERAC${actor.Id:x2}:${actor.SubId:x2} differs update={update}: port={actor.State}/{actor.PrecisePosition}, ROM={rom[address+4]}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}.");
                }
                foreach (var enemy in _entities.EntityAdapters<PossessedNayruRoomEntity>())
                {
                    int address=0xd080+enemy.Slot*256; var actor=enemy.Actor;
                    FailIf(rom[address]==0 || actor.State!=rom[address+4] || actor.Counter!=rom[address+6] || actor.Counter2!=rom[address+7] || (actor.State!=0 && actor.Health!=rom[address+0x29]) ||
                        actor.Position!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f),
                        $"ENEMY$61:${actor.SubId:x2} differs update={update}: port={actor.State:x2}/{actor.Counter}/{actor.Counter2}/{actor.Position}, angle/speed={actor.Angle:x2}/{actor.Speed:x2}, target={actor.Target}; ROM={rom[address+4]:x2}/{rom[address+6]}/{rom[address+7]}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}, angle/speed={rom[address+9]:x2}/{rom[address+16]:x2}, target={rom[address+0x32]},{rom[address+0x31]}.");
                }
                var projectiles=_entities.EntityAdapters<VeranProjectileRoomEntity>().ToArray();
                int nativeProjectiles=Enumerable.Range(0,16).Count(slot=>rom[0xd0c0+slot*256]!=0&&rom[0xd0c1+slot*256]==0x37);
                FailIf(projectiles.Length!=nativeProjectiles,$"Veran PART$37 allocation differs update={update}: port={projectiles.Length}, ROM={nativeProjectiles}.");
                var slots=(Dictionary<IRoomEntity,int>)typeof(RoomEntityManager).GetField("_partSlots",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(_entities)!;
                foreach (var part in projectiles)
                {
                    int address=0xd0c0+slots[part]*256; projectileUpdates++;
                    FailIf(part.State!=rom[address+4] || part.Counter!=rom[address+6] || part.Position!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f) ||
                        part.ZFixed!=unchecked((short)rom.Word(address+14)),
                        $"PART$37:${part.SubId:x2} differs update={update}: port={part.State}/{part.Counter}/{part.Position}/{part.ZFixed}, ROM={rom[address+4]}/{rom[address+6]}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}/{unchecked((short)rom.Word(address+14))}.");
                }
                FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                    $"Possession Link movement differs update={update}: port={_player.PrecisePosition}, ROM={rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}.");
                if (generation!=rom.TextGeneration)
                {
                    int[] expected=[0x5a08,0x5a09,0x5a0a,0x332a,0x5a0b];
                    FailIf(texts>=expected.Length || rom.Word(0xcba2)!=expected[texts] || !_dialogue.IsOpen,
                        $"Possession introduction text differs after {texts} messages: ROM=${rom.Word(0xcba2):x4}.");
                    generation=rom.TextGeneration; texts++;hold=3;
                }
                if (hold>0 && --hold==0) { _dialogue.Close();rom[0xcba0]=0; }
            }
            StepGameplayUpdates(620,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(texts!=3 || projectileUpdates==0 || !_saveData.HasGlobalFlag(0x18) || _entities.RuntimeState.ReadWramByte(WramAddress.wMenuDisabled)!=0,
                "Possession introduction must show all three source texts and hand off to the counted boss with controls released.");
            var boss=_entities.EntityAdapters<PossessedNayruRoomEntity>().Single();
            for(int wait=0;wait<240&&!boss.Actor.CollisionEnabled;wait++) StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            var spawns=new List<RoomEntitySpawn>();
            boss.SetLinkSwordState(SwordActionState.Swing,1);
            rom.HitEnemyWithItem(boss.Slot,4,1);
            FailIf(!boss.ApplySwordHit(boss.Actor.CollisionBounds,boss.Actor.Position-Vector2.Right,1,EnemyKnockbackStrength.Low,spawns),
                "Possessed Nayru must accept the direct-attack collision without losing host health.");
            StepGameplayUpdates(2,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(boss.Actor.InvincibilityCounter>=0 || boss.Actor.RemainingHits!=3,
                "Direct attack must cause native retaliation and leave Nayru's three-hit progression unchanged.");
            StepGameplayUpdates(32,Vector2.Zero,batched:batch,afterUpdate:Compare);
            for(int wait=0;wait<240&&!boss.Actor.CollisionEnabled;wait++) StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            FailIf(!new SeedSatchelDatabase().TryGet(ItemId.MysterySeed,out var seed),"Missing Mystery seed record.");
            rom.HitEnemyWithItem(boss.Slot,0x1a,0,ItemId.MysterySeed);
            var response=boss.ApplySeedCollision(boss.Actor.CollisionBounds,boss.Actor.Position-Vector2.Right,seed,0x1a,spawns);
            FailIf(!response.Contact,"Possessed Nayru must accept the live Mystery collision through its source effect$20.");
            StepGameplayUpdates(30,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(boss.Actor.State!=15 || _entities.EntityAdapters<PossessedNayruRoomEntity>().Count()!=2,
                "Mystery must collapse Nayru and allocate Veran separately after the source 30-update delay.");
            StepGameplayUpdates(170,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(texts!=4 || _entities.EntityAdapters<PossessedNayruRoomEntity>().Count()!=1 || boss.Actor.RemainingHits!=3,
                "Exposed Veran must show its first taunt, expire, and return without consuming one of the three required hits.");
            _inventory.GiveTreasure(TreasureId.SwitchHook,1); _inventory.EquipA(TreasureId.SwitchHook);
            for(int address=0xc5b0;address<0xcb00;address++) rom[address]=_saveData.ReadWramByte(address);
            for(int remaining=3;remaining>0;remaining--)
            {
                for(int wait=0;wait<400&&!boss.Actor.CollisionEnabled;wait++) StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
                rom.HitEnemyWithItem(boss.Slot,0x1a,0,ItemId.MysterySeed);
                FailIf(!boss.ApplySeedCollision(boss.Actor.CollisionBounds,boss.Actor.Position-Vector2.Right,seed,0x1a,spawns).Contact,
                    "Repeat Mystery collision must collapse the host.");
                StepGameplayUpdates(42,Vector2.Zero,batched:batch,afterUpdate:Compare);
                var child=_entities.EntityAdapters<PossessedNayruRoomEntity>().Single(enemy=>enemy.Actor.SubId==2);
                var approach=new[] {Vector2I.Down,Vector2I.Up,Vector2I.Right,Vector2I.Left}.FirstOrDefault(direction=>
                    Enumerable.Range(12,13).All(distance=>Enumerable.Range(-5,11).All(side=>
                        !_currentRoom.IsSolid(child.Actor.Position.Floor()+(Vector2)direction*distance+new Vector2(-direction.Y,direction.X)*side))));
                FailIf(approach==Vector2I.Zero,$"Veran hook approach must have a clear Link corridor near {child.Actor.Position}.");
                Vector2 origin=child.Actor.Position.Floor()+(Vector2)approach*24;
                _player.WarpTo(origin); _player.Face(-approach);
                int hookDirection=approach==Vector2I.Down?0:approach==Vector2I.Up?2:approach==Vector2I.Right?3:1;
                rom.Word(0xd00c,(int)origin.X<<8);rom.Word(0xd00a,(int)origin.Y<<8);rom[0xd008]=(byte)hookDirection;rom[0xd009]=(byte)(hookDirection*8);
                int nativeInput=1;
                void HookCompare()
                {
                    // Compare supplies the same ITEM input to the native gameplay pass.
                    int address=0xd080+child.Slot*256;
                    rom[0xcc29]=rom[0xcc2a]=(byte)nativeInput;
                    rom.AdvanceWarpPalette();rom.UpdateGameplay(nativeInput,nativeInput,0xff,++update);nativeInput=0;
                    FailIf(child.Actor.State!=rom[address+4] || child.Actor.Substate!=rom[address+5] || child.Actor.Position!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f) ||
                        _player.IsUsingSwitchHook!=(rom[0xd200]!=0) || _entities.SwitchHook!.ExchangeState!=rom[0xccdd],
                        $"Veran actual Switch Hook handoff differs update={update}: port={child.Actor.State}/{child.Actor.Substate}/{child.Actor.Position}/{_entities.SwitchHook.ExchangeState}, ROM={rom[address+4]}/{rom[address+5]}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}/{rom[0xccdd]}.");
                }
                StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],afterUpdate:HookCompare);
                for(int wait=0;wait<100&&(child.Actor.State!=11||_player.IsUsingSwitchHook);wait++)
                    StepGameplayUpdates(1,Vector2.Zero,afterUpdate:HookCompare);
                FailIf(child.Actor.State!=11 || _player.IsUsingSwitchHook,"Veran hook exchange must land and release Link before vulnerability.");
                StepGameplayUpdates(40,Vector2.Zero,batched:batch,afterUpdate:Compare);
                child.SetLinkSwordState(SwordActionState.Swing,1);
                rom.HitEnemyWithItem(child.Slot,4,1);
                FailIf(!child.ApplySwordHit(child.Actor.CollisionBounds,child.Actor.Position-Vector2.Right,1,EnemyKnockbackStrength.Low,spawns),
                    "Exposed human Veran must accept one source sword hit.");
                StepGameplayUpdates(2,Vector2.Zero,batched:batch,afterUpdate:Compare);
                FailIf(child.Actor.RemainingHits!=remaining-1,"Human Veran must consume one remaining hit per successful exposure.");
                if(remaining>1) StepGameplayUpdates(70,Vector2.Zero,batched:batch,afterUpdate:Compare);
                else
                {
                    for(int wait=0;wait<120&&texts<5;wait++) StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
                    FailIf(texts!=5 || boss.Actor.State!=18,"Third human-form hit must reach the defeat text and native rescue state.");
                }
            }
            for(int wait=0;wait<120&&!_entities.EntityAdapters<PalacePossessionRoomEntity>().Any(actor=>actor.Id==0x6e);wait++)
                StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            FailIf(!_entities.EntityAdapters<PalacePossessionRoomEntity>().Any(actor=>actor.Id==0x6e && actor.SubId==0) ||
                _entities.EntityAdapters<PossessedNayruRoomEntity>().Any() ||
                _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects)!=rom[0xcc8a],
                "Defeated possession boss must clear its enemies and hand the same gameplay loop to INTERAC$6e:$00.");
        }
    }
}
