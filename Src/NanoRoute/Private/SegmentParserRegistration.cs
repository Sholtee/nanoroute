/********************************************************************************
* SegmentParserRegistration.cs                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute.Internals
{
    /// <summary>
    /// Stores a named route-segment parser together with its argument binder.
    /// </summary>
    internal record struct SegmentParserRegistration(string Name, SegmentParserDelegate Parse, BindArgumentsDelegate BindArguments);
}
