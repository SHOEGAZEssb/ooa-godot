using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace oracleofages;

internal enum GeneratedTableKeySemantics
{
    Unique,
    Grouped,
    Ordered,
    Aliased,
    Repeated
}
