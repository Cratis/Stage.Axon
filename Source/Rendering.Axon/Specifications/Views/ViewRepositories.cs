// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

/// <summary>Emits the typed Spring Data repository wiring of generated view specifications.</summary>
internal static class ViewRepositories
{
    internal static void Declare(JavaCodeBuilder code, SemanticReadModel model) =>
        code.Declare(ReadModelEntities.RepositoryName(model)).Declare(ReadModelEntities.EntityName(model));

    /// <summary>Declares the projection-owned join lookup repository of a scoped projection.</summary>
    internal static void DeclareJoins(JavaCodeBuilder code, SemanticProjection projection) => code.Declare(ProjectionJoinEntities.RepositoryName(projection));

    internal static JavaCodeBuilder JoinField(JavaCodeBuilder body, SemanticProjection projection) =>
        body.Line($"@{body.Reference("org.springframework.beans.factory.annotation.Autowired")}").Line($"private {ProjectionJoinEntities.RepositoryName(projection)} joins;");

    internal static void Annotate(JavaCodeBuilder code) => code.Line($"@{code.Reference("org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest")}");

    internal static JavaCodeBuilder Field(JavaCodeBuilder body, SemanticReadModel model) =>
        body.Line($"@{body.Reference("org.springframework.beans.factory.annotation.Autowired")}").Line($"private {ReadModelEntities.RepositoryName(model)} repository;");

    internal static string Get(SemanticReadModel model, string? key) => $"repository.findById({key ?? "null"}).map({ReadModelEntities.EntityName(model)}::toModel).stream()";

    internal static string All(SemanticReadModel model) => $"repository.findAll().stream().map({ReadModelEntities.EntityName(model)}::toModel)";

    /// <summary>Resolves a repository or entity type of a read model declared in <paramref name="owner"/> for a class in <paramref name="package"/>.</summary>
    internal static string Type(JavaCodeBuilder code, ViewJavaTypes types, LocatedSemanticSlice owner, string package, string name)
    {
        if (types.Package(owner) != package) return code.Reference(types.Package(owner) + "." + name);
        code.Declare(name);
        return name;
    }
}
