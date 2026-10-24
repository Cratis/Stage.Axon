// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Admission;

internal static class AxonSupportedVersions
{
    // Exact version pairs measured by blueprints (2), simple reactions (6),
    // application reactions with roles (10), and protected decision reads (25).
    // Admission of a version is not admission of every feature in that version.
    internal static bool Contains(LanguageVersion language, SemanticVersion semantic) =>
        (language.Major, language.Minor, semantic.Major, semantic.Minor) is
            (2, 0, 2, 0) or (6, 0, 6, 0) or (10, 0, 10, 0) or (25, 0, 25, 0);
}
