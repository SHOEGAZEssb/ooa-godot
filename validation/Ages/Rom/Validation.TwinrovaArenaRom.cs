using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTwinrovaArenaLayoutRom()
    {
        ReinitializeGameplayForValidation();LoadValidationRoom(5,0xf5);
        var room=_currentRoom;
        var actor=_entities.EntityAdapters<TwinrovaRoomEntity>().Single();
        var rom=new SomariaRom(_saveData,_random.CaptureState(),room,0,0x78,0x88);
        rom.PrepareRoomBackground();
        foreach(int mode in new[] {1,2,3,4,255,0})
        {
            _roomEvents.Get<RoomOfRitesBattleEvent>().ReloadArena(mode);
            rom.ReloadTwinrovaArena(mode);
            FailIf(!ReferenceEquals(room,_currentRoom) || !_entities.EntityAdapters<TwinrovaRoomEntity>().Contains(actor),
                "func_131f arena reload must retain room identity and native enemy slots.");
            CompareTwinrovaArena(rom,$"mode${mode:x2}");
        }
        GD.Print("Validated lava/ice/normal/seizure arena requests, full native layouts/padding, terrain collisions, underlying bytes and background mappings against the clean ROM.");
    }
    private void CompareTwinrovaArena(SomariaRom rom,string phase)
    {
        FailIf(_runtimeState.ReadWramByte(TwinrovaArenaDatabase.ModeAddress)!=rom[0xcca9],$"Twinrova arena {phase}: mode consumption differs.");
        for(int position=0;position<176;position++)
        {
            byte tile=_currentRoom.GetPackedStorageMetatile((byte)position);
            FailIf(tile!=rom[0xcf00+position] || _currentRoom.GetUnderlyingStorageMetatile(position)!=rom.Underlying(position),
                $"Twinrova arena {phase}: layout${position:x2}={tile:x2}/{rom[0xcf00+position]:x2} or underlying differs.");
            if((position&15)==15)continue;
            Vector2 point=new((position&15)*16+8,(position>>4)*16+8);
            FailIf((byte)_currentRoom.GetTerrainInfo(point).Collision!=rom[0xce00+position],
                $"Twinrova arena {phase}: collision${position:x2} differs.");
        }
        for(int y=0;y<22;y++) for(int x=0;x<30;x++)
            FailIf(_currentRoom.GetBackgroundSubtileForValidation(x,y)!=rom.BackgroundTile(x,y) ||
                _currentRoom.GetBackgroundAttributeForValidation(x,y)!=rom.BackgroundAttribute(x,y),
                $"Twinrova arena {phase}: background tile/attribute ({x},{y}) differs.");
    }
    private void ValidateMergedTwinrovaArenaRom()
    {
        var animationCounter=typeof(EnemyAnimationPlayer).GetField("_frameCounter",BindingFlags.NonPublic|BindingFlags.Instance)!;
        foreach(bool batched in new[] {false,true})
        foreach(byte initial in new byte[] {0,1})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0xf5);
            var placed=_entities.EntityAdapters<TwinrovaRoomEntity>().Single();var world=placed.Actor.World;
            _entities.RetireEntity(placed);
            var boss=new MergedTwinrovaRoomEntity(new(world,new(0x78,0x48),0));
            _entities.RegisterEnemySlot(boss,0);_entities.AddEntity(boss);
            _player.WarpTo(new(0x78,0x78));_player.Face(Vector2I.Up);_transitions.ResetCamera();
            _inventory.EquipA(0);_inventory.EquipB(0);
            _random.RestoreState(_random.CaptureState() with {Rng1=initial,Rng2=1});
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x78)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            rom.PrepareRoomBackground();rom.InitializeLinkGameplay();rom.InitializeLinkWalkingAnimation();
            rom[0xd080]=1;rom[0xd081]=1;rom[0xd08b]=0x48;rom[0xd08d]=0x78;rom[0xcdd1]=1;rom[0xcd17]=0xd0;
            rom[0xcc08]=0xdb;rom[0xcc0a]=0xdc;rom[0xcc0c]=0xdd;rom[0xcc0e]=0xde;rom[0xcc10]=0xdf;
            Vector2 camera=-_transitions.WorldToGameplayScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var sounds=_sound.AttachPlayRequestAudit();int update=0,generation=0,hold=0;bool loaded=false;
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            Step(100);
            while(boss.Actor.State!=0x0b&&update<350)Step(1);
            FailIf(boss.Actor.State!=0x0b || !loaded || boss.Actor.Subid!=(initial==0?1:0),
                "Merged Twinrova must finish both source ice/lava transformation branches before its attack delay.");
            Step(30);
            FailIf(boss.Actor.State!=0x0c || _runtimeState.ReadWramByte(0xcbca)!=0 || _transitions.NativePaletteFadeActive,
                "Merged Twinrova must complete the 30-update arena attack delay and release native collision/menu control.");
            // Exercise the real ice volley before its snowball handoff.
            bool ice=boss.Actor.Subid==1;
            int attackUpdates=0;bool sawAttack=false,sawPart=false;
            do
            {
                Step(1);attackUpdates++;
                sawAttack|=boss.Actor.State==(ice?0x10:0x0e);
                sawPart|=_entities.EntityAdapters<TwinrovaAttackPart>().Any(p=>p.Id==(ice?0x4e:0x4c));
            }while((!sawAttack||boss.Actor.State!=(ice?0x0e:0x0c))&&attackUpdates<450);
            FailIf(!sawAttack||!sawPart||attackUpdates>=450,
                "Merged Twinrova first flame/snowball attack did not complete its source positioning/charge/PART handoff.");
            if(!ice)
            {
                // Its next table entry ($02) is the four-bat attack. Stage
                // arrival rather than replaying another movement circuit.
                Set("State",0x0d);Set("MinimumMovement",0);Set("SwapDisabled",1);
                boss.Actor.Position=boss.Actor.Target;rom.Word(0xd08c,boss.Actor.Target.X<<8);rom.Word(0xd08a,boss.Actor.Target.Y<<8);
                rom[0xd084]=0x0d;rom[0xd0b3]=0;rom[0xd0b9]=1;
                Step(272);
                FailIf(boss.Actor.Attack!=1||boss.Actor.State!=0x0c,
                    "Merged Twinrova bat attack did not finish its four checked allocations and 180-update recovery.");
            }
            // Stage the independently traced timer boundary, avoiding hundreds
            // of attack updates. The helper runs before the current handler,
            // retries at one while locked, and can enter stateA in that visit.
            boss.Actor.Position=new(0x78,0x48);rom.Word(0xd08c,0x7800);rom.Word(0xd08a,0x4800);
            Set("State",0x0b);Set("Counter",30);Set("SwapCounter",1);Set("SwapDisabled",1);
            rom[0xd084]=0x0b;rom[0xd086]=30;rom[0xd0b0]=1;rom[0xd0b9]=1;
            Step(4);
            FailIf(boss.Actor.State!=0x0b || boss.Actor.SwapCounter!=1,
                "The merged room-swap timer must retain one while an attack owns var39.");
            Set("SwapDisabled",0);rom[0xd0b9]=0;
            Step(4);
            FailIf(boss.Actor.State!=0x0a || boss.Actor.SwapDisabled!=1,
                "An expired unlocked swap timer must execute its state9 center arrival in the same native enemy visit.");
            _sound.SetRequestObserver(null);

            void Set(string property,int value)=>typeof(MergedTwinrovaCharacter).GetProperty(property,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(boss.Actor,value);

            void Compare()
            {
                rom.AdvanceWarpPalette();rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();update++;
                var a=boss.Actor;
                FailIf(a.State!=rom[0xd084] || a.Substate!=rom[0xd085] || a.Counter!=rom[0xd086] || a.SwapCounter!=rom[0xd0b0] ||
                    a.SwapDisabled!=rom[0xd0b9] || a.MovementWait!=rom[0xd0bb] || a.Subid!=rom[0xd082] || a.Palette!=rom[0xd09b] ||
                    a.MinimumMovement!=rom[0xd0b3]||a.AttackIndex!=rom[0xd0b2]||a.Attack!=rom[0xd083]||a.Counter2!=rom[0xd087]||
                    a.TargetIndex!=rom[0xd0b6]||a.Target!=new Vector2I(rom[0xd0b8],rom[0xd0b7])||
                    a.Position!=new Vector2(rom.Word(0xd08c)/256f,rom.Word(0xd08a)/256f)||
                    a.CollisionType!=rom[0xd0a4] || (int)animationCounter.GetValue(a.Animation)! !=rom[0xd0a0],
                    $"Merged arena update{update}: state={a.State:x2}:{a.Substate}/{rom[0xd084]:x2}:{rom[0xd085]},count={a.Counter}/{rom[0xd086]},swap={a.SwapCounter}/{rom[0xd0b0]},disable={a.SwapDisabled}/{rom[0xd0b9]},subid={a.Subid}/{rom[0xd082]},palette={a.Palette}/{rom[0xd09b]}.");
                foreach(var part in _entities.EntityAdapters<TwinrovaAttackPart>())
                {
                    int slot=Enumerable.Range(0,16).Single(s=>ReferenceEquals(_entities.ResolvePartSlot(s),part));int at=0xd0c0+slot*256;
                    FailIf(part.Id!=rom[at+1]||part.Subid!=rom[at+2]||part.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                        part.ZFixed!=unchecked((short)rom.Word(at+14))||part.State!=rom[at+4]||part.Counter!=rom[at+6],
                        $"Merged arena update{update}: parent-spawned PART slot${slot:x2} differs.");
                }
                for(int slot=0;slot<16;slot++)
                {
                    int at=0xd080+slot*256;var child=_entities.ResolveEnemySlot(slot) as TwinrovaMinionRoomEntity;
                    FailIf((child is not null)!=(rom[at]!=0&&rom[at+1] is 0x5d or 0x5e),
                        $"Merged arena update{update}: minion slot${slot:x2} admission/lifetime differs.");
                    if(child is null)continue;
                    var c=child.Actor;
                    FailIf(c.Id!=rom[at+1]||c.State!=rom[at+4]||c.Counter!=rom[at+6]||c.Counter2!=rom[at+7]||c.Angle!=rom[at+9]||
                        c.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||c.ZFixed!=unchecked((short)rom.Word(at+14)),
                        $"Merged arena update{update}: parent-spawned minion slot${slot:x2} differs; state={c.State}/{rom[at+4]},angle={c.Angle}/{rom[at+9]},pos={c.Position}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}.");
                }
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls) || !sounds.Requests.SequenceEqual(rom.Sounds) ||
                    _transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0) || _runtimeState.ReadWramByte(0xcbca)!=rom[0xcbca],
                    $"Merged arena update{update}: RNG/audio/palette/control differs; sounds={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                FailIf(_entities.LinkCollisionsAndMenuDisabled!=(rom[0xcbca]!=0) || rom[0xcbca]!=0&&!_entities.PlayerMenusDisabled,
                    $"Merged arena update{update}: raw wDisableLinkCollisionsAndMenu must reach collision and menu consumers.");
                if(a.State==0x0a && a.Substate>=3 && !loaded)
                {CompareTwinrovaArena(rom,$"transformation{initial}, update{update}");loaded=true;}
                if(generation!=rom.TextGeneration){generation=rom.TextGeneration;hold=3;}
                if(hold!=0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
                _player.ApplyInteractionInvincibility(128);rom[0xd02b]=128;
            }
        }
        GD.Print("Validated both merged Twinrova arena transformations, palette flicker/fades, exact counters, attack-lock timer expiry and Link-control release in split/batched gameplay against the clean ROM.");
    }
}
