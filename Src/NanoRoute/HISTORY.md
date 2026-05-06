# History

## 1.0.0-preview2

### Breaking Changes

- Renamed `ArgumentSourceAttribute` to `ValueSourceAttribute`, and renamed the companion `ArgumentSource` enum to `ValueSource`.
- Removed the router timeout API. Pass a `CancellationToken` to `Router.Handle()` or `HttpListenerRouter.Route()` when request processing needs a deadline.
- Standardized public string collection parameters on `IEnumerable<string>` instead of mixing `IEnumerable<string>` and `IReadOnlyCollection<string>`. Verb-scoped `AddJsonBody()` overloads now take `verb` or `verbs` first, matching `AddHandler()`, `AddQueryBindings()`, and `AddExceptionHandler()`.

### Added

- `ValueSourceAttribute` now supports `ValueSource.Skip` to leave a typed-handler property untouched.
- `ValueSourceAttribute` supports named service-locator bindings for keyed services through `Name`.

### Migration

Replace usages like:

```csharp
[ArgumentSource(ArgumentSource.Context, Name = "query_filter")]
```

with:

```csharp
[ValueSource(ValueSource.Context, Name = "query_filter")]
```
