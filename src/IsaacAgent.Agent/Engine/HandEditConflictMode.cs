namespace IsaacAgent.Agent.Engine;

/// <summary>
/// Restore policy when a Hand-edit (or unreadable tip comparison) is present.
/// </summary>
public enum HandEditConflictMode
{
    /// <summary>Always apply Before-images (default).</summary>
    Force = 0,

    /// <summary>Leave Hand-edit / unreadable paths unchanged and list them.</summary>
    Skip = 1
}
