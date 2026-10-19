namespace oracleofages;

internal interface IRoomEvent
{
    bool HasState { get; }
    bool BlocksGameplay { get; }
    // A native game-state/cutscene handler replaces cutscene01. Ordinary
    // interaction scripts can lock Link without replacing that dispatch.
    bool OwnsGameLogic => false;
    bool SkipsObjectUpdate => false;
    bool DisablesLink => false;
    bool ObjectUpdateSuspended => false;
    void ResumeObjectUpdate() { }
    void UpdateSpecialObjectFrame() { }
    bool UpdatesInObjectPhases => false;
    void UpdatePartFrame() { }
    bool FreezesNonInteractionObjects => false;
    bool MenusDisabled => false;
    // Some native cutscene callers omit updateStatusBar while still running
    // objects. Hidden presentation alone does not establish that eligibility.
    bool SuppressesStatusBar => false;
    bool SuppressesNormalMenuDispatch => false;
    bool ScreenTransitionsDisabled => false;
    bool AllScreenTransitionsDisabled => false;
    // Composite sequences own their children's dispatch, including reduced
    // dialogue passes. Retained state alone must never silently lose updates.
    bool OwnsUpdatesOf(IRoomEvent other) => false;
    void ReleaseOutgoingActors(int group, OracleRoomData room) { }
    void UpdateFrame();
    void Cancel();
}
