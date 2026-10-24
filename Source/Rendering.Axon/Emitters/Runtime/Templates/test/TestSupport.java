// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import tools.jackson.databind.json.JsonMapper;
import __RUNTIME__.json.ConceptsModule;

public final class TestSupport {
    private TestSupport() {}
    public static JsonMapper mapper() { return JsonMapper.builder().addModule(new ConceptsModule()).build(); }
}
