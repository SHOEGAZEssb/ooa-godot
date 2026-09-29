namespace oracleofages;

internal interface IRoomEvent
{
    bool HasState { get; }
    bool BlocksGameplay { get; }
    // A native game-state/cutscene handler replaces cutscene01. Ordinary
    // interaction scripts can lock Link without replacing that dispatch.
    bool OwnsGameLogic => false;
    bool DisablesLink => false;
    bool ObjectUpdateSuspended => false;
    void ResumeObjectUpdate() { }
    void UpdateSpecialObjectFrame() { }
    bool FreezesNonInteractionObjects => false;
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
