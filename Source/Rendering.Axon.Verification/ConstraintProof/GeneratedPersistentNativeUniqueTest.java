package __RUNTIME__;

import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.test.context.ActiveProfiles;

@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.NONE, properties = {
        "spring.datasource.url=jdbc:h2:mem:native-unique;DB_CLOSE_DELAY=-1;LOCK_TIMEOUT=5000",
        "spring.jpa.hibernate.ddl-auto=create-drop"})
@ActiveProfiles("persistent")
class GeneratedPersistentNativeUniqueTest extends GeneratedNativeUniqueTest {}
