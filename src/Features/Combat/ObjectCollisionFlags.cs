namespace oracleofages;

internal static class ObjectCollisionFlags
{
    // include/structs.s:ObjectStruct.collisionType: bit 7 enables collisions;
    // the lower seven bits select an item/enemy/part collision type.
    internal const int Enabled = 0x80;
    internal const int TypeMask = 0x7f;

    // constants/common/itemCollisionTypes.s: enemy/part var2a records the
    // colliding item type, with bit 7 set when that collision just occurred.
    internal const int JustHit = 0x80;
}
