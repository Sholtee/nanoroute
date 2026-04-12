/********************************************************************************
* ValueParserRegistration.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute.Internals
{
    /// <summary>
    /// Stores a named route-value parser together with its argument binder.
    /// </summary>
    internal sealed record ValueParserRegistration(string Name, ValueParserDelegate Parse, BindArgumentsDelegate BindArguments);
}

