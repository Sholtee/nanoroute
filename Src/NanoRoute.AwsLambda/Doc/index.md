# NanoRoute.AwsLambda

NanoRoute.AwsLambda adds AWS Lambda adapters for NanoRoute while keeping the core package transport-neutral.

The API documentation for this package is generated from the XML comments in the source. The links below are the intended published locations for the generated pages.

## Core Types

- [ApiGatewayHttpApiV2Router](https://sholtee.github.io/nanoroute/docs/NanoRoute.AwsLambda/NanoRoute.AwsLambda.ApiGatewayHttpApiV2Router.html)
- [AwsLambdaRouterConfig](https://sholtee.github.io/nanoroute/docs/NanoRoute.AwsLambda/NanoRoute.AwsLambda.AwsLambdaRouterConfig.html)

## Highlights

- Supports Amazon API Gateway HTTP APIs and Lambda Function URLs that use payload format version `2.0`.
- Translates `APIGatewayHttpApiV2ProxyRequest` events into `HttpRequestMessage` instances before running the normal NanoRoute pipeline.
- Converts produced `HttpResponseMessage` instances into `APIGatewayHttpApiV2ProxyResponse` values.
- `ApiGatewayHttpApiV2Router.CreateBuilder()` returns the same strongly typed builder style as the core package.
- Pass `ILambdaContext.RemainingTime` to `Route()` so the adapter can cancel work shortly before the Lambda runtime terminates the invocation.
- Request URIs are built from `RawPath`, `RawQueryString`, the `Host` header, and forwarding metadata.
- Plain request bodies are exposed as `StringContent`; base64-encoded request bodies are exposed as `StreamContent`.
- The original `APIGatewayHttpApiV2ProxyRequest` is available through the NanoRoute request context as the original request object.
- String responses are returned as plain bodies, while other content types are base64 encoded.
- `Set-Cookie` values are returned through the API Gateway `Cookies` collection.
- Typed handlers, route parsers, query bindings, JSON body binders, prefixes, and middleware work the same way under Lambda as they do in the core package.

## Supported API Gateway Model

This package is intentionally narrow:

- Supported: API Gateway HTTP API events represented by `APIGatewayHttpApiV2ProxyRequest`.
- Supported: Lambda Function URL events represented by `APIGatewayHttpApiV2ProxyRequest`.
- Supported: Lambda proxy responses represented by `APIGatewayHttpApiV2ProxyResponse`.
- Not currently supported: REST API payload format `1.0`, Application Load Balancer events, or custom event models.

For unsupported transports, derive a custom router from the core `Router` type and map your event model to `HttpRequestMessage` before calling `Handle()`.
