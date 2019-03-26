using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Protocol.WebSockets;
using Microsoft.Extensions.Logging;

namespace Microsoft.Bot.Builder.Integration.AspNet.Core.StreamingExtensions
{
    public class BotFrameworkWebSocketAdapter : IBotFrameworkHttpAdapter
    {
        private readonly IChannelProvider _channelProvider;
        private readonly ICredentialProvider _credentialProvider;
        private ConcurrentDictionary<string, WebSocketServer> _connections = new ConcurrentDictionary<string, WebSocketServer>();

        public BotFrameworkWebSocketAdapter(ICredentialProvider credentialProvider, IChannelProvider channelProvider = null, ILogger<BotFrameworkWebSocketAdapter> logger = null)
        {
            this._credentialProvider = credentialProvider;
            this._channelProvider = channelProvider;
        }

        public ConcurrentDictionary<string, WebSocketServer> Connections { get; set; }

        public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IBot bot, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (httpRequest == null)
            {
                throw new ArgumentNullException(nameof(httpRequest));
            }

            if (httpResponse == null)
            {
                throw new ArgumentNullException(nameof(httpResponse));
            }

            if (bot == null)
            {
                throw new ArgumentNullException(nameof(bot));
            }

            var authHeader = httpRequest.Headers["Authorization"];
            var channelId = httpRequest.Headers["ChannelId"];
            var claimsIdentity = await JwtTokenValidation.ValidateAuthHeader(authHeader, _credentialProvider, _channelProvider, channelId).ConfigureAwait(false);
            if (!claimsIdentity.IsAuthenticated)
            {
                httpRequest.HttpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            if (!httpRequest.HttpContext.WebSockets.IsWebSocketRequest)
            {
                httpRequest.HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await httpRequest.HttpContext.Response.WriteAsync("Upgrade to WebSocket required.").ConfigureAwait(false);
                return;
            }

            await CreateWebSocketConnectionAsync(httpRequest.HttpContext, authHeader, channelId, bot).ConfigureAwait(false);
        }

        public async Task CreateWebSocketConnectionAsync(HttpContext httpContext, string authHeader, string channelId, IBot bot)
        {
            var socket = await httpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            var claimsIdentity = await JwtTokenValidation.ValidateAuthHeader(authHeader, _credentialProvider, _channelProvider, channelId).ConfigureAwait(false);
            var server = new WebSocketServer(socket, new StreamingExtensionRequestHandler(new BotFrameworkStreamingExtensionsAdapter(), bot));

            try
            {
                _connections.TryAdd(default(Guid).ToString(), server);
            }
            catch (Exception)
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return;
            }
        }
    }
}
