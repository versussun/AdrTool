namespace AdrTool;

/// <summary>Represents an expected, user-facing error (bad config, bad args, etc.).</summary>
public sealed class AdrToolException(string message) : Exception(message);
