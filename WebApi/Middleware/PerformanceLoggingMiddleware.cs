using System.Diagnostics;

namespace WebApi.Middleware
{
    /// <summary>
    /// DotNet CQRS & Performance Behavior Middleware:
    /// Measures HTTP request execution time, appends Server-Timing header,
    /// and logs a structured warning if a request exceeds the SLA threshold (>500ms).
    /// </summary>
    public class PerformanceLoggingMiddleware
    {
        private const int SlaThresholdMs = 500;
        private readonly RequestDelegate _next;
        private readonly ILogger<PerformanceLoggingMiddleware> _logger;

        public PerformanceLoggingMiddleware(RequestDelegate next, ILogger<PerformanceLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            context.Response.OnStarting(() =>
            {
                stopwatch.Stop();
                var elapsedMs = stopwatch.ElapsedMilliseconds;

                // Client-side and APM diagnostic header
                if (!context.Response.Headers.ContainsKey("Server-Timing"))
                {
                    context.Response.Headers.Append("Server-Timing", $"total;dur={elapsedMs}");
                }

                if (elapsedMs > SlaThresholdMs)
                {
                    _logger.LogWarning(
                        "[PERFORMANCE SLA WARNING] Long Running Request: {Method} {Path} responded in {ElapsedMs} ms (Status: {StatusCode})",
                        context.Request.Method,
                        context.Request.Path,
                        elapsedMs,
                        context.Response.StatusCode);
                }

                return Task.CompletedTask;
            });

            await _next(context);
        }
    }

    public static class PerformanceLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UsePerformanceLogging(this IApplicationBuilder app)
        {
            return app.UseMiddleware<PerformanceLoggingMiddleware>();
        }
    }
}
