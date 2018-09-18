using System;

/// <summary>
/// Namespace to use.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public class NamespaceAttribute : Attribute
{
    /// <summary>
    /// Initialize a new instance of <see cref="NamespaceAttribute"/>
    /// </summary>
    public NamespaceAttribute(string @namespace)
    {
    }
}

/// <summary>
/// Apply this attribute to a auto-implemented get/set property, and it will be transformed in a Xamarin bindable property
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class BindableAttribute : Attribute
{
}