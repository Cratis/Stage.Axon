// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Persistence;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public partial class when_event_names_collide_in_sequencing_policies : Persistence.for_PersistentViewAdmission.given.a_flat_view
{
    [GeneratedRegex("public static final class (?<policy>\\w+) implements", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Declarations { get; }

    [GeneratedRegex("@SequencingPolicy\\(type = (?<policy>\\w+)\\.class\\)", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Annotations { get; }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    void should_allocate_distinct_matching_policies_without_shadowing_the_model(bool root, bool collision)
    {
        var context = RenderCollisions(root, collision);
        var owner = ScopeSelection.Locate(context.Request.Model.Application).Single(owner => owner.Slice.Projections.Any(projection => projection.Name == "TotalProjection"));
        PersistentViewAdmission.Supports(context, owner.Slice.ReadModels.Single().Id).ShouldBeTrue();
        AssertPolicies(Code(context, "TotalProjectionProjector"), collision);
        AssertPolicies(Code(context, "RebuiltTotalReducerProjector"), false);
        AssertProof(context, collision ? "ChangedSequence" : "Total", "TotalProjectionProjector");
        AssertProof(context, "RebuiltTotal", "RebuiltTotalReducerProjector");
    }

    static void AssertPolicies(string code, bool collision)
    {
        var declarations = Declarations.Matches(code).Select(match => match.Groups["policy"].Value).ToArray();
        declarations.Length.ShouldEqual(2);
        declarations.Distinct(StringComparer.Ordinal).Count().ShouldEqual(2);
        if (collision) declarations.Contains("ChangedSequence", StringComparer.Ordinal).ShouldBeFalse();
        var annotations = Annotations.Matches(code).Select(match => match.Groups["policy"].Value).ToArray();
        annotations.Order(StringComparer.Ordinal).SequenceEqual(declarations.Order(StringComparer.Ordinal)).ShouldBeTrue();
        code.ShouldContain("events.Changed event");
    }

    static void AssertProof(AxonEmissionContext context, string model, string projector)
    {
        var policies = Declarations.Matches(Code(context, projector)).Select(match => match.Groups["policy"].Value).ToArray();
        var support = Code(context, model + "PersistentSupport");
        policies.Any(policy => support.Contains(projector + "." + policy + ".class", StringComparison.Ordinal)).ShouldBeTrue();
        var dispatch = context.Artifacts.FirstOrDefault(artifact => artifact.RelativePath.EndsWith('/' + model + "PersistentDefaultDispatchTest.java", StringComparison.Ordinal));
        if (dispatch is not null) policies.Any(policy => Text(dispatch).Contains(projector + "." + policy + ".class", StringComparison.Ordinal)).ShouldBeTrue();
    }

    AxonEmissionContext RenderCollisions(bool root, bool collision)
    {
        SemanticSlice Change(SemanticSlice slice) => slice with
        {
            Events = [.. slice.Events.Select(contract => new[] { "Increased", "Decreased", "CountRecorded", "CounterDeleted" }.Contains(contract.Name, StringComparer.Ordinal) ? contract with { Name = "Changed", StoredName = contract.Name } : contract)],
            ReadModels = [.. slice.ReadModels.Select(model => collision && model.Name == "Total" ? model with { Name = "ChangedSequence" } : model)],
            Projections = [.. slice.Projections.Select(projection => !root && projection.Name == "TotalProjection" ? projection with { Scope = null, Transitions = [.. ProjectionTransitions.From(projection).Select(from => new SemanticProjectionTransition(from.EventContract, new(AffectedInstanceCardinality.One, new SemanticValueExpression(new SemanticTextValue("global"))), []))] } : projection)]
        };
        var application = _request.Model.Application;
        var modules = application.Modules.Select(module => module with { Features = [.. module.Features.Select(feature => feature with { Slices = [.. feature.Slices.Select(Change)] })] }).ToList();
        foreach (var module in modules.ToArray())
        {
            var feature = module.Features.FirstOrDefault(feature => feature.Slices.Any(slice => slice.Events.Any(contract => contract.Name == "Changed")));
            if (feature is null) continue;
            var moved = feature.Slices.Last(slice => slice.Events.Any(contract => contract.Name == "Changed"));
            modules[modules.IndexOf(module)] = module with { Features = [.. module.Features.Select(candidate => candidate with { Slices = [.. candidate.Slices.Where(slice => slice.Id != moved.Id)] })] };
            modules.Add(module with { Id = SemanticId.Create(SemanticKind.Module, module.Name + "Other"), Name = module.Name + "Other", Features = [feature with { Id = SemanticId.Create(SemanticKind.Feature, module.Name + "Other"), Features = [], Slices = [moved] }] });
        }
        var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with { Modules = [.. modules] });
        var context = new AxonEmissionContext(_request with { Model = model }, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        new AxonQueriesEmitter().Emit(context);
        new AxonPersistenceEmitter().Emit(context);
        return context;
    }
}
