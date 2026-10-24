using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGanonDeathRom()
    {
        foreach(bool batch in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0xf5);
            foreach(var enemy in _entities.EntityAdapters<TwinrovaRoomEntity>().ToArray())_entities.RetireEntity(enemy);
            _player.WarpTo(new(0x78,0x78));_player.Face(Vector2I.Up);_transitions.ResetCamera();_inventory.EquipA(0);_inventory.EquipB(0);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,1);_runtimeState.SetWramByte(0xcbca,1);
            var target=_roomEvents.Get<RoomOfRitesBattleEvent>().CreateGanon()!;var actor=target.Actor;
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x78)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false,CompanionDispatchEnabled=true};
            rom.EnableVramDma();rom.CreateMenuView().LoadHudGraphics();rom[0xd000]=0;rom[0xd004]=0;
            rom[0xcc8a]=1;rom[0xcbca]=1;rom[0xd080]=1;rom[0xd081]=4;rom[0xcdd1]=1;rom[0xcd17]=0xd0;
            rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter+1);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();StepGameplayUpdates(1,Vector2.Zero);
            actor.World.ClearPalette();rom[0xc4ab]=0;
            Set("State",8);Set("Substate",4);Set("Counter",200);Set("ZFixed",0);
            rom[0xd084]=8;rom[0xd085]=4;rom[0xd086]=200;rom.Word(0xd08e,0);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0);_runtimeState.SetWramByte(0xcbca,0);rom[0xcc8a]=rom[0xcbca]=0;
            _player.WarpTo(new(0x78,0x88));_player.Face(Vector2I.Up);_transitions.ResetCamera();
            rom[0xd004]=1;rom[0xd005]=0;rom.Word(0xd00c,0x7800);rom.Word(0xd00a,0x8800);rom.InitializeLinkWalkingAnimation();
            Vector2 camera=-_transitions.WorldToGameplayScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            // The sword-collision regression covers the physical hit. This
            // fixture starts at its independently traced lethal publication.
            actor.PublishHit(6,100,32);rom.HitEnemyWithItem(0,6,100,ItemId.Sword);
            _player.ClearInteractionKnockback();rom[0xd02d]=0;
            _player.ApplyInteractionInvincibility(5);rom[0xd02b]=5;
            var sounds=_sound.AttachPlayRequestAudit();int initialSounds=rom.Sounds.Count,update=0,generation=0,hold=0;
            do{StepGameplayUpdates(3,Vector2.Zero,batched:batch,afterUpdate:Compare);}while(!actor.IsDead&&update<400);
            _sound.SetRequestObserver(null);
            FailIf(!actor.IsDead||_entities.RoomEnemyCount!=0||!_saveData.HasRoomFlag(5,0xf1,0x80)||!_saveData.HasRoomFlag(5,0xf5,0x80),
                "Ganon defeat must retire both enemy counts and preserve rescue flags in $5f1/$5f5.");
            void Set(string name,int value)=>typeof(GanonCharacter).GetProperty(name,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(actor,value);
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();update++;
                FailIf(actor.IsDead!=(rom[0xd080]==0)||!actor.IsDead&&(actor.State!=rom[0xd084]||actor.Substate!=rom[0xd085]||actor.Counter!=rom[0xd086]||
                    actor.Health!=rom[0xd0a9]||actor.Visible!=((rom[0xd09a]&128)!=0)),
                    $"Ganon death update{update}: state={actor.State:x2}:{actor.Substate}/{rom[0xd084]:x2}:{rom[0xd085]},counter={actor.Counter}/{rom[0xd086]},HP={actor.Health}/{rom[0xd0a9]},visible={actor.Visible}/{rom[0xd09a]:x2},dead={actor.IsDead}/{rom[0xd080]==0}.");
                FailIf(_entities.RoomEnemyCount!=rom[0xcdd1]||_runtimeState.ReadWramByte(0xcbca)!=rom[0xcbca]||
                    _transitions.NativePaletteThreadMode!=rom[0xc4ab]||_saveData.GetRoomFlags(5,0xf1)!=rom[0xcaf1]||_saveData.GetRoomFlags(5,0xf5)!=rom[0xcaf5],
                    $"Ganon death update{update}: count={_entities.RoomEnemyCount}/{rom[0xcdd1]},collision={_runtimeState.ReadWramByte(0xcbca)}/{rom[0xcbca]},fade={_transitions.NativePaletteThreadMode}/{rom[0xc4ab]},flags={_saveData.GetRoomFlags(5,0xf1):x2},{_saveData.GetRoomFlags(5,0xf5):x2}/{rom[0xcaf1]:x2},{rom[0xcaf5]:x2}.");
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds.Skip(initialSounds)),
                    $"Ganon death update{update}: RNG/audio differs {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                if(generation!=rom.TextGeneration){generation=rom.TextGeneration;hold=3;}
                if(hold!=0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
            }
        }
        GD.Print("Validated Ganon's vulnerable-Link death gate, rescue flags, text, shared explosion/count lifetime, arena reload and control release against the clean ROM.");
    }
}
