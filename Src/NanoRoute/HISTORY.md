# History

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
