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
- [RequestHandler](https://sholtee.github.io/nanoroute/docs/NanoRoute/NanoRoute.RequestHandler.html)

## Highlights

- Routes can be exact matches or prefix matches depending on whether the pattern ends with `/`.
- Segment parsers turn path segments into typed values that can be stored in `RequestContext.Parameters`.
- `WithBase()` creates scoped route subtrees without forcing you to repeat common prefixes.
- `MatchingBehavior` lets you choose whether literal or parameterized child segments take precedence.
- `NanoRoute.Json` adds JSON request binding and JSON error/response helpers on top of the core pipeline.

