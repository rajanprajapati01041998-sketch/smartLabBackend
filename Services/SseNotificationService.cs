using System.Collections.Concurrent;
using System.Text.Json;

namespace LISD.Services
{
    public class SseNotificationService
    {
        private readonly ConcurrentDictionary<string, StreamWriter> _clients = new();

        public void AddClient(string clientId, StreamWriter writer)
        {
            _clients.TryAdd(clientId, writer);
        }

        public void RemoveClient(string clientId)
        {
            _clients.TryRemove(clientId, out _);
        }

        public async Task SendToAdminsAsync(object data)
        {
            var json = JsonSerializer.Serialize(data);

            var deadClients = new List<string>();

            foreach (var client in _clients)
            {
                try
                {
                    await client.Value.WriteAsync($"data: {json}\n\n");
                    await client.Value.FlushAsync();
                }
                catch
                {
                    deadClients.Add(client.Key);
                }
            }

            foreach (var id in deadClients)
            {
                RemoveClient(id);
            }
        }
    }
}