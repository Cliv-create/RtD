using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using RtD.Models;
using RtD.Utils;

namespace RtD.Services
{
    public class ShikimoriApiService
    {
        private readonly IApiService _apiService;
        private const string GraphQLEndpoint = "https://shikimori.one/api/graphql";

        private const string GraphQLQuery = @"
            query($page: PositiveInt!, $limit: PositiveInt!, $userId: ID!) {
              userRates(page: $page, limit: $limit, userId: $userId, targetType: Anime, order: {field: updated_at, order: desc}) {
                id
                anime { id malId russian name url genres { name } episodes description }
                text
                createdAt
                updatedAt
                score
                status
                rewatches
              }
            }
        ";

        public ShikimoriApiService(IApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<string> FetchRates(long userId, int page = 1, int limit = 50)
        {
            var variables = new
            {
                page,
                limit,
                userId
            };

            var payloadObj = new
            {
                operationName = (string?)null,
                query = GraphQLQuery,
                variables
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var jsonPayload = JsonSerializer.Serialize(payloadObj, options);

            return await _apiService.PostJsonAsync(GraphQLEndpoint, jsonPayload);
        }
    }
}