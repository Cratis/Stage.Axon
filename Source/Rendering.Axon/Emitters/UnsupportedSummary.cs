// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Rendering.Axon.Emitters;

internal static class UnsupportedSummary
{
    internal static void Emit(AxonEmissionContext context)
    {
        var entries = context.Degradation.UnsupportedConstructs.Zip(context.Degradation.Intents)
            .Select(pair => $"| {Cell(pair.Second.Code)} | {Cell(pair.Second.ModelPath)} | {Cell(pair.Second.Intent)} | {Cell(pair.First.Message)} |")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var text = "# Unsupported Screenplay constructs\n\nThe application can build and start, but the following modeled behavior is not implemented. Disabled specifications are not proof that the behavior works.\n\n| Code | Model path | Intent | Reason |\n| --- | --- | --- | --- |\n" + string.Join('\n', entries) + "\n";
        context.Artifacts.Add(PlannedArtifact.CreateText("UNSUPPORTED.md", text));
    }

    static string Cell(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
}
