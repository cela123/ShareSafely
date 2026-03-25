// Filters/FormFileSchemaFilter.cs
using Microsoft.OpenApi.Models;
using ShareSafely.API.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ShareSafely.API.Filters;

public class FormFileSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        // Target only our UploadRequest DTO, not every type
        if (context.Type != typeof(UploadRequest)) return;

        // Replace the auto-generated schema wholesale
        schema.Type = "object";
        schema.Properties = new Dictionary<string, OpenApiSchema>
        {
            ["file"] = new()
            {
                Type = "string",
                Format = "binary",
                Description = "The file to upload"
            },
            ["expiryHours"] = new()
            {
                Type = "integer",
                Format = "int32",
                Default = new Microsoft.OpenApi.Any.OpenApiInteger(24),
                Description = "Expiry in hours (1–168)"
            }
        };
        schema.Required = new HashSet<string> { "file" };
    }
}