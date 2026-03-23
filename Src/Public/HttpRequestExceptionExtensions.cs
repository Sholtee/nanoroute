/********************************************************************************
* HttpRequestExceptionExtensions.cs                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;

namespace NanoRoute
{
    /// <summary>
    /// 
    /// </summary>
    public static class HttpRequestExceptionExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        public const string ERRORS_NAME = "Errors";

        /// <summary>
        /// 
        /// </summary>
        public const string STATUS_NAME = "StatusCode";
    
        extension(HttpRequestException)
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="status"></param>
            /// <param name="title"></param>
            /// <param name="errors"></param>
            [DoesNotReturn]
            public static void Throw(HttpStatusCode status, string title, params string[] errors)
            {
                HttpRequestException ex = new(title);
                ex.Data[STATUS_NAME] = status;
                if (errors.Length > 0)
                    ex.Data[ERRORS_NAME] = errors;
                throw ex;
            }
        }
    }
}
