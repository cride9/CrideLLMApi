using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace CrideLLMApi.Helpers;

public sealed class CrideApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ResponseBody { get; }

    public CrideApiException(HttpStatusCode statusCode, string? responseBody)
        : base($"API request failed with {(int)statusCode} ({statusCode}). Response: {responseBody}")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
