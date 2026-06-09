# History

## 1.0.0-preview3

### Changed

- Changed `ApiGatewayV2Router` to compose the core `RequestPipeline` and expose its own explicit `CreateBuilder()` factory.

## 1.0.0-preview2

### Breaking Changes

- Changed `ApiGatewayV2RouterConfig` to an immutable record, following the core `RouterConfig` model.
- Dropped the `netstandard2.0` target from NanoRoute.AwsLambda because the AWS Lambda .NET runtime does not support legacy .NET Framework hosts; the adapter now targets `netstandard2.1`.

### Added

- Added `ApiGatewayV2RouterConfig.LambdaTimeoutBuffer` to configure how much time the AWS Lambda adapter reserves before the invocation timeout. The default remains one second.

### Migration

Configure AWS Lambda router settings through immutable record updates:

```csharp
ApiGatewayV2Router router = ApiGatewayV2Router
    .CreateBuilder()
    .ConfigureRouting(config => config with
    {
        LambdaTimeoutBuffer = TimeSpan.FromSeconds(3)
    })
    .CreateRouter();
```
