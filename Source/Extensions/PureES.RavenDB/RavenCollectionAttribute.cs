// ReSharper disable ClassNeverInstantiated.Global

namespace PureES.RavenDB;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class RavenCollectionAttribute : Attribute
{
    public RavenCollectionAttribute(string name) => Name = name;
    public string Name { get; }
}