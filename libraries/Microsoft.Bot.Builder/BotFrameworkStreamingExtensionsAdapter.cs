// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Protocol.WebSockets;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Bot.Builder
{
    public class BotFrameworkStreamingExtensionsAdapter : BotAdapter
    {
        private const string InvokeReponseKey = "BotFrameworkAdapter.InvokeResponse";
        private const string BotIdentityKey = "BotIdentity";
        private readonly IChannelProvider _channelProvider;
        private readonly ILogger _logger;
        private ConcurrentDictionary<string, MicrosoftAppCredentials> _appCredentialMap = new ConcurrentDictionary<string, MicrosoftAppCredentials>();
        private ConcurrentDictionary<string, WebSocketServer> _connections = new ConcurrentDictionary<string, WebSocketServer>();

        public BotFrameworkStreamingExtensionsAdapter(
            IChannelProvider channelProvider = null,
            IMiddleware middleware = null,
            ILogger logger = null)
        {
            _channelProvider = channelProvider;
            _logger = logger ?? NullLogger.Instance;

            if (middleware != null)
            {
                Use(middleware);
            }
        }

        /// <summary>
        /// Sends a proactive message from the bot to a conversation.
        /// </summary>
        /// <param name="botAppId">The application ID of the bot. This is the appId returned by Portal registration, and is
        /// generally found in the "MicrosoftAppId" parameter in appSettings.json.</param>
        /// <param name="reference">A reference to the conversation to continue.</param>
        /// <param name="callback">The method to call for the resulting bot turn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="botAppId"/>, <paramref name="reference"/>, or
        /// <paramref name="callback"/> is <c>null</c>.</exception>
        /// <remarks>Call this method to proactively send a message to a conversation.
        /// Most _channels require a user to initaiate a conversation with a bot
        /// before the bot can send activities to the user.
        /// <para>This method registers the following services for the turn.<list type="bullet">
        /// <item><description><see cref="IIdentity"/> (key = "BotIdentity"), a claims identity for the bot.
        /// </description></item>
        /// <item><description><see cref="IConnectorClient"/>, the channel connector client to use this turn.
        /// </description></item>
        /// </list></para>
        /// <para>
        /// This overload differers from the Node implementation by requiring the BotId to be
        /// passed in. The .Net code allows multiple bots to be hosted in a single adapter which
        /// isn't something supported by Node.
        /// </para>
        /// </remarks>
        /// <seealso cref="ProcessActivityAsync(string, Activity, BotCallbackHandler, CancellationToken)"/>
        /// <seealso cref="BotAdapter.RunPipelineAsync(ITurnContext, BotCallbackHandler, CancellationToken)"/>
        public override async Task ContinueConversationAsync(string botAppId, ConversationReference reference, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(botAppId))
            {
                throw new ArgumentNullException(nameof(botAppId));
            }

            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            _logger.LogInformation($"Sending proactive message.  botAppId: {botAppId}");

            using (var context = new TurnContext(this, reference.GetContinuationActivity()))
            {
                // Hand craft Claims Identity.
                var claimsIdentity = new ClaimsIdentity(new List<Claim>
                {
                    // Adding claims for both Emulator and Channel.
                    new Claim(AuthenticationConstants.AudienceClaim, botAppId),
                    new Claim(AuthenticationConstants.AppIdClaim, botAppId),
                });

                context.TurnState.Add<IIdentity>(BotIdentityKey, claimsIdentity);

                if (_connections.ContainsKey(reference.Conversation.Id))
                {
                    _connections.TryGetValue(reference.Conversation.Id, out var conversationServer);
                }
                else
                {
                    // How do we handle this?
                }

                context.TurnState.Add(connectorClient);
                await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Adds middleware to the adapter's pipeline.
        /// </summary>
        /// <param name="middleware">The middleware to add.</param>
        /// <returns>The updated adapter object.</returns>
        /// <remarks>Middleware is added to the adapter at initialization time.
        /// For each turn, the adapter calls middleware in the order in which you added it.
        /// </remarks>
        public new BotFrameworkAdapter Use(IMiddleware middleware)
        {
            MiddlewareSet.Use(middleware);
            return this;
        }

        /// <summary>
        /// Creates a turn context and runs the middleware pipeline for an incoming activity.
        /// </summary>
        /// <param name="authHeader">The HTTP authentication header of the request.</param>
        /// <param name="activity">The incoming activity.</param>
        /// <param name="callback">The code to run at the end of the adapter's middleware pipeline.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute. If the activity type
        /// was 'Invoke' and the corresponding key (channelId + activityId) was found
        /// then an InvokeResponse is returned, otherwise null is returned.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="activity"/> is <c>null</c>.</exception>
        /// <exception cref="UnauthorizedAccessException">authentication failed.</exception>
        /// <remarks>Call this method to reactively send a message to a conversation.
        /// If the task completes successfully, then if the activity's <see cref="Activity.Type"/>
        /// is <see cref="ActivityTypes.Invoke"/> and the corresponding key
        /// (<see cref="Activity.ChannelId"/> + <see cref="Activity.Id"/>) is found
        /// then an <see cref="InvokeResponse"/> is returned, otherwise null is returned.
        /// <para>This method registers the following services for the turn.<list type="bullet">
        /// <item><see cref="IIdentity"/> (key = "BotIdentity"), a claims identity for the bot.</item>
        /// <item><see cref="IConnectorClient"/>, the channel connector client to use this turn.</item>
        /// </list></para>
        /// </remarks>
        /// <seealso cref="ContinueConversationAsync(string, ConversationReference, BotCallbackHandler, CancellationToken)"/>
        /// <seealso cref="BotAdapter.RunPipelineAsync(ITurnContext, BotCallbackHandler, CancellationToken)"/>
        public async Task<InvokeResponse> ProcessActivityAsync(string authHeader, Activity activity, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            BotAssert.ActivityNotNull(activity);

            var claimsIdentity = await JwtTokenValidation.AuthenticateRequest(activity, authHeader, _credentialProvider, _channelProvider, _httpClient).ConfigureAwait(false);
            return await ProcessActivityAsync(claimsIdentity, activity, callback, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a turn context and runs the middleware pipeline for an incoming activity.
        /// </summary>
        /// <param name="identity">A <see cref="ClaimsIdentity"/> for the request.</param>
        /// <param name="activity">The incoming activity.</param>
        /// <param name="callback">The code to run at the end of the adapter's middleware pipeline.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity identity, Activity activity, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            BotAssert.ActivityNotNull(activity);

            _logger.LogInformation($"Received an incoming activity.  ActivityId: {activity.Id}");

            using (var context = new TurnContext(this, activity))
            {
                context.TurnState.Add<IIdentity>(BotIdentityKey, identity);

                var connectorClient = await CreateConnectorClientAsync(activity.ServiceUrl, identity, cancellationToken).ConfigureAwait(false);
                context.TurnState.Add(connectorClient);

                await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);

                // Handle Invoke scenarios, which deviate from the request/response model in that
                // the Bot will return a specific body and return code.
                if (activity.Type == ActivityTypes.Invoke)
                {
                    var activityInvokeResponse = context.TurnState.Get<Activity>(InvokeReponseKey);
                    if (activityInvokeResponse == null)
                    {
                        return new InvokeResponse { Status = (int)HttpStatusCode.NotImplemented };
                    }
                    else
                    {
                        return (InvokeResponse)activityInvokeResponse.Value;
                    }
                }

                // For all non-invoke scenarios, the HTTP layers above don't have to mess
                // withthe Body and return codes.
                return null;
            }
        }

        /// <summary>
        /// Sends activities to the conversation.
        /// </summary>
        /// <param name="turnContext">The context object for the turn.</param>
        /// <param name="activities">The activities to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the activities are successfully sent, the task result contains
        /// an array of <see cref="ResourceResponse"/> objects containing the IDs that
        /// the receiving channel assigned to the activities.</remarks>
        /// <seealso cref="ITurnContext.OnSendActivities(SendActivitiesHandler)"/>
        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, Activity[] activities, CancellationToken cancellationToken)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }

            if (activities.Length == 0)
            {
                throw new ArgumentException("Expecting one or more activities, but the array was empty.", nameof(activities));
            }

            var responses = new ResourceResponse[activities.Length];

            /*
             * NOTE: we're using for here (vs. foreach) because we want to simultaneously index into the
             * activities array to get the activity to process as well as use that index to assign
             * the response to the responses array and this is the most cost effective way to do that.
             */
            for (var index = 0; index < activities.Length; index++)
            {
                var activity = activities[index];
                var response = default(ResourceResponse);

                _logger.LogInformation($"Sending activity.  ReplyToId: {activity.ReplyToId}");

                if (activity.Type == ActivityTypesEx.Delay)
                {
                    // The Activity Schema doesn't have a delay type build in, so it's simulated
                    // here in the Bot. This matches the behavior in the Node connector.
                    int delayMs = (int)activity.Value;
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);

                    // No need to create a response. One will be created below.
                }
                else if (activity.Type == ActivityTypesEx.InvokeResponse)
                {
                    turnContext.TurnState.Add(InvokeReponseKey, activity);

                    // No need to create a response. One will be created below.
                }
                else if (activity.Type == ActivityTypes.Trace && activity.ChannelId != "emulator")
                {
                    // if it is a Trace activity we only send to the channel if it's the emulator.
                }
                else if (!string.IsNullOrWhiteSpace(activity.ReplyToId))
                {
                    var connectorClient = turnContext.TurnState.Get<IConnectorClient>();
                    response = await connectorClient.Conversations.ReplyToActivityAsync(activity, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var connectorClient = turnContext.TurnState.Get<IConnectorClient>();
                    response = await connectorClient.Conversations.SendToConversationAsync(activity, cancellationToken).ConfigureAwait(false);
                }

                // If No response is set, then defult to a "simple" response. This can't really be done
                // above, as there are cases where the ReplyTo/SendTo methods will also return null
                // (See below) so the check has to happen here.

                // Note: In addition to the Invoke / Delay / Activity cases, this code also applies
                // with Skype and Teams with regards to typing events.  When sending a typing event in
                // these _channels they do not return a RequestResponse which causes the bot to blow up.
                // https://github.com/Microsoft/botbuilder-dotnet/issues/460
                // bug report : https://github.com/Microsoft/botbuilder-dotnet/issues/465
                if (response == null)
                {
                    response = new ResourceResponse(activity.Id ?? string.Empty);
                }

                responses[index] = response;
            }

            return responses;
        }

        public override Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, Activity activity, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
