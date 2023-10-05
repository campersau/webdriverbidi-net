// <copyright file="Proxy.cs" company="WebDriverBiDi.NET Committers">
// Copyright (c) WebDriverBiDi.NET Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace WebDriverBiDi.Session;

using Newtonsoft.Json;

/// <summary>
/// Object representing a proxy to be used by the browser.
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public class Proxy
{
    private ProxyType? type;
    private string? proxyAutoConfigUrl;
    private string? httpProxy;
    private string? sslProxy;
    private string? ftpProxy;
    private List<string>? noProxyAddresses;
    private string? socksProxy;
    private int? socksVersion;

    /// <summary>
    /// Gets an empty proxy with no properties set.
    /// </summary>
    public static Proxy EmptyProxy => new();

    /// <summary>
    /// Gets or sets the type of proxy.
    /// </summary>
    [JsonProperty("proxyType", NullValueHandling = NullValueHandling.Ignore)]
    public ProxyType? Type { get => this.type; set => this.type = value; }

    /// <summary>
    /// Gets or sets the URL to the proxy autoconfig (PAC) settings.
    /// </summary>
    [JsonProperty("proxyAutoconfigUrl", NullValueHandling = NullValueHandling.Ignore)]
    public string? ProxyAutoConfigUrl { get => this.proxyAutoConfigUrl; set => this.proxyAutoConfigUrl = value; }

    /// <summary>
    /// Gets or sets the address to be used to proxy HTTP commands.
    /// </summary>
    [JsonProperty("httpProxy", NullValueHandling = NullValueHandling.Ignore)]
    public string? HttpProxy { get => this.httpProxy; set => this.httpProxy = value; }

    /// <summary>
    /// Gets or sets the address to be used to proxy HTTPS commands.
    /// </summary>
    [JsonProperty("sslProxy", NullValueHandling = NullValueHandling.Ignore)]
    public string? SslProxy { get => this.sslProxy; set => this.sslProxy = value; }

    /// <summary>
    /// Gets or sets the address to be used to proxy FTP commands.
    /// </summary>
    [JsonProperty("ftpProxy", NullValueHandling = NullValueHandling.Ignore)]
    public string? FtpProxy { get => this.ftpProxy; set => this.ftpProxy = value; }

    /// <summary>
    /// Gets or sets the address of a SOCKS proxy used to proxy commands.
    /// </summary>
    [JsonProperty("socksProxy", NullValueHandling = NullValueHandling.Ignore)]
    public string? SocksProxy { get => this.socksProxy; set => this.socksProxy = value; }

    /// <summary>
    /// Gets or sets the version of the SOCKS proxy to be used.
    /// </summary>
    [JsonProperty("socksVersion", NullValueHandling = NullValueHandling.Ignore)]
    public int? SocksVersion { get => this.socksVersion; set => this.socksVersion = value; }

    /// <summary>
    /// Gets or sets a list of addresses to be bypassed by the proxy.
    /// </summary>
    [JsonProperty("noProxy", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? NoProxyAddresses { get => this.noProxyAddresses; set => this.noProxyAddresses = value; }
}
