# NanoRoute

NanoRoute is a small, dependency-light router for `HttpRequestMessage` pipelines, with optional transport adapters and focused helpers for JSON payloads and error handling.

The core library is centered around `RouteScopeBuilder`, `Router`, and `RequestContext`, so you can plug the routing pipeline into your own transport or hosting model as well.

NanoRoute targets `netstandard2.0` and `netstandard2.1`, and is compatible with Native AOT scenarios.

For AWS Lambda integrations, use the separate [NanoRoute.AwsLambda](https://www.nuget.org/packages/NanoRoute.AwsLambda/) package.

## Quick Start

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using NanoRoute;

// UserRepository is your application service that implements IUserRepository.
IServiceProvider services = new ServiceCollection()
    .AddSingleton<IUserRepository, UserRepository>()
    .BuildServiceProvider();

HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddDefaultValueParsers()
    .AddJsonErrorDetails()
    .AddEndPoint("GET", "/api/users/{user_id:int}/", endpoint => endpoint
        .WithHandler(static async (GetUserRequest request) =>
        {
            return HttpResponseMessage.Json(HttpStatusCode.OK, new UserResponse
            {
                Id = request.UserId,
                Name = await request.Users.GetNameAsync(request.UserId)
            });
        }))
    .AddEndPoint("POST", "/api/users/", endpoint => endpoint
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

HttpListener listener = new();
listener.Prefixes.Add("http://localhost:8080/");
listener.Start();

HttpListenerContext context = await listener.GetContextAsync();
await router.Route(context, services);

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

`AddEndPoint()` is the recommended application-level entry point for most routes: it captures the HTTP verb and route pattern once, then endpoint helpers such as `WithHandler()`, `WithJsonBody()`, and `WithQueryBindings()` add endpoint-local middleware without repeating the route. Typed handlers bind route values, query values, JSON bodies, services, and framework values into request objects before your handler runs.

`AddHandler()` is still available when you need lower-level pipeline composition, such as custom middleware chains or manually scoped prefix routes.

## At A Glance

- Exact route patterns start and end with `/`, for example `/items/`.
- Prefix route patterns start with `/` and end with `/*`, for example `/items/*`.
- `AddDefaultValueParsers()` registers the built-in `int`, `guid`, `bool`, and `str` parsers.
- `AddPrefix()` and `CreatePrefix()` define scoped route subtrees.
- `AddQueryBindings()` and `WithQueryBindings()` parse selected query-string values into `RequestContext.Parameters`.
- `AddJsonBody()` and `WithJsonBody()` bind JSON request content into `RequestContext.Parameters`.
- Typed handlers can bind route values, query values, JSON bodies, services, `RequestContext`, and `CancellationToken` into request objects.
- `AddJsonErrorDetails()` turns routing failures into JSON `ErrorDetails` responses.
- `HttpResponseMessage.Json(...)` creates JSON responses with the library's serializer defaults.

## Documentation

Full package documentation and API reference are published at:

- <https://sholtee.github.io/nanoroute/docs/NanoRoute/>
