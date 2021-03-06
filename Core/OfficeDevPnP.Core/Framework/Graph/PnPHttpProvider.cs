﻿using Microsoft.Graph;
using OfficeDevPnP.Core.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.SharePoint.Client.ClientContextExtensions;

namespace OfficeDevPnP.Core.Framework.Graph
{
    public class PnPHttpProvider : HttpProvider, IHttpProvider
    {
        private int _retryCount;
        private int _delay;

        public PnPHttpProvider(int retryCount = 10, int delay = 500) :
            base()
        {
            if (retryCount <= 0)
                throw new ArgumentException("Provide a retry count greater than zero.");

            if (delay <= 0)
                throw new ArgumentException("Provide a delay greater than zero.");

            this._retryCount = retryCount;
            this._delay = delay;
        }

        /// <summary>
        /// Custom implementation of the IHttpProvider.SendAsync method to handle retry logic
        /// </summary>
        /// <param name="request">The HTTP Request Message</param>
        /// <param name="completionOption">The completion option</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The result of the asynchronous request</returns>
        /// <remarks>See here for further details: https://graph.microsoft.io/en-us/docs/overview/errors</remarks>
        Task<HttpResponseMessage> IHttpProvider.SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken)
        {
            Task<HttpResponseMessage> result = null;

            // Retry logic variables
            int retryAttempts = 0;
            int backoffInterval = this._delay;

            // Loop until we need to retry
            while (retryAttempts < this._retryCount)
            {
                try
                {
                    // Make the request
                    result = base.SendAsync(request, completionOption, cancellationToken);

                    // And return the response in case of success
                    return (result);
                }
                // Or handle any ServiceException
                catch (ServiceException ex)
                {
                    // Check if the is an InnerException
                    if (ex.InnerException != null)
                    {
                        // And if it is a WebException
                        var wex = ex.InnerException as WebException;
                        if (wex != null)
                        {
                            var response = wex.Response as HttpWebResponse;
                            // Check if request was throttled - http status code 429
                            // Check is request failed due to server unavailable - http status code 503
                            if (response != null && (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == (HttpStatusCode)503))
                            {
                                Log.Warning(Constants.LOGGING_SOURCE, CoreResources.GraphExtensions_SendAsyncRetry, backoffInterval);

                                //Add delay for retry
                                Thread.Sleep(backoffInterval);

                                //Add to retry count and increase delay.
                                retryAttempts++;
                                backoffInterval = backoffInterval * 2;
                            }
                            else
                            {
                                Log.Error(Constants.LOGGING_SOURCE, CoreResources.GraphExtensions_SendAsyncRetryException, wex.ToString());
                                throw;
                            }
                        }
                    }
                    throw;
                }
            }

            throw new MaximumRetryAttemptedException(string.Format("Maximum retry attempts {0}, has be attempted.", this._retryCount));
        }
    }
}
