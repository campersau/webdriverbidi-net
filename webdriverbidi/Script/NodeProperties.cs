namespace WebDriverBidi.Script;

using Newtonsoft.Json;

[JsonObject(MemberSerialization.OptIn)]
public class NodeProperties
{
    private uint nodeType = 0;
    private uint childNodeCount = 0;
    private string? nodeValue;
    private string? localName;
    private string? namespaceUri;
    private List<RemoteValue>? children;
    private NodeAttributes? attributes;
    private Dictionary<string, string>? attributesDictionary;
    private RemoteValue? shadowRoot;

    [JsonConstructor]
    internal NodeProperties()
    {
    }

    [JsonProperty("nodeType", Required = Required.Always)]
    public uint NodeType { get => this.nodeType; internal set => this.nodeType = value; }

    [JsonProperty("childNodeCount", Required = Required.Always)]
    public uint ChildNodeCount { get => this.childNodeCount; internal set => this.childNodeCount = value; }

    [JsonProperty("nodeValue", NullValueHandling = NullValueHandling.Ignore)]
    public string? NodeValue { get => this.nodeValue; internal set => this.nodeValue = value; }

    [JsonProperty("localName", NullValueHandling = NullValueHandling.Ignore)]
    public string? LocalName { get => this.localName; internal set => this.localName = value; }

    [JsonProperty("namespaceURI", NullValueHandling = NullValueHandling.Ignore)]
    public string? NamespaceUri { get => this.namespaceUri; internal set => this.namespaceUri = value; }

    public IList<RemoteValue>? Children
    {
        get
        {
            if (this.children is null)
            {
                return null;
            }

            return this.children.AsReadOnly();
        }
    }

    public NodeAttributes? Attributes
    {
        get
        {
            if (this.attributesDictionary is null)
            {
                return null;
            }

            this.attributes ??= new NodeAttributes(this.attributesDictionary);

            return this.attributes;
        }
    }

    [JsonProperty("shadowRoot", NullValueHandling = NullValueHandling.Ignore)]
    public RemoteValue? ShadowRoot { get => this.shadowRoot; internal set => this.shadowRoot = value; }

    [JsonProperty("children", NullValueHandling = NullValueHandling.Ignore)]
    internal List<RemoteValue>? SerializableChildren { get => this.children; set => this.children = value; }

    [JsonProperty("attributes", NullValueHandling = NullValueHandling.Ignore)]
    internal Dictionary<string, string>? SerializableAttributes { get => this.attributesDictionary; set => this.attributesDictionary = value; }
}