using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using GraphQL.Http;
using GraphQL.Server.Transports.Subscriptions.Abstractions;
using Microsoft.Extensions.Logging;

namespace GraphQL.Server.Transports.WebSockets
{
    public class WebSocketWriterPipeline : IWriterPipeline
    {
        private readonly WebSocket _socket;
        private readonly IDocumentWriter _documentWriter;
        private readonly ILogger<WebSocketWriterPipeline> _logger;
        private readonly ITargetBlock<OperationMessage> _startBlock;

        public WebSocketWriterPipeline(WebSocket socket, IDocumentWriter documentWriter, ILogger<WebSocketWriterPipeline> logger)
        {
            _socket = socket;
            _documentWriter = documentWriter;
            _logger = logger;

            _startBlock = CreateMessageWriter();
        }

        public bool Post(OperationMessage message)
        {
            _logger?.LogDebug("message {messageId} posted to pipeline", message.Id);
            return _startBlock.Post(message);
        }

        public Task SendAsync(OperationMessage message)
        {
            _logger?.LogDebug("message {messageId} sent to pipeline", message.Id);
            return _startBlock.SendAsync(message);
        }

        public Task Completion => _startBlock.Completion;

        public Task Complete()
        {
            _startBlock.Complete();
            return Task.CompletedTask;
        }

        private ITargetBlock<OperationMessage> CreateMessageWriter()
        {
            var target = new ActionBlock<OperationMessage>(
                WriteMessageAsync, new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 1,
                    EnsureOrdered = true
                });

            return target;
        }

        private async Task WriteMessageAsync(OperationMessage message)
        {
            if (_socket.CloseStatus.HasValue) return;

            _logger?.LogDebug("writing message {messageId} to websocket", message.Id);
            var stream = new WebsocketWriterStream(_socket);
            try
            {
                await _documentWriter.WriteAsync(stream, message);
                _logger?.LogDebug("message {messageId} completely written to websocket", message.Id);
            }
            finally
            {
                await stream.FlushAsync();
                stream.Dispose();
            }
        }
    }
}