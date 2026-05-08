# NanoRoute

NanoRoute is a small router for `HttpRequestMessage` pipelines with an optional `HttpListener` adapter.

The API documentation for this package is generated from the XML comments in the source. The links below are the intended published locations for the generated pages.

## Core Types

- [RouteBuilder](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouteBuilder.html)
- [BuilderMetadata](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.BuilderMetadata.html)
- [Router](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.Router.html)
- [RouterBuilder`2](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouterBuilder-2.html)
- [HttpListenerRouter](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.HttpListenerRouter.html)
- [RequestContext](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestContext.html)
- [ErrorDetails](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ErrorDetails.html)
- [ValueParserDelegate](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ValueParserDelegate.html)
- [RequestHandlerDelegate](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestHandlerDelegate.html)
- [NanoRouteHandlerExtensions](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.HandlerExtensions.NanoRouteHandlerExtensions.html)
- [ValueSource](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.HandlerExtensions.ValueSource.html)
- [ValueSourceAttribute](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.HandlerExtensions.ValueSourceAttribute.html)

## Highlights

- Routes can be exact matches or prefix matches depending on whether the pattern ends with `/`.
- Route patterns must start with `/`, and repeated `/` separators such as `//` are invalid.
- Value parsers can be synchronous or asynchronous, and they can optionally bind route-template arguments such as `{id:int(min=1)}` once during registration.
- Parser-backed segments support optional parameter names. `{id:int}` stores the parsed value in `RequestContext.Parameters`, while `{int}` only validates the segment.
- `AddPrefix()` and `CreatePrefix()` create scoped route subtrees without forcing you to repeat common prefixes.
- `RouteBuilder.Metadata` stores type-keyed extension settings and follows the same scoped inheritance model as prefix builders.
- `RouterConfig` is immutable and can be replaced with `WithConfiguration(config => config with { ... })` before creating a router snapshot.
- `MatchingPrecedence` lets you choose whether literal or parameterized child segments are selected first.
- Once a child branch has been selected for a request, NanoRoute continues only within that branch.
- `AddQueryBindings()` uses query descriptors such as `{filter:str(min=3)}&{page?:int(min=1)}` and matches query keys through `Uri.Query` normalization.
- `AddHandler<TRequestContext>()` can project route parameters, query bindings, services, keyed services, `RequestContext`, and `CancellationToken` into typed request objects.
- `ValueSourceAttribute` customizes typed-handler property binding with `Context`, `ServiceLocator`, and `Skip` sources.
- `NanoRoute.Json` adds JSON request binding and JSON error/response helpers on top of the core pipeline.

## Value Parser Syntax

- `{parameterName:parserName}` parses a segment and stores the parsed value under `parameterName`.
- `{parserName}` parses a segment without storing it in `RequestContext.Parameters`.
- `{parameterName:parserName(arg=value, text='hello')}` also passes a case-insensitive raw argument map through the parser's `BindArgumentsDelegate`.
- Parser arguments support `null`, `true` or `false`, numbers, and single-quoted strings with `\'` escaping.

Use `AddValueParser()` to register custom parsers, or `AddDefaultValueParsers()` to register the built-in `int`, `guid`, `bool`, and `str` parsers.

