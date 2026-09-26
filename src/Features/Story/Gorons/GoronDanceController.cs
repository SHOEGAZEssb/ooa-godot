using Godot;
using System;

namespace oracleofages;

// Native $66:$00 states. Script completion, rather than dialogue closure,
// transfers control between the command stream and the rhythm game.
internal sealed class GoronDanceController(GoronCaveScriptHost host)
{
    private RoomEventContext Context => host.Context;
    private OracleRuntimeState Wram => Context.Entities.RuntimeState;
    private int _state=1, _substate, _counter, _z, _speedZ;
    private bool _failureScript;
    internal bool ScriptRunning => _state is 1 or 4 || _state==3&&_substate==4&&_failureScript;
    internal int State => _state;
    internal int Substate => _substate;
    internal int Counter => _counter;
    private int Read(int address) => Wram.ReadWramByte(address);
    private void Write(int address,int value) => Wram.SetWramByte(address,(byte)value);

    internal void InitializeRounds()
    {
        for(int i=0;i<10;i++) Write(0xcee0+i,i);
        Write(0xcfde,10);
    }
    internal void Initialize()
    {
        for(int address=0xcfc0;address<0xcfe0;address++) Write(address,0);
        Write(0xcfd2,2);
        _state=1; _substate=0;
    }
    internal void Clear()
    {
        // scriptHelp.goronDance_clearVariables also turns Link; native
        // goronSubid00@state0 only clears the dance bytes and animation.
        Initialize();
        Context.Player.Face(Vector2I.Down);
        Context.Player.SetScriptedLinkAnimationMode(null);
    }
    internal void Restart()
    {
        Write(0xcfda,0); Write(0xcfdb,0);
        Context.Player.Face(Vector2I.Down); InitializeRounds();
    }
    internal void ScriptEnded()
    {
        if(_state==1)
        {
            InitializeRounds(); Write(0xcfd2,2);
            _state=2; _substate=0; _counter=30;
            Update(); // state 1 falls directly through state 2.
        }
        else if(_state==3&&_substate==4) NextRound();
    }
    private void NextRound()
    {
        _state=2; _substate=0; _counter=30;
        Write(0xcfd4,0); Write(0xcfd2,2);
        Context.Player.SetScriptedLinkAnimationMode(null); Context.Player.Face(Vector2I.Down);
    }
    private void EndDance()
    {
        _state=4; _substate=0; _counter=60;
        Write(0xcfd4,0); Write(0xcfd2,2);
        Context.Player.SetScriptedLinkAnimationMode(null); Context.Player.Face(Vector2I.Down);
    }
    internal void Update()
    {
        if(_state==4)
        {
            if(_substate==0) { _substate=1; host.StartScript("goronDanceScript_givePrize"); }
            host.AdvanceScript(); return;
        }
        if(_state==1) { host.AdvanceScript(); return; }
        if(_state==2) Demonstrate(); else Play();
    }
    private void ResetRound()
    {
        foreach(int address in new[]{0xcfd3,0xcfd4,0xcfd5,0xcfd6,0xcfd7,0xcfd8,0xcfd9,0xcfdc}) Write(address,0);
    }
    private void Demonstrate()
    {
        if(_substate==0)
        {
            if(--_counter!=0) return;
            _substate=1; _counter=90; Context.Sound.PlaySound(0xcc);
            int remaining=Read(0xcfde);
            if(remaining!=0)
            {
                int selected=Context.Entities.NextRandomValue()%remaining;
                Write(0xcfde,remaining-1); Write(0xcfdf,Read(0xcee0+selected));
                for(int i=selected;i<remaining-1;i++) Write(0xcee0+i,Read(0xcee1+i));
            }
            ResetRound();
        }
        if(_substate==4)
        {
            if(--_counter==0) { _state=3; _substate=0; }
            return;
        }
        if(_substate==3)
        {
            _counter=(_counter-1)&255;
            if(!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x40))
            {
                host.Actor.SetScriptDrawOffset(new Vector2(0,_z>>8));
                if(_speedZ==0) { Write(0xcfd2,2); host.SetNativeAnimation(2); }
                return;
            }
            _substate=2; host.Actor.SetScriptDrawOffset(Vector2.Zero);
        }
        if(--_counter!=0) return;
        if(_substate==1) _substate=2; else Write(0xcfdc,Read(0xcfdc)+1);
        int move=NextMove();
        if((move&0x80)!=0)
        { _substate=4; _counter=60; host.SetNativeAnimation(2); return; }
        UpdateConsecutive(move);
        int animation=move==1?6:new[]{2,3,4,1,0,0x50}[Read(0xcfd8)];
        _counter=20;
        if(animation==0x50)
        {
            _substate=3; _z=0; _speedZ=-0x200;
            Context.Sound.PlaySound(0xcd);
        }
        else { host.SetNativeAnimation(animation); PlayMoveSound(move); }
    }
    private void Play()
    {
        if(_substate==0)
        {
            _substate=1; ResetRound(); Context.Sound.PlaySound(0xcc);
            Write(0xcfd2,2); Context.Player.SetScriptedLinkAnimationMode(null); Context.Player.Face(Vector2I.Down); return;
        }
        if(_substate==3)
        {
            if(--_counter!=0) return;
            if(Read(0xcfda)==8) EndDance(); else NextRound();
            return;
        }
        if(_substate==4)
        {
            if(!_failureScript)
            {
                if(--_counter!=0) return;
                Write(0xcfd9,1); Write(0xcfda,Read(0xcfda)+1); Write(0xcfdb,Read(0xcfdb)+1);
                if(Read(0xcfdb)!=3&&Read(0xcfda)==8) { EndDance(); return; }
                _failureScript=true; host.StartScript("goronDanceScript_failedRound");
            }
            host.AdvanceScript(); return;
        }
        int time=Read(0xcfd5)|(Read(0xcfd6)<<8);
        if(Read(0xcfd4)!=0)
        { time=Math.Min(65535,time+1); Write(0xcfd5,time&255); Write(0xcfd6,time>>8); }
        if(_substate==2) { if(Read(0xcfd3)==0) _substate=1; return; }
        int move=NextMove(), needed=Read(0xcfdc)*20;
        int input=(Input.IsActionJustPressed("attack")?1:0)|(Input.IsActionJustPressed("item")?2:0);
        if(move==0)
        {
            if(time<=needed) { if(input!=0) Fail(2); return; }
        }
        else
        {
            if(time>needed+8) { Fail(1); return; }
            if(input==0) return;
            Write(0xcfd4,input);
            if(input!=move) { Fail(2); return; }
            if(time<needed-8) { Fail(0); return; }
        }
        UpdateConsecutive(move);
        int presses=Read(0xcfd8);
        bool linkedPast=Context.Rooms.SaveData.IsLinkedGame&&(Context.Rooms.CurrentRoom.TilesetFlags&0x80)!=0;
        int animation=move==1?6:(linkedPast?new[]{2,3,1,3,0,0x50}:new[]{2,3,4,1,0,0x50})[presses];
        if(animation!=0x50) Write(0xcfd2,animation);
        if(move==1) Context.Player.SetScriptedLinkAnimationMode(8);
        else if(presses!=5)
        {
            int direction=new[]{2,3,1,4,3}[presses];
            Context.Player.SetScriptedLinkAnimationMode(direction==4?0x0e:null);
            if(direction!=4) Context.Player.Face(direction switch {0=>Vector2I.Up,1=>Vector2I.Right,3=>Vector2I.Left,_=>Vector2I.Down});
        }
        Write(0xcfdc,Read(0xcfdc)+1);
        int next=NextMove();
        if(presses==5&&move!=1)
        { if(host.SpawnDanceJump()) { Write(0xcfd3,1); _substate=2; } return; }
        PlayMoveSound(move);
        if((next&0x80)!=0)
        { Write(0xcfd9,0); Write(0xcfda,Read(0xcfda)+1); _substate=3; _counter=30; }
    }
    private void Fail(int reason)
    {
        Write(0xcfd1,reason); _substate=4; _failureScript=false; _counter=30;
        Context.Sound.PlaySound(0x5a); Context.Player.SetScriptedLinkAnimationMode(2);
        bool subrosians=Context.Rooms.SaveData.IsLinkedGame&&(Context.Rooms.CurrentRoom.TilesetFlags&0x80)!=0;
        Write(0xcfd2,subrosians?2:4);
    }
    private int NextMove()
    {
        string level=new[]{"platinum","gold","silver","bronze"}[Read(0xcfdd)];
        int value=host.Database.Bytes("dance-"+level)[Read(0xcfdf)*16+Read(0xcfdc)];
        Write(0xcfd7,value); return value;
    }
    private void UpdateConsecutive(int move) => Write(0xcfd8,move==2?Read(0xcfd8)+1:0);
    private void PlayMoveSound(int move)
    { if(move==1) Context.Sound.PlaySound(0xc8); else if(move==2) Context.Sound.PlaySound(0xcd); }
}
