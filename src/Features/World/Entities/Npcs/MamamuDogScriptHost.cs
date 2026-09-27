using Godot;
using System;

namespace oracleofages;

// dog_subid00 and dogInMamamusHouseScript. The false wcddb branch yields
// before movement; the ROM scriptjump continues through the next decrement.
internal sealed class MamamuDogScriptHost : CutsceneCommandHost
{
    private readonly NpcCharacter _actor;
    private readonly Func<byte> _random;
    private readonly Func<bool> _dialogueOpen;
    private readonly CutsceneCommandRunner _runner;
    private readonly int[] _counters;
    private readonly string[] _animations;
    // scriptHelper.s:mamamuDog_checkReverseDirection / mamamuDog_hop.
    private const int LeftBoundary = 0x18;
    private const int RunWidth = 0x70;
    private const int HopSpeedZ = -0xc0;
    private const int Gravity = 0x20;
    private int _counter;
    private int _angle = 0x18;
    private int _animation = 2;
    private int _z;
    private int _speedZ;
    private bool _dying;
    internal int MovementCounter => _counter;
    internal int RestCounter => _runner.Counter;
    internal int ZFixed => _z;
    internal int Animation => _animation;
    public override bool DialogueOpen => _dialogueOpen();
    public override bool ScriptExecutionBlocked => _dying || DialogueOpen;

    internal MamamuDogScriptHost(NpcCharacter actor, Func<byte> random, Func<bool> dialogueOpen)
    {
        _actor = actor; _random = random; _dialogueOpen = dialogueOpen;
        var database = new MamamuDogDatabase();
        _counters = database.Counters;
        _animations = database.Animations;
        actor.SetScriptAnimation(_animations[_animation]);
        actor.SetAnimationRate(0);
        actor.SetBlocksLink(false); // objectMarkSolidPosition reserves time-warp landing only.
        actor.SetDialogue(0, string.Empty, canFace: false);
        _runner = new(this);
        _runner.Start(CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/mamamu_dog_commands.tsv"));
    }
    internal void Update(Player player)
    {
        if (!_actor.Active) return;
        _dying = player.IsDying;
        _runner.AdvanceFrame();
        _actor.SetScriptDrawOffset(new Vector2(0, _z >> 8));
        _actor.AnimateAndUpdateDrawPriorityOneUpdate(player);
    }
    public override bool MemoryEquals(string binding, int value) => binding == "Counter"
        ? _counter == value : throw UnsupportedCommand($"indoor dog memory '{binding}'");
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "setCounterRandomly": _counter = _counters[_random() & 7]; _speedZ = HopSpeedZ; _z = 0; break;
            case "decCounter": _counter = (_counter - 1) & 0xff; break;
            case "updateSpeedZ":
                if (OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, Gravity)) _speedZ = HopSpeedZ;
                break;
            case "checkReverseDirection":
                _actor.Position += new Vector2(_angle == 0x18 ? -1 : 1, 0);
                if ((((int)_actor.Position.X - LeftBoundary) & 0xff) >= RunWidth)
                {
                    _angle ^= 0x10;
                    SetAnimation(_animation ^ 1);
                }
                break;
            case "setZPositionTo0": _z = 0; break;
            case "reverseDirection": SetAnimation(_animation ^ 2); break;
            default: throw UnsupportedCommand($"indoor dog helper '{handler}'");
        }
    }
    private void SetAnimation(int animation)
    {
        _animation = animation;
        _actor.SetScriptAnimation(_animations[animation]);
    }
}
