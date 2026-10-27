package __RUNTIME__;

import io.cratis.fundamentals.jackson.ConceptKeyFormat;
import io.cratis.fundamentals.jackson.ConceptsModule;
import io.cratis.fundamentals.jackson.TemporalModule;
import io.cratis.fundamentals.jackson.TimeSpanModule;
import org.axonframework.eventsourcing.configuration.EventSourcingConfigurer;
import org.axonframework.test.fixture.AxonTestFixture;
import tools.jackson.databind.json.JsonMapper;

public final class TestSupport {
    private TestSupport() {}
    public static AxonTestFixture fixture(EventSourcingConfigurer configurer) { return AxonTestFixture.with(configurer); }
    public static JsonMapper mapper() {
        return JsonMapper.builder()
            .addModule(new ConceptsModule(ConceptKeyFormat.EMBEDDED_JSON))
            .addModule(new TemporalModule())
            .addModule(new TimeSpanModule())
            .build();
    }
}
