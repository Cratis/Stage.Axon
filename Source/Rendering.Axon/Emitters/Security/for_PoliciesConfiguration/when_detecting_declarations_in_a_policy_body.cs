// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_PoliciesConfiguration;

public class when_detecting_declarations_in_a_policy_body : Specification
{
    const string CommentedDeclaration = "// var claims = legacyClaims;\nreturn claims.containsKey(\"department\");";
    const string BlockCommentedDeclaration = "/* final Map<String, Object> claims = null; */\nreturn claims.isEmpty();";
    const string StringDeclaration = "var text = \"var claims = 1;\";\nreturn claims.isEmpty();";
    const string TextBlockDeclaration = "var text = \"\"\"\n  var claims = 1;\n  \"\"\";\nreturn claims.isEmpty();";
    const string EscapedTextBlockDelimiter = "var text = \"\"\"\n  \\\"\"\"\n  var claims = 1;\n  \"\"\";\nreturn claims.isEmpty();";
    const string EscapedQuoteInString = "var text = \"\\\" var claims = 1;\";\nreturn claims.isEmpty();";
    const string UnterminatedBlockComment = "return claims.isEmpty();\n/* var claims = 1;";
    const string RealDeclaration = "var claims = legacyClaims;\nreturn claims.isEmpty();";

    [Fact] void should_ignore_a_declaration_in_a_line_comment() => PoliciesConfiguration.Declares(CommentedDeclaration, "claims").ShouldBeFalse();
    [Fact] void should_ignore_a_declaration_in_a_block_comment() => PoliciesConfiguration.Declares(BlockCommentedDeclaration, "claims").ShouldBeFalse();
    [Fact] void should_ignore_a_declaration_in_a_string_literal() => PoliciesConfiguration.Declares(StringDeclaration, "claims").ShouldBeFalse();
    [Fact] void should_ignore_a_declaration_in_a_text_block() => PoliciesConfiguration.Declares(TextBlockDeclaration, "claims").ShouldBeFalse();
    [Fact] void should_ignore_a_declaration_after_an_escaped_text_block_delimiter() => PoliciesConfiguration.Declares(EscapedTextBlockDelimiter, "claims").ShouldBeFalse();
    [Fact] void should_ignore_a_declaration_after_an_escaped_quote_in_a_string() => PoliciesConfiguration.Declares(EscapedQuoteInString, "claims").ShouldBeFalse();
    [Fact] void should_ignore_a_declaration_in_an_unterminated_block_comment() => PoliciesConfiguration.Declares(UnterminatedBlockComment, "claims").ShouldBeFalse();
    [Fact] void should_detect_a_real_declaration() => PoliciesConfiguration.Declares(RealDeclaration, "claims").ShouldBeTrue();
}
