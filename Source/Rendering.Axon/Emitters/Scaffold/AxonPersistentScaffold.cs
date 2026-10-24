// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold;

internal static class AxonPersistentScaffold
{
    internal const string Properties = """
        # Copyright (c) Cratis. All rights reserved. Licensed under the MIT license.
        spring.datasource.url=jdbc:h2:file:./data/stage
        spring.datasource.username=sa
        spring.datasource.password=
        spring.jpa.hibernate.ddl-auto=update
        spring.jpa.open-in-view=false
        """ + "\n";
}
