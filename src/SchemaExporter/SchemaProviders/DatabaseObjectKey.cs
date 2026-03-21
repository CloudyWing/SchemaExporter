#nullable enable

namespace CloudyWing.SchemaExporter.SchemaProviders;

public readonly record struct DatabaseObjectKey(string SchemaName, string ObjectName, string ObjectType);
