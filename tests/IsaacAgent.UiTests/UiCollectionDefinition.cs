using Xunit;

namespace IsaacAgent.UiTests;

/// <summary>
/// Serializes real-window launches so FlaUI sessions do not contend.
/// </summary>
[CollectionDefinition("Ui", DisableParallelization = true)]
public sealed class UiCollectionDefinition;
