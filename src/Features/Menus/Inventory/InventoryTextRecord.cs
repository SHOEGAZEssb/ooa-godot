using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct InventoryTextRecord(string Kind, int Index, int NameTextId, int DescriptionTextId, string Message);
