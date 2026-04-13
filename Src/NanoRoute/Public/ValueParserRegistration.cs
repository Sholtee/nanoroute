/********************************************************************************
* ValueParserRegistration.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute
{
    /// <summary>
    /// Stores a named value parser together with its argument binder.
    /// </summary>
    public sealed record ValueParserRegistration(string Name, ValueParserDelegate Parse, BindArgumentsDelegate BindArguments);
}

