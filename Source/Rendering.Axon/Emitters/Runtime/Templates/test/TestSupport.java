// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import __RUNTIME__.json.ConceptsModule;
import org.axonframework.eventsourcing.configuration.EventSourcingConfigurer;
import org.axonframework.test.fixture.AxonTestFixture;
import tools.jackson.databind.json.JsonMapper;

public final class TestSupport {
    private TestSupport() {}
    public static AxonTestFixture fixture(EventSourcingConfigurer configurer) { return AxonTestFixture.with(configurer); }
    public static JsonMapper mapper() { return JsonMapper.builder().addModule(new ConceptsModule()).build(); }
}
