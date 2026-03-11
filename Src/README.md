# NanoRoute ![Tests](https://sholtee.github.io/nanoroute/badges/tests-badge.svg) [![Coverage](https://sholtee.github.io/nanoroute/badges/coverage-badge.svg)](https://sholtee.github.io/nanoroute/CoverageReport/)


NanoRoute is a small, dependency-light router for request/response pipelines.

It matches requests by HTTP method and URI path, supports literal and parameterized segments, treats path matching as case-insensitive, and lets multiple compatible handlers form a pipeline through `next()`.

## Features

- Exact and prefix route matching
- Literal and parser-based parameter segments
- Case-insensitive path matching
- String-based HTTP method handlers or handlers registered for all methods
- Handler pipelines that can share data through `RequestContext<TRequest>.Parameters`
- Minimal abstraction surface for custom request types

## Quick Start

```csharp
using System;
using System.Net.Http;

using Microsoft.Extensions.DependencyInjection;
using NanoRoute;

sealed class HttpRouter : Router<HttpRequestMessage, string>
{
    protected override Uri GetUri(HttpRequestMessage request) => request.RequestUri!;

    protected override string GetRequestId(HttpRequestMessage request) =>
        request.Headers.TryGetValues("X-Request-Id", out var values)
            ? string.Join(",", values)
            : Guid.NewGuid().ToString("N");

    protected override string GetVerb(HttpRequestMessage request) => request.Method.Method;
}

Router<HttpRequestMessage, string> router = new HttpRouter()
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
        return (string) context.Parameters["User"]!;
    });

string response = router.Handle(
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

## Main Types

- [Router<TRequest, TResponse>](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.Router-2.html): base class for extracting URI, request id, and HTTP method from your request type.
- [RequestContext<TRequest>](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestContext-1.html): exposes the request, services, and shared route parameters.
- [ParameterParserDelegate](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ParameterParserDelegate.html): validates and parses a single path segment.
- [RequestHandler<TRequest, TResponse>](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestHandler-2.html): handler delegate for pipeline-based request processing.

## Documentation

API documentation is generated from the XML comments in the source and published at:

- <https://sholtee.github.io/nanoroute/docs/NanoRoute/>
