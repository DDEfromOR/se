using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Rest.TransientFaultHandling;
using Microsoft.Bot.Protocol;

namespace Microsoft.Bot.Builder.Integration.AspNet.Core
{
    //This may be replaced with the ProtocolAdapter from the Microsoft.Bot.Protocol :(
    public class BotFrameworkWebSocketAdapter : IBotFrameworkWebSocketAdapter
    {
        public Task ProcessAsync(Request request, Response response, IBot bot, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (bot == null)
            {
                throw new ArgumentNullException(nameof(bot));
            }

            var activity = await request.

            // deserialize the incoming Activity
            //var activity = await HttpHelper.ReadRequestAsync(request, cancellationToken).ConfigureAwait(false);

            // grab the auth header from the inbound http request

            var authHeader = request.Headers.Authorization?.ToString();

            // process the inbound activity with the bot
            var invokeResponse = await ProcessActivityAsync(authHeader, activity, bot.OnTurnAsync, cancellationToken).ConfigureAwait(false);

            // write the response, potentially serializing the InvokeResponse
            HttpHelper.WriteResponse(request, response, invokeResponse);
        }
    }
}
