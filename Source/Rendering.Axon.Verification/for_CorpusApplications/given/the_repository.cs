// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace Cratis.Stage.Rendering.Axon.Verification.for_CorpusApplications.given;

internal static class the_repository
{
    internal static string Root([CallerFilePath] string path = "")
    {
        var directory = Directory.GetParent(path);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Stage.Axon.slnx")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }
}
