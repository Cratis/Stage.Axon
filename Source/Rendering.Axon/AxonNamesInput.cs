// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon;

internal static partial class AxonNamesInput
{
    internal const string Name = "axon-names";

    static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [GeneratedRegex("\\A[A-Za-z0-9_.-]+\\z", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex ArtifactIds { get; }

    internal static ArtifactRenderInput Create(string? javaPackage, string? projectName)
    {
        if (javaPackage is null && projectName is null)
        {
            throw new InvalidArtifactRenderContract("An Axon names input requires a Java package or project name.");
        }

        return ArtifactRenderInput.Create(Name, "1", [.. JsonSerializer.SerializeToUtf8Bytes(new Payload(javaPackage, projectName), _jsonOptions)]);
    }

    internal static bool TryRead(ArtifactRenderProfile profile, out AxonNames? names)
    {
        names = null;
        var inputs = profile.Inputs.Where(input => input.Name == Name).ToArray();
        if (inputs.Length == 0) return true;
        if (inputs.Length != 1 || inputs[0].Version != "1") return false;

        try
        {
            var read = JsonSerializer.Deserialize<Payload>(new UTF8Encoding(false, true).GetString(inputs[0].Bytes.AsSpan()), _jsonOptions);
            if (read is null or { JavaPackage: null, ProjectName: null }) return false;
            var canonical = Create(read.JavaPackage, read.ProjectName);
            if (!canonical.Bytes.SequenceEqual(inputs[0].Bytes)) return false;
            names = new(read.JavaPackage, read.ProjectName);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            return false;
        }
    }

    internal static string Normalize(string javaPackage)
    {
        var segments = javaPackage.Split('.');
        return segments.Any(segment => segment.Length == 0) ? javaPackage : string.Join('.', segments.Select(JavaNames.PackageSegment));
    }

    internal static bool ValidPackage(string package) => package.Length > 0 &&
        package.Split('.').All(segment => segment.Length > 0 && JavaNames.PackageSegment(segment) == segment) &&
        !JavaCodeBuilder.IsFramework(package + ".");

    internal static bool ValidArtifactId(string artifactId) => ArtifactIds.IsMatch(artifactId);

    internal static string BasePackage(ArtifactRenderRequest request) =>
        TryRead(request.Profile, out var names) && names?.JavaPackage is { } package && ValidPackage(package)
            ? package : JavaNames.PackageSegment(request.Model.Application.Name);

    internal static string ArtifactId(ArtifactRenderRequest request, string basePackage) =>
        TryRead(request.Profile, out var names) && names?.ProjectName is { } projectName && ValidArtifactId(projectName)
            ? projectName : basePackage + "-application";

    internal sealed record AxonNames(string? JavaPackage, string? ProjectName);

    sealed record Payload(string? JavaPackage, string? ProjectName);
}
