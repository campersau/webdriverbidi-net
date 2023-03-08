// <copyright file="BrowserLauncher.cs" company="WebDriverBidi.NET Committers">
// Copyright (c) WebDriverBidi.NET Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace WebDriverBidi.Client;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public abstract class BrowserLauncher
{
    private static readonly SemaphoreSlim lockObject = new(1, 1);
    private readonly string launcherPath;
    private readonly string launcherExecutableName;
    private readonly HttpClient httpClient = new();
    private string launcherHostName = "localhost";
    private int launcherPort;
    private bool hideCommandPromptWindow;
    private TimeSpan initializationTimeout = TimeSpan.FromSeconds(20);
    private Process? launcherProcess;
    private string sessionId = string.Empty;
    private string webSocketUrl = string.Empty;

    protected BrowserLauncher(string launcherExecutablePath, string launcherExecutableName, int port)
    {
        this.launcherPath = launcherExecutablePath;
        this.launcherExecutableName = launcherExecutableName;
        this.launcherPort = port;
    }

    /// <summary>
    /// Occurs when the launcher process is starting. 
    /// </summary>
    public event EventHandler<BrowserLauncherProcessStartingEventArgs>? LauncherProcessStarting;

    /// <summary>
    /// Occurs when the launcher process has completely started. 
    /// </summary>
    public event EventHandler<BrowserLauncherProcessStartedEventArgs>? LauncherProcessStarted;

    /// <summary>
    /// Gets or sets the host name of the launcher. Defaults to "localhost."
    /// </summary>
    /// <remarks>
    /// Most browser launcher executables do not allow connections from remote
    /// (non-local) machines. This property can be used as a workaround so
    /// that an IP address (like "127.0.0.1" or "::1") can be used instead.
    /// </remarks>
    public string HostName { get => this.launcherHostName; set => this.launcherHostName = value; }

    /// <summary>
    /// Gets or sets the port on which the launcher should listen.
    /// </summary>
    public int Port { get => this.launcherPort; set => this.launcherPort = value; }

    /// <summary>
    /// Gets or sets a value indicating whether the command prompt window of the service should be hidden.
    /// </summary>
    public bool HideCommandPromptWindow { get => this.hideCommandPromptWindow; set => this.hideCommandPromptWindow = value; }

    /// <summary>
    /// Gets or sets a value indicating the time to wait for an initial connection before timing out.
    /// </summary>
    public TimeSpan InitializationTimeout { get => this.initializationTimeout; set => this.initializationTimeout = value; }

    /// <summary>
    /// Gets a value indicating whether the service is running.
    /// </summary>
    public bool IsRunning => this.launcherProcess is not null && !this.launcherProcess.HasExited;

    /// <summary>
    /// Gets the WebSocket URL for communicating with the browser via the WebDriver BiDi protocol.
    /// </summary>
    public string WebSocketUrl => this.webSocketUrl;

    /// <summary>
    /// Gets the process ID of the running driver service executable. Returns 0 if the process is not running.
    /// </summary>
    public int ProcessId
    {
        get
        {
            if (this.IsRunning)
            {
                // There's a slight chance that the Process object is running,
                // but does not have an ID set. This should be rare, but we
                // definitely don't want to throw an exception.
                try
                {
                    // IsRunning contains a null check for the process.
                    return this.launcherProcess!.Id;
                }
                catch (InvalidOperationException)
                {
                }
            }

            return 0;
        }
    }

    /// <summary>
    /// Gets the command-line arguments for the driver service.
    /// </summary>
    protected virtual string CommandLineArguments => $"--port={this.launcherPort}";

    /// <summary>
    /// Gets a value indicating whether the service has a shutdown API that can be called to terminate
    /// it gracefully before forcing a termination.
    /// </summary>
    protected virtual bool HasShutdownApi => true;

    /// <summary>
    /// Gets a value indicating the time to wait for the service to terminate before forcing it to terminate.
    /// </summary>
    protected virtual TimeSpan TerminationTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the Uri of the service.
    /// </summary>
    private string ServiceUrl => $"http://{this.launcherHostName}:{this.launcherPort}";

    /// <summary>
    /// Creates a launcher for the specified browser.
    /// </summary>
    /// <param name="browserType">The type of browser launcher to create.</param>
    /// <param name="launcherPath">The path to the browser launcher.</param>
    /// <returns>The launcher for the specified browser type.</returns>
    /// <exception cref="WebDriverBidiException">Thrown when an invalid browser type is specified.</exception>
    public static BrowserLauncher Create(BrowserType browserType, string launcherPath)
    {
        if (browserType == BrowserType.Firefox)
        {
            return new FirefoxLauncher(launcherPath);
        }

        if (browserType == BrowserType.Chrome)
        {
            return new ChromeLauncher(launcherPath);
        }

        throw new WebDriverBidiException("Invalid browser type");
    }

    /// <summary>
    /// Starts the browser launcher if it is not already running.
    /// </summary>
    public async Task Start()
    {
        if (this.launcherProcess is not null)
        {
            return;
        }

        // A word about the locking mechanism. It's not entirely possible to make
        // atomic the finding of a free port, then using that port as the port for
        // the launcher to listen on. There will always be a race condition between
        // releasing the port and starting the launcher where another application
        // could acquire the same port. The window of opportunity is likely in the
        // millisecond order of magnitude, but the chance does exist. We will attempt
        // to mitigate at least other instances of a BrowserLauncher acquiring the
        // same port when launching the browser.
        bool launcherAvailable = false;
        await lockObject.WaitAsync();
        try
        {
            if (this.launcherPort == 0)
            {
                this.launcherPort = FindFreePort();
            }

            this.launcherProcess = new Process();
            this.launcherProcess.StartInfo.FileName = Path.Combine(this.launcherPath, this.launcherExecutableName);
            this.launcherProcess.StartInfo.Arguments = this.CommandLineArguments;
            this.launcherProcess.StartInfo.UseShellExecute = false;
            this.launcherProcess.StartInfo.CreateNoWindow = this.hideCommandPromptWindow;

            BrowserLauncherProcessStartingEventArgs eventArgs = new(this.launcherProcess.StartInfo);
            this.OnLauncherProcessStarting(eventArgs);

            this.launcherProcess.Start();
            launcherAvailable = await this.WaitForInitialization();
            BrowserLauncherProcessStartedEventArgs processStartedEventArgs = new(this.launcherProcess);
            this.OnLauncherProcessStarted(processStartedEventArgs);
        }
        finally
        {
            lockObject.Release();
        }

        if (!launcherAvailable)
        {
            string msg = "Cannot start the browser launcher on " + this.ServiceUrl;
            throw new WebDriverBidiException(msg);
        }
    }

    /// <summary>
    /// Stops the browser launcher.
    /// </summary>
    public async Task Stop()
    {
        if (this.IsRunning)
        {
            if (this.HasShutdownApi)
            {
                DateTime timeout = DateTime.Now.Add(this.TerminationTimeout);
                while (this.IsRunning && DateTime.Now < timeout)
                {
                    try
                    {
                        // Issue the shutdown HTTP request, then wait a short while for
                        // the process to have exited. If the process hasn't yet exited,
                        // we'll retry. We wait for exit here, since catching the exception
                        // for a failed HTTP request due to a closed socket is particularly
                        // expensive.
                        using HttpResponseMessage response = await this.httpClient.GetAsync($"{this.ServiceUrl}/shutdown");
                        this.launcherProcess!.WaitForExit(3000);
                    }
                    catch (WebException)
                    {
                    }
                }
            }

            // If at this point, the process still hasn't exited, wait for one
            // last-ditch time, then, if it still hasn't exited, kill it. Note
            // that falling into this branch of code should be exceedingly rare.
            if (this.IsRunning)
            {
                this.launcherProcess!.WaitForExit(Convert.ToInt32(this.TerminationTimeout.TotalMilliseconds));
                if (!this.launcherProcess.HasExited)
                {
                    this.launcherProcess.Kill();
                }
            }

            this.launcherProcess!.Dispose();
            this.launcherProcess = null;
        }
    }

    /// <summary>
    /// Asynchronously launches the browser.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="BrowserNotLaunchedException">Thrown when the browser cannot be launched.</exception>
    public async Task LaunchBrowser()
    {
        Dictionary<string, object> classicCapabilities = new()
        {
            ["capabilities"] = new Dictionary<string, object>()
            {
                ["firstMatch"] = new List<object>()
                {
                    this.CreateBrowserLaunchCapabilities()
                }
            }
        };
        string json = JsonConvert.SerializeObject(classicCapabilities);
        StringContent content = new(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await this.httpClient.PostAsync($"{this.ServiceUrl}/session", content);
        string responseJson = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new BrowserNotLaunchedException($"Unable to launch browser. Received status code {response.StatusCode} with body {responseJson} from launcher");
        }

        JObject returned = JObject.Parse(responseJson);
        if (returned.ContainsKey("value") && returned["value"] is not null && returned["value"]!.Type == JTokenType.Object)
        {
            JObject? returnedValue = returned["value"] as JObject;
            if (returnedValue is not null && returnedValue.ContainsKey("sessionId") && returnedValue["sessionId"]!.Type == JTokenType.String)
            {
                string? returnedSessionId = returnedValue["sessionId"]!.Value<string>();
                this.sessionId = returnedSessionId!;
            }

            if (returnedValue is not null && returnedValue.ContainsKey("capabilities") && returnedValue["capabilities"]!.Type == JTokenType.Object)
            {
                JObject? capabilities = returnedValue["capabilities"] as JObject;
                if (capabilities is not null && capabilities.ContainsKey("webSocketUrl") && capabilities["webSocketUrl"]!.Type == JTokenType.String)
                {
                    string? returnedWebSocketUrl = capabilities["webSocketUrl"]!.Value<string>();
                    this.webSocketUrl = returnedWebSocketUrl!;
                }
            }
        }

        if (string.IsNullOrEmpty(this.sessionId))
        {
            throw new BrowserNotLaunchedException($"Unable to launch browser. Could not dectect session ID in WebDriver classic new session response (response JSON: {responseJson})");
        }

        if (string.IsNullOrEmpty(this.webSocketUrl))
        {
            throw new BrowserNotLaunchedException($"Unable to connect to WebSocket. Launched browse may not supoort the WebDriver BiDi protocol (response JSON: {responseJson})");
        }
    }

    /// <summary>
    /// Asynchronously quits the browser.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="BrowserNotLaunchedException"></exception>
    public async Task QuitBrowser()
    {
        if (!string.IsNullOrEmpty(this.sessionId))
        {
            using HttpResponseMessage response = await this.httpClient.DeleteAsync($"{this.ServiceUrl}/session/{this.sessionId}");
            string responseJson = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new CannotQuitBrowserException($"Unable to quit browser. Received status code {response.StatusCode} with body {responseJson} from launcher");
            }
        }
    }

    /// <summary>
    /// Creates the WebDriver Classic capabilities used to launch the browser.
    /// </summary>
    /// <returns>A dictionary containing the capabilities.</returns>
    protected abstract Dictionary<string, object> CreateBrowserLaunchCapabilities();

    /// <summary>
    /// Raises the <see cref="LauncherProcessStarting"/> event.
    /// </summary>
    /// <param name="eventArgs">A <see cref="BrowserLauncherProcessStartingEventArgs"/> that contains the event data.</param>
    protected void OnLauncherProcessStarting(BrowserLauncherProcessStartingEventArgs eventArgs)
    {
        if (this.LauncherProcessStarting is not null)
        {
            this.LauncherProcessStarting(this, eventArgs);
        }
    }

    /// <summary>
    /// Raises the <see cref="LauncherProcessStarted"/> event.
    /// </summary>
    /// <param name="eventArgs">A <see cref="BrowserLauncherProcessStartedEventArgs"/> that contains the event data.</param>
    protected void OnLauncherProcessStarted(BrowserLauncherProcessStartedEventArgs eventArgs)
    {
        if (this.LauncherProcessStarted is not null)
        {
            this.LauncherProcessStarted(this, eventArgs);
        }
    }

    /// <summary>
    /// Finds a random, free port to be listened on.
    /// </summary>
    /// <returns>A random, free port to be listened on.</returns>
    private static int FindFreePort()
    {
        // Locate a free port on the local machine by binding a socket to
        // an IPEndPoint using IPAddress.Any and port 0. The socket will
        // select a free port.
        int listeningPort = 0;
        Socket portSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            IPEndPoint socketEndPoint = new(IPAddress.Any, 0);
            portSocket.Bind(socketEndPoint);
            if (portSocket.LocalEndPoint is not null)
            {
                socketEndPoint = (IPEndPoint)portSocket.LocalEndPoint;
                listeningPort = socketEndPoint.Port;
            }
        }
        finally
        {
            portSocket.Close();
        }

        return listeningPort;
    }

    /// <summary>
    /// Asynchronously waits for the initialization of the browser launcher.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    private async Task<bool> WaitForInitialization()
    {
        bool isInitialized = false;
        DateTime timeout = DateTime.Now.Add(this.InitializationTimeout);
        while (!isInitialized && DateTime.Now < timeout)
        {
            // If the driver service process has exited, we can exit early.
            if (!this.IsRunning)
            {
                break;
            }

            try
            {
                using HttpResponseMessage response = await this.httpClient.GetAsync($"{this.ServiceUrl}/status");

                // Checking the response from the 'status' end point. Note that we are simply checking
                // that the HTTP status returned is a 200 status, and that the resposne has the correct
                // Content-Type header. A more sophisticated check would parse the JSON response and
                // validate its values. At the moment we do not do this more sophisticated check.
                isInitialized = response.StatusCode == HttpStatusCode.OK &&
                    response.Content.Headers.ContentType is not null &&
                    response.Content.Headers.ContentType.MediaType is not null &&
                    response.Content.Headers.ContentType.MediaType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
            }
            catch (HttpRequestException)
            {
            }
        }

        return isInitialized;
    }
}