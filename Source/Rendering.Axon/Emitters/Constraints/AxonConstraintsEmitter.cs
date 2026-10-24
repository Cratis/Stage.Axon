// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Constraints;

internal sealed class AxonConstraintsEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var constraints = ConstraintCoverage.All(context).ToArray();
        foreach (var (slice, constraint) in constraints.Where(item => !ConstraintCoverage.Supported(context, item.Constraint)))
        {
            var path = slice + "/constraints/" + constraint.Name;
            var intent = "enforce constraint " + constraint.Name;
            context.Degradation.Unsupported(AxonRefusalCodes.Constraint, slice, "Append-time uniqueness is not implemented for this constraint; affected commands fail closed.", intent, path);
            EmitIntent(context, slice, constraint);
        }
        var package = context.BasePackage + ".infrastructure";
        var code = new JavaCodeBuilder(package).Block("public final class ModeledConstraints", body =>
        {
            body.Line("private ModeledConstraints() {}")
                .Block($"public static void apply({context.BasePackage}.runtime.UniqueValueStore store, Object event, org.axonframework.messaging.core.unitofwork.ProcessingContext context)", method =>
            {
                foreach (var (slice, constraint) in constraints.Where(item => ConstraintCoverage.Supported(context, item.Constraint))) EmitRule(context, method, slice, constraint);
            });
        });
        context.AddJava(package, "ModeledConstraints", code.Build(), sources: [.. constraints.Select(item => item.Slice)]);
    }

    static void EmitRule(AxonEmissionContext context, JavaCodeBuilder body, SemanticId slice, SemanticConstraint constraint)
    {
        var key = JavaCodeBuilder.StringLiteral(slice + "/" + constraint.Name);
        var message = JavaCodeBuilder.StringLiteral(constraint.Message ?? ("Constraint violated: " + constraint.Name));
        foreach (var target in constraint.Targets)
        {
            var contract = CommandAppends.Contract(context, target.EventContract);
            var owner = AxonEventSources.PrimaryTag(context, contract).ComponentName;
            var values = target.Properties.Select(id => "fact." + JavaNames.Member(contract.Properties.Single(property => property.Id == id).Name) + "()");
            body.Line($"if (event instanceof {AxonEventsEmitter.TypeFor(context, contract.Id)} fact) store.assign(context, {key}, java.util.Arrays.asList({string.Join(", ", values)}), fact.{owner}(), {constraint.IgnoreCasing.ToString().ToLowerInvariant()}, {message});");
        }
        foreach (var id in constraint.ReleasedBy)
        {
            var contract = CommandAppends.Contract(context, id);
            var owner = AxonEventSources.PrimaryTag(context, contract).ComponentName;
            body.Line($"if (event instanceof {AxonEventsEmitter.TypeFor(context, id)} fact) store.releaseOwner(context, {key}, fact.{owner}());");
        }
    }

    static void EmitIntent(AxonEmissionContext context, SemanticId slice, SemanticConstraint constraint)
    {
        var package = context.BasePackage + ".constraints";
        var name = JavaNames.Type(constraint.Name) + "ConstraintIntent";
        var code = new JavaCodeBuilder(package)
            .Block("public final class " + name, body => body.Line("private " + name + "() {}")
                .Block("public static void enforce()", method => method.Line("throw new UnsupportedOperationException(\"Append-time uniqueness is not implemented\");")))
            .Build();
        context.AddJava(package, name, code, sources: [slice]);
    }
}
