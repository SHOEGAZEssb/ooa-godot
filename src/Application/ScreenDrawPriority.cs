namespace oracleofages;

// Screen-space layers are independent of the native world-object OAM queues.
internal static class ScreenDrawPriority
{
    internal const int StoryScreenZIndex = 14;
    internal const int DialogueBackdropZIndex = 48;
    internal const int NameEntryZIndex = 60;
    internal const int FrontendZIndex = 200;
}
