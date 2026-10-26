// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_emitting_persistent_probe_support : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _source;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonRuntimeEmitter().Emit(context);
        _source = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/PersistentProbeSupport.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact]
    void should_assert_refusal_types_in_separate_typed_branches()
    {
        _source.ShouldContain("if (early) {\n            assertInstanceOf(BeanDefinitionStoreException.class, root);\n        } else {\n            assertInstanceOf(IllegalStateException.class, root);\n        }");
        _source.ShouldNotContain("assertInstanceOf(early ? BeanDefinitionStoreException.class : IllegalStateException.class, root)");
    }
}
