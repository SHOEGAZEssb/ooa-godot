using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateOffscreenCrowFlockRom()
    {
        foreach(var target in new[] { new Vector2(32,32),new Vector2(112,32),new Vector2(32,96),new Vector2(112,96) })
        {
            bool batched=target==new Vector2(112,96);
            InitializeCompletedIntroRoom(0,0x2b);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(target);_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(AgesWramAddress.wScrollMode,0);
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            var data=EnemyDatabase.Shared;
            var row=data.GetRoomObjects(0,0x2b).Single(r=>r.Id==0x41&&r.SubId==1);
            data.TryGetCrowDefinition(row,out var record);
            var source=data.EnemyHandlers.ResolveHandler(row).CombatSource(row,0);
            var crows=new CrowCharacter[3];
            for(int slot=0;slot<3;slot++)
            {
                var crow=new CrowCharacter();crow.Initialize(record,_currentRoom,Vector2.Zero,_random,
                    ()=>_entities.Entities<CrowCharacter>().Count(c=>c.OccupiesAttackLane));
                crows[slot]=crow;var adapter=new CrowRoomEntity(crow,source,_sound.PlaySound);
                _entities.RegisterEnemySlot(adapter,slot);_entities.AddEntity(adapter);
            }
            var seed=_random.CaptureState();var rom=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            rom[0xff94]=seed.Rng1;rom[0xff95]=seed.Rng2;rom[0xcc08]=0x93;
            for(int slot=0;slot<3;slot++){int at=0xd080+slot*256;rom[at]=1;rom[at+1]=0x41;rom[at+2]=1;}
            int updates=0,reentries=0;
            void Compare()
            {
                rom.Update(_entities.FrameCounter,_player.Position);updates++;
                for(int slot=0;slot<3;slot++)
                {
                    var crow=crows[slot];int at=0xd080+slot*256;
                    if(crow.SourceState==9&&crow.Counter1==60)reentries++;
                    FailIf(crow.SourceState!=rom[at+4]||crow.Counter1!=rom[at+6]||crow.Counter2!=rom[at+7]||
                        crow.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||crow.Angle!=rom[at+9]||
                        crow.Visible!=((rom[at+0x1a]&128)!=0),
                        $"Crow flock target{target}, slot${slot:x2}, update{updates}: physical admission, spawn quadrant or repeated charge differs from ROM.");
                }
                FailIf(crows.Count(c=>c.OccupiesAttackLane)>2||RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls),
                    "Crow flock must preserve the shared two-attacker limit and ordered RNG.");
            }
            StepGameplayUpdates(2,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(crows[0].Counter1!=60||crows[1].Counter1!=240||crows[2].SourceState!=8,
                "Source crows must admit their first two physical slots with 60/240 waits and defer the third.");
            StepGameplayUpdates(650,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(reentries<2,"Offscreen crows must repeatedly reuse the attack lanes after crossing the room.");
            _entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
        }
        GD.Print("Validated clean-US $41:$01 ordered 60/240 admission, two-attacker limit, all four spawn quadrants, repeated attack-lane reuse and shared RNG through gameplay.");
    }
}
