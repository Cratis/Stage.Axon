// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests;

public class when_validating_modeled_examples : a_view_request
{
    const string Email = "writer@example.test";
    const string Pattern = "[a-z]+@[a-z]+[.][a-z]{2,}";
    AxonEmissionContext _context;
    SemanticCommand _command;
    SemanticReadModel _model;
    SemanticTypeReference _email;
    SemanticTypeReference _details;
    SemanticTypeReference _title;
    SemanticCompositeValue _validDetails;
    SemanticCompositeValue _invalidDetails;

    async Task Establish()
    {
        var loaded = await SemanticModelLoader.LoadFromPathAsync(Path.Combine(Root(), "Source/Rendering.Axon.Verification/Corpus/client-proof.play"), null, "ValidatedExamples");
        _request = new(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        _command = ScopeSelection.Select(_request).SelectMany(owner => owner.Slice.Commands).Single(command => command.Name == "RecordContact");
        _model = ScopeSelection.Select(_request).SelectMany(owner => owner.Slice.ReadModels).Single(model => model.Name == "WorkItemContacts");
        _email = _model.Properties.Single(property => property.Name == "email").Type;
        _details = _model.Properties.Single(property => property.Name == "details").Type;
        var contact = _request.Model.Application.Types.Single(type => type.Id == _details.Target);
        _title = contact.Properties.Single(property => property.Name == "title").Type;
        _validDetails = new([.. contact.Properties.Select(property => new SemanticPropertyValue(property.Id, new SemanticTextValue(property.Name == "email" ? Email : "Valid")))]);
        _invalidDetails = new([.. contact.Properties.Select(property => new SemanticPropertyValue(property.Id, new SemanticTextValue(property.Name == "email" ? "invalid" : "X")))]);
        Use(_request.Model.Application);
    }

    [Fact]
    void should_continue_past_an_invalid_then_errors_when_value()
    {
        InvalidWhenFirst();
        var examples = HttpModeledExamples.Specifications(_context).ToArray();
        var rejection = examples.First(spec => spec.Name == "NativeFixtureAllowsManager");
        rejection.ThenErrors.Length.ShouldEqual(1);
        Array.IndexOf(examples, rejection).ShouldBeLessThan(Array.FindIndex(examples, spec => spec.Name == "RichContactExample"));
        HttpModeledExamples.Value(_context, _email, out var value).ShouldBeTrue();
        value.ShouldEqual(Email);
    }

    [Fact]
    void should_use_the_same_proven_values_for_the_body_and_query_parameter()
    {
        InvalidWhenFirst();
        HttpSecurityExamples.CanSupply(_context, CommandEndpoint()).ShouldBeTrue();
        HttpSecurityExamples.Parameter(_context, new("email", true, _email)).ShouldEqual(Email);
        using var body = JsonDocument.Parse(HttpSecurityExamples.Body(_context, CommandEndpoint()));
        body.RootElement.GetProperty("email").GetString().ShouldEqual(Email);
        body.RootElement.GetProperty("details").GetProperty("email").GetString().ShouldEqual(Email);
        body.RootElement.GetProperty("details").GetProperty("title").GetString().ShouldEqual("Valid");
    }

    [Fact]
    void should_validate_expected_rows_before_falling_back_to_seed_rows()
    {
        ChangeSpecifications(spec => spec.Name == "RichContactExample" ? spec with
        {
            GivenReadModels = spec.ThenReadModels,
            ThenReadModels = [.. spec.ThenReadModels.Select(row => row with { Key = new SemanticTextValue("invalid"), Values = [.. row.Values.Select(value => value.TargetProperty == _model.Properties.Single(property => property.Name == "email").Id ? value with { Value = new SemanticTextValue("invalid") } : value)] })]
        } : spec);
        HttpModeledExamples.Row(_context, _model)["email"].ShouldEqual(Email);
    }

    [Fact] void should_enforce_the_native_title_length_rule() => HttpModeledExamples.Literal(_context, _title, new SemanticTextValue("X"), out _).ShouldBeFalse();
    [Fact] void should_reject_an_invalid_email_in_a_composite() => HttpModeledExamples.Literal(_context, _details, _invalidDetails, out _).ShouldBeFalse();
    [Fact] void should_accept_a_valid_composite() => HttpModeledExamples.Literal(_context, _details, _validDetails, out _).ShouldBeTrue();
    [Fact] void should_reject_an_invalid_collection_element() => HttpModeledExamples.Literal(_context, _email with { IsCollection = true }, new SemanticArrayValue([new SemanticTextValue(Email), new SemanticTextValue("invalid")]), out _).ShouldBeFalse();
    [Fact] void should_accept_a_valid_collection() => HttpModeledExamples.Literal(_context, _email with { IsCollection = true }, new SemanticArrayValue([new SemanticTextValue(Email)]), out _).ShouldBeTrue();
    [Fact] void should_reject_an_invalid_nested_collection_element() => HttpModeledExamples.Literal(_context, _details with { IsCollection = true }, new SemanticArrayValue([_validDetails, _invalidDetails]), out _).ShouldBeFalse();
    [Fact] void should_not_treat_a_scalar_as_a_collection() => HttpModeledExamples.Literal(_context, _email with { IsCollection = true }, new SemanticTextValue(Email), out _).ShouldBeFalse();

    [Fact]
    void should_skip_unprovable_constraints_instead_of_trusting_the_examples()
    {
        UnprovableEmail();
        HttpModeledExamples.Value(_context, _email, out _).ShouldBeFalse();
        HttpModeledExamples.Row(_context, _model).ShouldBeNull();
        HttpSecurityExamples.CanSupply(_context, CommandEndpoint()).ShouldBeFalse();
        _context.Degradation.Warnings.ShouldContain(warning => warning.Code == "STAGE-AXON-HTTP-SPEC" && warning.Artifact == _command.Id);
    }

    [Fact]
    void should_not_bypass_unprovable_nested_constraints_with_synthesized_values()
    {
        UnprovableEmail();
        HttpModeledExamples.Literal(_context, _details, _validDetails, out _).ShouldBeFalse();
        HttpSecurityExamples.CanSupply(_context, new("query", "GET", "/contacts", null, "hasRole('Editor')", [new("details", true, _details)], "zeroOrOne", _model.Id)).ShouldBeFalse();
        _context.Degradation.Warnings.ShouldContain(warning => warning.Code == "STAGE-AXON-HTTP-SPEC");
    }

    [Fact]
    void should_not_bypass_unprovable_collection_constraints_with_an_empty_fallback()
    {
        UnprovableEmail();
        var collection = _email with { IsCollection = true };
        HttpModeledExamples.Literal(_context, collection, new SemanticArrayValue([new SemanticTextValue(Email)]), out _).ShouldBeFalse();
        Assert.Throws<InvalidOperationException>(() => HttpSecurityExamples.Value(_context, collection, 0));
        HttpSecurityExamples.CanSupply(_context, new("query", "GET", "/contacts", null, "hasRole('Editor')", [new("emails", true, collection)], "zeroOrOne", _model.Id)).ShouldBeFalse();
        _context.Degradation.Warnings.ShouldContain(warning => warning.Code == "STAGE-AXON-HTTP-SPEC");
    }

    [Theory]
    [InlineData(".*")]
    [InlineData("(?=writer)[a-z]+@[a-z]+[.][a-z]{2,}")]
    [InlineData("[a-z]+@(?:example)[.]test")]
    void should_conservatively_reject_patterns_outside_the_portable_subset(string pattern)
    {
        EmailPattern(pattern);
        HttpModeledExamples.Literal(_context, _email, new SemanticTextValue(Email), out _).ShouldBeFalse();
    }

    void InvalidWhenFirst()
    {
        ChangeSpecifications(spec => spec.Name == "NativeFixtureAllowsManager" ? spec with
        {
            When = spec.When with
            {
                Command = _command.Id,
                Values = [.. _command.Properties.Select(property => new SemanticPropertyValue(property.Id, property.Name switch
                {
                    "id" => new SemanticTextValue("00000000-0000-0000-0000-000000000003"),
                    "email" => new SemanticTextValue("invalid"),
                    "details" => _invalidDetails,
                    _ => new SemanticTextValue("open")
                }))]
            },
            ThenEvents = [],
            ThenErrors = [new(null, "Invalid email")]
        } : spec);
    }

    void UnprovableEmail() => EmailPattern("(?=writer)" + Pattern);

    void EmailPattern(string pattern) => Use(_request.Model.Application with
    {
        Concepts = [.. _request.Model.Application.Concepts.Select(concept => concept.Id == _email.Target ? concept with { Validations = [new(default, SemanticValidationRuleKind.Matches, new SemanticTextValue(pattern), null)] } : concept)]
    });

    void ChangeSpecifications(Func<SemanticSpecification, SemanticSpecification> change)
    {
        SemanticFeature Update(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Update)],
            Slices = [.. feature.Slices.Select(slice => slice with { Specifications = [.. slice.Specifications.Select(change)] })]
        };
        Use(_request.Model.Application with { Modules = [.. _request.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Update)] })] });
    }

    void Use(SemanticApplication application)
    {
        _request = _request with { Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application) };
        _context = new(_request, new(_request.Profile));
    }

    HttpEndpoint CommandEndpoint() => new("command", "POST", "/contacts", null, "hasRole('Editor')", [], null, _command.Id);
}
