// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Cratis.Stage.Rendering.Axon.Verification;

if (args.Length != 2 || args[0] is not ("render" or "verify"))
{
    throw new NativeVerificationFailed("Usage: render|verify <generated-corpus-directory>; run from the repository root.");
}

var repository = Directory.GetCurrentDirectory();
var output = Path.GetFullPath(args[1]);
if (args[0] == "render")
{
    await CorpusRenderer.Render(repository, output);
}
else
{
    var budget = JsonSerializer.Deserialize<Dictionary<string, DisabledTestBudget>>(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "disabled-tests.json")))!;
    NativeTestResults.Verify(repository, output, budget);
}
