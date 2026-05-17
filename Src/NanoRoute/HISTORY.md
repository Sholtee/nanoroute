# History

## 1.0.0-preview3

### Breaking Changes

- Changed `RouterConfig` and `HttpListenerRouterConfig` to immutable records.
- Renamed `RouterBuilder.WithConfiguration()` to `ConfigureRouting()` and changed its callback from a mutating `Action<TConfig>` shape to the replacing `ConfigureBuilderDelegate<TConfig>` shape.
- Changed JSON error-detail diagnostics from `AddJsonErrorDetails(populateErrorInfo: true)` to `ConfigureJsonErrorDetails(config => config with { PopulateErrorInfo = true }).AddJsonErrorDetails()`.
- Removed the public `Router.MatchingPrecedence` snapshot property. Matching precedence is now carried by the immutable `RouterConfig` used to create the router.
- Removed inline query-binding overloads from typed `AddHandler()` APIs. Register query bindings explicitly with `AddQueryBindings()` before adding the typed handler.
- Removed the `NanoRoute.HandlerExtensions` namespace. `ValueSource`, `ValueSourceAttribute`, and typed-handler `AddHandler()` extension methods now live directly in the `NanoRoute` namespace.
- Renamed `RouteBuilder` to `RouteScopeBuilder` to better describe scoped prefix/subtree configuration.
- Moved pattern-only and multi-verb `AddHandler()` overloads from builder instance methods to extension methods in the `NanoRoute` namespace. The single-verb `AddHandler(string verb, string pattern, ...)` overload remains on `RouteScopeBuilder` and strongly typed router builders.
- Changed prefix-route patterns to use a trailing `/*` marker. A trailing `/` is now an exact route pattern, `RouteScopeBuilder.CurrentExact` is `/`, and `RouteScopeBuilder.CurrentPrefix` is `/*`.
- Renamed `ValueSource.Context` to `ValueSource.Parameter` to describe that typed handlers read from `RequestContext.Parameters`.

### Added

- Added `JsonErrorDetailsConfig` and `ConfigureJsonErrorDetails()` to configure JSON error-response diagnostics and `ErrorDetails` serialization metadata.
- Added `QueryParsingConfig`, `UnexpectedParameterBehavior`, and `ConfigureQueryParsing()` to configure how query bindings handle undeclared query-string parameters.
- Added typed `AddHandler()` overloads for pattern-only and single-verb registration, matching the rest of the route-scope builder API.
- Added `NanoRoutePrefixExtensions` as the extension-method home for `AddPrefix()`.
- Added `EndpointBuilder`, `AddEndpoint()`, `CreateEndpoint()`, and endpoint-scoped `WithHandler()`, `WithJsonBody()`, and `WithQueryBindings()` helpers for registering endpoint-local middleware without repeating an endpoint's verbs and pattern.

### Performance

- Optimized frozen route-tree snapshots by building immutable and frozen collections directly during `RouteNode.Copy(freeze: true)`, avoiding temporary mutable collections during router creation.
- Reduced mutable route-node copy overhead by pre-sizing copied child and handler collections.
- Avoided duplicate raw-query extraction in `QueryStringParser`.

### Migration

Replace mutating `WithConfiguration()` callbacks like:

```csharp
builder.WithConfiguration(config =>
{
    config.MatchingPrecedence = MatchingPrecedence.ParameterizedFirst;
});
```

with `ConfigureRouting()` immutable record updates:

```csharp
builder.ConfigureRouting(config => config with
{
    MatchingPrecedence = MatchingPrecedence.ParameterizedFirst
});
```

Remove the old typed-handler namespace import:

```csharp
using NanoRoute.HandlerExtensions;
```

and keep the normal package namespace import instead:

```csharp
using NanoRoute;
```

If a typed handler needs query-string values, register query bindings before the handler:

```csharp
builder
    .AddQueryBindings(["GET"], "/items/{id:int}/", "{filter?:str(min=3)}")
    .AddHandler
    (
        ["GET"],
        "/items/{id:int}/",
        static async (GetItemRequest request) =>
        {
            return await Handle(request);
        }
    );
```

Replace prefix route registrations like:

```csharp
builder.AddHandler("GET", "/api/users/{user_id:int}/", Middleware);
builder.AddPrefix("/api/users/{user_id:int}/", users => { ... });
```

with:

```csharp
builder.AddHandler("GET", "/api/users/{user_id:int}/*", Middleware);
builder.AddPrefix("/api/users/{user_id:int}/*", users => { ... });
```

Replace typed-handler binding attributes like:

```csharp
[ValueSource(ValueSource.Context, Name = "query_filter")]
```

with:

```csharp
[ValueSource(ValueSource.Parameter, Name = "query_filter")]
```

## 1.0.0-preview2

### Breaking Changes

- Renamed public diagnostic keys to follow .NET naming conventions: `ERRORS_NAME` to `ErrorsName`, `DEVELOPER_MESSAGE` to `DeveloperMessagesName`, `STATUS_NAME` to `StatusName`, `Router.TRACE_ID_NAME` to `Router.TraceIdName`, and `Router.ORIGINAL_REQUEST_NAME` to `Router.OriginalRequestName`.
- Renamed `ErrorDetails.DeveloperMessage` to `DeveloperMessages` to match its collection shape.
- Renamed `MatchingPrecedence.ParameterizedChildrenFirst` to `MatchingPrecedence.ParameterizedFirst` for symmetry with `LiteralFirst`.
- Renamed `ArgumentSourceAttribute` to `ValueSourceAttribute`, and renamed the companion `ArgumentSource` enum to `ValueSource`.
- Removed the router timeout API. Pass a `CancellationToken` to `Router.Handle()` or `HttpListenerRouter.Route()` when request processing needs a deadline.
- Standardized public string collection parameters on `IEnumerable<string>` instead of mixing `IEnumerable<string>` and `IReadOnlyCollection<string>`. Verb-scoped `AddJsonBody()` overloads now take `verb` or `verbs` first, matching `AddHandler()`, `AddQueryBindings()`, and `AddExceptionHandler()`.

### Added

- `ValueSourceAttribute` now supports `ValueSource.Skip` to leave a typed-handler property untouched.
- `ValueSourceAttribute` supports named service-locator bindings for keyed services through `Name`.
- `TypedExceptionNormalizer<TException>` and `ExceptionNormalizer.For<TException>()` simplify registering typed exception normalizers.

### Migration

Replace typed-handler binding attributes like:

```csharp
[ArgumentSource(ArgumentSource.Context, Name = "query_filter")]
```

with:

```csharp
[ValueSource(ValueSource.Parameter, Name = "query_filter")]
```

Replace diagnostic key constants like:

```csharp
ex.Data[NanoRouteExceptionExtensions.ERRORS_NAME]
ex.Data[NanoRouteExceptionExtensions.DEVELOPER_MESSAGE]
ex.Data[NanoRouteExceptionExtensions.STATUS_NAME]

request.SetProperty(Router.TRACE_ID_NAME, traceId);
request.SetProperty(Router.ORIGINAL_REQUEST_NAME, originalRequest);
```

with:

```csharp
ex.Data[NanoRouteExceptionExtensions.ErrorsName]
ex.Data[NanoRouteExceptionExtensions.DeveloperMessagesName]
ex.Data[NanoRouteExceptionExtensions.StatusName]

request.SetProperty(Router.TraceIdName, traceId);
request.SetProperty(Router.OriginalRequestName, originalRequest);
```

Replace error-detail and exception-helper usages like:

```csharp
HttpRequestException.Throw(status, title, developerMessage: messages);
IEnumerable<string>? messages = errorDetails.DeveloperMessage;
```

with:

```csharp
HttpRequestException.Throw(status, title, developerMessages: messages);
IEnumerable<string>? messages = errorDetails.DeveloperMessages;
```

Replace matching-precedence usages like:

```csharp
MatchingPrecedence.ParameterizedChildrenFirst
```

with:

```csharp
MatchingPrecedence.ParameterizedFirst
```
