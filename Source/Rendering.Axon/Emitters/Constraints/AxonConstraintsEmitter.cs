// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Constraints;

internal sealed class AxonConstraintsEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        foreach (var (slice, constraint) in ConstraintCoverage.All(context))
        {
            var reason = ConstraintCoverage.Reason(context, constraint);
            if (reason is null)
            {
                EmitEntity(context, slice, constraint);
                continue;
            }
            var path = slice + "/constraints/" + constraint.Name;
            context.Degradation.Unsupported(AxonRefusalCodes.Constraint, slice, $"Uniqueness of {constraint.Name} is not enforced: {reason}.", "enforce constraint " + constraint.Name, path);
        }
    }

    static void EmitEntity(AxonEmissionContext context, SemanticId slice, SemanticConstraint constraint)
    {
        var package = ConstraintEntities.Package(context);
        var name = ConstraintEntities.Type(constraint);
        var occurrence = constraint.Kind == SemanticConstraintKind.UniqueEventOccurrence;
        var first = ConstraintCoverage.Contract(context, constraint.Targets[0].EventContract)!;
        var owner = ConstraintCoverage.Owner(context, first.Id)!.Value;
        var value = occurrence ? null : first.Properties.Single(property => property.Id == constraint.Targets[0].Properties[0]);
        var valueType = value is null ? owner.JavaType : new Commands.CommandJavaTypes(context.Request.Model.Application, context.BasePackage).Type(value.Type);
        var events = constraint.Targets.Select(target => target.EventContract).Distinct().ToArray();
        var releases = constraint.ReleasedBy.Distinct().ToArray();
        var code = new JavaCodeBuilder(package).Declare(name);
        code.ClaimExpressions([.. events.Concat(releases).Select(id => AxonEventsEmitter.TypeFor(context, id)), owner.JavaType, valueType]);
        var tag = EventTagsEmitter.ConstraintReference(context, constraint, first, code, package);
        var ownerType = code.Reference(owner.JavaType);
        var idType = code.Reference(valueType);
        var nullable = code.Reference("org.jspecify.annotations.Nullable");
        var message = JavaCodeBuilder.StringLiteral(ConstraintEntities.Message(constraint));
        code.Line($"/** DCB set validation; holds on the default in-memory event store. Not enforced on a single-tag aggregate event store such as AggregateBasedJpaEventStorageEngine, see UNSUPPORTED.md ({AxonRefusalCodes.Constraint}). */")
            .Line($"@{code.Reference("org.axonframework.extension.spring.stereotype.EventSourced")}(tagKey = {tag}, idType = {idType}.class)")
            .Block($"public final class {name}", body =>
            {
                body.Line(occurrence ? "private boolean occurred;" : $"private @{nullable} {ownerType} claimedBy;").Line().Line($"@{body.Reference("org.axonframework.eventsourcing.annotation.reflection.ForcedEntityCreator")}")
                    .Block($"public {name}(@{body.Reference("org.axonframework.eventsourcing.annotation.reflection.InjectEntityId")} {idType} id)", _ => { });
                var assert = ConstraintEntities.Assert(constraint, value is null ? string.Empty : JavaNames.Member(value.Name));
                body.Line().Block($"public void {assert}({(occurrence ? string.Empty : ownerType + " owner")})", method =>
                    method.Line(occurrence ? $"if (occurred) throw new IllegalStateException({message});" : $"if (claimedBy != null && !claimedBy.equals(owner)) throw new IllegalStateException({message});"));
                foreach (var id in events) Handler(context, body, package, id, claimed: true, occurrence);
                foreach (var id in releases) Handler(context, body, package, id, claimed: false, occurrence);
                if (value is not null && ConstraintCoverage.Normalized(context, constraint, value.Type, "id") is { } normalized) CriteriaBuilder(body, tag, idType, normalized);
            });
        context.AddJava(package, name, code.Build(), sources: [slice]);
    }

    static void Handler(AxonEmissionContext context, JavaCodeBuilder body, string package, SemanticId id, bool claimed, bool occurrence)
    {
        var component = ConstraintCoverage.Owner(context, id)!.Value.ComponentName;
        var type = AxonEventsEmitter.TypeFor(context, id, body, package);
        body.Line().Line($"@{body.Reference("org.axonframework.eventsourcing.annotation.EventSourcingHandler")}")
            .Block($"public void on({type} event)", method =>
            {
                if (occurrence) method.Line($"occurred = {(claimed ? "true" : "false")};");
                else if (claimed) method.Line($"claimedBy = event.{component}();");
                else method.Line($"if ({body.Reference("java.util.Objects")}.equals(claimedBy, event.{component}())) claimedBy = null;");
            });
    }

    static void CriteriaBuilder(JavaCodeBuilder body, string tag, string idType, string normalized)
    {
        var criteria = body.Reference("org.axonframework.messaging.eventstreaming.EventCriteria");
        body.Line().Line($"@{body.Reference("org.axonframework.eventsourcing.annotation.EventCriteriaBuilder")}")
            .Block($"public static {criteria} criteria({idType} id)", method =>
                method.Line($"return {criteria}.havingTags({body.Reference("org.axonframework.messaging.eventstreaming.Tag")}.of({tag}, {body.ReferenceExpression(normalized)}));"));
    }
}
