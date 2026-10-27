// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Concepts;

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    static void ClassifyDomain(LedgerBuilder rows)
    {
        rows.Rendered("Concept identity, name and primitive determine the generated concept record and its artifact attribution.", "SemanticConcept.Id SemanticConcept.Name SemanticConcept.Primitive", typeof(AxonConceptsEmitter));
        rows.Rendered("Declared enum values and explicit numeric wire values determine generated enum constants and serialization.", "SemanticConcept.Values SemanticConcept.Numbers", typeof(ConceptEnums));
        rows.Rendered("Composite declarations supply generated record identity, name and fields.", "SemanticCompositeType.Id SemanticCompositeType.Name SemanticCompositeType.Properties", typeof(AxonConceptsEmitter));
        rows.Rendered("Property names become generated record component names.", "SemanticProperty.Name", typeof(AxonConceptsEmitter));
        rows.Rendered("Property identity resolves composite value fields to their modeled target properties.", "SemanticProperty.Id", typeof(CommandJavaTypes));
        rows.Rendered("Optional composite fields may be absent when rendering a modeled value.", "SemanticTypeReference.IsOptional", typeof(CommandJavaTypes));
        rows.Rendered("Supported type-reference categories select primitive, concept and composite Java references.", "SemanticTypeReferenceKind.Primitive SemanticTypeReferenceKind.Concept SemanticTypeReferenceKind.CompositeType", typeof(JavaSemanticTypes));
        rows.Rendered("Supported primitive categories select the generated Java scalar types.", "SemanticPrimitiveType.Uuid SemanticPrimitiveType.Text SemanticPrimitiveType.WholeNumber SemanticPrimitiveType.DecimalNumber SemanticPrimitiveType.Boolean SemanticPrimitiveType.Date SemanticPrimitiveType.DateTime", typeof(JavaSemanticTypes));
        rows.Warned(["STAGE-AXON-GENERATION", "STAGE-AXON-GENERATION-OPTIONS"], "STAGE-AXON-GENERATION: non-UUID strategies degrade. STAGE-AXON-GENERATION-OPTIONS: UUID creation warns when nondefault generation options are present. These options are not implemented; warning is not a claim that their behavior is rendered.", "SemanticGeneration.Prefix SemanticGeneration.Case SemanticGeneration.Size SemanticGeneration.Alphabet SemanticGeneration.Count SemanticGeneration.Encoding SemanticGeneration.Pattern SemanticGeneration.Checksum SemanticGeneration.Minimum SemanticGeneration.Maximum SemanticGeneration.RequirementId");
        rows.Rendered("Validation messages supply the generated exception message for supported concept predicates.", "SemanticValidationRule.Message", typeof(ConceptValidations));
        rows.Rendered("Supported scalar/text validation operators select emitted concept predicates.", "SemanticValidationRuleKind.NotEmpty SemanticValidationRuleKind.Maximum SemanticValidationRuleKind.Minimum SemanticValidationRuleKind.Equal SemanticValidationRuleKind.NotEqual SemanticValidationRuleKind.GreaterThan SemanticValidationRuleKind.GreaterThanOrEqual SemanticValidationRuleKind.LessThan SemanticValidationRuleKind.LessThanOrEqual SemanticValidationRuleKind.Length SemanticValidationRuleKind.Matches SemanticValidationRuleKind.Contains SemanticValidationRuleKind.NotContains SemanticValidationRuleKind.StartsWith SemanticValidationRuleKind.EndsWith", typeof(ConceptValidations));
        rows.Rendered("Collection comparison predicates are generated for supported command validation operands.", "SemanticValidationRuleKind.AllGreaterThan SemanticValidationRuleKind.AllGreaterThanOrEqual", typeof(CommandValidationPredicates));
        rows.Rendered("Only error-severity concept predicates are enforced by generated constructors.", "SemanticValidationSeverity.Error", typeof(ConceptValidations));
        rows.Warned(["STAGE-AXON-IMPL", "STAGE-AXON-0107"], "STAGE-AXON-IMPL: concept validation falls back to an unimplemented predicate diagnostic. STAGE-AXON-0107: command rules without a supported predicate degrade. These validation kinds and non-error severities are not implemented; their presence in the SDK is not evidence of rendering.", "SemanticValidationRuleKind.Checksum SemanticValidationRuleKind.RulePredicate SemanticValidationRuleKind.CodeValidation SemanticValidationRuleKind.DecimalPlaces SemanticValidationSeverity.Information SemanticValidationSeverity.Warning");
        rows.Warned(["STAGE-AXON-IMPL"], "STAGE-AXON-IMPL: the validation requirement reference is not consumed by a validation-body slot; unplaced implementation requirements are reported separately.", "SemanticValidationRule.RequirementId");
        rows.Ignored(IgnoredReason.Identity, "Validation declaration name is a diagnostic identity label, not an executable predicate or error message.", "SemanticValidationRule.Name");
        rows.Ignored(IgnoredReason.UnknownSentinel, "Unknown type and validation categories supply no supported executable case; their owning type or validation reports degradation.", "SemanticTypeReferenceKind.Unknown SemanticPrimitiveType.Unknown SemanticValidationRuleKind.Unknown");
    }
}
