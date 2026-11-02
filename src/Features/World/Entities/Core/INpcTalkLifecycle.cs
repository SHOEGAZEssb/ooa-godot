
namespace Oracles;

internal interface INpcTalkLifecycle
{
    NpcCharacter TalkNpc { get; }
    void OnNpcTalkStarted();
    void OnNpcTalkEnded();
}
