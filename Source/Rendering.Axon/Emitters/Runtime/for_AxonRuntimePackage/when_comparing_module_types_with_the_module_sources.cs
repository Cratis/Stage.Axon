// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimePackage;

public partial class when_comparing_module_types_with_the_module_sources : Specification
{
    [GeneratedRegex(@"(?m)^package (?<package>io\.cratis\.stage\.axon\.runtime(?:\.[\w.]+)?);", RegexOptions.None, 1000)]
    private static partial Regex Package { get; }

    [GeneratedRegex(@"(?m)^public (?:(?:abstract|final|sealed|non-sealed) )*(?:class|interface|record|enum) (?<type>\w+)\b", RegexOptions.None, 1000)]
    private static partial Regex PublicType { get; }
    static readonly IReadOnlySet<string> AutoConfigurations = new HashSet<string>(StringComparer.Ordinal)
    {
        "StageAxonRuntimeAutoConfiguration",
        "persistent.StageAxonPersistenceAutoConfiguration"
    };

    string _root;
    Dictionary<string, string> _sources;

    void Establish()
    {
        _root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Source/Runtime/src/main/java/io/cratis/stage/axon/runtime");
        _sources = Directory.GetFiles(_root, "*.java", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(_root, path)[..^5].Replace(Path.DirectorySeparatorChar, '.'), File.ReadAllText, StringComparer.Ordinal);
    }

    [Fact] void should_have_a_source_file_for_every_module_type() => AxonRuntimePackage.ModuleTypes.All(name => File.Exists(Path.Combine(_root, name.Replace('.', Path.DirectorySeparatorChar) + ".java"))).ShouldBeTrue();
    [Fact] void should_declare_the_matching_package() => AxonRuntimePackage.ModuleTypes.All(name => Package.Match(_sources[name]).Groups["package"].Value == AxonRuntimePackage.ModulePackage + (name.Contains('.') ? "." + name[..name.LastIndexOf('.')] : "")).ShouldBeTrue();
    [Fact] void should_declare_every_module_type_as_public() => AxonRuntimePackage.ModuleTypes.All(name => PublicType.Matches(_sources[name]).Any(match => match.Groups["type"].Value == name[(name.LastIndexOf('.') + 1)..])).ShouldBeTrue();
    [Fact] void should_publish_every_public_top_level_type_except_the_auto_configurations() => _sources.Values.SelectMany(source => PublicType.Matches(source).Select(type => (Package.Match(source).Groups["package"].Value + "." + type.Groups["type"].Value)[(AxonRuntimePackage.ModulePackage.Length + 1)..])).All(name => AxonRuntimePackage.ModuleTypes.Contains(name) || AutoConfigurations.Contains(name)).ShouldBeTrue();
}
