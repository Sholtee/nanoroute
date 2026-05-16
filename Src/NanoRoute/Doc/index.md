# NanoRoute

NanoRoute is a small router for `HttpRequestMessage` pipelines with an optional `HttpListener` adapter.

The API documentation for this package is generated from the XML comments in the source. The links below are the intended published locations for the generated pages.

## Core Types

- [RouteScopeBuilder](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouteScopeBuilder.html)
- [BuilderMetadata](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.BuilderMetadata.html)
- [ConfigureBuilderDelegate`1](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ConfigureBuilderDelegate-1.html)
- [ExceptionHandlingConfig](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ExceptionHandlingConfig.html)
- [Router](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.Router.html)
- [RouterConfig](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouterConfig.html)
- [RouterBuilder`2](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouterBuilder-2.html)
- [EndPointBuilder](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.EndPointBuilder.html)
- [HttpListenerRouter](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.HttpListenerRouter.html)
- [RequestContext](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestContext.html)
- [QueryParsingConfig](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.QueryParsingConfig.html)
- [UnexpectedParameterBehavior](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.UnexpectedParameterBehavior.html)
- [ErrorDetails](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ErrorDetails.html)
- [ValueParserDelegate](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ValueParserDelegate.html)
- [RequestHandlerDelegate](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestHandlerDelegate.html)
- [NanoRouteHandlerExtensions](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.NanoRouteHandlerExtensions.html)
- [NanoRouteEndPointExtensions](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.NanoRouteEndPointExtensions.html)
- [NanoRoutePrefixExtensions](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.NanoRoutePrefixExtensions.html)
- [ValueSource](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ValueSource.html)
- [ValueSourceAttribute](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ValueSourceAttribute.html)

## Highlights

- Exact route patterns must start and end with `/`; prefix route patterns must start with `/` and end with `/*`.
- Repeated `/` separators such as `//` are invalid.
- Value parsers can be synchronous or asynchronous, and they can optionally bind route-template arguments such as `{id:int(min=1)}` once during registration.
- Parser-backed segments support optional parameter names. `{id:int}` stores the parsed value in `RequestContext.Parameters`, while `{int}` only validates the segment.
- `AddPrefix()` and `CreatePrefix()` create scoped route subtrees without forcing you to repeat common prefixes.
- `RouteScopeBuilder.Metadata` stores type-keyed extension settings and follows the same scoped inheritance model as prefix scopes. It is public for extension authors and is not the normal application configuration surface.
- `RouterConfig` is immutable and can be replaced with `ConfigureRouting(config => config with { ... })` before creating a router snapshot.
- `MatchingPrecedence` lets you choose whether literal or parameterized child segments are selected first.
- `ParametersCapacity` sets the initial capacity of the per-request `RequestContext.Parameters` dictionary.
- Once a child branch has been selected for a request, NanoRoute continues only within that branch.
- `AddQueryBindings()` uses query descriptors such as `{filter:str(min=3)}&{page?:int(min=1)}&{tag:str(min=2)[]}` and matches query keys through `Uri.Query` normalization. Undeclared query keys are ignored by default, or rejected when `ConfigureQueryParsing()` sets `UnexpectedParameterBehavior` to `Reject`.
- `ConfigureQueryParsing()` stores scoped query-binding settings used by subsequently registered `AddQueryBindings()` middleware.
- `ConfigureExceptionHandling()` stores scoped exception-normalization settings used by subsequently registered `AddExceptionHandler()` middleware.
- `ConfigureJsonErrorDetails()` stores scoped JSON `ErrorDetails` response settings used by subsequently registered `AddJsonErrorDetails()` middleware.
- Handler convenience overloads such as pattern-only registration, multi-verb registration, and `AddHandler<TRequestContext>()` are extension methods in the `NanoRoute` namespace.
- `AddHandler<TRequestContext>()` can project route parameters, query bindings, services, keyed services, `RequestContext`, and `CancellationToken` into typed request objects.
- `ValueSourceAttribute` customizes typed-handler property binding with `Parameter`, `ServiceLocator`, and `Skip` sources.
- `AddEndPoint()` and `CreateEndPoint()` create endpoint builders that capture HTTP verbs and an exact or prefix route pattern once.
- Endpoint-aware helpers such as `WithHandler()` and `WithJsonBody()` register endpoint-local middleware without repeating the endpoint route.

## Value Parser Syntax

- `{parameterName:parserName}` parses a segment and stores the parsed value under `parameterName`.
- `{parserName}` parses a segment without storing it in `RequestContext.Parameters`.
- `{parameterName:parserName(arg=value, text='hello')}` also passes a case-insensitive raw argument map through the parser's `BindArgumentsDelegate`.
- Query bindings may add `[]` after the parser definition, such as `{tag:str[]}` or `{tag:str(min=2)[]}`, to collect repeated query keys into a `List<object?>` in request order.
- List parser syntax is supported for query bindings only, not route path parameters.
- Parser arguments support `null`, `true` or `false`, numbers, and single-quoted strings with `\'` escaping.

Use `AddValueParser()` to register custom parsers, or `AddDefaultValueParsers()` to register the built-in `int`, `guid`, `bool`, and `str` parsers.

