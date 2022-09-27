// ReSharper disable ClassNeverInstantiated.Global
namespace PureES.RavenDB;



[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class RavenCollectionAttribute : Attribute
{
    public string Name { get; }

    public RavenCollectionAttribute(string name) => Name = name;
}