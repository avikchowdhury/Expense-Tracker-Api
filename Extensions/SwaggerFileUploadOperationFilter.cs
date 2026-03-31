using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ExpenseTracker.Api.Extensions
{
    public class SwaggerFileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var fileParameters = context.MethodInfo.GetParameters()
                .Where(p =>
                    p.ParameterType == typeof(Microsoft.AspNetCore.Http.IFormFile) ||
                    p.ParameterType == typeof(IEnumerable<Microsoft.AspNetCore.Http.IFormFile>) ||
                    (p.GetCustomAttribute<FromFormAttribute>() != null &&
                     p.ParameterType == typeof(Microsoft.AspNetCore.Http.IFormFile)))
                .ToList();

            if (!fileParameters.Any())
            {
                return;
            }

            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = fileParameters.ToDictionary(
                                p => p.Name!,
                                p => new OpenApiSchema
                                {
                                    Type = "string",
                                    Format = "binary"
                                }),
                            Required = new HashSet<string>(fileParameters.Select(p => p.Name!))
                        }
                    }
                }
            };
        }
    }
}
