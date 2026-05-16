# NanoRoute

NanoRoute is a small, dependency-light router for `HttpRequestMessage` pipelines, with optional transport adapters and focused helpers for JSON payloads and error handling.

The core library is centered around `RouteScopeBuilder`, `Router`, and `RequestContext`, so you can plug the routing pipeline into your own transport or hosting model as well.

NanoRoute targets `netstandard2.0` and `netstandard2.1`, and is compatible with Native AOT scenarios.

For AWS Lambda integrations, use the separate `NanoRoute.AwsLambda` package.

## Quick Start

```csharp
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using NanoRoute;

HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddDefaultValueParsers()
    .AddJsonErrorDetails()
    .AddEndPoint("GET", "/api/users/{user_id:int}/details/", endpoint => endpoint
        .WithHandler(static async (context, _) =>
        {
            await Task.CompletedTask;

            return HttpResponseMessage.Json(new
            {
                id = context.Parameters["user_id"]
            });
        }))
    .CreateRouter();

HttpListener listener = new();
listener.Prefixes.Add("http://localhost:8080/");
listener.Start();

HttpListenerContext context = await listener.GetContextAsync();
await router.Route(context, new ServiceCollection().BuildServiceProvider());
```

`AddEndPoint()` is the recommended application-level entry point for most routes: it captures the HTTP verb and route pattern once, then endpoint helpers such as `WithHandler()` and `WithJsonBody()` add endpoint-local middleware without repeating the route.

`AddHandler()` is still available when you need lower-level pipeline composition, such as custom middleware chains or manually scoped prefix routes.

## At A Glance

- Exact route patterns start and end with `/`, for example `/items/`.
- Prefix route patterns start with `/` and end with `/*`, for example `/items/*`.
- `AddDefaultValueParsers()` registers the built-in `int`, `guid`, `bool`, and `str` parsers.
- `AddPrefix()` and `CreatePrefix()` define scoped route subtrees.
- `AddQueryBindings()` parses selected query-string values into `RequestContext.Parameters`.
- Typed handlers can bind route values, query values, services, `RequestContext`, and `CancellationToken` into request objects.
- `AddJsonErrorDetails()` turns routing failures into JSON `ErrorDetails` responses.
- `HttpResponseMessage.Json(...)` creates JSON responses with the library's serializer defaults.

## Documentation

Full package documentation and API reference are published at:

- <https://sholtee.github.io/nanoroute/docs/NanoRoute/>
