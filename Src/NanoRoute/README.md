# NanoRoute

NanoRoute is a small, dependency-light router for `HttpRequestMessage` pipelines, with optional transport adapters and focused helpers for JSON payloads and error handling.

The core library is centered around `RouteBuilder`, `Router`, and `RequestContext`, so you can plug the routing pipeline into your own transport or hosting model as well.

NanoRoute targets `netstandard2.0` and `netstandard2.1`.

> Note: NanoRoute is compatible with Native AOT scenarios.

For AWS Lambda integrations, use the separate `NanoRoute.AwsLambda` package.

## Quick Start

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using NanoRoute;

HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddValueParser("int", static (ReadOnlyMemory<char> segment, object? _, out object? parsed) =>
    {
        bool success = int.TryParse(segment.Span, out int value);
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

## Matching Rules

- A trailing `/` makes a route a prefix match.
- Without a trailing `/`, the route matches only the exact normalized request path.
- Route patterns must start with `/`, except for `RouteBuilder.CurrentExact`, which matches the current scoped path exactly.
- Use `RouteBuilder.CurrentPrefix` when a handler or middleware should match the current scoped path as a prefix.
- Repeated `/` separators in route patterns, such as `//` or `/items//details`, are invalid.
- Literal segments are matched case-insensitively.
- Parser-backed segments use registered parsers such as `{user_id:int}`, `{int}`, or `{slug:str(min=3,max=32)}`.
- The parameter name is optional. Segments like `{int}` still validate the path but do not add an entry to `RequestContext.Parameters`.
- When multiple handlers match within the selected route branch, NanoRoute evaluates compatible handlers from shorter prefixes toward more specific matches.
- At the same path depth, `RouterConfig.MatchingPrecedence` decides whether literal or parameterized child segments are selected first.
- Once NanoRoute selects a child branch at a given depth, it does not return to sibling branches later in the pipeline.

## Advanced Usage

### Router Configuration

`RouterConfig` controls runtime behavior that applies to a created router snapshot. Configuration records are immutable, so use `ConfigureRouting()` with a `with` expression when you want to replace one or more settings before calling `CreateRouter()`. The callback uses the same `ConfigureBuilderDelegate<TConfig>` shape as module-specific configuration methods.

```csharp
HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .ConfigureRouting(config => config with
    {
        MatchingPrecedence = MatchingPrecedence.ParameterizedFirst,
        ParametersCapacity = 8
    })
    .AddDefaultValueParsers()
    .AddHandler("GET", "/items/{slug:str}", static async (context, _) =>
    {
        await Task.CompletedTask;
        return HttpResponseMessage.Json(new { slug = context.Parameters["slug"] });
    })
    .CreateRouter();
```

Created routers are immutable snapshots: later route or configuration changes on the builder do not affect routers that have already been created.

`ParametersCapacity` sets the initial capacity of the per-request `RequestContext.Parameters` dictionary. Raise it when most requests collect several route parameters, query bindings, or middleware-added values and you want to avoid resizing.

### Module Configuration

Some builder modules expose `ConfigureXxx()` methods for settings that are shared by later registrations in the same builder scope. This supports a "configure once, use anywhere" style when several route registrations should use the same module behavior.

```csharp
HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .ConfigureJsonErrorDetails(config => config with
    {
        PopulateErrorInfo = true
    })
    .AddJsonErrorDetails("/api/")
    .AddJsonErrorDetails("/admin/")
    .CreateRouter();
```

`ConfigureXxx()` methods update the configuration visible from the current builder scope. They affect module registrations made after the configuration call. Registrations that have already been added keep the configuration they captured when they were registered.

Prefix builders follow the same rule as value parsers and metadata: a child builder receives a scoped copy when it is created. Configuration changes made later on the parent do not rewrite existing child scopes, and child changes stay local to that child scope.

### AddPrefix() and CreatePrefix()

When several routes share the same prefix, `AddPrefix()` lets you define that prefix once and register child routes relative to it. If you want to hold onto a scoped child builder and add routes incrementally, use `CreatePrefix()`.

```csharp
RouterBuilder<HttpListenerRouter, HttpListenerRouterConfig> builder = HttpListenerRouter
    .CreateBuilder()
    // Register the built-in int/guid/bool/str route parsers.
    .AddDefaultValueParsers()
    // Convert routing exceptions into JSON error responses.
    .AddJsonErrorDetails();

builder.AddPrefix("/api/users/{user_id:int}/", users => users
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

### Value Parsers

NanoRoute supports both synchronous and asynchronous value parsers:

- `AddValueParser("name", SyncValueParserDelegate)` for lightweight synchronous parsing.
- `AddValueParser("name", ValueParserDelegate)` when parsing needs request services or async work.
- `AddValueParser("name", BindArgumentsDelegate, ...)` when the route template includes parser arguments such as `{id:int(min=1)}`.

`BindArgumentsDelegate` receives the raw parser arguments as a case-insensitive dictionary and can turn them into any cached object. That object is then exposed as `ValueParserContext.Arguments` for async parsers or as the `arguments` parameter for sync parsers.

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

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
            return new ValueParseResult(false, null);

        IUserRepository repository = context.Services.GetRequiredService<IUserRepository>();
        object? user = await repository.TryGetAsync(userId, context.Cancellation);
        return new ValueParseResult(user is not null, user);
    });
```

Built-in parsers use the same mechanism:

- `int` supports `min` and `max`.
- `str` supports `min`, `max`, and `pattern`.
- `guid` and `bool` do not take arguments.

### Query Bindings

`AddQueryBindings()` lets you validate and parse selected query-string values with the same registered value parsers used by route segments.

```csharp
HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddDefaultValueParsers()
    .AddPrefix("/items/", items => items
        .AddQueryBindings("GET", RouteBuilder.CurrentExact, "{filter:str(min=3)}&{page?:int(min=1)}&{tag:str(min=2)[]}")
        .AddHandler("GET", RouteBuilder.CurrentExact, async (context, _) =>
        {
            return HttpResponseMessage.Json(new
            {
                filter = context.Parameters["filter"],
                page = context.Parameters.TryGetValue("page", out object? page) ? page : null,
                tags = context.Parameters.TryGetValue("tag", out object? tags) ? tags : null
            });
        }))
    .CreateRouter();
```

- Add `?` to the query parameter name to make it optional, for example `{page?:int(min=1)}`.
- Add `[]` after the query value parser definition to collect repeated query keys, for example `{tag:str[]}` or `{tag:str(min=2)[]}` for `?tag=red&tag=blue`.
- Query parameter names may contain ASCII letters, digits, and underscores.
- Parsed values are stored in `RequestContext.Parameters` under the configured key.
- List query bindings store a `List<object?>` containing each parsed value in request order.
- Query keys are matched case-insensitively using the normalized key exposed by `Uri.Query`.
- Repeated declared scalar query parameters are rejected with `400 Bad Request`.
- Undeclared query parameters are ignored by default. Use `ConfigureQueryParsing()` to reject them instead.
- List value parsers are supported only for query bindings, not route path parameters.
- As with JSON binding and prefix handlers, later middleware can overwrite earlier values in `RequestContext.Parameters`.

Use `ConfigureQueryParsing()` before `AddQueryBindings()` when you want later query-binding registrations in the same builder scope to reject query keys that were not declared in their binding descriptor:

```csharp
HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddDefaultValueParsers()
    .ConfigureQueryParsing(config => config with
    {
        UnexpectedParameterBehavior = UnexpectedParameterBehavior.Reject
    })
    .AddQueryBindings("GET", "/items", "{filter:str(min=3)}")
    .AddHandler("GET", "/items", static async (context, _) =>
    {
        await Task.CompletedTask;
        return HttpResponseMessage.Json(new { filter = context.Parameters["filter"] });
    })
    .CreateRouter();
```

`AddQueryBindings()` snapshots the current `QueryParsingConfig` at registration time. Prefix builders follow the normal `RouteBuilder.Metadata` scoping rules, so a prefix can override query parsing before registering its own scoped query-binding middleware. The overload that accepts a `ConfigureBuilderDelegate<QueryParsingConfig>` applies a one-off override to that query-binding registration without changing builder metadata.

### Typed Handlers

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

    [ValueSource(ValueSource.Context, Name = "query_filter")]
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
    .AddQueryBindings("GET", "/items/{id:int}", "{query_filter:str(min=3)}")
    .AddHandler
    (
        "GET",
        "/items/{id:int}",
        async (GetItemRequest request) =>
        {
            Item item = await request.Items.GetAsync(request.Id, request.Filter, request.Cancellation);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(item.Name)
            };
        }
    )
    .CreateRouter();
```

Binding rules:

- Writable public properties are bound from `RequestContext.Parameters` by default, using the property name as the key.
- `RequestContext` properties receive the current request context automatically.
- `CancellationToken` properties receive the active request token automatically.
- `[ValueSource(ValueSource.Context, Name = "...")]` binds from a different parameter or query-binding name.
- `[ValueSource(ValueSource.ServiceLocator)]` resolves a service from `RequestContext.Services`.
- `[ValueSource(ValueSource.ServiceLocator, Name = "...")]` resolves a keyed service.
- `[ValueSource(ValueSource.Skip)]` leaves the property untouched and does not allow `Name`.
- Read-only properties are ignored.
- Missing required values or services fail fast with `InvalidOperationException`.

Register query bindings with `AddQueryBindings()` before the typed handler when the request object needs parsed query values.
Typed `AddHandler()` supports the same route selection shapes as regular handlers: pattern-only for all verbs, single `verb` plus `pattern`, or an `IEnumerable<string>` of verbs plus `pattern`.

Typed handlers also have middleware-style overloads that receive `CallNextHandlerDelegate`:

```csharp
.AddHandler
(
    ["GET"],
    "/items/{id:int}",
    async (GetItemRequest request, CallNextHandlerDelegate next) =>
    {
        HttpResponseMessage response = await next();
        response.Headers.Add("X-Filter", request.Filter);
        return response;
    }
)
```

### Exception Handling

`AddExceptionHandler()` adds middleware that converts unexpected exceptions into enriched `HttpRequestException` values. Existing `HttpRequestException` values are passed through unchanged, and `OperationCanceledException` still propagates to the caller.

Use `ConfigureExceptionHandling()` before `AddExceptionHandler()` when you want to customize how specific exception types are normalized:

```csharp
HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .ConfigureExceptionHandling(config => config with
    {
        ExceptionNormalizers = config.ExceptionNormalizers.SetItem
        (
            typeof(NotSupportedException),
            static ex =>
            {
                HttpRequestException.Throw(HttpStatusCode.BadRequest, "Not supported", ex);
                return null!;
            }
        )
    })
    .AddExceptionHandler()
    .AddHandler("GET", "/items", (_, _) => throw new NotSupportedException())
    .CreateRouter();
```

`AddExceptionHandler()` snapshots the current `ExceptionHandlingConfig` at registration time. Prefix builders follow the normal `RouteBuilder.Metadata` scoping rules, so a prefix can override exception normalization before registering its own scoped exception middleware.

### JSON Error Details

`AddJsonErrorDetails()` turns routing and normalized exception failures into JSON `ErrorDetails` responses. Configure the error payload before adding the middleware when you want to include developer diagnostics or customize `ErrorDetails` serialization:

```csharp
HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .ConfigureJsonErrorDetails(config => config with
    {
        PopulateErrorInfo = true
    })
    .AddJsonErrorDetails()
    .AddHandler("GET", "/items", (_, _) => throw new InvalidOperationException("Boom"))
    .CreateRouter();
```

`PopulateErrorInfo` can expose exception messages or stack traces, so keep it disabled for production responses unless the caller is trusted to see those details.

`AddJsonErrorDetails()` snapshots the current `JsonErrorDetailsConfig` at registration time. Prefix builders follow the normal `RouteBuilder.Metadata` scoping rules, so a prefix can override JSON error-detail settings before registering its own scoped error middleware.

### Custom Routers

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

This keeps the transport-specific concerns in your own router type while still reusing NanoRoute's matching, value parsing, and handler pipeline.

### Cancellation

- NanoRoute exposes the caller-provided cancellation token to async value parsers and handlers through `ValueParserContext.Cancellation` and `RequestContext.Cancellation`.
- `OperationCanceledException` is not converted into an HTTP error by `AddExceptionHandler()` or `AddJsonErrorDetails()`. It propagates to the caller or transport adapter unchanged.
- `HttpListenerRouter.Route()` aborts the active `HttpListenerResponse` and then rethrows the cancellation exception.

## Common Building Blocks

- `HttpListenerRouter.CreateBuilder()` starts a strongly typed builder for `HttpListener` scenarios.
- `ConfigureRouting()` customizes router-level behavior such as matching precedence and the initial request-parameter dictionary capacity before creating a router snapshot.
- `AddDefaultValueParsers()` registers the built-in `int`, `guid`, `bool`, and `str` value parsers.
- `AddPrefix("/prefix/", ...)` configures a scoped route subtree and returns the current builder.
- `CreatePrefix("/prefix/")` creates a scoped child builder for a route subtree.
- `RouteBuilder.Metadata` stores extension-defined build-time settings with prefix-local scoping; it is mainly for extension authors.
- `AddQueryBindings()` binds selected query-string values into `RequestContext.Parameters`.
- `ConfigureQueryParsing()` customizes query-binding behavior used by subsequently registered `AddQueryBindings()` middleware.
- `AddHandler<TRequestContext>()` projects `RequestContext` into a typed request object before invoking the handler.
- `ConfigureExceptionHandling()` customizes exception normalization used by subsequently registered `AddExceptionHandler()` middleware.
- `AddJsonBody()` binds JSON request content into `RequestContext.Parameters`.
- `AddJsonErrorDetails()` turns routing exceptions into JSON `ErrorDetails` responses.
- `ConfigureJsonErrorDetails()` customizes JSON `ErrorDetails` response diagnostics and serialization metadata used by subsequently registered `AddJsonErrorDetails()` middleware.
- `HttpResponseMessage.Json(...)` creates JSON responses with the library's serializer defaults.

## Core Types

- [RouteBuilder](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouteBuilder.html)
- [BuilderMetadata](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.BuilderMetadata.html)
- [ConfigureBuilderDelegate`1](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ConfigureBuilderDelegate-1.html)
- [ExceptionHandlingConfig](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ExceptionHandlingConfig.html)
- [Router](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.Router.html)
- [RouterConfig](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouterConfig.html)
- [RouterBuilder`2](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouterBuilder-2.html)
- [HttpListenerRouter](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.HttpListenerRouter.html)
- [RequestContext](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestContext.html)
- [QueryParsingConfig](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.QueryParsingConfig.html)
- [UnexpectedParameterBehavior](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.UnexpectedParameterBehavior.html)
- [ErrorDetails](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ErrorDetails.html)
- [ValueParserDelegate](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ValueParserDelegate.html)
- [RequestHandlerDelegate](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestHandlerDelegate.html)
- [NanoRouteHandlerExtensions](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.NanoRouteHandlerExtensions.html)
- [NanoRoutePrefixExtensions](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.NanoRoutePrefixExtensions.html)
- [ValueSource](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ValueSource.html)
- [ValueSourceAttribute](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ValueSourceAttribute.html)

## Documentation

API documentation is generated from the XML comments in the source and published at:

- <https://sholtee.github.io/nanoroute/docs/NanoRoute/>

