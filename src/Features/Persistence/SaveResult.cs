using Godot;
using System;
using System.IO;
using System.Security;

namespace oracleofages;
public readonly record struct SaveResult(bool Success, string ErrorMessage)
{
    public static readonly SaveResult Succeeded = new(true, string.Empty);
    public static SaveResult Failed(string message) => new(false, string.IsNullOrWhiteSpace(message) ? "Unknown save error." : message);
}
