// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client.for_AxonClientEmitter;

public class when_mapping_types : given.a_client_request
{
    string concepts;

    void Because()
    {
        Render();
        concepts = Text("client/src/concepts.ts");
    }

    [Fact] void should_alias_text_concepts_as_strings() => concepts.ShouldContain("export type OrderNumber = string;");
    [Fact] void should_alias_decimal_concepts_as_numbers() => concepts.ShouldContain("export type Money = number;");
    [Fact] void should_document_the_decimal_precision() => concepts.ShouldContain("IEEE 754 double");
    [Fact] void should_map_string_enums_to_literal_unions() => concepts.ShouldContain("export type OrderStatus = \"new\" | \"picking\" | \"shipped\" | \"delivered\" | \"cancelled\";");
    [Fact] void should_check_enum_membership() => concepts.ShouldContain("decodeOneOf(value, path, orderStatusValues)");
    [Fact] void should_not_cast() => concepts.ShouldNotContain(" as T");
    [Fact] void should_warn_about_decimal_precision_once_per_declaration() => plan.Diagnostics.Count(diagnostic => diagnostic.Code == Admission.AxonRefusalCodes.ClientPrecision && diagnostic.Message.Contains("Money", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_not_report_precision_in_the_unsupported_summary() => Text("UNSUPPORTED.md").ShouldNotContain(Admission.AxonRefusalCodes.ClientPrecision);
}
