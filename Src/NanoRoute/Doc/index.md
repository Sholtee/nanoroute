# NanoRoute

NanoRoute is a small, dependency-light router for `HttpRequestMessage` pipelines, with optional transport adapters and focused helpers for JSON payloads and error handling.

The core library includes `HttpMessageRouter` for already materialized `HttpRequestMessage` requests and `HttpListenerRouter` for listener-hosted requests. `RouterBase<TConfig>`, `RouteScopeBuilder`, and `RequestContext` remain available when you want to plug the routing pipeline into your own transport or hosting model.

NanoRoute targets `netstandard2.0` and `netstandard2.1`, and is compatible with Native AOT scenarios.

For AWS Lambda integrations, use the separate `NanoRoute.AwsLambda` package.

## Quick Start

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using NanoRoute;

// UserRepository is your application service that implements IUserRepository.
IServiceProvider services = new ServiceCollection()
    .AddSingleton<IUserRepository, UserRepository>()
    .BuildServiceProvider();

HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddDefaultValueParsers()
    .AddJsonErrorDetails()
    .AddEndpoint("GET", "/api/users/{user_id:int}/", endpoint => endpoint
        .WithHandler(static async (GetUserRequest request) =>
        {
            return HttpResponseMessage.Json(HttpStatusCode.OK, new UserResponse
            {
                Id = request.UserId,
                Name = await request.Users.GetNameAsync(request.UserId)
            });
        }))
    .AddEndpoint("POST", "/api/users/", endpoint => endpoint
        .WithJsonBody<CreateUserBody>(nameof(CreateUserRequest.Body))
        .WithHandler(static async (CreateUserRequest request) =>
        {
            int userId = await request.Users.CreateAsync(request.Body.Name);

            return HttpResponseMessage.Json(HttpStatusCode.Created, new UserResponse
            {
                Id = userId,
                Name = request.Body.Name
            });
        }))
    .CreateRouter();

HttpListener listener = new();
listener.Prefixes.Add("http://localhost:8080/");
listener.Start();

HttpListenerContext context = await listener.GetContextAsync();
await router.Route(context, services);

public sealed class GetUserRequest
{
    [ValueSource(ValueSource.Parameter, Name = "user_id")]
    public int UserId { get; set; }

    [ValueSource(ValueSource.ServiceLocator)]
    public IUserRepository Users { get; set; } = null!;
}

public sealed class CreateUserRequest
{
    public CreateUserBody Body { get; set; } = null!;

    [ValueSource(ValueSource.ServiceLocator)]
    public IUserRepository Users { get; set; } = null!;
}

public sealed class CreateUserBody
{
    public string Name { get; set; } = string.Empty;
}

public sealed class UserResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public interface IUserRepository
{
    Task<int> CreateAsync(string name);

    Task<string> GetNameAsync(int userId);
}
```

`AddEndpoint()` is the recommended application-level entry point for most routes. It captures an HTTP verb or verbs and a route pattern once, then endpoint helpers such as `WithHandler()` and `WithJsonBody()` add endpoint-local middleware without repeating the route. Typed handlers bind route values, JSON bodies, services, and framework values into request objects before your handler runs.

`AddHandler()` remains available as the lower-level pipeline primitive for custom middleware composition, prefix pipelines, and extension authors.

## Core Types

- [RouteScopeBuilder](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouteScopeBuilder.html)
- [ExceptionHandlingOptions](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ExceptionHandlingOptions.html)
- [JsonErrorDetailsOptions](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.JsonErrorDetailsOptions.html)
- [RouterConfig](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouterConfig.html)
- [RouterBase`1](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouterBase-1.html)
- [RouterBuilder`2](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouterBuilder-2.html)
- [EndpointBuilder](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.EndpointBuilder.html)
- [HttpMessageRouter](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.HttpMessageRouter.html)
- [HttpListenerRouter](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.HttpListenerRouter.html)
- [RequestContext](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestContext.html)
- [UnexpectedParameterBehavior](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.UnexpectedParameterBehavior.html)
- [ErrorDetails](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ErrorDetails.html)
- [ValueParserDelegate](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ValueParserDelegate.html)
- [RequestHandlerDelegate](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestHandlerDelegate.html)
- [HttpMethodExtensions](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.HttpMethodExtensions.html)
- [NanoRouteHandlerExtensions](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.NanoRouteHandlerExtensions.html)
- [NanoRouteEndpointExtensions](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.NanoRouteEndpointExtensions.html)
- [NanoRoutePrefixExtensions](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.NanoRoutePrefixExtensions.html)
- [ValueSource](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ValueSource.html)
- [ValueSourceAttribute](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ValueSourceAttribute.html)

## Matching Rules

- Exact route patterns must start and end with `/`, for example `/items/`.
- Prefix route patterns must start with `/` and end with `/*`, for example `/items/*`.
- `/items` is invalid as a route pattern; use `/items/` for an exact route or `/items/*` for a prefix route.
- Repeated `/` separators in route patterns, such as `//` or `/items//details`, are invalid.
- Literal segments are matched case-insensitively.
- Parser-backed segments use registered parsers such as `{user_id:int}`, `{int}`, or `{slug:str(min=3,max=32)}`.
- The parameter name is optional. Segments like `{int}` still validate the path but do not add an entry to `RequestContext.Parameters`.
- When multiple handlers match within the selected route branch, NanoRoute evaluates compatible handlers from shorter prefixes toward more specific matches.
- `RequestContext.RemainingPath` is updated for each matched handler. Prefix handlers receive the unmatched path tail with its leading `/`, exact handlers receive an empty value when no path remains, and query strings are not included.
- At the same path depth, `RouterConfig.MatchingPrecedence` decides whether literal or parameterized child segments are selected first.
- Once NanoRoute selects a child branch at a given depth, it does not return to sibling branches later in the pipeline.

## Router Configuration

`RouterConfig` controls runtime behavior for one router snapshot. Pass a configuration callback to `CreateRouter(...)` when that snapshot should use non-default matching behavior.

```csharp
HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddDefaultValueParsers()
    .AddEndpoint("GET", "/items/{slug:str}/", endpoint => endpoint
        .WithHandler(static async (context, _) =>
        {
            await Task.CompletedTask;
            return HttpResponseMessage.Json(new { slug = context.Parameters["slug"] });
        }))
    .CreateRouter(config =>
    {
        config.MatchingPrecedence = MatchingPrecedence.ParameterizedFirst;
    });
```

Created routers are immutable snapshots: later route changes on the builder and later `CreateRouter(...)` configuration callbacks do not affect routers that have already been created.

## Prefix Scopes

When several routes share the same prefix, `AddPrefix()` lets you define that prefix once and register child routes relative to it. If you want to hold onto a child `RouteScopeBuilder` and add routes incrementally, use `CreatePrefix()`.

```csharp
RouterBuilder<HttpListenerRouter, HttpListenerRouterConfig> builder = HttpListenerRouter
    .CreateBuilder()
    .AddDefaultValueParsers()
    .AddJsonErrorDetails();

builder.AddPrefix("/api/users/{user_id:int}/*", users => users
    .AddHandler("GET", "/*", async (context, next) =>
    {
        context.Parameters["user"] = $"user-{context.Parameters["user_id"]}";
        return await next();
    })
    .AddEndpoint("GET", "/details/", endpoint => endpoint
        .WithHandler(static async (context, _) =>
        {
            await Task.CompletedTask;

            return HttpResponseMessage.Json(new
            {
                id = context.Parameters["user_id"],
                name = context.Parameters["user"]
            });
        })));

HttpListenerRouter router = builder.CreateRouter();
```

This produces the same effective routes as registering `/api/users/{user_id:int}/*` and `/api/users/{user_id:int}/details/` directly, but keeps repeated base patterns out of endpoint registrations.

Inside prefix middleware, `context.RemainingPath` exposes the current request path tail that has not been matched by that handler's route pattern. For `/api/users/{user_id:int}/*` handling `/api/users/42/details`, the value is `/details`; the final `/details/` endpoint sees an empty value.

## Endpoint Builders

`AddEndpoint()` and `CreateEndpoint()` capture an endpoint's HTTP verb or verbs and exact or prefix route pattern once. Endpoint-aware helpers such as `WithHandler()`, `WithJsonBody()`, and `WithQueryBindings()` then register middleware for that captured endpoint without repeating the route.

```csharp
public sealed class CreateItemRequest
{
    public string Name { get; set; } = string.Empty;
}

HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddJsonErrorDetails()
    .AddDefaultValueParsers()
    .AddEndpoint("POST", "/items/{id:int}/", endpoint => endpoint
        .WithQueryBindings("{source:str}")
        .WithJsonBody<CreateItemRequest>("body")
        .WithHandler(static async (context, _) =>
        {
            await Task.CompletedTask;

            return HttpResponseMessage.Json(HttpStatusCode.Created, new
            {
                id = context.Parameters["id"],
                source = context.Parameters["source"],
                body = context.Parameters["body"]
            });
        }))
    .CreateRouter();
```

Endpoint builders are useful when several pieces of endpoint-local middleware need the same verbs and pattern. Multiple `WithHandler()` calls run in registration order, and each handler can call the supplied `next` delegate to continue the endpoint pipeline. `WithQueryBindings()` uses the endpoint's captured verbs and match kind, so query parsing stays local to that endpoint. `CreateEndpoint()` returns an `EndpointBuilder` when you want to configure an endpoint incrementally, and `EndpointBuilder.Prefix` exposes the endpoint's scoped route builder for endpoint-aware extensions that need the lower-level builder surface.

## Value Parsers

NanoRoute supports both synchronous and asynchronous value parsers:

- `AddValueParser("name", SyncValueParserDelegate)` for lightweight synchronous parsing.
- `AddValueParser("name", ValueParserDelegate)` when parsing needs request services or async work.
- `AddValueParser("name", BindArgumentsDelegate, ...)` when the route template includes parser arguments such as `{id:int(min=1)}`.

`BindArgumentsDelegate` receives the raw parser arguments as a case-insensitive dictionary and can turn them into any cached object. That object is then exposed as `ValueParserContext.Arguments` for async parsers or as the `arguments` parameter for sync parsers.

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using NanoRoute;

RouterBuilder<HttpListenerRouter, HttpListenerRouterConfig> builder = HttpListenerRouter
    .CreateBuilder()
    .AddValueParser
    (
        "int",
        static (IReadOnlyDictionary<string, string> rawArgs) => (
            Min: rawArgs.TryGetValue("min", out string? min) ? int.Parse(min, CultureInfo.InvariantCulture) : null,
            Max: rawArgs.TryGetValue("max", out string? max) ? int.Parse(max, CultureInfo.InvariantCulture) : null
        ),
        static (ReadOnlyMemory<char> segment, object? arguments, out object? parsed) =>
        {
            (int? Min, int? Max) limits = ((int? Min, int? Max)) arguments!;
            parsed = null;

            if (!int.TryParse(segment.Span, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return false;

            if (limits.Min is int min && value < min)
                return false;

            if (limits.Max is int max && value > max)
                return false;

            parsed = value;
            return true;
        }
    )
    .AddValueParser("user", static async (ValueParserContext context) =>
    {
        if (!Guid.TryParse(context.Segment.Span, out Guid userId))
            return ValueParseResult.False;

        IUserRepository repository = context.Services.GetRequiredService<IUserRepository>();
        object? user = await repository.TryGetAsync(userId, context.Cancellation);
        return new ValueParseResult(user is not null, user);
    });
```

Asynchronous parsers can return `ValueParseResult.False` for the common non-match result where `Success` is `false` and `Parsed` is `null`.

Built-in parsers use the same mechanism:

- `int` supports `min` and `max`.
- `str` supports `min` and `max`.
- `regex` supports required `pattern`, optional `timeoutMs` that defaults to `50`, and optional `caseSensitive` that defaults to `false`; timed-out matches are treated as non-matches.
- `guid` and `bool` do not take arguments.

## Value Parser Syntax

- `{parameterName:parserName}` parses a segment and stores the parsed value under `parameterName`.
- `{parserName}` parses a segment without storing it in `RequestContext.Parameters`.
- `{parameterName:parserName(arg=value, text='hello')}` also passes a case-insensitive raw argument map through the parser's `BindArgumentsDelegate`.
- Query bindings may add `[]` after the parser definition, such as `{tag:str[]}` or `{tag:str(min=2)[]}`, to collect repeated query keys into a `List<object?>` in request order.
- List parser syntax is supported for query bindings only, not route path parameters.
- Parser arguments support `null`, `true` or `false`, numbers, and single-quoted strings with `\'` escaping.

Use `AddValueParser()` to register custom parsers, or `AddDefaultValueParsers()` to register the built-in `int`, `guid`, `bool`, `str`, and `regex` parsers.

## Query Bindings

`AddQueryBindings()` lets you validate and parse selected query-string values with the same registered value parsers used by route segments.

```csharp
HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddDefaultValueParsers()
    .AddPrefix("/items/*", items => items
        .AddQueryBindings("GET", RouteScopeBuilder.CurrentExact, "{filter:str(min=3)}&{page?:int(min=1)}&{tag:str(min=2)[]}")
        .AddEndpoint("GET", RouteScopeBuilder.CurrentExact, endpoint => endpoint
            .WithHandler(static async (context, _) =>
            {
                await Task.CompletedTask;

                return HttpResponseMessage.Json(new
                {
                    filter = context.Parameters["filter"],
                    page = context.Parameters.TryGetValue("page", out object? page) ? page : null,
                    tags = context.Parameters.TryGetValue("tag", out object? tags) ? tags : null
                });
            })))
    .CreateRouter();
```

- Add `?` to the query parameter name to make it optional, for example `{page?:int(min=1)}`.
- Add `[]` after the query value parser definition to collect repeated query keys, for example `{tag:str[]}` or `{tag:str(min=2)[]}` for `?tag=red&tag=blue`.
- Query parameter names may contain ASCII letters, digits, and underscores.
- Parsed values are stored in `RequestContext.Parameters` under the configured key.
- List query bindings store a `List<object?>` containing each parsed value in request order.
- Query keys are matched case-insensitively using the normalized key exposed by `Uri.Query`.
- Repeated declared scalar query parameters are rejected with `400 Bad Request`.
- Undeclared query parameters are ignored by default. Pass `unexpected: UnexpectedParameterBehavior.Reject` to reject them for a specific binding.
- List value parsers are supported only for query bindings, not route path parameters.
- As with JSON binding and prefix handlers, later middleware can overwrite earlier values in `RequestContext.Parameters`.

Pass `unexpected: UnexpectedParameterBehavior.Reject` when a query-binding registration should reject query keys that were not declared in its binding descriptor:

```csharp
HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddDefaultValueParsers()
    .AddQueryBindings("GET", "/items/", "{filter:str(min=3)}", unexpected: UnexpectedParameterBehavior.Reject)
    .AddEndpoint("GET", "/items/", endpoint => endpoint
        .WithHandler(static async (context, _) =>
        {
            await Task.CompletedTask;
            return HttpResponseMessage.Json(new { filter = context.Parameters["filter"] });
        }))
    .CreateRouter();
```

`AddQueryBindings()` and `WithQueryBindings()` capture their `unexpected` behavior at registration time. Use the overload on each binding that needs non-default behavior; other query bindings continue to ignore undeclared query parameters.

## Typed Handlers

Typed handlers let you describe the data a route needs as a request object instead of reading everything manually from `RequestContext.Parameters`.

```csharp
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using NanoRoute;

public sealed class GetItemRequest
{
    public int Id { get; set; }

    [ValueSource(ValueSource.Parameter, Name = "query_filter")]
    public string Filter { get; set; } = null!;

    [ValueSource(ValueSource.ServiceLocator)]
    public IItemService Items { get; set; } = null!;

    [ValueSource(ValueSource.Skip)]
    public string? DiagnosticsLabel { get; set; }

    public CancellationToken Cancellation { get; set; }
}

HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddDefaultValueParsers()
    .AddQueryBindings("GET", "/items/{id:int}/", "{query_filter:str(min=3)}")
    .AddEndpoint("GET", "/items/{id:int}/", endpoint => endpoint
        .WithHandler(async (GetItemRequest request) =>
        {
            Item item = await request.Items.GetAsync(request.Id, request.Filter, request.Cancellation);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(item.Name)
            };
        }))
    .CreateRouter();
```

Binding rules:

- Writable public properties are bound from `RequestContext.Parameters` by default, using the property name as the key.
- `RequestContext` properties receive the current request context automatically.
- `CancellationToken` properties receive the active request token automatically.
- `[ValueSource(ValueSource.Parameter, Name = "...")]` binds from a different parameter or query-binding name.
- `[ValueSource(ValueSource.ServiceLocator)]` resolves a service from `RequestContext.Services`.
- `[ValueSource(ValueSource.ServiceLocator, Name = "...")]` resolves a keyed service.
- `[ValueSource(ValueSource.Skip)]` leaves the property untouched and does not allow `Name`.
- Read-only properties are ignored.
- Missing required values or services fail fast with `InvalidOperationException`.

Typed endpoint handlers support the same route selection shapes as regular endpoint handlers: a single `verb` plus `pattern`, or an `IEnumerable<string>` of verbs plus `pattern`.

They also have middleware-style overloads that receive `CallNextHandlerDelegate`:

```csharp
.AddEndpoint(["GET"], "/items/{id:int}/", endpoint => endpoint
    .WithHandler(async (GetItemRequest request, CallNextHandlerDelegate next) =>
    {
        HttpResponseMessage response = await next();
        response.Headers.Add("X-Filter", request.Filter);
        return response;
    }))
```

## Exception Handling

`AddExceptionHandler()` adds middleware that converts unexpected exceptions into enriched `HttpRequestException` values. Existing `HttpRequestException` values are passed through unchanged, and `OperationCanceledException` still propagates to the caller.

Pass an options callback to `AddExceptionHandler()` when one exception-handling middleware should customize how specific exception types are normalized. Normalizers are matched against the thrown exception's runtime type first, then against its base exception types, so a base-type normalizer handles derived exceptions unless a more specific normalizer is registered:

```csharp
HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddExceptionHandler(options => options.Map<NotSupportedException>
    (
        static ex =>
        {
            HttpRequestException.Throw(HttpStatusCode.BadRequest, "Not supported", ex);
            return null!;
        }
    ))
    .AddEndpoint("GET", "/items/", endpoint => endpoint
        .WithHandler((_, _) => throw new NotSupportedException()))
    .CreateRouter();
```

The options callback configures only the exception-handling middleware being registered. Other `AddExceptionHandler()` calls keep the default normalizers unless they receive their own options callback.

## JSON Error Details

`AddJsonErrorDetails()` turns routing and normalized exception failures into JSON `ErrorDetails` responses. Pass an options callback when the error payload should include developer diagnostics, custom `ErrorDetails` serialization metadata, or custom exception normalization:

```csharp
HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddJsonErrorDetails(options =>
    {
        options.PopulateErrorInfo = true;
        options.ErrorDetailsTypeInfo = MyJsonContext.Default.ErrorDetails;
        options.Map<NotSupportedException>
        (
            static ex =>
            {
                HttpRequestException.Throw(HttpStatusCode.BadRequest, "Not supported", ex);
                return null!;
            }
        );
    })
    .AddEndpoint("GET", "/items/", endpoint => endpoint
        .WithHandler((_, _) => throw new NotSupportedException()))
    .CreateRouter();
```

`PopulateErrorInfo` can expose exception messages or stack traces, so keep it disabled for production responses unless the caller is trusted to see those details. Set `ErrorDetailsTypeInfo` when Native AOT or custom serialization settings require source-generated `ErrorDetails` metadata.

`AddJsonErrorDetails()` also registers exception handling internally so unexpected exceptions are normalized before they are rendered as JSON. The same `JsonErrorDetailsOptions` callback configures that internally registered exception handler because it derives from `ExceptionHandlingOptions`:

```csharp
builder.AddJsonErrorDetails(options => options.Map<NotSupportedException>
(
    static ex =>
    {
        HttpRequestException.Throw(HttpStatusCode.BadRequest, "Not supported", ex);
        return null!;
    }
));
```

## HTTP Message Routing

Use `HttpMessageRouter` when your application or test already has an `HttpRequestMessage` and wants the matching, value parsing, middleware, and handler pipeline without binding to a network listener.

```csharp
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using NanoRoute;

HttpMessageRouter router = HttpMessageRouter
    .CreateBuilder()
    .AddEndpoint("GET", "/health/", endpoint => endpoint
        .WithHandler(static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("ok")
        })))
    .CreateRouter();

using HttpRequestMessage request = new(HttpMethod.Get, "https://example.test/health");
using HttpResponseMessage response = await router.Route(request, services);
```

The returned `HttpResponseMessage` is owned by the caller. Dispose it after reading the response body.

## Custom Routers

If neither `HttpMessageRouter` nor `HttpListenerRouter` fits the transport you want, derive from `RouterBase<TConfig>`. Your router entry point prepares an `HttpRequestMessage`, calls the protected `Route()` method, and deals with the returned `HttpResponseMessage`.

`RouterBase<TConfig>` stores the immutable router configuration and snapshots the supplied route scope into a reusable pipeline.

## Cancellation

- NanoRoute exposes the caller-provided cancellation token to async value parsers and handlers through `ValueParserContext.Cancellation` and `RequestContext.Cancellation`.
- `OperationCanceledException` is not converted into an HTTP error by `AddExceptionHandler()` or `AddJsonErrorDetails()`. It propagates to the caller or transport adapter unchanged.
- `HttpMessageRouter.Route()` rethrows the cancellation exception and leaves response ownership with the caller.
- `HttpListenerRouter.Route()` aborts the active `HttpListenerResponse` and then rethrows the cancellation exception.

## Common Building Blocks

- `HttpMessageRouter.CreateBuilder()` starts a strongly typed builder for already materialized `HttpRequestMessage` scenarios.
- `HttpListenerRouter.CreateBuilder()` starts a strongly typed builder for `HttpListener` scenarios.
- `RouterBase<TConfig>` stores router configuration and runs a captured request pipeline for custom transports.
- `AddDefaultValueParsers()` registers the built-in `int`, `guid`, `bool`, `str`, and `regex` value parsers.
- `AddPrefix("/prefix/*", ...)` configures a scoped route subtree and returns the current builder.
- `CreatePrefix("/prefix/*")` creates a scoped child builder for a route subtree.
- `AddEndpoint()` and `CreateEndpoint()` capture an endpoint's verbs and route pattern once.
- `EndpointBuilder.WithHandler()`, `EndpointBuilder.WithJsonBody()`, and `EndpointBuilder.WithQueryBindings()` register endpoint-local handlers, JSON body middleware, and query bindings.
- `AddQueryBindings()` and `EndpointBuilder.WithQueryBindings()` bind selected query-string values into `RequestContext.Parameters`.
- Query-binding overloads that take `UnexpectedParameterBehavior` can reject undeclared query-string keys per registration.
- `AddHandler<TRequestContext>()` and `EndpointBuilder.WithHandler<TRequestContext>()` project `RequestContext` into a typed request object before invoking the handler.
- `AddExceptionHandler(options => ...)` customizes exception normalization for that middleware registration.
- `AddJsonBody()` and `EndpointBuilder.WithJsonBody()` bind JSON request content into `RequestContext.Parameters`.
- `AddJsonErrorDetails(options => ...)` turns routing exceptions into JSON `ErrorDetails` responses and configures diagnostics, serialization metadata, and exception normalization for that middleware registration.
- `HttpMethod.For(...)` returns shared known `HttpMethod` instances and creates custom methods for valid extension verbs.
- `HttpResponseMessage.Json(...)` creates JSON responses with the library's serializer defaults.
