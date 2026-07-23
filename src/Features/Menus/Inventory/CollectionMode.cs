using System;
using System.Collections.Generic;

namespace oracleofages;
public enum CollectionMode
{
    None = 0x0,
    SetBit = 0x1,
    Increment = 0x2,
    IncrementBcd = 0x3,
    AddBcd = 0x4,
    Set = 0x5,
    SetDungeonBit = 0x6,
    IncrementDungeonKey = 0x7,
    SetMinimum = 0x8,
    AddUnappraisedRing = 0x9,
    Add = 0xa,
    SetUpgradeBit = 0xb,
    AddCapped = 0xc,
    AddBcdCapped = 0xd,
    AddRupees = 0xe,
    AddSeeds = 0xf
}
