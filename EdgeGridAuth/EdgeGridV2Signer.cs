// Copyright 2014 Akamai Technologies http://developer.akamai.com.
//
// Licensed under the Apache License, KitVersion 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Author: akamaiEdgeGrid@spbk.us (Nick Miller)
//
using Akamai.EdgeGrid.Auth;
using Akamai.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Akamai.EdgeGrid.AuthV2
{
    /// <summary>
    /// The EdgeGrid Signer is responsible for brokering a requests.This class is responsible 
    /// for the core interaction logic given an API command and the associated set of parameters.
    /// 
    /// When event is executed, the 'Authorization' header is decorated
    /// 
    /// If connection is going to be reused, pass the persistent HttpWebRequest object when calling execute()
    /// 
    /// TODO: support rebinding on IO communication errors (eg: connection reset)
    /// TODO: support Async callbacks and Async IO
    /// TODO: support multiplexing 
    /// TODO: optimize and adapt throughput based on connection latency
    /// 
    /// This is a new implementation of the EdgeGrid Signer. It is not backwards compatible with the V1 implementation, so the original
    /// class is kept for legacy applications.  This class replaces WebRequest with System.Net.Http.HttpClient and utilizes async/await.
    /// 
    /// Author: akamaiEdgeGrid@spbk.us (Nick Miller)
    /// </summary>
    public class EdgeGridV2Signer: IRequestSigner
    {

        public class SignType
        {
            public static SignType HMACSHA256 = new SignType("EG1-HMAC-SHA256", KeyedHashAlgorithm.HMACSHA256);

            public string Name { get; private set; }
            public KeyedHashAlgorithm Algorithm { get; private set; }
            private SignType(string name, KeyedHashAlgorithm algorithm)
            {
                this.Name = name;
                this.Algorithm = algorithm;
            }
        }

        public class HashType
        {
            public static HashType SHA256 = new HashType(ChecksumAlgorithm.SHA256);

            public ChecksumAlgorithm Checksum { get; private set; }
            private HashType(ChecksumAlgorithm checksum)
            {
                this.Checksum = checksum;
            }
        }

        public const string AuthorizationHeader = "Authorization";

        /// <summary>
        /// The SignVersion enum value
        /// </summary>
        internal SignType SignVersion {get; private set;}

        /// <summary>
        /// The checksum mechanism to hash the request body
        /// </summary>
        internal HashType HashVersion {get; private set;}

        /// <summary>
        /// The ordered list of header names to include in the signature.
        /// </summary>
        internal IList<string> HeadersToInclude { get; private set; }

        /// <summary>
        /// The maximum body size used for computing the POST body hash (in bytes).
        /// </summary>
	    internal long? MaxBodyHashSize {get; private set; }

        public EdgeGridV2Signer(IList<string> headers = null, long? maxBodyHashSize = 2048)
        {
            this.HeadersToInclude = headers ?? new List<string>();
            this.MaxBodyHashSize = maxBodyHashSize;
            this.SignVersion = SignType.HMACSHA256;
            this.HashVersion = HashType.SHA256;
        }

        internal string GetAuthDataValue(ClientCredential credential, DateTime timestamp)
        {
            if (timestamp == null)
                throw new ArgumentNullException("timestamp cannot be null");

            Guid nonce = Guid.NewGuid();
            return string.Format("{0} client_token={1};access_token={2};timestamp={3};nonce={4};", 
                this.SignVersion.Name,
                credential.ClientToken,
                credential.AccessToken,
                timestamp.ToISO8601(),
                nonce.ToString().ToLower());
        }

        internal string GetRequestData(string method, Uri uri, System.Net.Http.Headers.HttpRequestHeaders requestHeaders = null, Stream requestStream = null)
        {
            if (string.IsNullOrEmpty(method))
                throw new ArgumentNullException("Invalid request: empty request method");

            String headers = GetRequestHeaders(requestHeaders);
            String bodyHash = "";
            // Only POST body is hashed
            if (method == "POST")
                bodyHash = GetRequestStreamHash(requestStream);

            return string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t",
                method.ToUpper(),
                uri.Scheme,
                uri.Host,
                uri.PathAndQuery,
                headers,
                bodyHash);
        }

        internal string GetRequestHeaders(System.Net.Http.Headers.HttpRequestHeaders requestHeaders)
        {
            if (requestHeaders == null) return string.Empty;

            StringBuilder headers = new StringBuilder();
            foreach (string name in this.HeadersToInclude)
            {
                //TODO: should auto detect headers and remove standard non-http headers
                string value = requestHeaders.GetValues(name).ToString();
                if (!string.IsNullOrEmpty(value))
                    headers.AppendFormat("{0}:{1}\t", name, Regex.Replace(value.Trim(), "\\s+", " ", RegexOptions.Compiled));
            }
            return headers.ToString();
        }

        internal string GetRequestStreamHash(Stream requestStream)
        {
            if (requestStream == null) return string.Empty;

            if (!requestStream.CanRead)
                throw new IOException("Cannot read stream to compute hash");

            if (!requestStream.CanSeek)
                throw new IOException("Stream must be seekable!");

            string streamHash = requestStream.ComputeHash(this.HashVersion.Checksum, MaxBodyHashSize).ToBase64();
            requestStream.Seek(0, SeekOrigin.Begin);
            return streamHash;
        }

        internal string GetAuthorizationHeaderValue(ClientCredential credential, DateTime timestamp, string authData, string requestData)
        {
            string signingKey = timestamp.ToISO8601().ToByteArray().ComputeKeyedHash(credential.Secret, this.SignVersion.Algorithm).ToBase64();
            string authSignature = string.Format("{0}{1}", requestData, authData).ToByteArray().ComputeKeyedHash(signingKey, this.SignVersion.Algorithm).ToBase64();
            return string.Format("{0}signature={1}", authData, authSignature);
        }

        /// <summary>
        /// Validates the response and attempts to detect root causes for failures for non 200 responses. The most common cause is 
        /// due to time synchronization of the local server. If the local server is more than 30seconds out of sync then the 
        /// API server will reject the request.
        /// 
        /// TODO: catch rate limitting errors. Should delay and retry.
        /// </summary>
        /// <param name="response">the active response object</param>
        public void Validate(HttpResponseMessage response)
        {
            if (response.StatusCode != HttpStatusCode.OK)
            {
                DateTime responseDate;
                string date = response.Headers.Date.ToString();
                if (date != null
                    && DateTime.TryParse(date, out responseDate))
                    if (DateTime.Now.Subtract(responseDate).TotalSeconds > 30)
                        throw new HttpRequestException("Local server Date is more than 30s out of sync with Remote server");

                string responseBody = response.Content.ReadAsStringAsync().Result;
                throw new HttpRequestException(string.Format("Unexpected Response from Server: {0} {1}\n{2}\n\n{3}", response.StatusCode, response.ReasonPhrase, response.Headers, responseBody));
            }
        }

        // Update the method signature to use async Task<HttpRequestMessage>
        public HttpRequestMessage Sign(HttpRequestMessage request, ClientCredential credential, Stream uploadStream = null)
        {
            DateTime timestamp = DateTime.UtcNow;

            //already signed?
            if (request.Headers.Contains(EdgeGridV1Signer.AuthorizationHeader))
                request.Headers.Remove(EdgeGridV1Signer.AuthorizationHeader);

            string requestData = GetRequestData(request.Method.Method, request.RequestUri, request.Headers, uploadStream);
            string authData = GetAuthDataValue(credential, timestamp);
            string authHeader = GetAuthorizationHeaderValue(credential, timestamp, authData, requestData);
            request.Headers.Add(EdgeGridV1Signer.AuthorizationHeader, authHeader);

            return request;
        }

        /// <summary>
        /// Opens the connection to the {OPEN} API, assembles the signing headers and uploads any files.
        /// </summary>
        /// <param name="request">the </param>
        /// <returns> the output stream of the response</returns>
        public async Task<Stream> Execute(HttpRequestMessage request, ClientCredential credential, Stream uploadStream = null)
        {
            // Make sure that this connection will behave nicely with multiple calls in a connection pool.
            ServicePointManager.EnableDnsRoundRobin = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            request = Sign(request, credential, uploadStream);

            if (request.Method == HttpMethod.Put || request.Method == HttpMethod.Post || request.Method == HttpMethod.Patch)
            {
                // Disable the nastiness of Expect100Continue
                ServicePointManager.Expect100Continue = false;
                if (uploadStream == null)
                    request.Content = new StreamContent(Stream.Null);
                



                if (uploadStream != null)
                {
                    // Avoid internal memory allocation before buffering the output
                    request.Content?.LoadIntoBufferAsync().ConfigureAwait(false);
                    request.Content = new StreamContent(uploadStream);

                    if (string.IsNullOrEmpty(request.Content?.Headers.ContentType?.MediaType))
                        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    if (uploadStream.CanSeek)
                        request.Content.Headers.ContentLength = uploadStream.Length;
                    else if (request is HttpRequestMessage)
                        request.Headers.TransferEncodingChunked = true;
                }
            }
               
            if (request is HttpRequestMessage)
            {
                var httpRequest = (HttpRequestMessage)request; 
                httpRequest.Headers.Add("Accept", "*/*");
                if (String.IsNullOrEmpty(httpRequest.Headers.UserAgent.ToString()))
                    httpRequest.Headers.Add("UserAgent", "EdgeGrid.Net/v2"); 
            }

            HttpResponseMessage response = null;
            try
            {
                response = await new HttpClient().SendAsync(request);
            }
            catch (HttpRequestException e)
            {
                // non 200 OK responses throw exceptions.
                // is this because of Time drift? can we re-try?
                //using (response = e.HResult)
                    Validate(response);
            }

            return await response.Content.ReadAsStreamAsync();
        }

    }
}
