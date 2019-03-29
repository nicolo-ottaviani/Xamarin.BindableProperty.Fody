using Mono.Cecil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;

public static class Utils
{
    public static bool DerivesFrom(this TypeDefinition type, string @namespace, string name, bool ignoreUnresolvedTypes = false)
    {
        while (true)
        {
            if (type.Namespace == @namespace && type.Name == name) return true;
            if (type.BaseType == null) return false;
            try
            {
                type = type.BaseType.Resolve();
            }
            catch (Mono.Cecil.AssemblyResolutionException)
            {
                if (ignoreUnresolvedTypes) return false;
                throw;
            }
        }
    }

    public static MethodDefinition WithParams(this MethodDefinition x, params ParameterDefinition[] pars)
    {
        foreach (var p in pars) x.Parameters.Add(p);
        return x;
    }

    public static MethodReference WithParams(this MethodReference x, params ParameterDefinition[] pars)
    {
        foreach (var p in pars) x.Parameters.Add(p);
        return x;
    }

    public static MethodReference WithParams(this MethodReference x, params TypeReference[] pars)
    {
        foreach (var p in pars) x.Parameters.Add(new ParameterDefinition(p));
        return x;
    }

    public static IEnumerable<System.Xml.Linq.XAttribute> XPathSelectAttributes(this System.Xml.Linq.XElement xelement, string expression)
        => ((IEnumerable)xelement.XPathEvaluate(expression)).Cast<System.Xml.Linq.XAttribute>();
    public static IEnumerable<System.Xml.Linq.XAttribute> XPathSelectAttributes(this System.Xml.Linq.XElement xelement, string expression, System.Xml.IXmlNamespaceResolver resolver)
        => ((IEnumerable)xelement.XPathEvaluate(expression, resolver)).Cast<System.Xml.Linq.XAttribute>();
    public static IEnumerable<System.Xml.Linq.XAttribute> XPathSelectAttributes(this System.Xml.Linq.XDocument xdocument, string expression)
        => ((IEnumerable)xdocument.XPathEvaluate(expression)).Cast<System.Xml.Linq.XAttribute>();
    public static IEnumerable<System.Xml.Linq.XAttribute> XPathSelectAttributes(this System.Xml.Linq.XDocument xdocument, string expression, System.Xml.IXmlNamespaceResolver resolver)
        => ((IEnumerable)xdocument.XPathEvaluate(expression, resolver)).Cast<System.Xml.Linq.XAttribute>();
    public static System.Xml.Linq.XAttribute XPathSelectAttribute(this System.Xml.Linq.XElement xelement, string expression)
        => ((IEnumerable)xelement.XPathEvaluate(expression)).Cast<System.Xml.Linq.XAttribute>().FirstOrDefault();
    public static System.Xml.Linq.XAttribute XPathSelectAttribute(this System.Xml.Linq.XElement xelement, string expression, System.Xml.IXmlNamespaceResolver resolver)
        => ((IEnumerable)xelement.XPathEvaluate(expression, resolver)).Cast<System.Xml.Linq.XAttribute>().FirstOrDefault();
    public static System.Xml.Linq.XAttribute XPathSelectAttribute(this System.Xml.Linq.XDocument xdocument, string expression)
        => ((IEnumerable)xdocument.XPathEvaluate(expression)).Cast<System.Xml.Linq.XAttribute>().FirstOrDefault();
    public static System.Xml.Linq.XAttribute XPathSelectAttribute(this System.Xml.Linq.XDocument xdocument, string expression, System.Xml.IXmlNamespaceResolver resolver)
        => ((IEnumerable)xdocument.XPathEvaluate(expression, resolver)).Cast<System.Xml.Linq.XAttribute>().FirstOrDefault();


}

