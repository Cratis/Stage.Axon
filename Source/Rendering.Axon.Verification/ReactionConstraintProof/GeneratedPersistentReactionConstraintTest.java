// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __APPLICATION__.runtime;

import __APPLICATION__.runtime.persistent.JpaUniqueValueStore;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.test.context.ActiveProfiles;

@SpringBootTest(properties = {"spring.datasource.url=jdbc:h2:mem:reaction-constraints;DB_CLOSE_DELAY=-1", "spring.jpa.hibernate.ddl-auto=create-drop"})
@ActiveProfiles("persistent")
class GeneratedPersistentReactionConstraintTest extends GeneratedReactionConstraintTest {
    @Override
    protected Class<?> storeType() { return JpaUniqueValueStore.class; }
}
