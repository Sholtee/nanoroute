# History

## 1.0.0-preview2

### Breaking Changes

- Changed `AwsLambdaRouterConfig` to an immutable record, following the core `RouterConfig` model.
- Dropped the `netstandard2.0` target from NanoRoute.AwsLambda because the AWS Lambda .NET runtime does not support legacy .NET Framework hosts; the adapter now targets `netstandard2.1`.

### Added

- Added `AwsLambdaRouterConfig.LambdaTimeoutBuffer` to configure how much time the AWS Lambda adapter reserves before the invocation timeout. The default remains one second.

### Migration

Configure AWS Lambda router settings through immutable record updates:

```csharp
ApiGatewayHttpApiV2Router router = ApiGatewayHttpApiV2Router
    .CreateBuilder()
    .ConfigureRouting(config => config with
    {
        LambdaTimeoutBuffer = TimeSpan.FromSeconds(3)
    })
    .CreateRouter();
```
