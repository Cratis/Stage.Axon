// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security;

internal static class PoliciesConfiguration
{
    // A body that declares a local or pattern variable named like an emitted parameter stays as written; the parameter moves.
    internal static bool Declares(string body, string name) => Regex.IsMatch(JavaCodeMask.Blank(body), $@"(?:\bvar|\bfinal|[>\]]|\b(?!(?:return|new|throw|else|case|instanceof)\b)\w+)\s+{name}\b\s*(?:=(?!=)|;|:|\)|,|\?|&&)", RegexOptions.None, TimeSpan.FromSeconds(1));

    internal static void Emit(AxonEmissionContext context)
    {
        foreach (var (policy, condition) in PolicyPredicates.Referenced(context).DistinctBy(item => JavaNames.Member(item.Policy.Name)))
        {
            if (PolicyPredicates.Resolve(context, policy, condition).Body is not { } body) continue;
            EmitPolicy(context, policy, body);
            context.ConsumedImplementations.Add(condition.RequirementId);
        }
    }

    static void EmitPolicy(AxonEmissionContext context, SemanticPolicy policy, string body)
    {
        var name = PolicyPredicates.PolicyClass(policy);
        var code = new JavaCodeBuilder(context.BasePackage + ".infrastructure").Declare(name);
        var authentication = Declares(body, "authentication") ? Fresh(body, "authenticationInput") : "authentication";
        var lowered = PolicyBodies.Lower(body, code);
        code.Line($"@{code.Reference("org.springframework.stereotype.Component")}({JavaCodeBuilder.StringLiteral(PolicyPredicates.BeanName(policy))})")
            .Block($"public class {name}", type => type.Block($"public boolean check({type.Reference("org.springframework.security.core.Authentication")} {authentication}) throws {type.Reference("java.lang.Exception")}", method =>
            {
                if (PolicyBodies.NeedsClaims(body))
                {
                    var jwt = Fresh(body, "jwt");
                    method.Line($"if (!({authentication} instanceof {method.Reference("org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken")} {jwt})) return false;")
                        .Line($"var claims = {jwt}.getTokenAttributes();");
                }
                method.Line(lowered);
            }));
        context.AddJava(context.BasePackage + ".infrastructure", name, code.Build(), sources: [context.Request.Model.Application.Id]);
    }

    static string Fresh(string body, string name)
    {
        var mask = JavaCodeMask.Blank(body);
        while (Regex.IsMatch(mask, @"\b" + name + @"\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) name += "Input";
        return name;
    }
}
