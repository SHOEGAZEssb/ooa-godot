using System;

namespace Oracles;

internal sealed class FaroreMemoryScriptHost : NpcInteractionCommandHost
{
    private readonly NpcCharacter _actor;
    private readonly Func<bool> _openList;
    private bool _ownsMask;
    internal FaroreMemoryScriptHost(NpcCharacter actor, RoomSession rooms, RoomEntityManager entities,
        DialogueBox dialogue, Func<bool> openList)
        : base("Book", rooms, entities, dialogue, new FaroreMemoryDatabase().Script)
    { _actor = actor; _openList = openList; BindObject(actor); }
    internal void Advance(Player player) => AdvanceObject(player);
    protected override bool MatchesAndPrepare(NpcCharacter npc) => ReferenceEquals(npc, _actor);
    public override bool TryInteract(NpcInteractionTarget target, Player player) =>
        MatchesAndPrepare(target.Npc) && QueueObjectInteraction(target, player);
    public override void SetDisabledObjects(int value)
    {
        if (value != 0x91) throw UnsupportedCommand($"book disabled mask ${value:x2}");
        Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x91); _ownsMask = true;
    }
    public override void SetInputEnabled(bool enabled)
    {
        NativeScriptInputControl.SetEnabled(Entities.RuntimeState, ScriptPlayer, enabled);
        if (enabled) { _ownsMask = false; base.SetInputEnabled(true); }
    }
    public override void ShowText(int id, string text) => ShowText(id, text, null);
    public override void ShowText(int id, string text, int? position) =>
        Dialogue.ShowGameplayChoiceMessage(text, Entities.WorldToScreen(ScriptPlayer.Position).Y, textPosition: position);
    public override void RunNativeHandler(string handler)
    {
        if (handler != "openMenu, $0a") throw UnsupportedCommand(handler);
        if (!_openList()) throw UnsupportedCommand("acquire MENU_SECRET_LIST $0a");
    }
    protected override void ResetHostState()
    {
        if (_ownsMask) NativeScriptInputControl.SetEnabled(Entities.RuntimeState, ScriptPlayer, true);
        _ownsMask = false;
    }
}
