
namespace Oracles;

// Native cutscene callers select reduced updates and their transition phases.
// The session owns the common clock, menus, object dispatch, status and text.
internal interface IGameplayForegroundPhases
{
    bool MainThreadRunning => true;
    bool ObjectUpdateSuspended => false;
    GameplayForegroundMode BeginFrame(double delta);
    bool AdvanceAdditionalMenus(double delta) => false;
    bool PrepareElectricShock(double delta);
    GameplayMenuDispatch PrepareMenus(double delta);
    void BeforeMenuUpdate() { }
    GameplayObjectUpdateMode BeforeObjects(double delta) => GameplayObjectUpdateMode.Full;
    bool StatusBarUpdatesAllowed => true;
    void AfterStatusBar(double delta);
    bool CutsceneRequestsAllowed => false;
    void SelectRequestedCutscene(int index) => throw new System.NotSupportedException(
        $"bank1.setCutsceneIndexIfCutsceneTriggerSet: unbound cutscene ${index:x2}.");
    void AfterCutsceneRequests(double delta) { }
    bool ObjectCollisionsAllowed { get; }
    void AfterObjectCollisions(double delta) { }
    bool RoomInitializationMayComplete => false;
    DialogueBox? FrameDialogue { get; }
    bool PresentationAllowed => true;
    void ApplyPresentation(bool hudBottom);
    void CompleteFrame() { }
}

internal enum GameplayForegroundMode { Normal, ObjectsOnly, Handled }
internal enum GameplayObjectUpdateMode { Full, ForegroundOnly, Handled }

internal readonly record struct GameplayMenuDispatch(bool Inventory, bool Map, bool LowHealthWarning);
