// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Admission;

internal static partial class AxonRefusalCodes
{
    internal const string KeyParts = "STAGE-AXON-0402";
    internal const string CompositeKey = "STAGE-AXON-0305";
    internal const string QueryFilter = "STAGE-AXON-0403";
    internal const string Reduction = "STAGE-AXON-0306";
    internal const string ProjectionMapping = "STAGE-AXON-0307";
    internal const string UnwrittenReadModel = "STAGE-AXON-0309";
    internal const string ScopedProjection = "STAGE-AXON-0310";

    /// <summary>Views are eventually consistent because they are applied by a pooled streaming processor; informational, never a refusal.</summary>
    internal const string EventuallyConsistentViews = "STAGE-AXON-0311";
    internal const string ObservableQuery = "STAGE-AXON-0401";
    internal const string QueryPerformer = "STAGE-AXON-0401";
    internal const string QueryAuthorization = "STAGE-AXON-0404";
    internal const string ViewSpecification = "STAGE-AXON-0501";
}
