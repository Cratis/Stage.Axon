// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold.for_AxonScaffoldEmitter;

public class when_emitting_a_scaffold_for_a_constrained_model : for_PersistentProfile.given.a_constrained_orders_request
{
    AxonEmissionContext context;

    void Because()
    {
        context = new(constrained, new(constrained.Profile));
        new AxonScaffoldEmitter().Emit(context);
    }

    [Fact]
    void should_keep_the_in_memory_beans_unguarded_with_an_explanation()
    {
        var code = Content(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/AxonConfiguration.java", StringComparison.Ordinal)).RelativePath);
        code.ShouldNotContain("@Profile");
        code.ShouldContain("// No persistent profile: modeled constraints need multi-tag event consistency (STAGE-AXON-PERSISTENT, see UNSUPPORTED.md).");
        code.ShouldContain("EventStorageEngine eventStorageEngine() { return new InMemoryEventStorageEngine(); }");
        code.ShouldContain("TokenStore tokenStore() { return new InMemoryTokenStore(); }");
    }

    [Fact] void should_not_emit_the_profile_configuration() => context.Artifacts.Exists(artifact => artifact.RelativePath == "src/main/resources/application-persistent.yml").ShouldBeFalse();
    [Fact] void should_not_emit_the_wiring_test() => context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/PersistentProfileTest.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_warn_once() => context.Degradation.Warnings.Count(warning => warning.Code == "STAGE-AXON-PERSISTENT").ShouldEqual(1);
    [Fact] void should_note_the_missing_profile() => context.Degradation.Notes.Any(note => note.Contains("STAGE-AXON-PERSISTENT", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_say_so_in_the_readme() => Content("README.md").ShouldContain("not generated");

    string Content(string path) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == path).Bytes.AsSpan());
}
