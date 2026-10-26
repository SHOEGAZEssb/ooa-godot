using Godot;

namespace Oracles;

internal sealed class BlackTowerBlockerScriptHost : NpcInteractionCommandHost
{
    private readonly BlackTowerBlockingVillagerRoomEntity _slot;
    private readonly BlackTowerWorkerDatabase _data;
    private bool _ownsInput;

    internal BlackTowerBlockerScriptHost(BlackTowerBlockingVillagerRoomEntity slot,
        BlackTowerWorkerDatabase data, RoomSession rooms, RoomEntityManager entities, DialogueBox dialogue)
        : base("Blocker", rooms, entities, dialogue, data.BlockerTalk)
    {
        _slot = slot; _data = data;
        BindObject(slot.Npc);
    }

    internal int MovementCounter => ActorMovementCounter("Blocker");
    internal void Advance(Player player) => AdvanceObject(player);
    internal void StartMoving() => ReplaceObjectScript(_data.BlockerMove);
    internal void StartWatching() => ReplaceObjectScript(_data.BlockerTalk);
    protected override bool MatchesAndPrepare(NpcCharacter npc) => ReferenceEquals(npc, _slot.Npc);
    public override bool TryInteract(NpcInteractionTarget target, Player player) =>
        MatchesAndPrepare(target.Npc) && QueueObjectInteraction(target, player);
    public override void SetInputEnabled(bool enabled)
    {
        NativeScriptInputControl.SetEnabled(Entities.RuntimeState, ScriptPlayer, enabled);
        _ownsInput = !enabled;
        if (enabled) base.SetInputEnabled(true);
    }
    protected override void ResetHostState()
    {
        if (_ownsInput) SetInputEnabled(true);
    }
    public override int ReadMemory(string binding) => binding == "BlockerDirection" ? _slot.Direction :
        throw UnsupportedCommand($"read '{binding}'");
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override void ShowLoadedText() => ShowDialogue(ScriptActor.Message, choice: false);
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) =>
        RequireActor(actor).SetScriptAnimation(encodedAnimation);
    public override void SetActorMovementAnimation(string actor, int angle, string encodedAnimation)
    {
        NpcCharacter npc = RequireActor(actor);
        npc.SetNativeAngle(angle); // The facing helper reads this same Interaction.angle.
        npc.SetScriptAnimation(encodedAnimation);
    }
    public override void MoveActorAtSpeed(string actor, int speed, int angle)
    {
        _ = RequireActor(actor);
        _slot.Move(Entities.RuntimeState, speed, angle);
    }
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "turnToFaceLink":
                ScriptActor.SetFacingDirection(RoomEventResources.DirectionToward(ScriptActor.Position, ScriptPlayer.Position));
                break;
            case "villager_setLinkYToVar39":
                ScriptPlayer.SetScriptedCoordinateHigh(horizontal: false, _slot.SavedLinkY);
                break;
            default: throw UnsupportedCommand($"run native handler '{handler}'");
        }
    }
    public override void ScriptEnded() { } // Native substate1 owns completion/reload.
}
