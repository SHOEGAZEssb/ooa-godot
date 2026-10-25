using Godot;
using System;
using System.Globalization;

namespace Oracles;

// linkInCutscene.s: the moving portions of linkCutscene3 and linkCutscene4.
// Script admission is separate: these native counters continue during text.
internal sealed class NayruLinkCutsceneMotion
{
    private readonly RoomEventContext _context;
    private readonly object _owner;
    private readonly Reaction _reaction;
    private readonly Aftermath _aftermath;
    private int _kind, _substate, _counter;
    private bool _releasePending, _masksHeld;
    internal bool ReactionStarted { get; private set; }
    internal bool ReactionFinished { get; private set; }
    internal bool AftermathStarted { get; private set; }
    internal bool AftermathFinished { get; private set; }

    internal NayruLinkCutsceneMotion(RoomEventContext context, object owner,
        CutsceneNativeBlockingCommand reaction, CutsceneNativeBlockingCommand aftermath)
    {
        _context=context; _owner=owner;
        int[] r=Read(reaction,6), a=Read(aftermath,10);
        _reaction=new(r[0],r[1],r[2],r[3],r[4],r[5]);
        _aftermath=new(a[0],a[1],a[2],a[3],a[4],a[5],a[6],a[7],a[8],a[9]);
        if(aftermath.Actor?.Value!="Player" || aftermath.Frames!=a[0]+a[3]+a[4]+a[6] || r[2]==0 || r[3]==0 || r[5]==0 ||
            a[0]==0 || a[3]==0 || a[4]==0 || a[6]==0 || a[9]>3)
            throw new InvalidOperationException($"{aftermath.Source}: unsupported native Link counter/control definition.");
    }

    internal void StartReaction()
    {
        if(ReactionStarted) return;
        RequireControl(); ReactionStarted=true; _kind=3; _substate=5; _counter=_reaction.LeftCounter;
        SetMotion(_reaction.Speed,_reaction.LeftAngle);
        _context.Sound.PlaySound(SoundId.SndUnknown5);
    }

    internal void StartAftermath()
    {
        if(AftermathStarted) return;
        RequireControl(); AftermathStarted=true; _kind=4; _substate=2; _counter=_aftermath.InitialWait;
    }

    internal void AdvanceFrame()
    {
        if(_releasePending)
        {
            // setLinkIDOverride becomes the normal object on the next
            // special-object dispatch, after the terminal mask/phase writes.
            _context.Player.EndCutsceneControl(_owner); _releasePending=false;
        }
        if(_kind==3 && !ReactionFinished) AdvanceReaction();
        else if(_kind==4 && !AftermathFinished) AdvanceAftermath();
    }

    private void AdvanceReaction()
    {
        switch(_substate)
        {
            case 5:
                if(--_counter!=0) { Move(_reaction.Speed,_reaction.LeftAngle); return; }
                Stop(); _substate=6; _counter=_reaction.Wait; return;
            case 6:
                if(--_counter!=0) return;
                _counter=_reaction.DownCounter; _substate=7;
                SetMotion(_reaction.Speed,_reaction.DownAngle); return;
            case 7:
                if(--_counter!=0) { Move(_reaction.Speed,_reaction.DownAngle); return; }
                Stop(); _substate=8; ReactionFinished=true;
                _context.Sound.PlaySound(SoundId.SndUnknown5); return;
            default: throw new InvalidOperationException($"linkCutscene3: unsupported native substate${_substate:x2}.");
        }
    }

    private void AdvanceAftermath()
    {
        switch(_substate)
        {
            case 2:
                if(--_counter!=0) return;
                _counter=_aftermath.DownCounter; _substate=3;
                SetMotion(_aftermath.Speed,_aftermath.DownAngle); return;
            case 3:
                if(--_counter!=0) { Move(_aftermath.Speed,_aftermath.DownAngle); return; }
                Stop(); _counter=_aftermath.MiddleWait; _substate=4; return;
            case 4:
                if(--_counter!=0) return;
                _counter=_aftermath.LeftCounter; _substate=5;
                SetMotion(_aftermath.Speed,_aftermath.LeftAngle); return;
            case 5:
                if(--_counter!=0) { Move(_aftermath.Speed,_aftermath.LeftAngle); return; }
                Stop(); AftermathFinished=true; _releasePending=true;
                // setLinkIDOverride clears state/substate immediately while
                // the ID replacement waits for the next dispatch.
                _substate=0;
                var memory=_context.Entities.RuntimeState;
                memory.SetWramByte(0xcfd0,(byte)_aftermath.Phase);
                memory.SetWramByte(AgesWramAddress.wMenuDisabled,(byte)_aftermath.Mask);
                memory.SetWramByte(AgesWramAddress.wDisabledObjects,(byte)_aftermath.Mask);
                _masksHeld=true;
                _context.Player.Face(_aftermath.Direction switch
                { 0=>Vector2I.Up, 1=>Vector2I.Right, 2=>Vector2I.Down, _=>Vector2I.Left });
                return;
            default: throw new InvalidOperationException($"linkCutscene4: unsupported native substate${_substate:x2}.");
        }
    }

    private void SetMotion(int speed,int angle)
    {
        _context.Player.SetNativeCutsceneSpeed(speed);
        _context.Player.SetNativeCutsceneAngle(angle);
    }
    private void Move(int speed,int angle) => _context.Player.AdvanceNativeCutsceneMovement(speed,angle);
    private void Stop() => _context.Player.AdvanceCutsceneMovement(Vector2.Zero,Vector2I.Zero);
    private void RequireControl()
    {
        if(!_context.Player.IsCutsceneControlOwner(_owner))
            throw new InvalidOperationException("linkInCutscene.s: native Nayru Link motion requires its existing control owner.");
    }

    internal void Clear()
    {
        if(_masksHeld && (!_context.Player.CutsceneControlled || _context.Player.IsCutsceneControlOwner(_owner)))
        {
            var memory=_context.Entities.RuntimeState;
            if(memory.ReadWramByte(AgesWramAddress.wMenuDisabled)==_aftermath.Mask) memory.SetWramByte(AgesWramAddress.wMenuDisabled,0);
            if(memory.ReadWramByte(AgesWramAddress.wDisabledObjects)==_aftermath.Mask) memory.SetWramByte(AgesWramAddress.wDisabledObjects,0);
        }
        _masksHeld=_releasePending=false; _kind=_substate=_counter=0;
        ReactionStarted=ReactionFinished=AftermathStarted=AftermathFinished=false;
    }

    private static int[] Read(CutsceneNativeBlockingCommand command,int count)
    {
        string[] fields=command.Payload.Split(','); int[] values=new int[count];
        if(fields.Length!=count) throw new InvalidOperationException($"{command.Source}: unsupported native Link motion payload.");
        for(int i=0;i<count;i++)
            if(!int.TryParse(fields[i],NumberStyles.None,CultureInfo.InvariantCulture,out values[i]) || values[i] is <0 or >0xff)
                throw new InvalidOperationException($"{command.Source}: invalid native Link byte '{fields[i]}'.");
        return values;
    }

    private readonly record struct Reaction(int Speed,int LeftAngle,int LeftCounter,int Wait,int DownAngle,int DownCounter);
    private readonly record struct Aftermath(int InitialWait,int Speed,int DownAngle,int DownCounter,int MiddleWait,int LeftAngle,int LeftCounter,int Phase,int Mask,int Direction);
}
