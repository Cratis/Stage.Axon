// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_a_composite_key_entity : given.a_view_request
{
    string _key;
    string _entity;
    string _repository;

    void Because()
    {
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        string Source(string name) => Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)));
        _key = Source("MetricKey");
        _entity = Source("MetricEntity");
        _repository = Source("MetricRepository");
    }

    [Fact] void should_emit_a_serializable_embeddable_record() => _key.ShouldContain("@Embeddable\npublic record MetricKey(");
    [Fact] void should_implement_serializable() => _key.ShouldContain("implements Serializable");
    [Fact] void should_map_unbounded_text_key_parts() => _key.ShouldContain("@Column(name = \"k_region\", columnDefinition = \"VARCHAR(1000000000)\") String region");
    [Fact] void should_map_integer_key_parts_with_the_existing_scalar_type() => _key.ShouldContain("@Column(name = \"k_month\") Long month");
    [Fact] void should_import_key_annotations() => _key.ShouldContain("import jakarta.persistence.Embeddable;");
    [Fact] void should_map_the_embedded_id() => _entity.ShouldContain("@EmbeddedId\n    private MetricKey rowKey;");
    [Fact] void should_accept_the_key_in_the_constructor() => _entity.ShouldContain("public MetricEntity(MetricKey rowKey, Metric model)");
    [Fact] void should_keep_model_columns_unchanged() => _entity.ShouldContain("@Column(name = \"p_region\", columnDefinition = \"VARCHAR(1000000000)\")");
    [Fact] void should_type_the_repository_with_the_key() => _repository.ShouldContain("JpaRepository<MetricEntity, MetricKey>");
}
