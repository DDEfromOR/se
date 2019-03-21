using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Protocol;

namespace Microsoft.Bot.Builder.Integration.AspNet.Core
{
    /// <summary>
    /// Interface to express the relationship between an mvc api Controller and a Bot Builder Adapter making use of the Bot Framework Protocl Streaming Extensions.
    /// This interface can be used for Dependency Injection.
    /// </summary>
    public class IBotFrameworkWebSocketAdapter
    {
        /// <summary>
        /// This method can be called from inside a POST method on any Controller implementation.
        /// </summary>
        /// <param name="request">The Bot Framework Protocol Streaming Extensions request object, typically in a POST handler by a Controller.</param>
        /// <param name= "response">The Bot Framework Protocol Streaming Extensions response object.</param>
        /// <param name="bot">The bot implementation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        Task ProcessAsync(Request request, Response response, IBot bot, CancellationToken cancellationToken = default(CancellationToken));
    }
}
