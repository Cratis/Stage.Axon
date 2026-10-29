// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Emitters.Security;

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    static void ClassifySecurity(LedgerBuilder rows)
    {
        rows.Rendered("Concrete authenticated and anonymous policy cases produce isAuthenticated() and permitAll() authorization expressions.", "SemanticAuthenticatedCondition.$type SemanticAnonymousCondition.$type", typeof(SecurityAnnotations));
        rows.Rendered("The declared role supplies the escaped hasRole argument in the generated authorization expression.", "SemanticRoleCondition.Role", typeof(SecurityAnnotations));
        rows.Warned(["STAGE-AXON-AUTHORIZATION", "STAGE-AXON-IMPL-LANGUAGE"], "STAGE-AXON-AUTHORIZATION and STAGE-AXON-IMPL-LANGUAGE: resolvable single inline Java policies are checked by native beans from endpoint @PreAuthorize. Handler policies not expressible as role sets warn that non-HTTP dispatch is not guarded. Inline-policy compositions deny at both endpoint and handler; foreign-language, unresolved or unsupported conditions also deny and report degradation.", "SemanticOpaquePolicyCondition.RequirementId SemanticImplementationRole.PolicyPredicate");
        rows.Warned(["STAGE-AXON-AUTHORIZATION"], "STAGE-AXON-AUTHORIZATION: claim conditions are not a supported policy pattern. Literal, Subject and Artifact targets are not read or rendered; the complete unsupported claim condition falls through to denyAll() and an owner diagnostic, rather than silently granting access.", "SemanticClaimTargetKind.Literal SemanticClaimTargetKind.Subject SemanticClaimTargetKind.Artifact");
    }
}
