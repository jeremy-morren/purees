using Newtonsoft.Json;

namespace PureES.EventStores.Marten;

/// <summary>
/// A json converter which throws an exception. This forces the user to configure System.Text.Json serialization
/// </summary>
internal class NewtonsoftJsonEventConverter : Newtonsoft.Json.JsonConverter<MartenEvent>
{
    public override void WriteJson(JsonWriter writer, MartenEvent? value, JsonSerializer serializer)
    {
        throw new NotImplementedException(ErrorMessage);
    }

    public override MartenEvent? ReadJson(JsonReader reader, 
        Type objectType, 
        MartenEvent? existingValue, 
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        throw new NotImplementedException(ErrorMessage);
    }

    private const string ErrorMessage =
        "Newtonsoft.Json is not supported. Configure StoreOptions to use System.Text.Json";
}