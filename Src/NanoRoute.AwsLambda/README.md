# NanoRoute.AwsLambda

NanoRoute.AwsLambda adds AWS Lambda adapters for NanoRoute while keeping the core package transport-neutral.

The package supports Amazon API Gateway HTTP APIs and Lambda Function URLs that invoke Lambda functions with payload format version `2.0`. It translates `APIGatewayHttpApiV2ProxyRequest` events into `HttpRequestMessage` instances, runs the normal NanoRoute pipeline, and converts the produced `HttpResponseMessage` into an `APIGatewayHttpApiV2ProxyResponse`.

NanoRoute.AwsLambda targets `netstandard2.0` and `netstandard2.1`.

## Quick Start

```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.DependencyInjection;
using NanoRoute;
using NanoRoute.AwsLambda;

public sealed class Function
{
    private static readonly IServiceProvider Services = new ServiceCollection().BuildServiceProvider();

    private static readonly ApiGatewayHttpApiV2Router Router = ApiGatewayHttpApiV2Router
        .CreateBuilder()
        .AddDefaultValueParsers()
        .AddJsonErrorDetails()
        .AddEndPoint("GET", "/health/", endpoint => endpoint
            .WithHandler(static async (_, _) =>
            {
                await Task.CompletedTask;
                return HttpResponseMessage.Json(new { status = "ok" });
            }))
        .CreateRouter();

    public Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        return Router.Route(request, Services, context.RemainingTime);
    }
}
```

`ApiGatewayHttpApiV2Router.CreateBuilder()` uses the same builder APIs as the core package. Prefer endpoint builders such as `AddEndPoint()` for application routes, and reach for lower-level `AddHandler()` only when you need custom pipeline composition.

Pass `ILambdaContext.RemainingTime` to `Route()` so the adapter can cancel work shortly before the Lambda runtime terminates the invocation.

## At A Glance

- Supported: API Gateway HTTP API and Lambda Function URL events represented by `APIGatewayHttpApiV2ProxyRequest`.
- Supported: Lambda proxy responses represented by `APIGatewayHttpApiV2ProxyResponse`.
- Not currently supported: REST API payload format `1.0`, Application Load Balancer events, or custom event models.
- Request URIs are built from `RawPath`, `RawQueryString`, the `Host` header, and forwarding metadata.
- Plain request bodies are exposed as `StringContent`; base64-encoded bodies are exposed as `StreamContent`.
- `Set-Cookie` response values are returned through the API Gateway `Cookies` collection.

## Documentation

Full package documentation and API reference are published at:

- <https://sholtee.github.io/nanoroute/docs/NanoRoute.AwsLambda/>
