/**
 * @copyright
 *
 * Copyright 2013-2015 Splunk, Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"): you may
 * not use this file except in compliance with the License. You may obtain
 * a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 */

using System;
using System.Net;
using System.Net.Http;
// ReSharper disable CheckNamespace

namespace Splunk.Logging
{
    /// <summary>
    /// HTTP event collector exception. This class is used when an HTTP event collector client is 
    /// unable to send events to the server;
    /// </summary>
    public class HttpEventCollectorException : Exception 
    {
        /// <summary>
        /// HTTP status code.
        /// </summary>
        public HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// Exception thrown by HTTP client when sending the data. This value 
        /// can be null.
        /// </summary>
        public Exception WebException { get; private set; }
        
        /// <summary>
        /// Reply from the Splunk server. This value can be null.
        /// </summary>
        public string ServerReply { get; private set; }

        /// <summary>
        /// Reply from the Splunk server. This value can be null.
        /// </summary>
        public HttpResponseMessage Response { get; private set; }

        /// <summary>
        /// JSON Serialized events that caused the problem. This value is set by HttpEventCollectorSender.
        /// </summary>
        public string SerializedEvents { get; internal set; }

        /// <summary>
        /// HTTP event collector exception container.
        /// </summary>
        /// <param name="code">HTTP status code.</param>
        /// <param name="webException">Exception thrown by HTTP client when sending the data.</param>
        /// <param name="reply">Splunk server reply.</param>
        /// <param name="response">HTTP response.</param>
        /// <param name="serializedEvents">JSON Serialized events that caused the problem.</param>
        public HttpEventCollectorException(
            HttpStatusCode code, 
            Exception webException = null, 
            string reply = null, 
            HttpResponseMessage response = null,
            string serializedEvents = null)
            :base(!string.IsNullOrEmpty(reply) ? $"Failed to send request: {code} - {reply}" : $"Failed to send request: {code}", webException)
        {
            this.StatusCode = code;
            this.WebException = webException;
            this.ServerReply = reply;
            this.Response = response;
            this.SerializedEvents = serializedEvents;
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            if (Response != null)
                return string.Concat(base.ToString(), Environment.NewLine, Response.ToString());
            else
                return base.ToString();
        }
    }
}