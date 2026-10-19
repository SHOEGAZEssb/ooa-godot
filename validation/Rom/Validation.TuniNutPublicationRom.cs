using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTuniNutPublicationRom()
    {
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetGlobalFlag(GlobalFlag.TuniNutPlaced,false);
            _inventory.GiveTreasure(TreasureId.TuniNut,2);LoadValidationRoom(5,0xf6);
            var quest=_roomEvents.Get<SymmetryEvent>();var nut=_entities.EntityAdapters<TuniNutRoomEntity>().Single();
            // Declared substate2 after rise/centering. Fractional words exercise
            // the helper independently; the integer case is the natural path.
            Vector2 initial=new(120+(fractional?.25f:0),40+(fractional?.5f:0));
            F1Set(nut,"<State>k__BackingField",3);F1Set(nut,"_substate",2);
            F1Set(nut,"_precisePosition",initial);nut.Position=initial.Floor();F1Set(nut,"_z",-4096);
            _player.WarpTo(new(120,80));_player.BeginCutsceneControl();
            _runtimeState.SetWramByte(0xcc8a,1);_runtimeState.SetWramByte(0xcd00,0);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,120,80)
                {CompanionDispatchEnabled=true};native.InitializeLinkGameplay();native[0xcc8a]=1;native[0xcd00]=0;
            // This grouped event does not yet represent the nut's physical
            // allocation. Declare source slot$d2 for this movement boundary.
            const int at=0xd240;
            native[at]=1;native[at+1]=0xb1;native[at+4]=3;native[at+5]=2;
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));native.Word(at+14,0xf000);
            int updates=0,moves=0;byte[] expected=new byte[4],observed=new byte[4];bool armed=false;
            // The post-object warp/terrain path may reuse$cec0. Observe the
            // completed helper write before that later caller, not end-of-tick.
            void Capture()
            {
                if(!armed||_runtimeState.ReadWramByte(0xcec3)==0xa8)return;
                for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);
                armed=false;
            }
            _runtimeState.Changed+=Capture;
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {
                armed=false;
                for(int i=0;i<4;i++){observed[i]=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));}
                armed=true;
            }));
            void Seed(){for(int i=0;i<4;i++){native[0xcec0+i]=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                if(native[0xcec3]!=0xa8)moves++;
                for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];
                FailIf(SomariaPrivate<Vector2>(nut,"_precisePosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    nut.Substate!=native[at+5]||nut.Height!=unchecked((short)native.Word(at+14))||
                    !observed.SequenceEqual(expected),
                    $"Tuni Nut fractional{fractional}/update{updates}: original given-speed motion, center, height or publication differs (sub{nut.Substate}/{native[at+5]} XY{SomariaPrivate<Vector2>(nut,"_precisePosition")}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, Z{nut.Height}/{unchecked((short)native.Word(at+14))}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}]).");
                armed=false;Seed();
            });
            Seed();Step(4);
            _dialogue.ShowMessage("Nut ordinary hold.",80);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,3);native[0xcc8a]=3;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(nut.Substate==2&&updates<100)Step(1);
            FailIf(nut.Substate!=3||moves!=(fractional?67:65),"Tuni Nut must complete its strict Y<$18 gate after source quarter-pixel motion.");
            _runtimeState.Changed-=Capture;quest.Cancel();_entities.Clear();_player.EndCutsceneControl();
        }
    }
}
