namespace oracleofages;

/// <summary>
/// Explicit counterpart to bit 7 of the original interaction/part enabled
/// byte. Ordinary non-state-zero room objects are skipped while
/// wTextIsActive is nonzero; only source objects carrying that bit opt in.
/// </summary>
internal interface IUpdatesDuringDialogueRoomEntity
{
    bool UpdatesDuringDialogue { get; }
}
