# NanoRoute.AwsLambda

NanoRoute.AwsLambda adds AWS Lambda adapters for NanoRoute while keeping the core package transport-neutral.

The package supports Amazon API Gateway HTTP APIs and Lambda Function URLs that invoke Lambda functions with payload format version `2.0`. It translates `APIGatewayHttpApiV2ProxyRequest` events into `HttpRequestMessage` instances, runs the normal NanoRoute pipeline, and converts the produced `HttpResponseMessage` into an `APIGatewayHttpApiV2ProxyResponse`.

NanoRoute.AwsLambda targets `netstandard2.1`.

## Install

```shell
dotnet add package NanoRoute.AwsLambda --prerelease
```

## Quick Start

Create a reusable router once, then call `Route()` from the Lambda handler with the API Gateway request and `ILambdaContext.RemainingTime`:

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using NanoRoute;
using NanoRoute.AwsLambda;

public sealed class Function
{
    private static readonly IServiceProvider Services = new EmptyServiceProvider();

    private static readonly ApiGatewayHttpApiV2Router Router = ApiGatewayHttpApiV2Router
        .CreateBuilder()
        .AddJsonErrorDetails()
        .AddEndpoint("GET", "/health/", endpoint => endpoint
            .WithHandler(static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            })))
        .CreateRouter();

    public Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        return Router.Route(request, Services, context.RemainingTime);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
```

## Typed Binding Example

The same router builder supports route parameters, JSON request bodies, query bindings, services, and typed request objects. The example below shows a small user API with service resolution:

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.DependencyInjection;
using NanoRoute;
using NanoRoute.AwsLambda;

public sealed class Function
{
    // UserRepository is your application service that implements IUserRepository.
    private static readonly IServiceProvider Services = new ServiceCollection()
        .AddSingleton<IUserRepository, UserRepository>()
        .BuildServiceProvider();

    private static readonly ApiGatewayHttpApiV2Router Router = ApiGatewayHttpApiV2Router
        .CreateBuilder()
        .AddDefaultValueParsers()
        .AddJsonErrorDetails()
        .AddEndpoint("GET", "/api/users/{user_id:int}/", endpoint => endpoint
            .WithHandler(static async (GetUserRequest request) =>
            {
                return HttpResponseMessage.Json(HttpStatusCode.OK, new UserResponse
                {
                    Id = request.UserId,
                    Name = await request.Users.GetNameAsync(request.UserId)
                });
            }))
        .AddEndpoint("POST", "/api/users/", endpoint => endpoint
            .WithJsonBody<CreateUserBody>(nameof(CreateUserRequest.Body))
            .WithHandler(static async (CreateUserRequest request) =>
            {
                int userId = await request.Users.CreateAsync(request.Body.Name);

                return HttpResponseMessage.Json(HttpStatusCode.Created, new UserResponse
                {
                    Id = userId,
                    Name = request.Body.Name
                });
            }))
        .CreateRouter();

    public Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        return Router.Route(request, Services, context.RemainingTime);
    }
}

public sealed class GetUserRequest
{
    [ValueSource(ValueSource.Parameter, Name = "user_id")]
    public int UserId { get; set; }

    [ValueSource(ValueSource.ServiceLocator)]
    public IUserRepository Users { get; set; } = null!;
}

public sealed class CreateUserRequest
{
    public CreateUserBody Body { get; set; } = null!;

    [ValueSource(ValueSource.ServiceLocator)]
    public IUserRepository Users { get; set; } = null!;
}

public sealed class CreateUserBody
{
    public string Name { get; set; } = string.Empty;
}

public sealed class UserResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public interface IUserRepository
{
    Task<int> CreateAsync(string name);

    Task<string> GetNameAsync(int userId);
}
```

`ApiGatewayHttpApiV2Router.CreateBuilder()` uses the same builder APIs as the core package. Prefer endpoint builders such as `AddEndpoint()` for application routes; typed handlers and endpoint helpers such as `WithJsonBody()` and `WithQueryBindings()` keep route values, query values, JSON bodies, services, and framework values in request objects.

Pass `ILambdaContext.RemainingTime` to `Route()` so the adapter can cancel work shortly before the Lambda runtime terminates the invocation.

## At A Glance

- Supported: API Gateway HTTP API and Lambda Function URL events represented by `APIGatewayHttpApiV2ProxyRequest`.
- Supported: Lambda proxy responses represented by `APIGatewayHttpApiV2ProxyResponse`.
- Not currently supported: REST API payload format `1.0`, Application Load Balancer events, or custom event models.
- Request URIs are built from `RawPath`, `RawQueryString`, the `Host` header, and forwarding metadata.
- Plain request bodies are exposed as `StringContent`; base64-encoded bodies are exposed as `StreamContent`.
- `Set-Cookie` response values are returned through the API Gateway `Cookies` collection.
- Endpoint builders and helpers such as `WithJsonBody()` and `WithQueryBindings()` work under Lambda the same way they do in the core package.

## Hands-On Example

For a small working fixture, see [Tests/NanoRoute.TestLambda](https://github.com/Sholtee/nanoroute/tree/master/Tests/NanoRoute.TestLambda). It wires `ApiGatewayHttpApiV2Router` into a Lambda handler and shows endpoint builders, query bindings, JSON body binding, JSON error responses, and cookie mapping in one project.

## Documentation

Full package documentation and API reference are published at:

- <https://sholtee.github.io/nanoroute/docs/NanoRoute.AwsLambda/>
