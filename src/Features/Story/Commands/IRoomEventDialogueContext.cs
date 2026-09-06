namespace oracleofages;

/// <summary>Source screen state supplied by a native cutscene owner.</summary>
internal interface IRoomEventDialogueContext
{
    DialogueScreenContext? DialogueScreen { get; }
}
