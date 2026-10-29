// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters;

internal static partial class UnsupportedSummary
{
    static Dictionary<SemanticId, string> Locations(SemanticApplication application)
    {
        var locations = new Dictionary<SemanticId, string>();
        try
        {
            locations.TryAdd(application.Id, application.Name + " application");
            foreach (var concept in Items(application.Concepts)) locations.TryAdd(concept.Id, application.Name + " / " + concept.Name + " concept");
            foreach (var module in Items(application.Modules))
            {
                locations.TryAdd(module.Id, module.Name);
                foreach (var feature in Items(module.Features)) AddFeature(locations, feature, [module.Name]);
            }
            foreach (var located in ScopeSelection.Locate(application)) AddSlice(locations, located);
        }
        catch (Exception)
        {
            // Malformed or partial models still get a summary, using ids for unmapped constructs.
            return locations;
        }
        return locations;
    }

    static void AddFeature(Dictionary<SemanticId, string> locations, SemanticFeature feature, IReadOnlyList<string> ancestors)
    {
        string[] path = [.. ancestors, feature.Name];
        locations.TryAdd(feature.Id, Path(path));
        foreach (var child in Items(feature.Features)) AddFeature(locations, child, path);
    }

    static void AddSlice(Dictionary<SemanticId, string> locations, LocatedSemanticSlice located)
    {
        var path = Path(located.Path);
        var slice = located.Slice;
        locations.TryAdd(slice.Id, path + " slice");
        AddMessages(locations, located, path);
        AddViews(locations, located, path);
        foreach (var item in Items(slice.Captures)) locations.TryAdd(item.Id, path + " / " + item.Name + " capture");
        foreach (var item in Items(slice.Specifications)) locations.TryAdd(item.Id, path + " / " + item.Name + " specification");
    }

    static void AddMessages(Dictionary<SemanticId, string> locations, LocatedSemanticSlice located, string path)
    {
        var slice = located.Slice;
        foreach (var item in Items(slice.Commands)) locations.TryAdd(item.Id, path + " / " + item.Name + " command");
        foreach (var item in Items(slice.Events)) locations.TryAdd(item.Id, path + " / " + item.Name + " event");
        foreach (var item in Items(slice.Queries)) locations.TryAdd(item.Id, path + " / " + item.Name + " query");
        foreach (var item in Items(slice.Reactions)) locations.TryAdd(item.Id, path + " / " + item.Name + " reaction");
    }

    static void AddViews(Dictionary<SemanticId, string> locations, LocatedSemanticSlice located, string path)
    {
        foreach (var item in Items(located.Slice.ReadModels)) locations.TryAdd(item.Id, path + " / " + item.Name + " read model");
        foreach (var item in Items(located.Slice.Projections)) locations.TryAdd(item.Id, path + " / " + item.Name + " projection");
    }

    static ImmutableArray<T> Items<T>(ImmutableArray<T> items) => items.IsDefault ? [] : items;

    static string Path(IReadOnlyList<string> parts) => string.Join(" / ", parts.Where((part, index) => index == 0 || !part.Equals(parts[index - 1], StringComparison.OrdinalIgnoreCase)));
}
