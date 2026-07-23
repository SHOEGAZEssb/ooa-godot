using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct StockRecord(ItemRecord Item, int Order, int Y, int X);
