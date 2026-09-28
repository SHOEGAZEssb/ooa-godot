using Godot;
using System;

namespace oracleofages;

// boyRunSubid01 and oldLady @runSubid1 run in that object order.
internal sealed class NayruStoneChildVignette : RoomCutsceneCommandHost
{
    private readonly NayruActorRegistry _actors;
    private readonly NayruIntroEventDatabase _database;
    private readonly Action<Vector2, int> _exclamation;
    private readonly CutsceneCommandRunner _boy;
    private readonly CutsceneCommandRunner _lady;
    private int _boyState;
    private int _ladyState;
    private int _ladyCounter;
    private bool _stonePalette;
    private bool _boyEnded;
    private bool _ladyEnded;
    private string _activeActor = "VignetteBoy";

    public NayruStoneChildVignette(RoomEventContext context, NayruActorRegistry actors,
        NayruIntroEventDatabase database, Action<Vector2, int> exclamation)
    {
        Context = context;
        _actors = actors;
        _database = database;
        _exclamation = exclamation;
        _boy = new CutsceneCommandRunner(this);
        _lady = new CutsceneCommandRunner(this);
        _boy.Start(database.VignetteBoyCommands);
        _lady.Start(database.VignetteLadyCommands);
        actors["VignetteBoy"].SetAnimationRate(0);
        actors["VignetteLady"].SetAnimationRate(0);
        context.Entities.RuntimeState.SetWramByte(0xcfd1, 0);
    }

    public override RoomEventContext Context { get; }
    public bool Complete { get; private set; }

    public void AdvanceFrame()
    {
        NpcCharacter boy = _actors["VignetteBoy"];
        _activeActor = "VignetteBoy";
        _boy.AdvanceFrame();
        int signal = Context.Entities.RuntimeState.ReadWramByte(0xcfd1);
        switch (_boyState)
        {
            case 0:
                if (signal == 1) _boyState = 1;
                else if (_boy.MovementCounter(new CutsceneActorId("VignetteBoy")) > 0)
                    boy.AdvanceAnimationUpdates(2);
                break;
            case 1:
                if (signal == 2)
                {
                    _boyState = 2;
                    _stonePalette = true;
                }
                else if ((Context.Entities.FrameCounter & 7) == 0)
                    _stonePalette = !_stonePalette;
                boy.SetScriptPaletteOverride(_stonePalette
                    ? _database.StoneSpritePalette : _database.BoySpritePalette);
                break;
            case 2:
                if (!_boyEnded) boy.AdvanceAnimationUpdates(1);
                break;
        }

        NpcCharacter lady = _actors["VignetteLady"];
        if (_ladyState == 0)
        {
            lady.AdvanceAnimationUpdates(1);
            _activeActor = "VignetteLady";
            _lady.AdvanceFrame();
            if (_ladyEnded)
            {
                _ladyCounter = 60;
                _ladyState = 1;
            }
            else if (_lady.MovementCounter(new CutsceneActorId("VignetteLady")) > 0)
                lady.AdvanceAnimationUpdates(2);
        }
        else if (!Complete)
        {
            if (--_ladyCounter == 0)
            {
                _ladyState++;
                if (_ladyState == 4) Complete = true;
                else _ladyCounter = _ladyState == 2 ? 20 : 60;
            }
            else if (_ladyState == 2)
                lady.AdvanceAnimationUpdates(3);
        }
    }

    public override bool HasActorBinding(CutsceneActorId actor) =>
        actor.Value is "VignetteBoy" or "VignetteLady";

    public override void SetActorMovementAnimation(string actor, int angle, string animation) =>
        _actors.SetAnimation(actor, angle / 8);

    public override void MoveActorAtSpeed(string actor, int speed, int angle)
    {
        NpcCharacter npc = _actors[actor];
        npc.Position = OracleObjectMovement.Shared.ApplySpeed(
            OracleObjectPosition.FromPixels(npc.Position), speed, angle).PrecisePosition;
    }

    public override bool MemoryEquals(string binding, int value) => binding == "VignetteSignal"
        ? Context.Entities.RuntimeState.ReadWramByte(0xcfd1) == value
        : throw UnsupportedCommand($"read '{binding}'");

    public override void WriteMemory(string binding, int value)
    {
        if (binding != "VignetteSignal") throw UnsupportedCommand($"write '{binding}'");
        Context.Entities.RuntimeState.SetWramByte(0xcfd1, (byte)value);
    }

    public override void RunNativeHandler(string handler)
    {
        if (handler != "BoyExclamation") throw UnsupportedCommand(handler);
        _exclamation(_actors["VignetteBoy"].Position + new Vector2(0, -13), 60);
    }

    public override void ScriptEnded()
    {
        if (_activeActor == "VignetteBoy") _boyEnded = true;
        else _ladyEnded = true;
    }
}
