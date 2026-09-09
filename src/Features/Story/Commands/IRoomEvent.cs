namespace oracleofages;

internal interface IRoomEvent
{
    bool HasState { get; }
    bool BlocksGameplay { get; }
    bool MenusDisabled => false;
    bool ScreenTransitionsDisabled => false;
    bool AllScreenTransitionsDisabled => false;
    // Composite sequences own their children's dispatch, including reduced
    // dialogue passes. Retained state alone must never silently lose updates.
    bool OwnsUpdatesOf(IRoomEvent other) => false;
    void ReleaseOutgoingActors(int group, OracleRoomData room) { }
    void UpdateFrame();
    void Cancel();
}
