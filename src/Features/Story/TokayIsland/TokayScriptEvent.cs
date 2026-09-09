namespace oracleofages;

/// <summary>
/// Shared services for the Tokay interaction scripts and Rosa's shovel script.
/// Native actor tails, waits, menu masks, rewards, and cleanup stay with the
/// concrete script owner.
/// </summary>
internal abstract class TokayScriptEvent(
    RoomEventContext context, TokayInteractionDatabase interactions) : RoomEventHost
{
    protected RoomEventContext Context { get; } = context;
    protected TokayInteractionDatabase Interactions { get; } = interactions;
    protected override RoomEventContext EventContext => Context;
    public bool BlocksGameplay => InputLocked;

    protected void Show(int textId) => Context.ShowDialogue(Interactions.Text(textId));
    protected void ShowChoice(int textId) => Context.ShowChoiceDialogue(Interactions.Text(textId));

    protected bool CurrentRoomFlag(byte flag) =>
        Context.Rooms.SaveData.HasRoomFlag(
            Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, flag);

    protected void SetCurrentRoomFlag(byte flag) =>
        Context.Rooms.SaveData.SetRoomFlag(
            Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, flag);

}
