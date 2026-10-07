namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBombCollisionDataRom()
    {
        var native = new ObjectCollisionRom(); var data = BombCollisionDatabase.Shared;
        for (int mode = 0; mode < 0x7d; mode++)
            FailIf(data.Effect(mode) != native.Table(0x6d0a + mode * 32 + 0x18),
                $"ITEMCOLLISION_BOMB mode ${mode:x2} must retain its clean-US effect.");
        foreach (bool part in new[] { false, true })
        for (int type = 0; type < (part ? 0x5a : 128); type++)
        {
            native.ClearObjects(); int target = part ? 0xd1c0 : 0xd080;
            native[target] = 1; native[target + 0xb] = 64; native[target + 0xd] = 80;
            native[target + 0x24] = (byte)(0x80 | type); native[target + 0x26] = native[target + 0x27] = 5;
            native[target + 0x29] = 0x40; native[target + 0x3e] = 1;
            native[0xd700] = 1; native[0xd701] = 0x0d; native[0xd704] = 0xff;
            native[0xd70b] = 64; native[0xd70d] = 80;
            native[0xd724] = 0x98; native[0xd726] = native[0xd727] = 0x18; native[0xd728] = 0xfc;
            native.Call(ObjectCollisionRom.Scan);
            FailIf(native.Dispatches.Count != ((part ? data.PartEnabled(type) : data.EnemyEnabled(type)) ? 1 : 0),
                $"ITEMCOLLISION_BOMB {(part ? "PART" : "ENEMY")} ${type:x2} native active mask differs.");
        }
    }
}
