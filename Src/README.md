# NanoRoute ![Tests](https://sholtee.github.io/nanoroute/badges/tests-badge.svg) [![Coverage](https://sholtee.github.io/nanoroute/badges/coverage-badge.svg)](https://sholtee.github.io/nanoroute/CoverageReport/)

NanoRoute is a small, dependency-light router for request/response pipelines.

## Quick Start

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using NanoRoute;

sealed class HttpRouter : HttpMessageRouter
{
    public Task<HttpResponseMessage> RouteAsync(HttpRequestMessage request, IServiceProvider services) =>
        Handle(request, services);
}

HttpRouter router = new HttpRouter();

router
    .AddParameterParser("int", (string segment, out object? parsed) =>
    {
        if (int.TryParse(segment, out int value))
        {
            parsed = value;
            return true;
        }

        parsed = null;
        return false;
    })
    .AddHandler("GET", "/api/users/{user_id:int}/", (context, next) =>
    {
        context.Parameters["User"] = $"user-{context.Parameters["user_id"]}";
        return next();
    })
    .AddHandler("GET", "/api/users/{user_id:int}/details", (context, next) =>
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent((string) context.Parameters["User"]!)
        });
    });

HttpResponseMessage response = await router.RouteAsync(
    new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/users/42/details"),
    services: new ServiceCollection().BuildServiceProvider());
```

In this example, `/api/users/{user_id:int}/` is a prefix route, so it runs before the more specific `/api/users/{user_id:int}/details` handler and can populate shared data in `Parameters`.

## Matching Rules

- A trailing `/` makes a route a prefix match.
- Without a trailing `/`, the route matches only the exact path.
- Literal segments are matched case-insensitively.
- Parameter segments use registered parsers such as `{user_id:int}`.
- When multiple handlers match, NanoRoute evaluates the shortest compatible prefix first.
- At the same path depth, literal segments are preferred over parameter matches.

## Core Types

- [Router<TRequest, TResponse>](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.Router-2.html)
- [HttpMessageRouter](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.HttpMessageRouter.html)
- [RequestContext<TRequest>](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestContext-1.html)
- [ParameterParserDelegate](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ParameterParserDelegate.html)
- [RequestHandler<TRequest, TResponse>](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestHandler-2.html)

## Documentation

API documentation is generated from the XML comments in the source and published at:

- <https://sholtee.github.io/nanoroute/docs/NanoRoute/>
