using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Logging.Middlewares
{
    public class RequestResponseLoggerMiddleware : IMiddleware
    {
        private readonly bool _isRequestResponseLoggingEnabled;

        public RequestResponseLoggerMiddleware(IConfiguration config)
        {
            _isRequestResponseLoggingEnabled = config.GetValue<bool>("EnableRequestResponseLogging", false);
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {

            // Middleware is enabled only when the EnableRequestResponseLogging config value is set.
            if (_isRequestResponseLoggingEnabled)
            {
                Console.WriteLine($"HTTP request information:\n" +
                    $"\tMethod: {context.Request.Method}\n" +
                    $"\tPath: {context.Request.Path}\n" +
                    $"\tQueryString: {context.Request.QueryString}\n" +
                    $"\tHeaders: {FormatHeaders(context.Request.Headers)}\n" +
                    $"\tSchema: {context.Request.Scheme}\n" +
                    $"\tHost: {context.Request.Host}\n" +
                    $"\tBody: {await ReadBodyFromRequest(context.Request)}");

                // Temporarily replace the HttpResponseStream, which is a write-only stream, with a MemoryStream to capture it's value in-flight.
                var originalResponseBody = context.Response.Body;
                using var newResponseBody = new MemoryStream();
                context.Response.Body = newResponseBody;

                // Call the next middleware in the pipeline
                await next(context);

                newResponseBody.Seek(0, SeekOrigin.Begin);
                var responseBodyText = await new StreamReader(context.Response.Body).ReadToEndAsync();

                Console.WriteLine($"HTTP request information:\n" +
                    $"\tStatusCode: {context.Response.StatusCode}\n" +
                    $"\tContentType: {context.Response.ContentType}\n" +
                    $"\tHeaders: {FormatHeaders(context.Response.Headers)}\n" +
                    $"\tBody: {responseBodyText}");

                newResponseBody.Seek(0, SeekOrigin.Begin);
                await newResponseBody.CopyToAsync(originalResponseBody);
            }
            else
            {
                await next(context);
            }
        }

        private static string FormatHeaders(IHeaderDictionary headers) => string.Join(", ", headers.Select(kvp => $"{{{kvp.Key}: {string.Join(", ", kvp.Value)}}}"));

        private static async Task<string> ReadBodyFromRequest(HttpRequest request)
        {
            // Ensure the request's body can be read multiple times (for the next middlewares in the pipeline).
            request.EnableBuffering();

            using var streamReader = new StreamReader(request.Body, leaveOpen: true);
            var requestBody = await streamReader.ReadToEndAsync();

            // Reset the request's body stream position for next middleware in the pipeline.
            request.Body.Position = 0;
            return requestBody;
        }


    }
}
