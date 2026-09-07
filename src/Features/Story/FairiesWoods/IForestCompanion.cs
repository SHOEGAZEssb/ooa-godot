namespace oracleofages;

internal interface IForestCompanion : IPlayerInteractable
{
    bool ForestButtonPressed { get; }
    bool LinkRiding { get; }
    void UseForestInteraction(bool outside);
    void NoticeForestLink(int animation);
    void ForceForestMount();
}
