// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_IngressProofArtifacts;

public class when_preparing_demo_identity_proof : Specification
{
    string source;

    void Because() => source = IngressProofArtifacts.Sources(IngressProofModel.Application)["GeneratedDemoIdentityTest.java"];

    [Fact] void should_boot_the_real_http_server() => source.ShouldContain("@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT)");
    [Fact] void should_activate_demo_explicitly() => source.ShouldContain("@ActiveProfiles(\"demo\")");
    [Fact] void should_use_the_modeled_command() => source.ShouldContain("/api/accounts/registry/register-account");
    [Fact] void should_use_the_modeled_query() => source.ShouldContain("/api/accounts/registry/account-snapshot?id=");
    [Fact] void should_verify_the_projected_identity() => source.ShouldContain("assertEquals(\"demo\", JsonPath.read(result.body(), \"$.actor\"))");
    [Fact] void should_not_supply_credentials() => source.ShouldNotContain("Authorization");
}
