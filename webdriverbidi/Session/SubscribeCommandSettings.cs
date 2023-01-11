namespace WebDriverBidi.Session;

using System;
using Newtonsoft.Json;

[JsonObject(MemberSerialization.OptIn)]
public class SubscribeCommandSettings : CommandSettings
{
    private readonly List<string> eventList = new();

    private readonly List<string> contextList = new();

    public SubscribeCommandSettings()
    {
    }

    public override string MethodName => "session.subscribe";

    public override Type ResultType => typeof(EmptyResult);

    [JsonProperty("events")]
    public List<string> Events => this.eventList;

    public List<string> Contexts => this.contextList;

    [JsonProperty("contexts", NullValueHandling = NullValueHandling.Ignore)]
    internal List<string>? SeralizableContexts
    {
        get
        {
            if (this.contextList.Count == 0)
            {
                return null;
            }

            return this.contextList;
        }
    }
}