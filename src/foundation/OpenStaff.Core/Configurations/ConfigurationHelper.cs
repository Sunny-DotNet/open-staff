using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace OpenStaff.Configurations;

public class ConfigurationHelper
{
    public static ConfigurationProperty[] GetConfigurationProperty<T>()
    {
        var type = typeof(T);
        var properties = type.GetProperties().Where(x => x.CanWrite && x.CanRead);
        return properties.Select(MapTo).ToArray();
    }

    private static ConfigurationProperty MapTo(PropertyInfo property)
    {
        var propertyType = property.PropertyType;
        var propertyName = JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name);
        var context = new NullabilityInfoContext();
        var info = context.Create(property);
        var required = info.WriteState == NullabilityState.NotNull;
        var type = MapType(propertyType);
        return new(propertyName, type, Required: required);
    }

    private static ConfigurationPropertyType MapType(Type propertyType) => propertyType.FullName switch
    {
        "System.String" => ConfigurationPropertyType.String,
        "System.Boolean" => ConfigurationPropertyType.Boolean,
        "System.Int64" => ConfigurationPropertyType.Int64,
        "System.Double" => ConfigurationPropertyType.Double,
        _ => throw new NotSupportedException($"Unsupported property type: {propertyType.FullName}")
    };
}
