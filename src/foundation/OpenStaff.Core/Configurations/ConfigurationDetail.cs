using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace OpenStaff.Configurations;


public enum ConfigurationPropertyType
{
    String,
    Boolean,
    Int64,
    Double
}

public record struct ConfigurationProperty(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] ConfigurationPropertyType Type,
    [property: JsonPropertyName("default_value")] object? DefaultValue = null,
    [property: JsonPropertyName("required")] bool Required = false);

public record struct GetConfigurationResult<TConfiguration>(
    [property: JsonPropertyName("properties")] ConfigurationProperty[] Properties,
    [property: JsonPropertyName("configuration")] TConfiguration Configuration);
