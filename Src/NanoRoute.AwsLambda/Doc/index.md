# NanoRoute.AwsLambda

NanoRoute.AwsLambda adds AWS Lambda adapters for NanoRoute while keeping the core package transport-neutral.

The package supports Amazon API Gateway HTTP APIs and Lambda Function URLs that invoke Lambda functions with payload format version `2.0`. It translates `APIGatewayHttpApiV2ProxyRequest` events into `HttpRequestMessage` instances, runs the normal NanoRoute pipeline, and converts the produced `HttpResponseMessage` into an `APIGatewayHttpApiV2ProxyResponse`.

NanoRoute.AwsLambda targets `netstandard2.1`.

## Quick Start

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

    private static readonly ApiGatewayV2Router Router = ApiGatewayV2Router
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

`ApiGatewayV2Router.CreateBuilder()` returns the same strongly typed NanoRoute builder style as the core package. Register value parsers, query bindings, JSON body binders, typed handlers, endpoint builders, prefixes, and handlers in the builder, then call `CreateRouter()` once and reuse the router between Lambda invocations.

Prefer endpoint builders such as `AddEndpoint()` for application routes. Typed handlers and endpoint helpers such as `WithJsonBody()` keep route values, JSON bodies, services, and framework values in request objects. `AddHandler()` is still available for lower-level middleware composition and custom pipelines.

The router entry point accepts the API Gateway request, a service provider, and the Lambda remaining time. Pass `ILambdaContext.RemainingTime` so the adapter can cancel work shortly before the Lambda runtime terminates the invocation.

For a small working fixture, see [Tests/NanoRoute.TestLambda](https://github.com/Sholtee/nanoroute/tree/master/Tests/NanoRoute.TestLambda). It wires `ApiGatewayV2Router` into a Lambda handler and shows endpoint builders, query bindings, JSON body binding, JSON error responses, and cookie mapping in one project.

## Core Types

- [ApiGatewayV2Router](https://sholtee.github.io/nanoroute/docs/NanoRoute.AwsLambda/NanoRoute.AwsLambda.ApiGatewayV2Router.html)
- [ApiGatewayV2RouterConfig](https://sholtee.github.io/nanoroute/docs/NanoRoute.AwsLambda/NanoRoute.AwsLambda.ApiGatewayV2RouterConfig.html)

## Routing

The Lambda adapter uses the same route matching and handler pipeline as NanoRoute.

```csharp
using System.Net;
using System.Net.Http;

using NanoRoute;
using NanoRoute.AwsLambda;

ApiGatewayV2Router router = ApiGatewayV2Router
    .CreateBuilder()
    .AddJsonErrorDetails()
    .AddDefaultValueParsers()
    .AddPrefix("/api/items/*", items => items
        .AddEndpoint("GET", RouteScopeBuilder.CurrentExact, endpoint => endpoint
            .WithQueryBindings("{filter?:str(min=3)}")
            .WithHandler(static async (context, _) =>
            {
                await Task.CompletedTask;

                return HttpResponseMessage.Json(new
                {
                    filter = context.Parameters.TryGetValue("filter", out object? filter) ? filter : null
                });
            }))
        .AddEndpoint("GET", "/{id:int(min=1)}/", endpoint => endpoint
            .WithHandler(static async (context, _) =>
            {
                await Task.CompletedTask;

                return HttpResponseMessage.Json(new
                {
                    id = context.Parameters["id"]
                });
            })))
    .CreateRouter();
```

`CreateBuilder()` does not add JSON error handling automatically. Add `AddJsonErrorDetails()` yourself when you want structured JSON errors, or omit it when you want custom exception handling middleware or response shaping handlers to own failures.

## Request Mapping

`ApiGatewayV2Router` converts the API Gateway event into an `HttpRequestMessage` before executing the NanoRoute pipeline.

- The request URI is built from `RawPath`, `RawQueryString`, the `Host` header, and the request scheme.
- The scheme is read from `Forwarded: proto=...`, `X-Forwarded-Proto`, or inferred as `https` for Lambda Function URL domains.
- Request headers are copied onto the `HttpRequestMessage` or its content headers.
- Plain request bodies are exposed as `StringContent`; base64-encoded request bodies are exposed as `StreamContent`.
- The original `APIGatewayHttpApiV2ProxyRequest` is available through the NanoRoute request context as the original request object.
- The API Gateway request id is used as the NanoRoute trace id.

Payload format `2.0` does not include the scheme directly, so the adapter derives it from forwarding metadata or the Lambda Function URL domain. If the adapter cannot determine the scheme or host, the request cannot be mapped to an absolute `HttpRequestMessage.RequestUri` and routing fails before handlers run.

## Response Mapping

NanoRoute handlers return ordinary `HttpResponseMessage` instances.

- `StringContent` is returned as a plain response body.
- Other content types are base64 encoded and set `IsBase64Encoded` to `true`.
- Response headers are flattened into the API Gateway response header dictionary.
- `Set-Cookie` values are returned through the API Gateway `Cookies` collection.

Header values other than `Set-Cookie` are joined with commas, matching the single-value header model used by `APIGatewayHttpApiV2ProxyResponse.Headers`.

## Timeout Handling

Pass `ILambdaContext.RemainingTime` to `Route()`. By default, the adapter starts cancellation one second before the Lambda timeout so async value parsers and handlers can observe `ValueParserContext.Cancellation` and `RequestContext.Cancellation`.

If the invocation is already inside that safety window, the request is cancelled immediately and the adapter returns a `504 Gateway Timeout` JSON error response.

Use `ApiGatewayV2RouterConfig.LambdaTimeoutBuffer` when your function needs a different safety window:

```csharp
ApiGatewayV2Router router = ApiGatewayV2Router
    .CreateBuilder()
    .ConfigureRouting(config => config with
    {
        LambdaTimeoutBuffer = TimeSpan.FromSeconds(3)
    })
    .AddJsonErrorDetails()
    .AddEndpoint("GET", "/health/", endpoint => endpoint
        .WithHandler(static async (_, _) =>
        {
            await Task.CompletedTask;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }))
    .CreateRouter();
```

## Typed Handlers

Typed handlers work the same way under Lambda as they do in the core package. The service provider passed to `Route()` is exposed through `RequestContext.Services`, so `[ValueSource(ValueSource.ServiceLocator)]` can resolve request object dependencies from your Lambda function's service container.

```csharp
using System.Net.Http;
using System.Threading;

using NanoRoute;
using NanoRoute.AwsLambda;

public sealed class GetItemRequest
{
    public int Id { get; set; }

    [ValueSource(ValueSource.Parameter, Name = "filter")]
    public string? Filter { get; set; }

    [ValueSource(ValueSource.ServiceLocator)]
    public IItemRepository Items { get; set; } = null!;

    public CancellationToken Cancellation { get; set; }
}

ApiGatewayV2Router router = ApiGatewayV2Router
    .CreateBuilder()
    .AddJsonErrorDetails()
    .AddDefaultValueParsers()
    .AddPrefix("/items/{id:int}/*", items => items
        .AddEndpoint(["GET"], RouteScopeBuilder.CurrentExact, endpoint => endpoint
            .WithQueryBindings("{filter?:str(min=3)}")
            .WithHandler(static async (GetItemRequest request) =>
            {
                Item item = await request.Items.GetAsync(request.Id, request.Filter, request.Cancellation);
                return HttpResponseMessage.Json(item);
            })))
    .CreateRouter();
```

## Common Building Blocks

- `ApiGatewayV2Router.CreateBuilder()` starts a strongly typed builder for API Gateway HTTP API and Lambda Function URL payload-format-2.0 scenarios.
- `ApiGatewayV2RouterConfig` inherits the core `RouterConfig`, including `MatchingPrecedence`, and adds `LambdaTimeoutBuffer`.
- `ConfigureRouting()` customizes `ApiGatewayV2RouterConfig` before creating a router snapshot.
- `Route(APIGatewayHttpApiV2ProxyRequest, IServiceProvider, TimeSpan)` executes the NanoRoute pipeline and returns an API Gateway v2 proxy response.
- `AddDefaultValueParsers()` registers the built-in `int`, `guid`, `bool`, and `str` route parsers.
- `AddQueryBindings()` and `EndpointBuilder.WithQueryBindings()` bind selected query-string values into `RequestContext.Parameters`.
- `ConfigureQueryParsing()` customizes query-binding behavior used by subsequently registered `AddQueryBindings()` and `EndpointBuilder.WithQueryBindings()` middleware.
- `AddEndpoint()` and `CreateEndpoint()` capture endpoint verbs and route patterns once; endpoint helpers such as `WithHandler()`, `WithJsonBody()`, and `WithQueryBindings()` work under Lambda the same way they do in the core package.
- `AddJsonBody()` and `EndpointBuilder.WithJsonBody()` bind JSON request content into `RequestContext.Parameters`.
- `AddJsonErrorDetails()` turns routing exceptions into JSON `ErrorDetails` responses when explicitly added.
- `ConfigureJsonErrorDetails()` customizes JSON `ErrorDetails` response diagnostics and serialization metadata used by subsequently registered `AddJsonErrorDetails()` middleware.
- `AddHandler<TRequest>()` and `EndpointBuilder.WithHandler<TRequest>()` project `RequestContext` into a typed request object before invoking the handler.
- `HttpResponseMessage.Json(...)` creates JSON responses with the library's serializer defaults.

## Supported API Gateway Model

This package is intentionally narrow:

- Supported: API Gateway HTTP API events represented by `APIGatewayHttpApiV2ProxyRequest`.
- Supported: Lambda Function URL events represented by `APIGatewayHttpApiV2ProxyRequest`.
- Supported: Lambda proxy responses represented by `APIGatewayHttpApiV2ProxyResponse`.
- Not currently supported: REST API payload format `1.0`, Application Load Balancer events, or custom event models.

For unsupported transports, derive a custom router from the core `Router<TDescendant, TConfig>` type and map your event model to `HttpRequestMessage` before calling `Handle()`.
