
namespace Oracles;

internal sealed record GameplayMenuBindings(
    Func<bool> CanOpen, Func<bool> CanOpenDebug,
    Action<int,int> Travel, Func<SaveResult> Save, Action Quit, Action Restart, Action CompanionChanged,
    Func<bool> CanOpenObjectSpawner);
