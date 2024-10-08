namespace ServicoF1.API
{
    public sealed class Client
    {
        //public static HttpClientHandler? handler { get; private set; }
        /// <summary>
        /// Client WEB for calls
        /// </summary>
        public static HttpClient httpClient { get; private set; } = new HttpClient(new SocketsHttpHandler() { PooledConnectionLifetime = TimeSpan.FromHours(2)}) { Timeout = TimeSpan.FromMinutes(5)};
        public static ILogger<Worker>? _logger = default;

        /// <summary>
        /// dispose of objects
        /// </summary>
        public static void Dispose()
        {
            httpClient?.Dispose();
        }
    }
}