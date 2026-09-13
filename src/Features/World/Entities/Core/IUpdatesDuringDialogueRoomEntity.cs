namespace oracleofages;

/// <summary>
/// Objects eligible for the native text-time dispatcher: interactions whose
/// state remains zero, or interaction/part objects with enabled bit 7 set.
/// Ordinary non-state-zero objects are skipped while wTextIsActive is nonzero.
/// </summary>
internal interface IUpdatesDuringDialogueRoomEntity
{
    bool UpdatesDuringDialogue => true;
}
