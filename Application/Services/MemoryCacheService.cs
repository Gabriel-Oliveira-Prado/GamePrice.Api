using GamePrice.Api.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GamePrice.Api.Application.Services
{
    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<MemoryCacheService> _logger;

        public MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public Task<T?> GetAsync<T>(string key)
        {
            var found = _cache.TryGetValue(key, out T? value);
            if (found)
                _logger.LogDebug("Cache GET - Key: {Key} - Encontrado", key);
            else
                _logger.LogDebug("Cache GET - Key: {Key} - Não encontrado", key);

            return Task.FromResult(value);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            var options = new MemoryCacheEntryOptions();

            if (expiration.HasValue)
                options.AbsoluteExpirationRelativeToNow = expiration.Value;
            else
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10); // Padrão

            _cache.Set(key, value, options);
            _logger.LogDebug("Cache SET - Key: {Key} - Expiração: {Expiration}", key, options.AbsoluteExpirationRelativeToNow);

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            _logger.LogDebug("Cache REMOVE - Key: {Key}", key);

            return Task.CompletedTask;
        }
    }
}
