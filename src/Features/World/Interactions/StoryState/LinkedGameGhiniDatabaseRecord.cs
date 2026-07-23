using Godot;
using System;

namespace oracleofages;
public readonly record struct LinkedGameGhiniDatabaseRecord(int SecretIndex, int ShortSecretIndex, int BeganFlag, int OfferTextId, int RefusalTextId, int ExplanationTextId, int SecretTextId, int FinalTextId, string OfferMessage, string RefusalMessage, string ExplanationMessage, string SecretMessage, string FinalMessage, string Source);
