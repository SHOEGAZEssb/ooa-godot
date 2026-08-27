namespace oracleofages;

/// <summary>
/// Shared decPushingAgainstTileCounter arithmetic for handlers which invoke
/// it and then decrement wPushingAgainstTileCounter once more.
/// </summary>
internal static class PushingAgainstTileCounter
{
    internal static bool DecrementTwiceToZero(ref int counter)
    {
        counter = unchecked((byte)(counter - 1));
        if (counter == 0)
            return true;
        counter = unchecked((byte)(counter - 1));
        return counter == 0;
    }
}
