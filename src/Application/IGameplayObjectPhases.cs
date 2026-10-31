namespace Oracles;

// These are native caller/transition hooks around the shared object walk,
// including an interaction which yields and resumes within that same walk.
internal interface IGameplayObjectPhases
{
    bool PrepareObjectUpdate(double delta) => true;
    int? SampleSimulatedInput() => null;
    bool CompanionUpdateAllowed => true;
    void UpdateLink();
    void AfterLinkUpdate() { }
    bool PrepareEntityUpdate(double delta) => true;
    bool UpdateRoomInteractions(double delta, bool resuming) => true;
    void BeforeObjectTail(double delta) { }
    bool TileGraphicsUpdateAllowed => true;
    void AfterObjectTail(double delta);
}
