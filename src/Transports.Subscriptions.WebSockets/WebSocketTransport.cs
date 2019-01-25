using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using GraphQL.Http;
using GraphQL.Server.Transports.Subscriptions.Abstractions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GraphQL.Server.Transports.WebSockets
{
    public class WebSocketTransport : IMessageTransport
    {
        private readonly WebSocket _socket;
        private readonly ILoggerFactory _loggerFactory;
        private ILogger<WebSocketTransport> _logger;

        public WebSocketTransport(WebSocket socket, IDocumentWriter documentWriter, ILoggerFactory loggerFactory)
        {
            _socket = socket;
            _loggerFactory = loggerFactory;
            var serializerSettings = new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateFormatString = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFF'Z'",
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            Reader = new WebSocketReaderPipeline(_socket, serializerSettings);
            Writer = new WebSocketWriterPipeline(_socket, documentWriter, _loggerFactory.CreateLogger<WebSocketWriterPipeline>());
        }


        public WebSocketCloseStatus? CloseStatus => _socket.CloseStatus;

        public IReaderPipeline Reader { get; }
        public IWriterPipeline Writer { get; }

        public Task CloseAsync()
        {
            if (_socket.State != WebSocketState.Open)
                return Task.CompletedTask;

            if (CloseStatus.HasValue)
                if (CloseStatus != WebSocketCloseStatus.NormalClosure || CloseStatus != WebSocketCloseStatus.Empty)
                    return AbortAsync();

            return _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
        }

        private Task AbortAsync()
        {
            _socket.Abort();
            return Task.CompletedTask;
        }
    }
}