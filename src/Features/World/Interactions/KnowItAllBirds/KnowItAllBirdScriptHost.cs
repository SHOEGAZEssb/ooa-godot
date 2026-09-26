using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>One knowItAllBirdScript lane, advanced by its native object slot.</summary>
internal sealed class KnowItAllBirdScriptHost : CutsceneCommandHost
{
    private readonly KnowItAllBirdCharacter _bird;
    private readonly IReadOnlyList<CutsceneCommand> _commands;
    private readonly Func<bool> _textActive;
    private readonly CutsceneCommandRunner _runner;
    private DialogueBox? _dialogue;
    private Func<Vector2, Vector2>? _worldToScreen;
    private Player? _player;
    private NpcInteractionTarget? _target;
    private bool _pressed;

    internal KnowItAllBirdScriptHost(KnowItAllBirdCharacter bird,
        IReadOnlyList<CutsceneCommand> commands, Func<bool> textActive)
    {
        _bird = bird; _commands = commands; _textActive = textActive;
        _runner = new(this);
    }

    internal bool ObjectsDisabled { get; private set; }
    internal int CommandIndex => _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal int Counter => _runner.Counter;
    public override bool DialogueOpen => _textActive();
    public override bool ScriptExecutionBlocked => DialogueOpen || _player?.IsDying == true;
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Bird";

    internal void Start() => _runner.Start(_commands);
    internal void Advance(Player player) { _player = player; _runner.AdvanceFrame(); }

    internal bool TryInteract(NpcInteractionTarget target, Player player,
        DialogueBox dialogue, Func<Vector2, Vector2> worldToScreen)
    {
        if (ObjectsDisabled || _pressed || !_bird.ScriptButtonSensitive) return false;
        _target = target; _player = player; _dialogue = dialogue; _worldToScreen = worldToScreen;
        _pressed = true;
        target.Begin();
        return true;
    }

    internal void Cancel()
    {
        ObjectsDisabled = false;
        _pressed = false;
        _bird.TalkingSignal = false;
        _target?.Cancel();
        _target = null;
        _dialogue = null;
        _worldToScreen = null;
        _player = null;
        _runner.Clear();
    }

    public override void SetActorCollisionRadii(string actor, int radiusY, int radiusX)
    {
        RequireBird(actor);
        _bird.SetCollisionRadii(radiusY, radiusX);
        _bird.SetBlocksLink(true);
    }
    public override void SetActorButtonSensitive(string actor)
    {
        RequireBird(actor);
        _bird.SetScriptButtonSensitive(true);
    }
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        RequireBird(actor.Value);
        bool pressed = _pressed;
        _pressed = false;
        if (pressed) _player!.ApplyObjectInteractionGrace();
        return pressed;
    }
    public override void SetDisabledObjects(int value)
    {
        if (value is not (0 or 0x91)) throw UnsupportedCommand($"wDisabledObjects=${value:x2}");
        ObjectsDisabled = value != 0;
        if (!ObjectsDisabled) _target?.End();
    }
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "bird_compareLinkX":
                _bird.Direction = (int)_bird.Position.X < (int)_player!.Position.X ? 1 : 0;
                break;
            case "bird_addText0a": _bird.SelectText(tutorial: true); break;
            case "bird_subtractText0a": _bird.SelectText(tutorial: false); break;
            default: throw UnsupportedCommand(handler);
        }
    }
    public override void WriteObjectByte(string actor, int address, int value)
    {
        RequireBird(actor);
        if (address != 0x37 || value is not (0 or 1))
            throw UnsupportedCommand($"write ${address:x2}=${value:x2}");
        _bird.TalkingSignal = value != 0;
    }
    public override void ShowLoadedText()
    {
        if (_dialogue is null || _worldToScreen is null || _player is null)
            throw UnsupportedCommand("show bird text without an A-button binding");
        float linkY = _worldToScreen(_player.Position).Y;
        if (_bird.TextId < 0x320a)
            _dialogue.ShowGameplayChoiceMessage(_bird.Message, linkY);
        else
            _dialogue.ShowGameplayMessage(_bird.Message, linkY);
    }
    public override bool TextOptionEquals(int value)
    {
        if (_dialogue is null || !_dialogue.TryTakeChoiceResult(out int choice))
            throw UnsupportedCommand("compare bird text option without a result");
        return choice == value;
    }
    private void RequireBird(string actor)
    {
        if (actor != "Bird") throw UnsupportedCommand($"bind {actor}");
    }
}
