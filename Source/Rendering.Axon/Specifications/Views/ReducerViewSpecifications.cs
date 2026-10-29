// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal sealed class ReducerViewSpecifications(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located, SemanticReadModel model)
{
    internal static void Emit(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located, SemanticReadModel model, SemanticSpecification[] specs) =>
        new ReducerViewSpecifications(context, types, located, model).Emit(specs);

    static string? SourceKey(List<SemanticSpecificationEvent> events)
    {
        var sources = events.Select(given => ((SemanticTextValue)given.EventSource!.Value).Value).Distinct(StringComparer.Ordinal).ToArray();
        return sources.Length == 1 ? JavaCodeBuilder.StringLiteral(sources[0]) : null;
    }

    static Plan Unsupported() => new(AxonRefusalCodes.ViewSpecification, "The complete reducer view specification cannot be executed natively.", [], string.Empty);

    static bool SourcesSupported(List<SemanticSpecificationEvent> events) =>
        events.Count != 0 && !events.Exists(given => given.EventSource?.Value is not SemanticTextValue);

    static bool Shape(SemanticSpecification spec)
    {
        bool[] supported =
        [
            spec.GivenCaller is null or { Authenticated: true, Claims.Length: 0 }, spec.GivenClock is null,
            spec.GivenReadModels.IsEmpty, spec.GivenCaptures.IsEmpty, spec.GivenGenerated.IsEmpty,
            spec.When is null, spec.WhenRedelivered is null, spec.WhenClock is null, spec.WhenCapture is null, spec.WhenTrigger is null,
            !spec.ThenDenied, spec.ThenErrors.IsEmpty, spec.ThenEvents.IsEmpty, spec.ThenQueries.IsEmpty, spec.ThenReturns is null,
            !spec.ThenNoEvents, !spec.ThenEventsInAnyOrder, spec.ThenInvocations.IsEmpty,
            !spec.ThenReadModels.IsEmpty || !spec.ThenAbsentReadModels.IsEmpty
        ];
        return supported.All(value => value);
    }

    void Emit(SemanticSpecification[] specs)
    {
        var plans = specs.Select(Admission).ToArray();
        var package = types.Package(located);
        var modelName = JavaNames.Type(model.Name);
        var name = modelName + "ViewSpecificationsTest";
        var code = new JavaCodeBuilder(package).Declare(name).Declare(modelName).Declare(modelName + "Reducer");
        ViewRepositories.Declare(code, model);
        ViewRepositories.Annotate(code);
        code.Block($"class {name}", body =>
        {
            ViewRepositories.Field(body, model);
            for (var index = 0; index < specs.Length; index++)
            {
                var spec = specs[index];
                var plan = plans[index];
                var method = JavaNames.Member(spec.Name);
                body.Line($"@{body.Reference("org.junit.jupiter.api.Test")}")
                    .Line($"@{body.Reference("org.junit.jupiter.api.DisplayName")}({JavaCodeBuilder.StringLiteral(spec.Id.ToString())})");
                if (plan.Code is { } disabled)
                {
                    body.Line($"@{body.Reference("org.junit.jupiter.api.Disabled")}({JavaCodeBuilder.StringLiteral(disabled + ": " + plan.Reason)})");
                    context.Degradation.Unsupported(disabled, spec.Id, plan.Reason);
                }
                body.Block($"void {method}()", test =>
                {
                    if (plan.Code is { } disabled)
                        test.UnsupportedIntent("execute the read model specification: " + plan.Reason, spec.Id.ToString(), disabled);
                    else EmitNative(spec, plan, test);
                });
                AxonSpecificationsEmitter.Register(context, spec.Id, package + "." + name, method, plan.Code);
            }
        });
        context.AddJava(package, name, code.Build(), true, [.. specs.Select(spec => spec.Id)]);
    }

    Plan Admission(SemanticSpecification spec)
    {
        var events = spec.GivenEvents.ToList();
        if (spec.WhenAppended is { } append) events.Add(new(append.EventContract, append.Values) { EventSource = append.EventSource, Route = append.Route });
        if (BodyFailure(events) is { } failure) return new(failure.Code, "Reducer implementation required: " + failure.Reason, [], string.Empty);
        if (!Shape(spec) || !SourcesSupported(events)) return Unsupported();
        var fallback = SourceKey(events);
        if (!spec.ThenReadModels.All(expected => RowSupported(expected, fallback))) return Unsupported();
        if (!spec.ThenAbsentReadModels.All(expected => AbsentSupported(expected, fallback))) return Unsupported();
        return new(null, string.Empty, [.. events.Select(given => AxonViewEvents.Normalize(context, given)!)], fallback ?? string.Empty);
    }

    bool AbsentSupported(SemanticSpecificationAbsentReadModel expected, string? fallback) =>
        expected.ReadModel == model.Id && KeySupported(expected.Key, fallback);

    (string Code, string Reason)? BodyFailure(List<SemanticSpecificationEvent> events)
    {
        var reducer = located.Slice.Reducers.FirstOrDefault(candidate => candidate.ReadModel == model.Id);
        if (reducer is null) return (AxonRefusalCodes.Reduction, "The reducer is unavailable.");
        var admission = ReducerBodies.Admission(context, located, reducer);
        if (admission.Blocked is { } blocked) return (admission.BlockedCode, blocked);
        return events.Exists(given => !reducer.Transitions.Any(transition => transition.EventContract == given.EventContract && admission.Transitions[transition.RequirementId].Placed))
            ? (AxonRefusalCodes.Reduction, "An observed event has no placed reducer transition.") : null;
    }

    bool RowSupported(SemanticSpecificationReadModel expected, string? fallback) =>
        expected.ReadModel == model.Id && !expected.Exactly && !expected.Values.IsEmpty &&
        expected.Values.All(PropertySupported) && KeySupported(expected.Key, fallback);

    bool PropertySupported(SemanticPropertyValue entry) =>
        model.Properties.FirstOrDefault(property => property.Id == entry.TargetProperty) is { } property && ValueSupported(entry.Value, property.Type);

    bool KeySupported(SemanticValue key, string? fallback)
    {
        if (key is SemanticNullValue) return fallback is not null;
        if (key is SemanticTextValue or SemanticNumberValue or SemanticBooleanValue) return true;
        if (key is not SemanticCompositeValue composite || composite.Properties.IsEmpty) return false;
        var identifiers = model.Properties.Where(property => property.IsIdentifier).ToArray();
        if (identifiers is [{ Type.Kind: SemanticTypeReferenceKind.CompositeType } identifier]) return ValueSupported(composite, identifier.Type);
        return composite.Properties.All(entry => entry.Value is not SemanticNullValue && PropertySupported(entry));
    }

    bool ValueSupported(SemanticValue value, SemanticTypeReference type)
    {
        if (value is SemanticNullValue) return true;
        if (type.IsCollection) return CollectionSupported(value, type);
        if (type.Kind == SemanticTypeReferenceKind.CompositeType) return CompositeSupported(value, type);
        return ScalarSupported(value, type);
    }

    bool CollectionSupported(SemanticValue value, SemanticTypeReference type) =>
        value is SemanticArrayValue array && array.Values.All(item => ValueSupported(item, type with { IsCollection = false, IsOptional = false }));

    bool CompositeSupported(SemanticValue value, SemanticTypeReference type)
    {
        var composite = context.Request.Model.Application.Types.FirstOrDefault(candidate => candidate.Id == type.Target);
        return composite is not null && value is SemanticCompositeValue expected &&
            expected.Properties.All(entry => composite.Properties.FirstOrDefault(property => property.Id == entry.TargetProperty) is { } property && ValueSupported(entry.Value, property.Type));
    }

    bool ScalarSupported(SemanticValue value, SemanticTypeReference type) => value switch
    {
        SemanticTextValue => true,
        SemanticNumberValue number => types.PrimitiveOf(type) == SemanticPrimitiveType.DecimalNumber || (types.PrimitiveOf(type) == SemanticPrimitiveType.WholeNumber && decimal.Truncate(number.Value) == number.Value),
        SemanticBooleanValue => types.PrimitiveOf(type) == SemanticPrimitiveType.Boolean,
        _ => false
    };

    void EmitNative(SemanticSpecification spec, Plan plan, JavaCodeBuilder method)
    {
        method.Block("try", body =>
        {
            CommandFixtureSecurity.CallerReferences(body, spec.GivenCaller);
            var modelName = JavaNames.Type(model.Name);
            var updates = new ProjectionUpdates(context, types, model.Id);
            body.Line($"var projector = new {modelName}Reducer(repository);");
            foreach (var given in plan.Events)
            {
                body.Block(string.Empty, block => block
                    .Line($"var event = {AxonViewEvents.Expression(context, types, given, block)};")
                    .Line($"var type = new {block.Reference("org.axonframework.messaging.core.annotation.AnnotationMessageTypeResolver")}().resolve(event.getClass()).orElseThrow();")
                    .Line($"var message = new {block.Reference("org.axonframework.messaging.eventhandling.GenericEventMessage")}(type, event);")
                    .Line($"projector.on(event, message{updates.DefaultArgument});"));
            }
            var native = new NativeViewSpecifications(context, types, located, model);
            foreach (var expected in spec.ThenReadModels) body.Line(native.Assertion(expected, plan.SourceKey, body));
            foreach (var absent in spec.ThenAbsentReadModels)
                body.Line($"{body.Reference("org.junit.jupiter.api.Assertions")}.assertTrue({ViewRepositories.Get(model, ViewExpectedKeys.Expression(absent.Key, model, types, body) ?? plan.SourceKey)}.findAny().isEmpty(), \"Expected the modeled instance to be removed\");");
        }).Block("finally", body => body.Line($"{body.Reference("org.springframework.security.core.context.SecurityContextHolder")}.clearContext();"));
    }

    sealed record Plan(string? Code, string Reason, SemanticSpecificationEvent[] Events, string SourceKey);
}
