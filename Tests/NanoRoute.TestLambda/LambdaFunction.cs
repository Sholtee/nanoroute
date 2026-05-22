/********************************************************************************
* LambdaFunction.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace NanoRoute.TestLambda
{
    using AwsLambda;

    /// <summary>
    /// Lambda function fixture for future NanoRoute.AwsLambda integration tests.
    /// </summary>
    public sealed class LambdaFunction
    {
        private static readonly IServiceProvider s_services = new EmptyServiceProvider();

        private static readonly ApiGatewayV2Router s_router = ApiGatewayV2Router
            .CreateBuilder()
            .ConfigureJsonErrorDetails(config => config with
            {
                PopulateErrorInfo = true
            })
            .AddJsonErrorDetails()
            .AddDefaultValueParsers()
            .AddEndpoint("GET", "/health/", endpoint => endpoint
                .WithHandler(static async (_, _) =>
                {
                    await Task.Yield();

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("ok")
                    };
                }))
            .AddPrefix("/items/{id:int(min=1)}/*", item => item
                .AddEndpoint("GET", RouteScopeBuilder.CurrentExact, endpoint => endpoint
                    .WithQueryBindings("{filter?:str(min=3)}")
                    .WithHandler(static async (context, _) =>
                    {
                        await Task.Yield();

                        return HttpResponseMessage.Json(new
                        {
                            id = context.Parameters["id"],
                            filter = context.Parameters.TryGetValue("filter", out object? filter)
                                ? filter
                                : null
                        });
                    })))
            .AddPrefix("/echo/*", echo => echo
                .AddEndpoint("POST", RouteScopeBuilder.CurrentExact, endpoint => endpoint
                    .WithJsonBody(JsonContext.Default.EchoRequest, "body")
                    .WithHandler(static async (context, _) =>
                    {
                        await Task.Yield();

                        return HttpResponseMessage.Json(context.Parameters["body"]);
                    })))
            .AddEndpoint("GET", "/cookies/", endpoint => endpoint
                .WithHandler(static async (_, _) =>
                {
                    await Task.Yield();

                    HttpResponseMessage response = HttpResponseMessage.Json(new
                    {
                        cookies = true
                    });

                    response.Headers.Add("Set-Cookie", "nano-route-cookie=ok; Path=/; HttpOnly");
                    response.Headers.Add("X-NanoRoute-Fixture", "aws-lambda");

                    return response;
                }))
            .CreateRouter();

        /// <summary>
        /// Handles API Gateway HTTP API and Lambda Function URL payload-format-2.0 requests.
        /// </summary>
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Lambda handler cannot be static")]
        public Task<APIGatewayHttpApiV2ProxyResponse> Handler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(context);

            return s_router.Route(request, s_services, context.RemainingTime);
        }

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }
    }
}
