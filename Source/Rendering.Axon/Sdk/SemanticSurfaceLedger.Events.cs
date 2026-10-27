// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Emitters.Events;

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    static void ClassifyEvents(LedgerBuilder rows)
    {
        rows.Rendered("The revision's numeric value supplies the generated event version annotation.", "EventContractRevision.Value", typeof(AxonEventsEmitter));
        rows.Rendered("The route's source identity resolves the concept used for the primary event-source tag.", "SemanticEventSource.Id SemanticEventSource.IdentifierType", typeof(AxonEventSources));
        rows.Rendered("Contract identity, Java name and stored wire name select the emitted event artifact and event annotation.", "SemanticEventContract.Id SemanticEventContract.Name SemanticEventContract.StoredName", typeof(AxonEventsEmitter));
        rows.Rendered("Current event properties supply the generated event record components.", "SemanticEventContract.Properties", typeof(AxonEventSources));
        rows.Rendered("Events are private by default: emission generates a local Axon event without public publication; public contracts separately degrade with STAGE-AXON-0601.", "SemanticEventVisibility.Private", typeof(AxonEventsEmitter));
        rows.Warned(["STAGE-AXON-ROUTING"], "STAGE-AXON-ROUTING: declared stream partitions and their component types have no runtime implementation. A command that declares a route reports the omitted routing semantics; an unused stream declaration alone does not guarantee a diagnostic.", "SemanticEventSource.Streams SemanticStreamIdPart.Type");
        rows.Warned(["STAGE-AXON-EVOLUTION"], "STAGE-AXON-EVOLUTION: a nonempty prior-revision list causes event emission to report the missing upcaster. Historical revision fields are not emitted or interpreted as an upcaster.", "SemanticEventRevision.Predecessor SemanticEventRevision.Revision SemanticEventRevision.Properties SemanticEventRevision.Tags");
        rows.Ignored(IgnoredReason.Identity, "Source, stream and stream-part declaration labels and identifiers are identity metadata; only the resolved source identifier concept supplies the emitted primary tag.", "SemanticEventSource.Name SemanticEventStream.Id SemanticEventStream.Name SemanticStreamIdPart.Name");
    }
}
