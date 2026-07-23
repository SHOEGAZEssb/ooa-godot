using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal enum FamilyNamingState
{
    None,
    AwaitOpeningClose,
    NameEntry,
    AwaitConfirmation,
    AwaitInvalidClose,
    ThanksDelay,
    AwaitThanksClose
}
