using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    // Independently traced scripts.s desk helpers and scriptHelper.s lesson:
    // SPEED_180 is retained across both calls to each four-command helper.
    private static readonly (int Line,int Speed,int Angle,int Frames)[] TokkeySourceMoves =
    [
        (8506,0x3c,8,0x20),(8509,0x3c,0x18,0x20),(8513,0x3c,8,0x20),(8517,0x3c,0x18,0x20),
        (8523,0x3c,0x18,0x10),(8526,0x3c,0x18,0x10),(8529,0x3c,8,0x10),(8532,0x3c,8,0x10),
        (7468,0x28,8,0x10),(7471,0x28,0x10,0x0a),(7479,0x3c,0x18,0x10),
        (7490,0x3c,8,0x10),(7497,0x3c,8,0x10),(7504,0x3c,0x18,0x10),(7515,0x28,0x10,0x18)
    ];

    private async Task ValidateTokkeyMovementPublicationRom()
    {
        foreach(var move in TokkeySourceMoves)
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);LoadValidationRoom(3,0x8f);
            var owner=_roomEvents.Get<TokkeyEvent>();var actor=_entities.Entities<NpcCharacter>().Single(a=>a.Record.Id==0x9d);
            F1Retain(_entities.EntityAdapters<IRoomEntity>().Single(e=>ReferenceEquals(e.Node,actor)));
            _player.WarpTo(new(80,104));_player.BeginCutsceneControl(owner:owner);_runtimeState.SetWramByte(0xcd00,0);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,80,104);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x9d;native[at+11]=(byte)actor.Position.Y;native[at+13]=(byte)actor.Position.X;
            native.AdvanceInteractions();
            Vector2 initial=new(40+(fractional?.25f:0),24+(fractional?.5f:0));
            F1Set(owner,"_precisePosition",initial);actor.Position=initial.Floor();F1Set(owner,"<State>k__BackingField",4);
            var command=owner.Database.Commands.OfType<CutsceneMoveCommand>().Single(c=>c.Source.SourceLine==move.Line);
            FailIf(command.Angle!=move.Angle||command.Counter!=move.Frames,"Tokkey source movement operands changed.");
            var runner=SomariaPrivate<CutsceneCommandRunner>(owner,"_runner");
            runner.Start([command with{Source=command.Source with{CommandIndex=0}},
                new CutsceneWaitCommand(command.Source with{CommandIndex=1,Opcode="wait"},255)]);
            runner.SetInitialMotionRegisters("Tokkey",move.Speed,move.Angle);runner.AdvanceFrame();
            // Declare the installed C2 boundary in animation-neutral state4.
            // Its post-motion native barrier is original stubScript; runtime
            // waits. Only no-motion/C2 is compared after that boundary.
            native[at+4]=4;native[at+6]=0;native[at+7]=(byte)move.Frames;
            native[at+9]=(byte)move.Angle;native[at+16]=(byte)move.Speed;
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));native.Word(at+0x18,0x45ef);
            native[0xcc8a]=0x81;native[0xcba0]=0;
            byte[] observed=new byte[4],expected=new byte[4];bool armed=false;int updates=0,moves=0;
            void Capture()
            {
                if(!armed||_runtimeState.ReadWramByte(0xcec3)==0xa8)return;
                for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);armed=false;
            }
            _runtimeState.Changed+=Capture;
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {
                armed=false;for(int i=0;i<4;i++)
                {observed[i]=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));}armed=true;
            }));
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                if(native[0xcec3]!=0xa8)moves++;
                for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];
                FailIf(SomariaPrivate<Vector2>(owner,"_precisePosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Position!=new Vector2(native[at+13],native[at+11])||runner.MovementCounter("Tokkey")!=native[at+7]||!observed.SequenceEqual(expected),
                    $"Tokkey line{move.Line}/update{updates}: original motion/C2/admission/publication differs.");
                armed=false;
            });
            Step(4);_dialogue.ShowMessage("Tokkey ordinary hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=0x83;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0x81;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,4);native[0xcd00]=4;Step(3);
            _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;Step(move.Frames-7);Step(2);
            FailIf(moves!=move.Frames-1,"Tokkey source C2 must execute exactly counter-1 movements and retain zero/post-completion no-motion visits.");
            _runtimeState.Changed-=Capture;owner.Cancel();_entities.Clear();
        }
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);
            // A released host key can remain just-pressed until Godot's next
            // frame. Explicit original-update samples must ignore that edge.
            Godot.Input.ActionPress("inventory");Godot.Input.ActionRelease("inventory");
            FailIf(!Godot.Input.IsActionJustPressed("inventory"),"Tokkey fixture must exercise a stale host inventory edge.");
            try{ValidateRoom38fTokkeyCore(true,batched);}
            finally
            {
                Godot.Input.ActionRelease("inventory");
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            }
        }
    }
}
