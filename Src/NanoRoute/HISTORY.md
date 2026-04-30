# History

## 1.0.0-preview2

### Breaking Changes

- Renamed `ArgumentSourceAttribute` to `ValueSourceAttribute`, and renamed the companion `ArgumentSource` enum to `ValueSource`.

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
