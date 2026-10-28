package __RUNTIME__;

import static org.junit.jupiter.api.Assertions.assertEquals;

import __APPLICATION__.infrastructure.CommandProblemDetails;
import __APPLICATION__.work.items.requestwirenames.EchoClass;
import jakarta.validation.Validation;
import java.util.List;
import java.util.Map;
import org.axonframework.messaging.core.interception.JSR303ViolationException;
import org.junit.jupiter.api.Test;

/** Proves validation errors name the JSON wire property, not the Java member, for a command whose JSON name is a Java keyword. */
class ClientWireNameValidationTest {
    @Test
    void reportsTheWirePropertyNameForARenamedMember() {
        try (var factory = Validation.buildDefaultValidatorFactory()) {
            var violations = factory.getValidator().validate((Object) new EchoClass(null));
            var problem = new CommandProblemDetails().validation(new JSR303ViolationException(violations));
            var errors = (List<?>) problem.getProperties().get("errors");
            assertEquals(1, errors.size());
            assertEquals("class", ((Map<?, ?>) errors.getFirst()).get("property"));
        }
    }
}
