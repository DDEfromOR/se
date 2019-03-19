using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Schema;

namespace Microsoft.Bot.Builder.Adapters
{
   public class BotFrameworkStreamingExtensionsAdapter : BotAdapter, IAdapterIntegration, IUserTokenProvider
    {
        public class PayloadType
        {
            public const char Request = 'A';
            public const char Response = 'B';
            public const char Stream = 'S';
            public const char CancelAll = 'X';
            public const char CancelStream = 'C';
        }

        public class Header
        {
            public const char Terminator = '\n';
            public const char Delimiter = '.';

            public PayloadType PayloadType { get; set; }

            public int Length { get; set; }

            public Guid RequestID { get; set; }

            public byte End { get; set; }

            public string GetRequestIdAsHex()
            {
                return this.RequestID.ToString();
            }
        }

        public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, Activity[] activities, CancellationToken cancellationToken)
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
                throw new ArgumentException("Activities array contained no activities.", nameof(activities));
            }

            foreach (var activity in activities)
            {
                //do some stuff, then send stuff!
            }

            var response
        }

        public override Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, Activity activity, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TokenResponse> GetUserTokenAsync(ITurnContext turnContext, string connectionName, string magicCode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetOauthSignInLinkAsync(ITurnContext turnContext, string connectionName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetOauthSignInLinkAsync(ITurnContext turnContext, string connectionName, string userId, string finalRedirect = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task SignOutUserAsync(ITurnContext turnContext, string connectionName = null, string userId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<TokenStatus[]> GetTokenStatusAsync(ITurnContext context, string userId, string includeFilter = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<string, TokenResponse>> GetAadTokensAsync(ITurnContext context, string connectionName, string[] resourceUrls, string userId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<InvokeResponse> ProcessActivityAsync(string authHeader, Activity activity, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
