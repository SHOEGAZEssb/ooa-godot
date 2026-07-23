using System;

namespace oracleofages;

/// <summary>
/// Central translation of every wActiveRing consumer. Systems call these
/// policies instead of duplicating numeric ring IDs or modifier arithmetic.
/// Damage values use the original signed-byte unit: two raw points equal one
/// quarter-heart.
/// </summary>
internal static class RingEffects
{
    internal static bool Active(InventoryState inventory, RingId ring) =>
        inventory.ActiveRing == (int)ring;

    internal static int BaseSwordDamage(int swordLevel) => swordLevel switch
    {
        <= 1 => 2,
        2 => 3,
        _ => 5
    };

    internal static int SwordDamage(
        InventoryState inventory, int swordLevel, byte whimsicalRoll)
    {
        int damage = BaseSwordDamage(swordLevel);
        if (Active(inventory, RingId.Whimsical))
            return whimsicalRoll == 0 ? 12 : 1;
        if (Active(inventory, RingId.DoubleEdged) &&
            inventory.HealthQuarters >= 5)
            return damage + 8;
        return inventory.ActiveRing switch
        {
            (int)RingId.PowerL1 => damage + 1,
            (int)RingId.PowerL2 => damage + 2,
            (int)RingId.PowerL3 => damage + 3,
            (int)RingId.ArmorL1 or
            (int)RingId.ArmorL2 or
            (int)RingId.ArmorL3 => Math.Max(1, damage - 1),
            (int)RingId.Red => damage * 2,
            (int)RingId.Green => damage + damage / 2,
            (int)RingId.Cursed => Math.Max(1, damage / 2),
            _ => damage
        };
    }

    internal static int IncomingDamageQuarters(
        InventoryState inventory, int quarters, RingDamageSource source)
    {
        if (quarters <= 0 || PreventsDamage(inventory, source))
            return 0;

        int raw = -2 * quarters;
        if (HalvesSourceDamage(inventory, source))
            raw >>= 1;
        if (source is RingDamageSource.Hole or RingDamageSource.TerrainHazard)
        {
            if (Active(inventory, RingId.Protection))
                raw = -8;
            return Math.Max(1, (-raw + 1) / 2);
        }
        raw = inventory.ActiveRing switch
        {
            (int)RingId.PowerL1 => raw - 2,
            (int)RingId.PowerL2 => raw - 4,
            (int)RingId.PowerL3 => raw - 8,
            (int)RingId.ArmorL1 => Math.Min(-1, raw + 1),
            (int)RingId.ArmorL2 => Math.Min(-1, raw + 2),
            (int)RingId.ArmorL3 => Math.Min(-1, raw + 3),
            (int)RingId.Blue => raw >> 1,
            (int)RingId.Green => -((-raw * 3) >> 2),
            (int)RingId.Cursed => raw * 2,
            (int)RingId.Protection => -8,
            _ => raw
        };
        return Math.Max(1, (-raw + 1) / 2);
    }

    internal static bool PreventsDamage(
        InventoryState inventory, RingDamageSource source) => source switch
    {
        RingDamageSource.OctorokProjectile => Active(inventory, RingId.RedHoly),
        RingDamageSource.ZoraFire => Active(inventory, RingId.BlueHoly),
        RingDamageSource.Electric => Active(inventory, RingId.GreenHoly),
        RingDamageSource.OwnBomb => Active(inventory, RingId.Bombproof),
        _ => false
    };

    internal static bool HalvesSourceDamage(
        InventoryState inventory, RingDamageSource source) => source switch
    {
        RingDamageSource.BladeTrap => Active(inventory, RingId.GreenLuck),
        RingDamageSource.Beam => Active(inventory, RingId.BlueLuck),
        RingDamageSource.Hole => Active(inventory, RingId.GoldLuck),
        RingDamageSource.Spike => Active(inventory, RingId.RedLuck),
        _ => false
    };

    internal static int KnockbackFrames(InventoryState inventory, int frames) =>
        Active(inventory, RingId.Steadfast) ? frames >> 1 : frames;

    internal static int SwordChargeStep(InventoryState inventory) =>
        Active(inventory, RingId.Charge) ? 4 : 1;

    internal static int SwordSpinCounter(InventoryState inventory) =>
        Active(inventory, RingId.Spin) ? 9 : 5;

    internal static int SwordSpinFrames(InventoryState inventory, int ordinaryFrames) =>
        ordinaryFrames * SwordSpinCounter(inventory) / 5;

    internal static (int DistanceFixed, int HealQuarters) HeartRefill(
        InventoryState inventory) => inventory.ActiveRing switch
    {
        (int)RingId.HeartL1 => (2 << 16, 0x08),
        (int)RingId.HeartL2 => (3 << 16, 0x10),
        _ => (0, 0)
    };

    internal static bool EnergyBeamOnCharge(InventoryState inventory) =>
        Active(inventory, RingId.Energy);

    internal static int SwordBeamMaximumMissingQuarters(InventoryState inventory) =>
        inventory.ActiveRing switch
        {
            (int)RingId.LightL1 => 8,
            (int)RingId.LightL2 => 12,
            _ => 0
        };

    internal static int BombDamage(int baseDamage, InventoryState inventory) =>
        Active(inventory, RingId.Blast) ? baseDamage + 2 : baseDamage;

    internal static int BoomerangDamage(int baseDamage, InventoryState inventory) =>
        inventory.ActiveRing switch
        {
            (int)RingId.RangL1 => baseDamage + 1,
            (int)RingId.RangL2 => baseDamage + 2,
            _ => baseDamage
        };

    internal static int BombsPlacedPerUse(InventoryState inventory) =>
        Active(inventory, RingId.Bombers) ? 2 : 1;

    internal static bool BombsExplode(InventoryState inventory) =>
        !Active(inventory, RingId.Peace);

    internal static int MapleKillThreshold(InventoryState inventory) =>
        Active(inventory, RingId.Maples) ? 15 : 30;

    internal static int PegasusSeedTimerDecrement(InventoryState inventory) =>
        Active(inventory, RingId.Pegasus) ? 1 : 2;

    internal static bool UsesStrongThrow(InventoryState inventory) =>
        Active(inventory, RingId.Toss);

    internal static bool UsesFastSwim(InventoryState inventory) =>
        Active(inventory, RingId.Swimmers);

    internal static bool IgnoresIce(InventoryState inventory) =>
        Active(inventory, RingId.Snowshoe);

    internal static bool ProtectsCrackedFloor(InventoryState inventory) =>
        Active(inventory, RingId.Rocs);

    internal static bool IgnoresQuicksand(InventoryState inventory) =>
        Active(inventory, RingId.Quicksand);

    internal static int DropMultiplier(
        InventoryState inventory, RingDropKind kind)
    {
        if (Active(inventory, RingId.GoldJoy))
            return 2;
        return kind switch
        {
            RingDropKind.Heart when Active(inventory, RingId.BlueJoy) => 2,
            RingDropKind.Rupee when Active(inventory, RingId.RedJoy) => 2,
            RingDropKind.Ore when Active(inventory, RingId.GreenJoy) => 2,
            _ => 1
        };
    }

    internal static bool DetectsSoftSoil(InventoryState inventory) =>
        Active(inventory, RingId.Discovery);

    internal static int LinkTransformation(InventoryState inventory) =>
        inventory.ActiveRing switch
        {
            (int)RingId.Octo => 5,
            (int)RingId.Moblin => 6,
            (int)RingId.LikeLike => 7,
            (int)RingId.Subrosian => 3,
            (int)RingId.FirstGen => 4,
            _ => 0
        };

    internal static bool PreventsJinx(InventoryState inventory) =>
        Active(inventory, RingId.Whisp);

    internal static int GashaKillCredits(InventoryState inventory) =>
        Active(inventory, RingId.Gasha) ? 2 : 1;

    internal static bool RemovesDiveTimer(InventoryState inventory) =>
        Active(inventory, RingId.Zora);

    internal static bool CanPunch(
        InventoryState inventory, bool bothButtonsEmpty) =>
        bothButtonsEmpty &&
        (Active(inventory, RingId.Fist) || Active(inventory, RingId.Experts));

    internal static bool UsesExpertPunch(InventoryState inventory) =>
        Active(inventory, RingId.Experts);
}

/// <summary>Indices from constants/common/rings.s.</summary>
public enum RingId
{
    Friendship = 0x00,
    PowerL1 = 0x01,
    PowerL2 = 0x02,
    PowerL3 = 0x03,
    ArmorL1 = 0x04,
    ArmorL2 = 0x05,
    ArmorL3 = 0x06,
    Red = 0x07,
    Blue = 0x08,
    Green = 0x09,
    Cursed = 0x0a,
    Experts = 0x0b,
    Blast = 0x0c,
    RangL1 = 0x0d,
    GbaTime = 0x0e,
    Maples = 0x0f,
    Steadfast = 0x10,
    Pegasus = 0x11,
    Toss = 0x12,
    HeartL1 = 0x13,
    HeartL2 = 0x14,
    Swimmers = 0x15,
    Charge = 0x16,
    LightL1 = 0x17,
    LightL2 = 0x18,
    Bombers = 0x19,
    GreenLuck = 0x1a,
    BlueLuck = 0x1b,
    GoldLuck = 0x1c,
    RedLuck = 0x1d,
    GreenHoly = 0x1e,
    BlueHoly = 0x1f,
    RedHoly = 0x20,
    Snowshoe = 0x21,
    Rocs = 0x22,
    Quicksand = 0x23,
    RedJoy = 0x24,
    BlueJoy = 0x25,
    GoldJoy = 0x26,
    GreenJoy = 0x27,
    Discovery = 0x28,
    RangL2 = 0x29,
    Octo = 0x2a,
    Moblin = 0x2b,
    LikeLike = 0x2c,
    Subrosian = 0x2d,
    FirstGen = 0x2e,
    Spin = 0x2f,
    Bombproof = 0x30,
    Energy = 0x31,
    DoubleEdged = 0x32,
    GbaNature = 0x33,
    Slayers = 0x34,
    Rupee = 0x35,
    Victory = 0x36,
    Sign = 0x37,
    Hundredth = 0x38,
    Whisp = 0x39,
    Gasha = 0x3a,
    Peace = 0x3b,
    Zora = 0x3c,
    Fist = 0x3d,
    Whimsical = 0x3e,
    Protection = 0x3f
}

internal enum RingDropKind
{
    Heart,
    Rupee,
    Ore,
    Other
}

internal enum RingDamageSource
{
    Generic,
    BladeTrap,
    OctorokProjectile,
    ZoraFire,
    Electric,
    Beam,
    Hole,
    Spike,
    OwnBomb,
    TerrainHazard
}
