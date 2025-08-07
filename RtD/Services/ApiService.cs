using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace RtD.Services
{
    // TODO: Implement rate limiting. 5 rps / 90 rpm
    public interface IApiService
    {
        Task<string> PostJsonAsync(string url, string jsonBody);
    }

    public class HttpApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly Stopwatch _timer;

        public HttpApiService(Stopwatch timer)
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30),
            };

            _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RtD/1.0");

            _timer = timer;
        }

        /// <summary>
        /// Makes HTTP request with EnsureSuccessStatusCode (might throw an error). Utilizes the timer, passed inside HttpApiService.
        /// </summary>
        /// <param name="url">URL for POST request.</param>
        /// <param name="jsonBody">JSON that will be sent as HttpContent.</param>
        /// <returns>string from HTTP response.</returns>
        public async Task<string> PostJsonAsync(string url, string jsonBody)
        {
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            _timer.Start();
            var response = await _httpClient.PostAsync(url, content);
            _timer.Stop();

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}