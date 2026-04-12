/********************************************************************************
* QueryParameterDefinition.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute.Internals
{
    internal sealed record QueryParameterDefinition(string Name, bool Optional, SegmentParser Parser);
}
