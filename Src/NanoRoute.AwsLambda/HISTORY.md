# History

## 1.0.0-preview2

### Breaking Changes

- Changed `AwsLambdaRouterConfig` to an immutable record, following the core `RouterConfig` model.

### Added

- Added `AwsLambdaRouterConfig.LambdaTimeoutBuffer` to configure how much time the AWS Lambda adapter reserves before the invocation timeout. The default remains one second.

### Migration

Configure AWS Lambda router settings through immutable record updates:

```csharp
ApiGatewayHttpApiV2Router router = ApiGatewayHttpApiV2Router
    .CreateBuilder()
    .WithConfiguration(config => config with
    {
        LambdaTimeoutBuffer = TimeSpan.FromSeconds(3)
    })
    .CreateRouter();
```
