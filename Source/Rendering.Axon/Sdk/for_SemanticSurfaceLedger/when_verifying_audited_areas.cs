// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger;

public class when_verifying_audited_areas
{
    [Fact]
    public void should_not_leave_model_members_legacy()
    {
        const string types = "ExecutableSemanticModel LanguageVersion SemanticVersion SemanticRevision SemanticId EventContractId EventContractRevision SemanticApplication SemanticReactionProductionMode SemanticModule SemanticFeature SemanticSlice SemanticSliceKind Diagnostic DiagnosticSeverity SourceLocation DiagnosticArguments DiagnosticArgument SemanticAddress SemanticKind SemanticAddressPart SemanticAddressPartKind ApplicationIdentity SemanticSourceMapEntry SemanticSourceSpan DocumentId SemanticIdentityOrigin SemanticSourceMapRole CodeBlockSourceLine";
        var names = types.Split(' ').ToHashSet(StringComparer.Ordinal);
        var entries = SemanticSurfaceLedger.Entries.Where(entry => names.Contains(entry.Key.Split('.')[0])).ToArray();
        Assert.Equal(125, entries.Length);
        Assert.Empty(entries.Where(entry => entry.Value.IsLegacy).Select(entry => entry.Key));
    }
}
