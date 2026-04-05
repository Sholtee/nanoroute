# NanoRoute ![Tests](https://sholtee.github.io/nanoroute/badges/tests-badge.svg) [![Coverage](https://sholtee.github.io/nanoroute/badges/coverage-badge.svg)](https://sholtee.github.io/nanoroute/CoverageReport/)

NanoRoute is a small, dependency-light router for `HttpRequestMessage` pipelines, with an optional `HttpListener` adapter and focused helpers for JSON payloads and error handling.

The core library is centered around `RouteBuilder`, `Router`, and `RequestContext`, so you can plug the routing pipeline into your own transport or hosting model as well.

## Quick Start

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using NanoRoute;
using NanoRoute.Json;

HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddParameterParser("int", static (string segment, out object? parsed) =>
    {
        bool success = int.TryParse(segment, out int value);
        parsed = success ? value : null;
        return success;
    })
    // Convert routing exceptions into JSON error responses.
    .AddJsonErrorDetails()
    .AddHandler("GET", "/api/users/{user_id:int}/", async (context, next) =>
    {
        context.Parameters["user"] = $"user-{context.Parameters["user_id"]}";
        return await next();
    })
    .AddHandler("GET", "/api/users/{user_id:int}/details", async (context, _) =>
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent((string) context.Parameters["user"]!)
        };
    })
    .CreateRouter();

HttpListener listener = new();
listener.Prefixes.Add("http://localhost:8080/");
listener.Start();

HttpListenerContext context = await listener.GetContextAsync();
await router.Route(context, new ServiceCollection().BuildServiceProvider());
```

In this example, `/api/users/{user_id:int}/` is a prefix route, so it runs before the more specific `/api/users/{user_id:int}/details` handler and can populate shared state in `RequestContext.Parameters`. `HttpListenerRouter` converts the incoming `HttpListenerContext` into an `HttpRequestMessage`, executes the NanoRoute pipeline, and copies the produced `HttpResponseMessage` back to the listener response.

## Advanced Usage

When several routes share the same prefix, `WithBase()` lets you define that prefix once and register child routes relative to it.

```csharp
RouterBuilder<HttpListenerRouter, HttpListenerRouterConfig> builder = HttpListenerRouter
    .CreateBuilder()
    // Register the built-in int/guid/bool/str route parsers.
    .AddDefaultParsers()
    // Convert routing exceptions into JSON error responses.
    .AddJsonErrorDetails();

builder.WithBase("/api/users/{user_id:int}/", users => users
    .AddHandler("GET", "/", async (context, next) =>
    {
        context.Parameters["user"] = $"user-{context.Parameters["user_id"]}";
        return await next();
    })
    .AddHandler("GET", "/details", async (context, _) =>
    {
        return HttpResponseMessage.Json(new
        {
            id = context.Parameters["user_id"],
            name = context.Parameters["user"]
        });
    }));

HttpListenerRouter router = builder.CreateRouter();
```

This produces the same effective routes as registering `/api/users/{user_id:int}/` and `/api/users/{user_id:int}/details` directly, but keeps repeated base patterns out of individual `AddHandler()` calls.

## Custom Routers

If `HttpListenerRouter` is not the transport you want, you can derive from `Router` and expose your own entry point that prepares an `HttpRequestMessage`, invokes `Handle()`, and deals with the returned `HttpResponseMessage`.

```csharp
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using NanoRoute;

public sealed class InMemoryRouter(RouterBuilder<InMemoryRouter, RouterConfig> builder) : Router(builder, builder.RouterConfig)
{
    public static RouterBuilder<InMemoryRouter, RouterConfig> CreateBuilder() =>
        new(static builder => new InMemoryRouter(builder));

    public Task<HttpResponseMessage> Route(HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation = default) =>
        Handle(request, services, cancellation);
}

InMemoryRouter router = InMemoryRouter
    .CreateBuilder()
    .AddHandler("GET", "/health", async (_, _) => new HttpResponseMessage())
    .CreateRouter();
```

This keeps the transport-specific concerns in your own router type while still reusing NanoRoute's matching, parameter parsing, and handler pipeline.

## Matching Rules

- A trailing `/` makes a route a prefix match.
- Without a trailing `/`, the route matches only the exact normalized path.
- Literal segments are matched case-insensitively.
- Parameter segments use registered parsers such as `{user_id:int}`.
- Percent-encoded parameter segments are decoded before the parser runs.
- When multiple handlers match, NanoRoute evaluates compatible handlers from shorter prefixes toward more specific matches.
- At the same path depth, literal segments are preferred over parameter matches by default, but `RouterConfig.MatchingBehavior` can change the precedence.

## Timeouts And Cancellation

- `RouterConfig.Timeout` defaults to `TimeSpan.FromMinutes(1)`.
- NanoRoute exposes a linked cancellation token to async parameter parsers and handlers through `ParameterParserContext.Cancellation` and `RequestContext.Cancellation`.
- That linked token is canceled when either the caller-provided cancellation token is canceled or the router timeout elapses.
- `OperationCanceledException` is not converted into an HTTP error by `AddExceptionHandler()` or `AddJsonErrorDetails()`. It propagates to the caller or transport adapter unchanged.
- `HttpListenerRouter.Route()` aborts the active `HttpListenerResponse` and then rethrows the cancellation exception.

## Common Building Blocks

- `HttpListenerRouter.CreateBuilder()` starts a strongly typed builder for `HttpListener` scenarios.
- `AddDefaultParsers()` registers the built-in `int`, `guid`, `bool`, and `str` parameter parsers.
- `WithBase("/prefix/")` creates a scoped child builder for a route subtree.
- `AddJsonBody<TBody>()` binds JSON request content into `RequestContext.Parameters`.
- `AddJsonErrorDetails()` turns routing exceptions into JSON `ErrorDetails` responses.
- `HttpResponseMessage.Json(...)` creates JSON responses with the library's serializer defaults.

## Core Types

- [RouteBuilder](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouteBuilder.html)
- [Router](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.Router.html)
- [RouterBuilder`2](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouterBuilder-2.html)
- [HttpListenerRouter](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.HttpListenerRouter.html)
- [RequestContext](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestContext.html)
- [ErrorDetails](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ErrorDetails.html)
- [ParameterParserDelegate](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ParameterParserDelegate.html)
- [RequestHandler](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestHandler.html)

## Documentation

API documentation is generated from the XML comments in the source and published at:

- <https://sholtee.github.io/nanoroute/docs/NanoRoute/>
