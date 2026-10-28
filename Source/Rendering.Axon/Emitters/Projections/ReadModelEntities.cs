// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Concepts;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

/// <summary>
/// Emits the Spring Data JPA entity and repository of a read model. The entity holds basic columns for scalars, the value of a concept
/// and a JSON column for composite and collection state; the immutable read-model record stays the public shape.
/// </summary>
internal static class ReadModelEntities
{
    /// <summary>The longest VARCHAR H2 supports. A modeled key is never truncated or rejected by an implicit limit, and a CLOB cannot be a primary key, so the column is spelled out.</summary>
    internal const int KeyLength = 1_000_000_000;

    internal static string EntityName(SemanticReadModel model) => JavaNames.Type(model.Name) + "Entity";

    internal static string RepositoryName(SemanticReadModel model) => JavaNames.Type(model.Name) + "Repository";

    internal static void Emit(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticReadModel model)
    {
        var application = context.Request.Model.Application;
        var name = JavaNames.Type(model.Name);
        var entity = EntityName(model);
        var code = new JavaCodeBuilder(package).Declare(name).Declare(entity);
        foreach (var property in model.Properties) types.Claim(property.Type, code);
        var columns = model.Properties.Select(property => (Property: property, Member: JavaNames.Member(property.Name), Column: Describe(application, types, property, code))).ToArray();
        var table = "rm_" + Snake(package + "." + name);
        code.Line($"@{code.Reference("jakarta.persistence.Entity")}(name = {JavaCodeBuilder.StringLiteral(package + "." + entity)})")
            .Line($"@{code.Reference("jakarta.persistence.Table")}(name = {JavaCodeBuilder.StringLiteral(table)})")
            .Block($"public class {entity}", body =>
            {
                body.Line($"@{body.Reference("jakarta.persistence.Id")}")
                    .Line($"@{body.Reference("jakarta.persistence.Column")}(name = \"row_key\", columnDefinition = \"VARCHAR({KeyLength})\")")
                    .Line($"private {body.Reference("java.lang.String")} rowKey;");
                foreach (var (_, member, column) in columns)
                {
                    foreach (var annotation in column.Annotations) body.Line(annotation);
                    body.Line($"private {column.Type} {member};");
                }
                body.Line($"protected {entity}() {{}}")
                    .Block($"public {entity}({code.Reference("java.lang.String")} rowKey, {name} model)", constructor =>
                    {
                        constructor.Line("this.rowKey = rowKey;");
                        foreach (var (_, member, column) in columns) constructor.Line($"this.{member} = {column.Write($"model.{member}()")};");
                    })
                    .Block($"public {name} toModel()", method => method.Line($"return new {name}({string.Join(", ", columns.Select(entry => entry.Column.Read(entry.Member)))});"));
            });
        ViewJavaTypes.Add(context, package, entity, code.Build(), model.Id);

        var repository = RepositoryName(model);
        var contract = new JavaCodeBuilder(package).Declare(entity).Declare(repository);
        contract.Line($"@{contract.Reference("org.springframework.stereotype.Repository")}({JavaCodeBuilder.StringLiteral(package + "." + repository)})")
            .Line($"public interface {repository} extends {contract.Reference("org.springframework.data.jpa.repository.JpaRepository")}<{entity}, {contract.Reference("java.lang.String")}> {{}}");
        ViewJavaTypes.Add(context, package, repository, contract.Build(), model.Id);
    }

    static Column Describe(SemanticApplication application, ViewJavaTypes types, SemanticProperty property, JavaCodeBuilder code)
    {
        var type = property.Type;
        var name = "p_" + Snake(JavaNames.Member(property.Name));
        var column = $"@{code.Reference("jakarta.persistence.Column")}(name = {JavaCodeBuilder.StringLiteral(name)}";
        if (type.IsCollection || type.Kind == SemanticTypeReferenceKind.CompositeType)
        {
            return new(types.Type(type, code), [$"@{code.Reference("org.hibernate.annotations.JdbcTypeCode")}({code.Reference("org.hibernate.type.SqlTypes")}.JSON)", column + ")"], value => value, value => value);
        }

        if (type.Kind == SemanticTypeReferenceKind.Concept)
        {
            var concept = application.Concepts.Single(candidate => candidate.Id == type.Target);
            var conceptType = types.Type(type, code);
            if (types.IsEnumeration(type))
            {
                var primitive = code.Reference(ConceptEnums.IsNumeric(concept) ? "java.lang.Integer" : "java.lang.String");
                return new(primitive, [column + ")"], value => $"{value} == null ? null : {value}.value()", value => $"{value} == null ? null : {conceptType}.fromValue({value})");
            }

            var scalar = types.ColumnType(types.PrimitiveOf(type), code);
            return new(scalar, [Basic(column, types.PrimitiveOf(type))], value => $"{value} == null ? null : {value}.value()", value => $"{value} == null ? null : new {conceptType}({value})");
        }

        return new(types.Type(type, code), [Basic(column, type.Primitive)], value => value, value => value);
    }

    static string Basic(string column, SemanticPrimitiveType primitive) => primitive switch
    {
        SemanticPrimitiveType.DecimalNumber => column + ", columnDefinition = \"DECFLOAT\")",
        SemanticPrimitiveType.Uuid or SemanticPrimitiveType.WholeNumber or SemanticPrimitiveType.Boolean or SemanticPrimitiveType.Date or SemanticPrimitiveType.DateTime => column + ")",
        _ => column + ", length = 4096)"
    };

    static string Snake(string value)
    {
        var text = new StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!char.IsAsciiLetterOrDigit(character)) character = '_';
            else if (char.IsAsciiLetterUpper(character) && index > 0 && !char.IsAsciiLetterUpper(value[index - 1]) && char.IsAsciiLetterOrDigit(value[index - 1])) text.Append('_');
            text.Append(char.ToLowerInvariant(character));
        }
        return text.ToString();
    }

    sealed record Column(string Type, string[] Annotations, Func<string, string> Write, Func<string, string> Read);
}
