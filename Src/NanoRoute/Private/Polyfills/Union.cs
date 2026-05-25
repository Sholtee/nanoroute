/********************************************************************************
* Union.cs                                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/

// TODO: remove once PolySharp will support this
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Defines the contract for union types, providing access to the underlying value.
    /// </summary>
    internal interface IUnion
    {
        /// <summary>
        /// Gets the underlying value of the union.
        /// </summary>
        object? Value { get; }
    }

    /// <summary>
    /// Indicates that a type is a union type, allowing the C# compiler to recognize it for union type features.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    internal sealed class UnionAttribute : Attribute
    {
    }
}