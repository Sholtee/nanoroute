# NanoRoute.HttpListener

NanoRoute.HttpListener adds a `System.Net.HttpListener` adapter for NanoRoute while keeping the core package transport-neutral. It converts incoming `HttpListenerContext` requests into the shared `HttpRequestMessage` pipeline and writes the produced `HttpResponseMessage` back to the listener response.

NanoRoute.HttpListener targets `netstandard2.0` and `netstandard2.1`.

## Install

```shell
dotnet add package NanoRoute.HttpListener --prerelease
```

## Quick Start

Create a reusable router once, then run it with `SimpleHttpListenerHost`:

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using NanoRoute;
using NanoRoute.HttpListener;

IServiceProvider services = new ServiceCollection().BuildServiceProvider();

HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddJsonErrorDetails()
    .AddEndpoint("GET", "/health/", endpoint => endpoint
        .WithHandler(static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("ok")
        })))
    .CreateRouter();

SimpleHttpListenerHost host = new
(
    "http://localhost:8080/",
    workerCount: 4,
    queueCapacity: 64,
    router,
    services
);

host.RunUntilCancelKeyPress();
```

`SimpleHttpListenerHost` owns the listener loop, creates a service scope per request, limits the total number of running or waiting requests, and stops gracefully on Ctrl+C. `workerCount` and `queueCapacity` must both be greater than zero. If you need custom accept loops, concurrency, or shutdown behavior, call `HttpListenerRouter.Route()` from your own `HttpListener` loop instead.

## Typed Binding Example

The adapter uses the same router builder APIs as the core package. Route parameters, query bindings, JSON bodies, services, `RequestContext`, and `CancellationToken` can all be projected into typed request objects before your handler runs:

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using NanoRoute;
using NanoRoute.HttpListener;

// UserRepository is your application service that implements IUserRepository.
IServiceProvider services = new ServiceCollection()
    .AddSingleton<IUserRepository, UserRepository>()
    .BuildServiceProvider();

HttpListenerRouter router = HttpListenerRouter
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

using HttpListener listener = new();
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

## At A Glance

- `HttpListenerRouter.CreateBuilder()` starts a strongly typed builder for `HttpListener` scenarios.
- `HttpListenerRouter` derives from the core `RouterBase<HttpListenerRouterConfig>` helper.
- `HttpListenerRouterConfig` inherits the core `RouterConfig`, including `MatchingPrecedence`.
- `Route(HttpListenerContext, IServiceProvider, CancellationToken)` executes the NanoRoute pipeline and writes the listener response.
- The original `HttpListenerRequest` is available through the NanoRoute request context as the original request object.
- The listener request trace identifier is used as the NanoRoute trace id.
- `SimpleHttpListenerHost` is a small host helper for prefix-based listeners, bounded request queueing, scoped services, and Ctrl+C shutdown.
- Endpoint builders and helpers such as `WithJsonBody()` and `WithQueryBindings()` work under `HttpListener` the same way they do in the core package.

## Documentation

Full package documentation and API reference are published at:

- <https://sholtee.github.io/nanoroute/docs/NanoRoute.HttpListener/>
