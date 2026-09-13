namespace oracleofages;

/// <summary>The two writes performed by ecom_killObjectH on a PART page.
/// The part's next dispatch decides what zero health means.</summary>
internal interface INativePartHealthRoomEntity
{
    void ClearHealthAndCollision();
}
