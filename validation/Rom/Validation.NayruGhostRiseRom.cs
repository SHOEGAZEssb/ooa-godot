using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateNayruGhostRiseRom()
    {
        foreach (bool reveal in new[] { false, true })
        foreach (int hold in new[] { 5, 160 })
        foreach (bool batched in new[] { false, true })
        foreach (Vector2 initial in new[] { new Vector2(80.25f,40.5f), new(128.25f,0.5f) })
        {
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(GlobalFlag.IntroDone,false); LoadValidationRoom(0,0x39);
            var owner=_roomEvents.Get<NayruIntroEvent>();
            var ghost=owner.ActorRegistry.Spawn("GhostVeran","GhostVeran",initial);
            F1Set(owner,"_nayruStage",NayruStage.Script);
            F1Set(owner,"_nayruGhostRevealFlickerRemaining",reveal ? 0x38 : 0);
            F1Set(owner,"_ghostRisePosition",OracleObjectPosition.FromPixels(initial));
            var remaining=typeof(NayruIntroEvent).GetField("_ghostRiseCounter",BindingFlags.Instance|BindingFlags.NonPublic);
            remaining?.SetValue(owner,reveal ? -1 : 0x5a);
            var database=new NayruIntroEventDatabase(); var commands=new List<CutsceneCommand>();
            foreach(string handler in reveal ? new[]{"WaitForGhostAppearance","GhostInitialRise"} : new[]{"GhostInitialRise"})
            {
                var original=database.Commands.OfType<CutsceneNativeBlockingCommand>().Single(command=>command.Handler==handler);
                commands.Add(original with { Source=original.Source with { CommandIndex=commands.Count } });
            }
            var source=commands[^1].Source;
            commands.Add(new CutsceneWaitCommand(source with { CommandIndex=commands.Count,Opcode="wait" },0xff));
            commands.Add(new CutsceneEndCommand(source with { CommandIndex=commands.Count,Opcode="scriptend" }));
            owner.CommandRunner.Start(commands);
            _player.WarpTo(new(40,112)); _player.BeginCutsceneControl(owner:owner);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,1); _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,40,112);
            native.InitializeLinkGameplay(); native[0xcc8a]=1; native[0xcc6e]=1;
            int at=0xd040+_entities.InteractionSlot(ghost)*256;
            native[at]=1; native[at+1]=0x3e; native.AdvanceInteractions();
            native.Word(at+10,(int)(initial.Y*256)); native.Word(at+12,(int)(initial.X*256));
            native[at+5]=(byte)(reveal ? 0 : 1); native[at+6]=(byte)(reveal ? 0x38 : 0x5a);
            native[at+0x1a]=(byte)(reveal ? 0x83 : 0x80);
            ghost.SetFixedDrawPriority(ObjectDrawPriority.FromVisible(native[at+0x1a]));
            native[at+9]=0; native[at+0x10]=0x0a;
            _sound.ClearPlayRequestAudit(); int visits=0,moves=0;
            void Seed() { for(int i=0;i<4;i++) { byte value=(byte)(0xa5+i); native[0xcec0+i]=value; _runtimeState.SetWramByte(0xcec0+i,value); } }
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int y=native.Word(at+10); native[0xcc00]=(byte)_entities.FrameCounter; native.AdvanceInteractions(); visits++;
                if(y!=native.Word(at+10)) moves++;
                bool wrongCounter=native[at+5]==0
                    ? SomariaPrivate<int>(owner,"_nayruGhostRevealFlickerRemaining")!=native[at+6]
                    : remaining is not null && (int)remaining.GetValue(owner)! != (native[at+5]==1 ? native[at+6] : 0);
                FailIf(owner.GhostRisePosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f) || wrongCounter ||
                    !ghost.ScriptVisible || ghost.ZIndex!=ObjectDrawPriority.FromVisible(native[at+0x1a]) ||
                    Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                    $"Ghost reveal/rise reveal={reveal},hold{hold},visit{visits}: native always-update counter/word position/publication differs (state{native[at+5]},counter${native[at+6]:x2},moves{moves}).");
                Seed();
            });
            Seed(); Step(4);
            _dialogue.ShowMessage("Ghost native reveal/rise continues during text.",_player.Position.Y); native[0xcba0]=1;
            Step(hold); _dialogue.Close(); native[0xcba0]=0;
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,3); native[0xcc8a]=3; Step(3);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,1); native[0xcc8a]=1;
            _runtimeState.SetWramByte(WramAddress.wScrollMode,8); native[0xcd00]=8; Step(3);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,1); native[0xcd00]=1;
            for(int limit=0;native[at+5]!=2 && limit<180;limit++) Step(1);
            Step(3);
            FailIf(moves!=89 || owner.CommandRunner.CurrentCommand is not CutsceneWaitCommand ||
                _sound.PlayRequestsFor(SoundId.MusRoomOfRites)!=(reveal ? 1 : 0) ||
                native.Sounds.Count(sound=>sound==SoundId.MusRoomOfRites)!=(reveal ? 1 : 0),
                "Ghost must finish its native reveal cue and89 quarter-pixel rise updates, including completion during held text, before script continuation.");
            owner.Cancel(); _entities.Clear();
        }
    }
}
