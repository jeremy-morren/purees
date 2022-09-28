using System.Text.Json;
using System.Text.Json.Nodes;
using PureES.EventStoreDB.Serialization;

namespace PureES.Extensions.Tests.EventStore;

public class TestSerializer : EventStoreDBSerializer<object>
{
    static TestSerializer()
    {
        TypeMapper = new TypeMapper();
        TypeMapper.AddType(typeof(JsonObject));
    }
    
    public TestSerializer() 
        : base(JsonOptions, TypeMapper)
    {
    }

    private static readonly TypeMapper TypeMapper;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}