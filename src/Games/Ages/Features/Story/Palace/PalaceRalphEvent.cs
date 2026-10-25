using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

// ralph.s:$37:$09 and scripts.s:ralphSubid09Script. The palace entry owner
// starts this lane after resolving the placed actors' independent story gates.
internal sealed class PalaceRalphEvent(RoomEventContext context) :
    InteractiveCutsceneCommandHost, IRoomEvent
{
    private readonly IReadOnlyList<CutsceneCommand> _commands = CutsceneCommandCatalog.Load(
        AgesGameContent.AssetRoot + "cutscenes/palace_ralph_commands.tsv");
    private readonly Dictionary<string, int> _constants = LoadConstants();
    private readonly string[] _animations = GeneratedTable.Load(
        AgesGameContent.AssetRoot + "cutscenes/palace_ralph_animations.tsv",
        new GeneratedTableSchema("palace Ralph animations", GeneratedTableKeySemantics.Unique,
            ["index", "animation"], ["index"], headerRequired: true))
        .Rows.Select(row => row.RequiredString(1)).ToArray();
    private CutsceneCommandRunner? _runner;
    private NpcCharacter? _ralph;
    private Vector2 _precise;

    public override RoomEventContext Context => context;
    public bool HasState => _runner?.Active == true;
    public bool BlocksGameplay => InputControlHeld;
    public bool MenusDisabled => InputControlHeld;
    internal int CompletionFlag => _constants["completion-flag"];

    internal void Start(NpcCharacter ralph)
    {
        Cancel();
        _ralph = ralph;
        _precise = ralph.Position;
        ralph.SetActive(true);
        ralph.SetBlocksLink(false);
        ralph.SetAnimationRate(0);
        ralph.SetScriptAnimation(_commands.OfType<CutsceneMoveCommand>().Single().EncodedAnimation);
        ralph.SetScriptVisible(true);
        _runner = new(this);
        _runner.Start(_commands);
        _runner.SetInitialMotionRegisters("Ralph", _constants["speed"], ObjectAngle.Up);
        SetInputEnabled(false);
        context.Player.RequestState08Control(this);
        Vector2I direction = context.Transitions.ScreenEntryDirection;
        context.Player.Face(direction == Vector2I.Zero ? Vector2I.Up : direction);
        context.Sound.PlayMusicIfChanged(_constants["music"]);
        // Source state 0 installs the script without executing it.
    }

    public void UpdateFrame()
    {
        if (!HasState || _ralph is not { Active: true } ralph ||
            !context.Entities.InitializedInteractionUpdatesAllowed) return;
        _runner!.AdvanceFrame();
        if (ralph.Active)
            ralph.AdvanceAnimationUpdates(_runner.MovementCounter("Ralph") == 0 ? 1 : 3);
    }

    public bool TryInteractNpc(NpcCharacter npc) => false;
    public void Cancel()
    {
        ReleaseInputControl();
        if (_ralph is not null && GodotObject.IsInstanceValid(_ralph))
            _ralph.SetActive(false);
        _ralph = null;
        _runner?.Clear();
    }

    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Ralph";
    public override void ShowText(int textId, string message, int? textboxPosition) =>
        context.ShowDialogue(message, textboxPosition);
    public override void SetActorMovementAnimation(string actor, int angle, string encodedAnimation) =>
        RequireRalph(actor).SetScriptAnimation(encodedAnimation);
    public override void MoveActorAtSpeed(string actor, int speed, int angle) =>
        RequireRalph(actor).SetStatePosition(NativeObjectMovement.ApplySpeed(context.Entities.RuntimeState, ref _precise, speed, angle));
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "FaceLinkAndExclaim":
                NpcCharacter ralph = RequireRalph("Ralph");
                int angle = OracleObjectMovement.Shared.RelativeAngle(ralph.Position, context.Player.Position);
                ralph.SetScriptAnimation(_animations[((angle + 4) & 0x18) >> 3]);
                if (context.Entities.InteractionSlotAvailable)
                    context.Entities.Spawn<NpcCharacter>(new ExclamationMarkSpawn(
                        ralph.Position + new Vector2(_constants["effect-x"], _constants["effect-y"]), true));
                return;
            case "ResetRoomMusic":
                context.Sound.PlayRoomMusic(context.Rooms.ActiveGroup, context.Rooms.CurrentRoom.Id);
                return;
            default: throw UnsupportedCommand($"run palace Ralph native '{handler}'");
        }
    }
    public override void ScriptEnded()
    {
        SetInputEnabled(true);
        RequireRalph("Ralph").SetActive(false);
    }
    private NpcCharacter RequireRalph(string actor) => actor == "Ralph" && _ralph is not null
        ? _ralph : throw UnsupportedCommand($"resolve actor '{actor}'");
    private static Dictionary<string, int> LoadConstants()
    {
        var constants = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "cutscenes/palace_ralph_constants.tsv",
            new GeneratedTableSchema("palace Ralph native", GeneratedTableKeySemantics.Unique,
                ["key", "value", "source"], ["key"], headerRequired: true)).Rows)
        {
            constants.Add(row.RequiredString(0), row.Decimal(1));
            _ = row.RequiredString(2);
        }
        if (constants.Count != 5 || constants["completion-flag"] != 0x32 || constants["speed"] != 0x50)
            throw new InvalidOperationException("ralph.s:$37:$09 native contract is incomplete.");
        return constants;
    }
}
