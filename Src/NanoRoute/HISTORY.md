# History

## 1.0.0-preview3

### Breaking Changes

- Changed `RouterConfig` and `HttpListenerRouterConfig` to immutable records.
- Changed `RouterBuilder.WithConfiguration()` from a mutating `Action<TConfig>` callback to a replacing `Func<TConfig, TConfig>` callback.
- Changed JSON error-detail diagnostics from `AddJsonErrorDetails(populateErrorInfo: true)` to `ConfigureJsonErrorDetails(config => config with { PopulateErrorInfo = true }).AddJsonErrorDetails()`.
- Removed the public `Router.MatchingPrecedence` snapshot property. Matching precedence is now carried by the immutable `RouterConfig` used to create the router.
- Removed inline query-binding overloads from typed `AddHandler()` APIs. Register query bindings explicitly with `AddQueryBindings()` before adding the typed handler.

### Added

- Added `JsonErrorDetailsConfig` and `ConfigureJsonErrorDetails()` to configure JSON error-response diagnostics and `ErrorDetails` serialization metadata.
- Added `QueryParsingConfig`, `UnexpectedParameterBehavior`, and `ConfigureQueryParsing()` to configure how query bindings handle undeclared query-string parameters.
- Added typed `AddHandler()` overloads for pattern-only and single-verb registration, matching the rest of the route-builder API.

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

with immutable record updates:

```csharp
builder.WithConfiguration(config => config with
{
    MatchingPrecedence = MatchingPrecedence.ParameterizedFirst
});
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

### Migration

Replace typed-handler binding attributes like:

```csharp
[ArgumentSource(ArgumentSource.Context, Name = "query_filter")]
```

with:

```csharp
[ValueSource(ValueSource.Context, Name = "query_filter")]
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
