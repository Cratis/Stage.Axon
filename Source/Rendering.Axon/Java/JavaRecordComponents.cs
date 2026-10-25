// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Java;

internal static class JavaRecordComponents
{
    internal static string JsonProperty(string modelName) => JavaNames.Member(modelName) == modelName
        ? string.Empty
        : $"@com.fasterxml.jackson.annotation.JsonProperty({JavaCodeBuilder.StringLiteral(modelName)}) ";
}
