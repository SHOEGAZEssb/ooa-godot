using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePlasmarineHookRom()
    {
        foreach(bool batch in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.SwitchHook,2);
            _inventory.GiveTreasure(TreasureId.Flippers,0);_inventory.GiveTreasure(TreasureId.MermaidSuit,0);
            LoadValidationRoom(5,0x62);_inventory.EquipA(TreasureId.SwitchHook);_inventory.EquipB(0);
            _player.WarpTo(new(120,152));_player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Plasmarine Hook fixture must start on source floor.");
            var boss=_entities.Entities<PlasmarineCharacter>().Single();
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,120,152)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();
            byte[] headers=[0xcb,0xcc,0xcd,0x8e,0xa7,0x78];
            for(int index=0;index<headers.Length;index++)rom[0xcc08+index*2]=headers[index];
            rom[0xd080]=1;rom[0xd081]=0x7e;rom[0xd08d]=80;rom[0xcdd1]=1;
            int update=0;bool swapped=false,released=false;
            void Compare(int pressed=0,int held=0,int angle=0xff)
            {
                rom.UpdateGameplay(pressed,held|pressed,angle,_entities.FrameCounter);update++;
                swapped|=boss.State==3 && boss.Substate==2;released|=boss.Substate==3;
                FailIf(boss.State!=rom[0xd084] || boss.Substate!=rom[0xd085] || boss.Counter1!=rom[0xd086] ||
                    boss.ColorIndex!=rom[0xd0b2] || boss.Health!=rom[0xd0a9] || boss.ZFixed!=unchecked((short)rom.Word(0xd08e)) ||
                    boss.Position!=new Vector2(rom.Word(0xd08c)/256f,rom.Word(0xd08a)/256f),
                    $"Plasmarine Hook update{update}: runtime state/sub/c/HP/color/XYZ={boss.State}/{boss.Substate}/{boss.Counter1}/{boss.Health}/{boss.ColorIndex}/{boss.Position}/{boss.ZFixed}; ROM={rom[0xd084]}/{rom[0xd085]}/{rom[0xd086]}/{rom[0xd0a9]}/{rom[0xd0b2]}/{rom.Word(0xd08c)/256f},{rom.Word(0xd08a)/256f}/{(short)rom.Word(0xd08e)}.");
                FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                    $"Plasmarine Hook update{update}: Link XY differs {_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}.");
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls),
                    $"Plasmarine Hook update{update}: shared RNG differs.");
            }
            for(int wait=0;boss.State!=10 && wait<160;wait++)StepGameplayUpdates(1,Vector2.Zero,batched:batch,afterUpdate:()=>Compare());
            FailIf(boss.State!=10,"Plasmarine must release its initial lock before the Hook input.");
            StepGameplayUpdates(1,Vector2.Zero,MenuRomActions(1),MenuRomActions(1),batch,()=>Compare(1));
            StepGameplayUpdates(80,Vector2.Zero,batched:batch,afterUpdate:()=>Compare());
            FailIf(!swapped || !released || boss.State!=10 || boss.ColorIndex!=1 || boss.Health!=7 || _player.IsUsingSwitchHook,
                $"Long Hook must finish Plasmarine's position/color swap, fall to ground, restore state$0a and retain seven HP: swapped={swapped}, released={released}, state/sub={boss.State}/{boss.Substate}, HP/color={boss.Health}/{boss.ColorIndex}, Link={_player.Position}, hook={_entities.SwitchHook?.Item?.State}/{_entities.SwitchHook?.Item?.Position}, using={_player.IsUsingSwitchHook}.");
            // Face the boss from the exchanged position and repeat after the
            // complete handoff, without changing either collision geometry.
            StepGameplayUpdates(1,Vector2.Down,MenuRomActions(0x80),batched:batch,afterUpdate:()=>Compare(0,0x80,16));
            StepGameplayUpdates(1,Vector2.Zero,MenuRomActions(1),MenuRomActions(1),batch,()=>Compare(1));
            StepGameplayUpdates(80,Vector2.Zero,batched:batch,afterUpdate:()=>Compare());
            FailIf(boss.State!=10 || boss.ColorIndex!=0 || boss.Health!=7 || _player.IsUsingSwitchHook,
                "A second Long Hook exchange must restore the first color and release Link again.");
        }
        GD.Print("Validated Long Hook/Plasmarine color and position exchange through clean-US Link, item, enemy and post-collision updates in split/batched gameplay.");
    }
}
