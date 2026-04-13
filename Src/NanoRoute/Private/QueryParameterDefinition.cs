/********************************************************************************
* QueryParameterDefinition.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute.Internals
{
    internal sealed record QueryParameterDefinition(string Name, int Index, bool Optional, ValueParser Parser);
}

