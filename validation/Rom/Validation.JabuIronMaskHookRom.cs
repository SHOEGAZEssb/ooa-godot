using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateJabuIronMaskHookRom()
    {
        foreach(bool batch in new[]{false,true})
        foreach(bool front in new[]{true,false})
        {
            ReinitializeGameplayForValidation();_inventory.GiveTreasure(TreasureId.SwitchHook,2);
            LoadValidationRoom(5,0x6e);_inventory.EquipA(TreasureId.SwitchHook);_inventory.EquipB(0);
            var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
            var active=SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free=typeof(RoomEntityManager).GetMethod("FreeEntity",BindingFlags.Instance|BindingFlags.NonPublic)!;
            foreach(var entity in active.Where(entity=>entity.Node is not IronMaskCharacter).ToArray())
            {active.Remove(entity);free.Invoke(_entities,[entity]);}
            int startX=front?40:104;
            _player.WarpTo(new(startX,136));_player.Face(front?Vector2I.Right:Vector2I.Left);
            FailIf(_collision.Collides(_player.Position),"Hook removal must approach $5:$6e masks from original reachable floor.");
            var original=slots.First().Key.Node as IronMaskCharacter;
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,5,startX,136)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xd008]=(byte)(front?1:3);rom[0xd009]=(byte)(front?8:24);
            rom[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x1c*4];rom[0xcdd1]=2;
            foreach(var (entity,slot) in slots)
            {
                // The uncounted $15:$00 has flag$01 and consumes no checked index.
                FailIf(((IronMaskRoomEntity)entity).Source.KillableEnemyIndex!=slot,"$5:$6e mask placements require checked indices$01/$02.");
                int address=0xd080+slot*256;rom[address]=(byte)(1|(slot<<4));rom[address+1]=0x1c;
                rom.Word(address+10,(int)entity.Node.Position.Y*256);rom.Word(address+12,(int)entity.Node.Position.X*256);
            }
            int update=0;bool sawChild=false,sawSwap=false,sawExpiry=false;
            void Compare(int edge,int held=0,int angle=0xff)
            {
                rom.UpdateGameplay(edge,held|edge,angle,_entities.FrameCounter);update++;
                var live=slots.Where(pair=>pair.Key.Node is IronMaskCharacter).ToArray();
                int nativeCount=Enumerable.Range(0,16).Count(slot=>rom[0xd080+slot*256]!=0);
                FailIf(live.Length!=nativeCount || _entities.RoomEnemyCount!=rom[0xcdd1],
                    $"Iron Mask Hook update{update}: live slots/count differ {live.Length}/{nativeCount}, {_entities.RoomEnemyCount}/{rom[0xcdd1]}.");
                foreach(var (entity,slot) in live)
                {
                    var mask=(IronMaskCharacter)entity.Node;int address=0xd080+slot*256;
                    FailIf(((IronMaskRoomEntity)entity).Source.KillableEnemyIndex!=(rom[address]>>4&7),"Iron Mask child must inherit its source enabled-byte checked index.");
                    FailIf(mask.Record.SubId!=rom[address+2] || mask.State!=rom[address+4] || mask.Substate!=rom[address+5] ||
                        mask.Counter1!=rom[address+6] || mask.Position!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f) ||
                        mask.ZFixed!=unchecked((short)rom.Word(address+14)) || mask.CollisionMode!=rom[address+0x25] ||
                        (mask.InvincibilityCounter&255)!=rom[address+0x2b] || mask.KnockbackCounter!=rom[address+0x2d],
                        $"Iron Mask Hook slot${slot:x2} update{update}: runtime subid/state/sub/counter={mask.Record.SubId}/{mask.State}/{mask.Substate}/{mask.Counter1}, XY={mask.Position}, Z={mask.ZFixed}, inv/recoil={mask.InvincibilityCounter}/{mask.KnockbackCounter}; ROM={rom[address+2]}/{rom[address+4]}/{rom[address+5]}/{rom[address+6]}, XY={rom.Word(address+12)/256f},{rom.Word(address+10)/256f}, Z={(short)rom.Word(address+14)}, inv/recoil={(sbyte)rom[address+0x2b]}/{rom[address+0x2d]}; hook runtime={_entities.SwitchHook?.Item?.State}/{_entities.SwitchHook?.Item?.Position}, ROM={rom[0xd604]}/{rom.Word(0xd60c)/256f},{rom.Word(0xd60a)/256f}, collision=${rom[0xd624]:x2}, enemycollision=${rom[address+0x24]:x2}.");
                    sawChild|=mask.Record.SubId==1;sawSwap|=mask.State==3 && mask.Substate==2;
                }
                sawExpiry|=original!.MaskExpired;
                var random=_random.CaptureState();
                FailIf(random.Rng1!=rom[0xff94] || random.Rng2!=rom[0xff95] || random.Calls-seed.Calls!=rom.RandomCalls,
                    $"Iron Mask Hook update{update}: shared allocation/route RNG differs.");
                FailIf((int)SomariaPrivate<float>(_player,"_enemyKnockbackFrames")!=rom[0xd02d],
                    $"Iron Mask Hook update{update}: Link recoil runtime={SomariaPrivate<float>(_player,"_enemyKnockbackFrames")}, ROM={rom[0xd02d]}, inv={SomariaPrivate<float>(_player,"_enemyInvincibilityFrames")}/{rom[0xd02b]}, angle={rom[0xd02c]}.");
                FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                    $"Iron Mask Hook update{update}: Link exchange position differs {_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}; angle runtime={SomariaPrivate<int>(_player,"_enemyKnockbackAngle"):x2}, native=${rom[0xd02c]:x2}.");
            }
            StepGameplayUpdates(1,Vector2.Zero,batched:batch,afterUpdate:()=>Compare(0));
            StepGameplayUpdates(1,Vector2.Zero,MenuRomActions(1),MenuRomActions(1),batch,()=>Compare(1));
            // Walk away through the original corridor after the exchange, so
            // this bounded handoff trace does not become an unrelated combat run.
            StepGameplayUpdates(38,Vector2.Zero,batched:batch,afterUpdate:()=>Compare(0));
            while(_player.IsUsingSwitchHook && update<90)
                StepGameplayUpdates(1,Vector2.Zero,batched:batch,afterUpdate:()=>Compare(0));
            StepGameplayUpdates(12,Vector2.Left,MenuRomActions(0x20),batched:batch,afterUpdate:()=>Compare(0,0x20,24));
            StepGameplayUpdates(80,Vector2.Zero,batched:batch,afterUpdate:()=>Compare(0));
            FailIf(sawChild!=front || !sawSwap || sawExpiry!=front,"Iron Mask front Hook must remove/expire the mask and transfer its index; rear Hook must swap the existing enemy.");
        }
        GD.Print("Validated clean-US Iron Mask removal through gameplay: Long Hook exchange, child/recoil/order, mask expiry, count transfer, Link position and RNG with split/batched updates.");
    }
}
