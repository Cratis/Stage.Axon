// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime;

/// <summary>
/// Sole owner of generated hand-written runtime building blocks, all in &lt;basePackage&gt;.runtime:
/// value-type bases, Jackson converters, Caller, validation and ProblemDetails, read/unique stores,
/// their in-memory implementations, Clock, SSE bridge and test support. Other families consume these
/// seams; they never emit their own copies. This package can later be replaced by a JVM dependency.
/// </summary>
internal sealed class AxonRuntimeEmitter : IAxonEmitter;
