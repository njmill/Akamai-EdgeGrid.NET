using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Akamai.EdgeGrid.AuthV2
{
    /// <summary>
    /// Interface describing a request signer that signs service requests.
    /// 
    /// Author: akamaiEdgeGrid@spbk.us (Nick Miller)
    /// </summary>
    interface IRequestSigner
    {
        /// <summary>
        /// Signs a request with the client credential.
        /// </summary>
        /// <param name="request">the HttpRequestMessage object to sign</param>
        /// <param name="credential">the credential used in the signing</param>
        /// <param name="uploadStream">the optional stream to upload</param>
        /// <returns>the HttpRequestMessage with the added signature headers</returns>
        HttpRequestMessage Sign(HttpRequestMessage request, ClientCredential credential, Stream uploadStream = null);

        /// <summary>
        /// Signs and Executes a request with the client credential.
        /// </summary>
        /// <param name="request">the HttpRequestMessage object to sign</param>
        /// <param name="credential">the credential used in the signing</param>
        /// <param name="uploadStream">the optional stream to upload</param>
        /// <returns>the stream from the response (may be empty)</returns>
        Task<Stream> Execute(HttpRequestMessage request, ClientCredential credential, Stream uploadStream = null);
    }
}
