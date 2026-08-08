using System.Globalization;

namespace oracleofages;

internal readonly record struct ActiveCollisionModeSet(int Mask)
{
    internal static ActiveCollisionModeSet Parse(
        GeneratedTableRow row,
        int column)
    {
        int mask = 0;
        foreach (string value in row.RequiredString(column).Split(','))
        {
            if (!int.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int mode) ||
                mode is < 0 or > 5 ||
                (mask & (1 << mode)) != 0)
            {
                throw row.Invalid(
                    column,
                    "unique comma-separated active-collision modes 0-5");
            }
            mask |= 1 << mode;
        }

        if (mask == 0)
        {
            throw row.Invalid(
                column,
                "one or more comma-separated active-collision modes 0-5");
        }
        return new ActiveCollisionModeSet(mask);
    }

    internal bool Contains(int mode) =>
        mode is >= 0 and <= 5 && (Mask & (1 << mode)) != 0;
}
