// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger;

public class when_auditing_the_executable_model : Specification
{
    readonly HashSet<Type> _visited = [];
    readonly HashSet<string> _members = new(StringComparer.Ordinal);

    void Establish()
    {
        Visit(typeof(ExecutableSemanticModel));
        Visit(typeof(SemanticImplementationRequirement));
        Visit(typeof(SemanticQueryFilterValue));
        Visit(typeof(SemanticAttachmentResolution));
        Visit(typeof(SemanticExecutionCapability));
    }

    [Fact]
    void should_classify_every_reachable_member()
    {
        var missing = _members.Except(SemanticSurfaceLedger.Entries.Keys).ToArray();
        Assert.True(missing.Length == 0, string.Join('\n', missing));
    }

    [Fact] void should_not_retain_stale_members() =>
        SemanticSurfaceLedger.Entries.Keys.Except(_members).ShouldBeEmpty();

    [Fact] void should_explain_every_ignored_member() =>
        SemanticSurfaceLedger.Entries.Values.Where(entry => entry.Kind == SemanticSurfaceDispositionKind.Ignored && string.IsNullOrWhiteSpace(entry.Detail)).ShouldBeEmpty();

    [Fact] void should_give_every_rejection_an_axon_code() =>
        SemanticSurfaceLedger.Entries.Values.Where(entry => entry.Kind == SemanticSurfaceDispositionKind.Rejected && !entry.Detail.StartsWith("STAGE-AXON-", StringComparison.Ordinal)).ShouldBeEmpty();

    void Visit(Type type)
    {
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments()) Visit(argument);
            return;
        }

        if (type.IsArray)
        {
            Visit(type.GetElementType()!);
            return;
        }

        if (type.Namespace?.StartsWith("Cratis.Screenplay", StringComparison.Ordinal) != true || !_visited.Add(type)) return;
        if (type.IsEnum)
        {
            _members.UnionWith(Enum.GetNames(type).Select(name => $"{type.Name}.{name}"));
            return;
        }

        VisitProperties(type);
        foreach (var derived in type.Assembly.GetTypes().Where(candidate => candidate.BaseType == type)) Visit(derived);
    }

    void VisitProperties(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(property => property.GetIndexParameters().Length == 0).ToArray();
        if (properties.Length == 0) _members.Add($"{type.Name}.$type");
        foreach (var property in properties)
        {
            _members.Add($"{type.Name}.{property.Name}");
            Visit(property.PropertyType);
        }
    }
}
