# NanoRoute

NanoRoute is a small router for `HttpRequestMessage` pipelines with an optional `HttpListener` adapter.

The API documentation for this package is generated from the XML comments in the source. The links below are the intended published locations for the generated pages.

## Core Types

- [RouteBuilder](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouteBuilder.html)
- [Router](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.Router.html)
- [RouterBuilder`2](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RouterBuilder-2.html)
- [HttpListenerRouter](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.HttpListenerRouter.html)
- [RequestContext](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestContext.html)
- [ErrorDetails](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.ErrorDetails.html)
- [SegmentParserDelegate](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.SegmentParserDelegate.html)
- [RequestHandlerDelegate](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestHandlerDelegate.html)

## Highlights

- Routes can be exact matches or prefix matches depending on whether the pattern ends with `/`.
- Segment parsers can be synchronous or asynchronous, and they can optionally bind route-template arguments such as `{id:int(min=1)}` once during registration.
- Parser-backed segments support optional parameter names. `{id:int}` stores the parsed value in `RequestContext.Parameters`, while `{int}` only validates the segment.
- `WithBase()` creates scoped route subtrees without forcing you to repeat common prefixes.
- `MatchingBehavior` lets you choose whether literal or parameterized child segments take precedence.
- `NanoRoute.Json` adds JSON request binding and JSON error/response helpers on top of the core pipeline.

## Segment Parser Syntax

- `{parameterName:parserName}` parses a segment and stores the parsed value under `parameterName`.
- `{parserName}` parses a segment without storing it in `RequestContext.Parameters`.
- `{parameterName:parserName(arg=value, text='hello')}` also passes a case-insensitive raw argument map through the parser's `BindArgumentsDelegate`.
- Parser arguments support `null`, `true` or `false`, numbers, and single-quoted strings with `\'` escaping.

Use `AddSegmentParser()` to register custom parsers, or `AddDefaultParsers()` to register the built-in `int`, `guid`, `bool`, and `str` parsers.

