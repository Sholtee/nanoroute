# NanoRoute.HttpListener

NanoRoute.HttpListener adds a `System.Net.HttpListener` adapter for NanoRoute while keeping the core package transport-neutral.

The package converts incoming `HttpListenerContext` requests into `HttpRequestMessage` instances, runs the normal NanoRoute pipeline, and writes the produced `HttpResponseMessage` back to the active `HttpListenerResponse`.

NanoRoute.HttpListener targets `netstandard2.0` and `netstandard2.1`.

## Quick Start

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using NanoRoute;
using NanoRoute.HttpListener;

IServiceProvider services = new ServiceCollection().BuildServiceProvider();

HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddJsonErrorDetails()
    .AddEndpoint("GET", "/health/", endpoint => endpoint
        .WithHandler(static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("ok")
        })))
    .CreateRouter();

SimpleHttpListenerHost host = new
(
    "http://localhost:8080/",
    workerCount: 4,
    queueCapacity: 64,
    router,
    services
);

host.RunUntilCancelKeyPress();
```

`HttpListenerRouter.CreateBuilder()` returns the same strongly typed NanoRoute builder style as the core package. Register value parsers, query bindings, JSON body binders, typed handlers, endpoint builders, prefixes, and handlers in the builder, then call `CreateRouter()` once and reuse the router for accepted listener contexts.

`SimpleHttpListenerHost` owns the listener loop, creates a service scope per request, and stops gracefully on Ctrl+C. If you need custom accept loops, concurrency, or shutdown behavior, call `HttpListenerRouter.Route()` from your own `HttpListener` loop instead.

Prefer endpoint builders such as `AddEndpoint()` for application routes. Typed handlers and endpoint helpers such as `WithJsonBody()` keep route values, JSON bodies, services, and framework values in request objects. `AddHandler()` is still available for lower-level middleware composition and custom pipelines.

`CreateBuilder()` does not add JSON error handling automatically. Add `AddJsonErrorDetails()` yourself when you want structured JSON errors, or omit it when you want custom exception handling middleware or response shaping handlers to own failures.

## Core Types

- [HttpListenerRouter](https://sholtee.github.io/nanoroute/docs/NanoRoute.HttpListener/NanoRoute.HttpListener.HttpListenerRouter.html)
- [HttpListenerRouterConfig](https://sholtee.github.io/nanoroute/docs/NanoRoute.HttpListener/NanoRoute.HttpListener.HttpListenerRouterConfig.html)
- [SimpleHttpListenerHost](https://sholtee.github.io/nanoroute/docs/NanoRoute.HttpListener/NanoRoute.HttpListener.SimpleHttpListenerHost.html)

## Routing

The HttpListener adapter uses the same route matching and handler pipeline as NanoRoute.

```csharp
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using NanoRoute;
using NanoRoute.HttpListener;

HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddJsonErrorDetails()
    .AddDefaultValueParsers()
    .AddPrefix("/api/items/*", items => items
        .AddEndpoint("GET", RouteScopeBuilder.CurrentExact, endpoint => endpoint
            .WithQueryBindings("{filter?:str(min=3)}")
            .WithHandler(static async (context, _) =>
            {
                await Task.CompletedTask;

                return HttpResponseMessage.Json(new
                {
                    filter = context.Parameters.TryGetValue("filter", out object? filter) ? filter : null
                });
            }))
        .AddEndpoint("GET", "/{id:int(min=1)}/", endpoint => endpoint
            .WithHandler(static async (context, _) =>
            {
                await Task.CompletedTask;

                return HttpResponseMessage.Json(new
                {
                    id = context.Parameters["id"]
                });
            })))
    .CreateRouter();
```

## Request Mapping

`HttpListenerRouter` converts the listener request into an `HttpRequestMessage` before executing the NanoRoute pipeline.

- The listener request URL becomes the `HttpRequestMessage.RequestUri`.
- The listener HTTP method is normalized through `HttpMethod.For(...)`.
- Request headers are copied onto the `HttpRequestMessage` or its content headers.
- Requests with an entity body expose the listener input stream as `StreamContent`.
- The original `HttpListenerRequest` is available through the NanoRoute request context as the original request object.
- The listener request trace identifier is used as the NanoRoute trace id.

## Response Mapping

NanoRoute handlers return ordinary `HttpResponseMessage` instances.

- The response status code is copied to the `HttpListenerResponse`.
- Response headers are copied unless `HttpListenerResponse` manages that header directly.
- Content headers are copied after the response content is available.
- `Content-Type` and seekable content length are set on the listener response when available.
- Response content is streamed to the listener output stream before the listener response is closed.

## Hosting

Use `HttpListenerRouter.Route()` from your own listener loop when you need full control over accept loops, concurrency, and shutdown.

For simple local hosts, `SimpleHttpListenerHost` wraps a prefix-based `HttpListener`, a bounded worker queue, and per-request dependency-injection scopes:

```csharp
using System;

using Microsoft.Extensions.DependencyInjection;
using NanoRoute.HttpListener;

IServiceProvider services = new ServiceCollection()
    .AddSingleton<IUserRepository, UserRepository>()
    .BuildServiceProvider();

HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddJsonErrorDetails()
    .CreateRouter();

SimpleHttpListenerHost host = new
(
    "http://localhost:8080/",
    workerCount: 4,
    queueCapacity: 64,
    router,
    services
);

host.RunUntilCancelKeyPress();
```

`SimpleHttpListenerHost` aborts newly accepted responses when the worker queue is full. It creates a service scope for each accepted request, so pass a root service provider that supports `CreateScope()`.

## Cancellation

- NanoRoute exposes the caller-provided cancellation token to async value parsers and handlers through `ValueParserContext.Cancellation` and `RequestContext.Cancellation`.
- `OperationCanceledException` is not converted into an HTTP error by `AddExceptionHandler()` or `AddJsonErrorDetails()`. It propagates to the caller or transport adapter unchanged.
- `HttpListenerRouter.Route()` aborts the active `HttpListenerResponse` and then rethrows the cancellation exception.
- `SimpleHttpListenerHost.Run()` stops accepting new requests when its cancellation token is cancelled.

## Common Building Blocks

- `HttpListenerRouter.CreateBuilder()` starts a strongly typed builder for `HttpListener` scenarios.
- `HttpListenerRouter` derives from the core `RouterBase<HttpListenerRouterConfig>` helper.
- `HttpListenerRouterConfig` inherits the core `RouterConfig`, including `MatchingPrecedence`.
- `CreateRouter(config => ...)` customizes `HttpListenerRouterConfig` while creating a router snapshot.
- `Route(HttpListenerContext, IServiceProvider, CancellationToken)` executes the NanoRoute pipeline and writes the listener response.
- `SimpleHttpListenerHost` can run a small prefix-based host with bounded request queueing and scoped services.
- `AddDefaultValueParsers()` registers the built-in `int`, `guid`, `bool`, `str`, and `regex` route parsers.
- `AddQueryBindings()` and `EndpointBuilder.WithQueryBindings()` bind selected query-string values into `RequestContext.Parameters`.
- `AddEndpoint()` and `CreateEndpoint()` capture endpoint verbs and route patterns once; endpoint helpers such as `WithHandler()`, `WithJsonBody()`, and `WithQueryBindings()` work under `HttpListener` the same way they do in the core package.
- `AddJsonBody()` and `EndpointBuilder.WithJsonBody()` bind JSON request content into `RequestContext.Parameters`.
- `AddJsonErrorDetails()` turns routing exceptions into JSON `ErrorDetails` responses when explicitly added.
- `AddHandler<TRequest>()` and `EndpointBuilder.WithHandler<TRequest>()` project `RequestContext` into a typed request object before invoking the handler.
- `HttpResponseMessage.Json(...)` creates JSON responses with the library's serializer defaults.
