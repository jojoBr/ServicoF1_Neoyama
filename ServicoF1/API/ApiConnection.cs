using Microsoft.AspNetCore.Mvc;
using ServicoF1.Models.WEB;
using System.Net.Http.Headers;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;

namespace ServicoF1.API
{
    public sealed class ApiConnection
    {
        JsonSerializerOptions options;
        private readonly string _token;
        private readonly ILogger _logger;

        public ApiConnection(string token, ILogger  logger)
        {
            _token = token;
            _logger = logger;
            options = new JsonSerializerOptions();
            options.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        }

        /// <summary>
        /// makes a get request an deserialize the string response to a json object.
        /// </summary>
        /// <typeparam name="TDataResponse"> the type of the object that the json will use to deserialize the string</typeparam>
        /// <param name="url"> the whole uri of the api call</param>
        /// <param name="headerType"> the type of header only acepts one type as of now </param>
        /// <param name="headerValue"> the value of the header</param>
        /// <returns></returns>
        public async Task<TDataResponse?> GET<TDataResponse>(string url, Header[] headers) where TDataResponse : class
        {
            try
            {
                using Stream json = (Stream)await GETString(url, headers, false);
                return await JsonSerializer.DeserializeAsync<TDataResponse>(json);
            }
            catch(HttpRequestException ex)
            {
                _logger.LogError("Erro ao buscar os dados do api: {erro} : {url}", ex.Message, url);
                return null;
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao buscar os dados do api: {erro} : {url}", ex.Message, url);
                return null;
            }
        }

        /// <summary>
        /// Get request
        /// </summary>
        /// <param name="url"> the whole uri of the api call</param>
        /// <param name="headerType"> the type of header only acepts one type as of now </param>
        /// <param name="headerValue"> the value of the header</param>
        /// <param name="asString"> return a string object otherwise a Stream object</param>
        /// <returns> return a object as a stream or as a string</returns>
        public async Task<object> GETString(string url, Header[] headers, bool asString = true)
        {
            SanitazeURLAsync(url);
            foreach (Header header in headers)
            {
                SanitazeHeader(header);

                if (header.Type == "Bearer")
                    Client.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(header.Type, header.Value);
                else
                    Client.httpClient.DefaultRequestHeaders.Add(header.Type, header.Value);
            }

            var response = Client.httpClient.GetAsync(url).Result;
            if (!response.IsSuccessStatusCode)
            {
                Client.httpClient.DefaultRequestHeaders.Clear();
                string error = await response.Content.ReadAsStringAsync();
                
                throw new Exception($"Erro ao Buscar dados: url {url} | response : {error}");
            }

            Client.httpClient.DefaultRequestHeaders.Clear();
            if (asString)
            {
                string result = await response.Content.ReadAsStringAsync();
                response.Dispose();
                return result;
            }
            else
                return await response.Content.ReadAsStreamAsync();
        }

        /// <summary>
        /// makes a post request using an object of a generic type and deserialze to an objects
        /// </summary>
        /// <typeparam name="TDataSend"> data type send to the api</typeparam>
        /// <typeparam name="TDataResponse"> the data type to be used to deserialize the json response </typeparam>
        /// <param name="url"> the whole uri of the api call</param>
        /// <param name="headerType"> the type of header only acepts one type as of now </param>
        /// <param name="headerValue"> the value of the header</param>
        /// <param name="Data"> obejct to be serialized and send</param>
        /// <returns> the json as an object</returns>
        public async Task<TDataResponse?> POST<TDataSend, TDataResponse>(string url, Header[] headers, TDataSend Data, bool putJson = false)
        {
            using (Stream json = (Stream)await POSTString(url, headers, Data, false, putJson))
            {
                TDataResponse? response = await JsonSerializer.DeserializeAsync<TDataResponse>(json);
                json.Close();
                return response;
            }
        }

        /// <summary>
        /// makes a post request using an object of a generic type
        /// </summary>
        /// <typeparam name="TDataSend"> data type send to the api</typeparam>
        /// <param name="url"> the whole uri of the api call</param>
        /// <param name="headerType"> the type of header only acepts one type as of now </param>
        /// <param name="headerValue"> the value of the header</param>
        /// <param name="Data"> obejct to be serialized and send</param>
        /// <param name="asString"> return a string object otherwise a Stream object</param>
        /// <returns> the json as an object</returns>
        public async Task<object> POSTString<TDataSend>(string url, Header[] headers, TDataSend Data, bool asString = true, bool putJson = false)
        {
            if (Data is null)
                throw new ArgumentNullException(nameof(Data));

            SanitazeURLAsync(url);

            foreach (Header header in headers)
            {
                SanitazeHeader(header);
                if (header.Type == "Bearer")
                    Client.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(header.Type, header.Value);
                else
                    Client.httpClient.DefaultRequestHeaders.Add(header.Type, header.Value);
            }

            string json;
            if (typeof(TDataSend) == typeof(string))
                json = Data.ToString()!;
            else
                json = JsonSerializer.Serialize(Data, typeof(TDataSend), options);

            var response = Client.httpClient.PostAsync(url, new StringContent(
                json!, Encoding.UTF8, "application/json")).Result;
            
            if (!response.IsSuccessStatusCode)
            {
                Client.httpClient.DefaultRequestHeaders.Clear();
                string error = await response.Content.ReadAsStringAsync();
                if (putJson)
                {
                    throw new Exception($"Erro ao enviar dados: url {url}\n | {error} | {json}");
                }
                else
                    throw new Exception($"Erro ao enviar dados: url {url}| {error} | {response.StatusCode}");
            }

            Client.httpClient.DefaultRequestHeaders.Clear();
            if (asString)
                return await response.Content.ReadAsStringAsync();
            else
                return await response.Content.ReadAsStreamAsync();
        }

        /// <summary>
        /// makes a Patch request using an object of a generic type
        /// </summary>
        /// <typeparam name="TDataSend"> data type send to the api</typeparam>
        /// <param name="url"> the whole uri of the api call</param>
        /// <param name="headerType"> the type of header only acepts one type as of now </param>
        /// <param name="headerValue"> the value of the header</param>
        /// <param name="Data"> obejct to be serialized and send</param>
        /// <param name="asString"> return a string object otherwise a Stream object</param>
        /// <returns> return a object as a stream or as a string</returns>
        public async Task<object> PATCHString<TDataSend>(string url, Header[] headers, TDataSend Data, bool eliminateLines, bool asString = true)
        {
            if(Data is null)
                throw new ArgumentNullException(nameof(Data));

            SanitazeURLAsync(url);

            foreach (Header header in headers)
            {
                SanitazeHeader(header);
                if (header.Type == "Bearer")
                    Client.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(header.Type, header.Value);
                else
                    Client.httpClient.DefaultRequestHeaders.Add(header.Type, header.Value);

            }

            if (eliminateLines)
            {
                Client.httpClient.DefaultRequestHeaders.Add("B1S-ReplaceCollectionsOnPatch", "true");
            }

            string? json;
            if (typeof(TDataSend) == typeof(string))
                json = Data.ToString();
            else
                json = JsonSerializer.Serialize(Data, typeof(TDataSend), options);
            using (var response = Client.httpClient.PatchAsync(url, new StringContent(
                json!, Encoding.UTF8, "application/json")).Result)
            {
                if (!response.IsSuccessStatusCode)
                {
                    Client.httpClient.DefaultRequestHeaders.Clear();
                    string error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Erro ao enviar dados: url {url} | response : {error}");
                }

                Client.httpClient.DefaultRequestHeaders.Clear();
                if (asString)
                    return await response.Content.ReadAsStringAsync();
                else
                    return await response.Content.ReadAsStreamAsync();
            }
        }

        /// <summary>
        /// makes a PATCH request using an object of a generic type
        /// </summary>
        /// <typeparam name="TDataSend"> data type send to the api</typeparam>
        /// <param name="url"> the whole uri of the api call</param>
        /// <param name="headerType"> the type of header only acepts one type as of now </param>
        /// <param name="headerValue"> the value of the header</param>
        /// <param name="Data"> obejct to be serialized and send</param>
        /// <returns> the json as an object</returns>
        public async Task<TDataResponse?> PATCH<TDataSend, TDataResponse>(string url, Header[] headers, TDataSend Data, bool eliminateLines)
        {
            using (Stream json = (Stream)await PATCHString(url, headers, Data, eliminateLines, false))
            {
                TDataResponse? response = await JsonSerializer.DeserializeAsync<TDataResponse>(json);
                json.Close();
                return response!;
            }
        }

        /// <summary>
        /// makes a PUT request using an object of a generic type
        /// </summary>
        /// <typeparam name="TDataSend"> data type send to the api</typeparam>
        /// <param name="url"> the whole uri of the api call</param>
        /// <param name="headerType"> the type of header only acepts one type as of now </param>
        /// <param name="headerValue"> the value of the header</param>
        /// <param name="Data"> obejct to be serialized and send</param>
        /// <returns> return a object as a stream or as a string</returns>
        public async Task<object> PUTString<TDataSend>(string url, Header[] headers, TDataSend Data, bool asString = true)
        {
            if (Data is null)
                throw new ArgumentNullException(nameof(Data));

            SanitazeURLAsync(url);

            Client.httpClient.DefaultRequestHeaders.Clear();
            foreach (Header header in headers)
            {
                SanitazeHeader(header);
                if (header.Type == "Bearer")
                    Client.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(header.Type, header.Value);
                else
                    Client.httpClient.DefaultRequestHeaders.Add(header.Type, header.Value);
            }

            string? json;
            if (typeof(TDataSend) == typeof(string))
                json = Data.ToString();
            else
                json = JsonSerializer.Serialize(Data, typeof(TDataSend), options);

            var response = Client.httpClient.PutAsync(url, new StringContent(
                json!, Encoding.UTF8, "application/json")).Result;


            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Erro ao enviar dados: url {url} | response : {error}");
            }

            if (asString)
                return await response.Content.ReadAsStringAsync();
            else
                return await response.Content.ReadAsStreamAsync();
        }

        /// <summary>
        /// makes a PUT request using an object of a generic type
        /// </summary>
        /// <typeparam name="TDataSend"> data type send to the api</typeparam>
        /// <param name="url"> the whole uri of the api call</param>
        /// <param name="headerType"> the type of header only acepts one type as of now </param>
        /// <param name="headerValue"> the value of the header</param>
        /// <param name="Data"> obejct to be serialized and send</param>
        /// <returns> the json as an object</returns>
        public async Task<TDataResponse?> PUT<TDataSend, TDataResponse>(string url, Header[] headers, TDataSend Data)
        {
            using (Stream json = (Stream)await PUTString(url, headers, Data, false))
            {
                TDataResponse? response = await JsonSerializer.DeserializeAsync<TDataResponse>(json);
                json.Close();
                return response;
            }
        }

        /// <summary>
        /// makes a PUT request using an object of a generic type
        /// </summary>
        /// <typeparam name="TDataSend"> data type send to the api</typeparam>
        /// <param name="url"> the whole uri of the api call</param>
        /// <param name="headerType"> the type of header only acepts one type as of now </param>
        /// <param name="headerValue"> the value of the header</param>
        /// <param name="asString"> return a string object otherwise a Stream object</param>
        /// <returns> return a object as a stream or as a string</returns>
        public async Task<object> DELETEString(string url, Header[] headers, bool asString = true)
        {
            SanitazeURLAsync(url);

            foreach (Header header in headers)
            {
                SanitazeHeader(header);
                if (header.Type == "Bearer")
                    Client.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(header.Type, header.Value);
                else
                    Client.httpClient.DefaultRequestHeaders.Add(header.Type, header.Value);
            }

            using (var response = Client.httpClient.DeleteAsync(url).Result)
            {
                if (!response.IsSuccessStatusCode)
                {
                    Client.httpClient.DefaultRequestHeaders.Clear();
                    string error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Erro ao enviar dados: url {url} | response : {error}");
                }

                Client.httpClient.DefaultRequestHeaders.Clear();
                if (asString)
                    return await response.Content.ReadAsStringAsync();
                else
                    return await response.Content.ReadAsStreamAsync();
            }
        }

        /// <summary>
        /// makes a PUT request using an object of a generic type
        /// </summary>
        /// <param name="url"> the whole uri of the api call</param>
        /// <param name="headerType"> the type of header only acepts one type as of now </param>
        /// <param name="headerValue"> the value of the header</param>
        /// <returns> return a object as a stream or as a string</returns>s
        public async Task<TDataResponse?> DELETE<TDataResponse>(string url, Header[] headers)
        {
            using (Stream json = (Stream)await DELETEString(url, headers))
            {
                TDataResponse? response = await JsonSerializer.DeserializeAsync<TDataResponse>(json);
                json.Close();
                return response;
            }
        }

        private void SanitazeURLAsync(string url)
        {
            // Validate the URL
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                // The URL is not well-formed
                throw new Exception("Invalid URL: " + url);
            }

            // Check if the URL scheme is HTTPS
            if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                // The URL does not use HTTPS
                throw new Exception("Invalid URL scheme: " + uri.Scheme + " (expected HTTPS)");
            }

            //// Check if the URL points to a valid resource
            //try
            //{
            //    using (var response = await Client.httpClient.GetAsync(uri))
            //    {
            //        if (!response.IsSuccessStatusCode)
            //        {
            //            // The URL does not point to a valid resource
            //            throw new Exception("Invalid URL: " + url + " (status code " + response.StatusCode + ")");
            //        }
            //    }
            //}
            //catch (HttpRequestException ex)
            //{
            //    // An error occurred while trying to access the resource
            //    throw new Exception("Invalid URL: " + url + " (" + ex.Message + ")");
            //}
        }

        private void SanitazeHeader(Header header)
        {
            // Define a list of allowed header types and values
            var allowedHeaders = new Dictionary<string, string>
            {
                {"Authorization", "Bearer"},
                {"Content-Type", "application/json"},
                {"Bearer", _token},
            };

            if (!allowedHeaders.ContainsKey(header.Type) || !allowedHeaders[header.Type].Equals(header.Value))
            {
                // Reject the header and throw an exception
                throw new Exception("Invalid header detected: " + header.Type + ": " + header.Value);
            }
        }
    }
}
