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

using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Splunk.Logging
{
    /// <summary>
    /// HTTP event collector client side implementation that collects, serializes and send 
    /// events to Splunk HTTP event collector endpoint. This class shouldn't be used directly
    /// by user applications.
    /// </summary>
    /// <remarks>
    /// * HttpEventCollectorSender is thread safe and Send(...) method may be called from
    /// different threads.
    /// * Events are sending asynchronously and Send(...) method doesn't 
    /// block the caller code.
    /// * HttpEventCollectorSender has an ability to plug middleware components that act 
    /// before posting data.
    /// For example:
    /// <code>
    /// new HttpEventCollectorSender(uri: ..., token: ..., 
    ///     middleware: (request, next) => {
    ///         // preprocess request
    ///         var response = next(request); // post data
    ///         // process response
    ///         return response;
    ///     }
    ///     ...
    /// )
    /// </code>
    /// Middleware components can apply additional logic before and after posting
    /// the data to Splunk server. See HttpEventCollectorResendMiddleware.
    /// </remarks>
    public class HttpEventCollectorSender : IDisposable
    {
        /// <summary>
        /// Post request delegate. 
        /// </summary>
        /// <param name="request">HTTP request.</param>
        /// <returns>Server HTTP response.</returns>
        public delegate Task<HttpResponseMessage> HttpEventCollectorHandler(
            string token, byte[] serializedEvents);

        /// <summary>
        /// HTTP event collector middleware plugin.
        /// </summary>
        /// <param name="request">HTTP request.</param>
        /// <param name="next">A handler that posts data to the server.</param>
        /// <returns>Server HTTP response.</returns>
        public delegate Task<HttpResponseMessage> HttpEventCollectorMiddleware(
            string token, byte[] serializedEvents, HttpEventCollectorHandler next);

        /// <summary>
        /// Override the default event format.
        /// </summary>
        /// <returns>A dynamic type to be serialized.</returns>
        public delegate dynamic HttpEventCollectorFormatter(HttpEventCollectorEventInfo eventInfo);

        /// <summary>
        /// Recommended default values for events batching
        /// </summary>
        public const int DefaultBatchInterval = 10 * 1000; // 10 seconds
        public const int DefaultBatchSize = 10 * 1024; // 10KB
        public const int DefaultBatchCount = 10;

        /// <summary>
        /// Sender operation mode. Parallel means that all HTTP requests are 
        /// asynchronous and may be indexed out of order. Sequential mode guarantees
        /// sequential order of the indexed events. 
        /// </summary>
        public enum SendMode
        {
            Parallel,
            Sequential
        };

        private static readonly Encoding HttpContentEncoding = new UTF8Encoding(false);
        private readonly MediaTypeHeaderValue HttpContentHeaderValue = new MediaTypeHeaderValue("application/json") { CharSet = HttpContentEncoding.WebName };
        private const string HttpEventCollectorPath = "/services/collector/event/1.0";
        private const string AuthorizationHeaderScheme = "Splunk";
        private readonly Uri httpEventCollectorEndpointUri; // HTTP event collector endpoint full uri
        private HttpEventCollectorEventInfo.Metadata metadata; // logger metadata
        private string token; // authorization token

        // events batching properties and collection 
        private int batchInterval = 0;
        private int batchSizeBytes = 0;
        private int batchSizeCount = 0;
        private SendMode sendMode = SendMode.Parallel;
        private Task activePostTask = null;
        private object eventsBatchLock = new object();
        private int eventsBatchCount;
        private readonly System.IO.StreamWriter serializedEventsBatch = new System.IO.StreamWriter(new System.IO.MemoryStream(), HttpContentEncoding, 1024, true);
        private readonly JsonSerializerSettings jsonSerializerSettings = JsonConvert.DefaultSettings?.Invoke() ?? new JsonSerializerSettings();
        private JsonSerializer jsonSerializer;
        private Timer timer;

        private HttpClient httpClient = null;
        private HttpEventCollectorMiddleware middleware = null;
        private HttpEventCollectorFormatter formatter = null;
        // counter for bookkeeping the async tasks 
        private long activeAsyncTasksCount = 0;

        /// <summary>
        /// On error callbacks.
        /// </summary>
        public event Action<HttpEventCollectorException> OnError = (e) => { };

        /// <param name="uri">Splunk server uri, for example https://localhost:8088.</param>
        /// <param name="token">HTTP event collector authorization token.</param>
        /// <param name="metadata">Logger metadata.</param>
        /// <param name="sendMode">Send mode of the events.</param>
        /// <param name="batchInterval">Batch interval in milliseconds.</param>
        /// <param name="batchSizeBytes">Batch max size.</param>
        /// <param name="batchSizeCount">Max number of individual events in batch.</param>
        /// <param name="middleware">
        /// HTTP client middleware. This allows to plug an HttpClient handler that 
        /// intercepts logging HTTP traffic.
        /// </param>
        /// <remarks>
        /// Zero values for the batching params mean that batching is off. 
        /// </remarks>
        public HttpEventCollectorSender(
            Uri uri, string token, HttpEventCollectorEventInfo.Metadata metadata,
            SendMode sendMode,
            int batchInterval, int batchSizeBytes, int batchSizeCount,
            HttpEventCollectorMiddleware middleware,
            HttpEventCollectorFormatter formatter = null)
        {
            this.httpEventCollectorEndpointUri = new Uri(uri, HttpEventCollectorPath);
            this.jsonSerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            this.jsonSerializerSettings.Formatting = Formatting.None;
            this.jsonSerializerSettings.Converters = new[] { new Newtonsoft.Json.Converters.StringEnumConverter() };
            this.jsonSerializer = JsonSerializer.CreateDefault(this.jsonSerializerSettings);
            this.sendMode = sendMode;
            this.batchInterval = batchInterval;
            this.batchSizeBytes = batchSizeBytes;
            this.batchSizeCount = batchSizeCount;
            this.metadata = metadata;
            this.token = token;
            this.middleware = middleware;
            this.formatter = formatter;

            // special case - if batch interval is specified without size and count
            // they are set to "infinity", i.e., batch may have any size 
            if (this.batchInterval > 0 && this.batchSizeBytes == 0 && this.batchSizeCount == 0)
            {
                this.batchSizeBytes = this.batchSizeCount = int.MaxValue;
            }

            // when size configuration setting is missing it's treated as "infinity",
            // i.e., any value is accepted.
            if (this.batchSizeCount == 0 && this.batchSizeBytes > 0)
            {
                this.batchSizeCount = int.MaxValue;
            }
            else if (this.batchSizeBytes == 0 && this.batchSizeCount > 0)
            {
                this.batchSizeBytes = int.MaxValue;
            }

            // setup the timer
            if (batchInterval != 0) // 0 means - no timer
            {
                timer = new Timer(OnTimer, null, batchInterval, batchInterval);
            }

            // setup HTTP client
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(AuthorizationHeaderScheme, token);
        }

        /// <summary>
        /// Send an event to Splunk HTTP endpoint. Actual event send is done 
        /// asynchronously and this method doesn't block client application.
        /// </summary>
        /// <param name="id">Event id.</param>
        /// <param name="level">Event severity info.</param>
        /// <param name="messageTemplate">Event message template.</param>
        /// <param name="renderedMessage">Event message rendered.</param>
        /// <param name="exception">Event exception.</param>
        /// <param name="properties">Additional event data.</param>
        /// <param name="metadataOverride">Metadata to use for this send.</param>
        public void Send(
            string id = null,
            string level = null,
            string messageTemplate = null,
            string renderedMessage = null,
            object exception = null,
            object properties = null,
            HttpEventCollectorEventInfo.Metadata metadataOverride = null)
        {
            HttpEventCollectorEventInfo ei =
                new HttpEventCollectorEventInfo(id, level, messageTemplate, renderedMessage, exception, properties, metadataOverride ?? metadata);

            DoSerialization(ei);
        }

        /// <summary>
        /// Send an event to Splunk HTTP endpoint. Actual event send is done 
        /// asynchronously and this method doesn't block client application.
        /// </summary>
        /// <param name="timestamp">Timestamp to use.</param>
        /// <param name="id">Event id.</param>
        /// <param name="level">Event level info.</param>
        /// <param name="messageTemplate">Event message template.</param>
        /// <param name="renderedMessage">Event message rendered.</param>
        /// <param name="exception">Event exception.</param>
        /// <param name="properties">Additional event data.</param>
        /// <param name="metadataOverride">Metadata to use for this send.</param>
        public void Send(
            DateTime timestamp,
            string id = null,
            string level = null,
            string messageTemplate = null,
            string renderedMessage = null,
            object exception = null,
            object properties = null,
            HttpEventCollectorEventInfo.Metadata metadataOverride = null)
        {
            HttpEventCollectorEventInfo ei =
                new HttpEventCollectorEventInfo(timestamp, id, level, messageTemplate, renderedMessage, exception, properties, metadataOverride ?? metadata);

            DoSerialization(ei);
        }

        private void DoSerialization(HttpEventCollectorEventInfo ei)
        {
            if (formatter != null)
            {
                var formattedEvent = formatter(ei);
                ei.Event = formattedEvent;
            }

            // we use lock serializedEventsBatch to synchronize both 
            // serializedEventsBatch and serializedEvents
            lock (eventsBatchLock)
            {
                ++eventsBatchCount;

                long orgLength = this.serializedEventsBatch.BaseStream.Length;

                try
                {
                    using (JsonTextWriter jsonWriter = new JsonTextWriter(this.serializedEventsBatch))
                    {
                        jsonWriter.Formatting = this.jsonSerializer.Formatting;
                        this.jsonSerializer.Serialize(jsonWriter, ei);
                    }
                    this.serializedEventsBatch.Flush();
                }
                catch
                {
                    // Unwind / truncate any bad output
                    this.serializedEventsBatch.Flush();
                    this.serializedEventsBatch.BaseStream.Position = orgLength;
                    this.serializedEventsBatch.BaseStream.SetLength(orgLength);
                    this.jsonSerializer = JsonSerializer.CreateDefault(this.jsonSerializerSettings);   // Reset bad state
                    throw;
                }

                if (eventsBatchCount >= batchSizeCount ||
                    serializedEventsBatch.BaseStream.Length >= batchSizeBytes)
                {
                    // there are enough events in the batch
                    FlushInternal();
                }
            }
        }

        /// <summary>
        /// Flush all events synchronously, i.e., flush and wait until all events
        /// are sent.
        /// </summary>
        public void FlushSync()
        {
            Flush();
            // wait until all pending tasks are done
            while(Interlocked.CompareExchange(ref activeAsyncTasksCount, 0, 0) != 0)
            {
                // wait for 100ms - not CPU intensive and doesn't delay process 
                // exit too much
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Flush all event.
        /// </summary>
        public Task FlushAsync()
        {            
            return new Task(() => 
            {
                FlushSync();
            });
        }

        /// <summary>
        /// Flush all batched events immediately. 
        /// </summary>
        private void Flush()
        {
            lock (eventsBatchLock)
            {
                FlushInternal();
            }
        }        

        private void FlushInternal()
        {
            // FlushInternal method is called only in contexts locked on eventsBatchLock  
            // therefore it's thread safe and doesn't need additional synchronization.
            eventsBatchCount = 0;
            if (serializedEventsBatch.BaseStream.Length == 0)
                return; // there is nothing to send

            // Create batch as new byte-array, so we can reuse the MemoryStream
            var batchPayload = ((System.IO.MemoryStream)this.serializedEventsBatch.BaseStream).ToArray();

            this.serializedEventsBatch.BaseStream.Position = 0;
            this.serializedEventsBatch.BaseStream.SetLength(0);

            // flush events according to the system operation mode
            if (this.sendMode == SendMode.Sequential)
                FlushInternalSequentialMode(batchPayload);
            else
                FlushInternalSingleBatch(batchPayload);
        }

        private void FlushInternalSequentialMode(
            byte[] serializedEvents)
        {
            // post events only after the current post task is done
            if (this.activePostTask == null)
            {
                this.activePostTask = Task.Factory.StartNew(() =>
                {
                    FlushInternalSingleBatch(serializedEvents).Wait();
                });
            }
            else
            {
                this.activePostTask = this.activePostTask.ContinueWith((_) =>
                {
                    FlushInternalSingleBatch(serializedEvents).Wait();
                });
            }
        }

        private Task<HttpStatusCode> FlushInternalSingleBatch(
            byte[] serializedEvents)
        {
            // post data and update tasks counter
            Interlocked.Increment(ref activeAsyncTasksCount);
            Task<HttpStatusCode> task = PostEvents(serializedEvents);
            task.ContinueWith((_) =>
            {
                Interlocked.Decrement(ref activeAsyncTasksCount);            
            });
            return task;
        }

        private async Task<HttpStatusCode> PostEvents(
            byte[] serializedEvents)
        {
            // encode data
            HttpResponseMessage response = null;
            string serverReply = null;
            HttpStatusCode responseCode = HttpStatusCode.OK;
            try
            {
                // post data
                HttpEventCollectorHandler next = (t, s) =>
                {
                    HttpContent content = new ByteArrayContent(serializedEvents);
                    content.Headers.ContentType = HttpContentHeaderValue;
                    return httpClient.PostAsync(httpEventCollectorEndpointUri, content);
                };
                HttpEventCollectorHandler postEvents = (t, s) =>
                {
                    return middleware == null ?
                        next(t, s) : middleware(t, s, next);
                };
                response = await postEvents(token, serializedEvents);
                responseCode = response.StatusCode;
                if (responseCode != HttpStatusCode.OK && response.Content != null)
                {
                    // record server reply
                    serverReply = await response.Content.ReadAsStringAsync();
                    OnError(new HttpEventCollectorException(
                        code: responseCode,
                        webException: null,
                        reply: serverReply,
                        response: response,
                        serializedEvents: HttpContentEncoding.GetString(serializedEvents)
                    ));
                }
            }
            catch (HttpEventCollectorException e)
            {
                responseCode = responseCode == HttpStatusCode.OK ? e.Response.StatusCode : responseCode;
                e.SerializedEvents = e.SerializedEvents ?? HttpContentEncoding.GetString(serializedEvents);
                OnError(e);
            }
            catch (Exception e)
            {
                responseCode = responseCode == HttpStatusCode.OK ? HttpStatusCode.BadRequest : responseCode;
                OnError(new HttpEventCollectorException(
                    code: responseCode,
                    webException: e,
                    reply: serverReply,
                    response: response,
                    serializedEvents: HttpContentEncoding.GetString(serializedEvents)
                ));
            }
            return responseCode;
        }

        private void OnTimer(object state)
        {
            Flush();
        }

        #region HttpClientHandler.IDispose

        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;
            if (disposing)
            {
                OnError = null;
                if (timer != null)
                {
                    timer.Dispose();
                }
                httpClient.Dispose();
            }
            disposed = true;
        }

        ~HttpEventCollectorSender()
        {
            Dispose(false);
        }

        #endregion
    }
}