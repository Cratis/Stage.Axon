// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

/// <summary>
/// Emits the projection-owned lookup table of a scoped projection's joins: one row per join key that stores the last join event,
/// so a child that appears after its join event hydrates from the table instead of from in-memory state.
/// </summary>
internal static class ProjectionJoinEntities
{
    internal static string EntityName(SemanticProjection projection) => JavaNames.Type(projection.Name) + "JoinEntity";

    internal static string RepositoryName(SemanticProjection projection) => JavaNames.Type(projection.Name) + "JoinRepository";

    /// <summary>Whether the rendered projector of the projection owns a join lookup table.</summary>
    internal static bool Has(AxonEmissionContext context, ViewJavaTypes types, SemanticProjection projection, SemanticReadModel model) =>
        ProjectionHandlerShape.Renderable(context, types, projection, model) && ProjectionJoinSlot.All(ProjectionScopeNode.Create(context.Request.Model.Application, types, projection, model, JavaNames.Type(model.Name))).Count > 0;

    internal static void Emit(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticProjection projection, IReadOnlyList<ProjectionJoinSlot> slots)
    {
        var entity = EntityName(projection);
        var code = new JavaCodeBuilder(package).Declare(entity);
        var text = code.Reference("java.lang.String");
        var events = slots.Select(slot => types.EventType(slot.Join.EventContract, code)).ToArray();
        var table = "rj_" + ReadModelEntities.Snake(package + "." + JavaNames.Type(projection.Name));
        code.Line($"@{code.Reference("jakarta.persistence.Entity")}(name = {JavaCodeBuilder.StringLiteral(package + "." + entity)})")
            .Line($"@{code.Reference("jakarta.persistence.Table")}(name = {JavaCodeBuilder.StringLiteral(table)})")
            .Block($"public class {entity}", body =>
            {
                body.Line($"@{body.Reference("jakarta.persistence.Id")}")
                    .Line($"@{body.Reference("jakarta.persistence.Column")}(name = \"join_key\", columnDefinition = \"VARCHAR({ReadModelEntities.KeyLength})\")")
                    .Line($"private {text} joinKey;");
                for (var index = 0; index < slots.Count; index++)
                {
                    body.Line($"@{body.Reference("org.hibernate.annotations.JdbcTypeCode")}({body.Reference("org.hibernate.type.SqlTypes")}.JSON)")
                        .Line($"@{body.Reference("jakarta.persistence.Column")}(name = {JavaCodeBuilder.StringLiteral("join_" + slots[index].Ordinal)})")
                        .Line($"private {events[index]} {slots[index].Member};");
                }
                body.Line($"protected {entity}() {{}}")
                    .Block($"public {entity}({text} joinKey)", constructor => constructor.Line("this.joinKey = joinKey;"));
                for (var index = 0; index < slots.Count; index++)
                {
                    var slot = slots[index];
                    body.Block($"public {events[index]} {slot.Member}()", method => method.Line($"return {slot.Member};"))
                        .Block($"public {entity} {slot.Member}({events[index]} event)", method => method.Line($"this.{slot.Member} = event;").Line("return this;"));
                }
            });
        ViewJavaTypes.Add(context, package, entity, code.Build(), projection.Id);

        var repository = RepositoryName(projection);
        var contract = new JavaCodeBuilder(package).Declare(entity).Declare(repository);
        contract.Line($"@{contract.Reference("org.springframework.stereotype.Repository")}({JavaCodeBuilder.StringLiteral(package + "." + repository)})")
            .Line($"public interface {repository} extends {contract.Reference("org.springframework.data.jpa.repository.JpaRepository")}<{entity}, {contract.Reference("java.lang.String")}> {{}}");
        ViewJavaTypes.Add(context, package, repository, contract.Build(), projection.Id);
    }
}
