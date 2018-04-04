﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CallTracerLibrary.DataProviders;
using CallTracerLibrary.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;


namespace CallTracerLibrary.Middlewares
{
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class TracingMiddleware
    {
        private Stopwatch _timeKeeper;
        private readonly RequestDelegate _next;
        private IRepository<TraceMetadata, int> _repository;
        private static int _defaultPageSize = 20;


        public TracingMiddleware(RequestDelegate next, IRepository<TraceMetadata, int> repository)//TraceMetadata traceMetadata//IApplicationBuilder app
        {
            _repository = repository;
            _next = next;

        }


        public async Task Invoke(HttpContext httpContext)
        {

            //Test Block
           // MySQLRepository.Method();


            //End Test Block

            TraceMetadata _trace = new TraceMetadata();
            if (httpContext.Request.Path != "/trace" && httpContext.Request.Path != "/ui")
            {
                _trace.RequestTimestamp = DateTime.Now;
                _timeKeeper = Stopwatch.StartNew();

                await _next(httpContext);
                _timeKeeper.Stop();

                _trace.ResponseTimestamp = DateTime.Now;
                _trace.ResponseTimeMs = _timeKeeper.Elapsed.TotalMilliseconds;
                _trace.ResponseStatusCode = httpContext.Response.StatusCode;
                _trace.RequestMethod = httpContext.Request.Method;
                _trace.RequestUri = httpContext.Request.Path;

                if (httpContext.Response.StatusCode < 500)
                {
                    _trace.Type = "Regular"; // Regular Trace
                }
                else
                {
                    _trace.Type = "Error"; // Server Sider Error
                }


                _trace.RequestContent = "Request Content Goes here";
                _trace.ResponseContent = "Response Content Goes here";

                _repository.SaveAsync(_trace);// Should await be used ?

            }

            else if(httpContext.Request.Path == "/trace") //Request Trace Logs --> Configurable  Endpoint
            {

                int pageSize, pageNumber, recordsToSkip;
                var pageSizeStr = httpContext.Request.Query["size"].ToString();
                var pageNumberStr = httpContext.Request.Query["page"].ToString();
                int.TryParse(pageSizeStr, out pageSize);
                int.TryParse(pageNumberStr, out pageNumber);
                pageSize = pageSize == 0 ? pageSize = _defaultPageSize : pageSize;
                recordsToSkip = pageSize * pageNumber;


                var asyncDocuments = await _repository.GetAll();
                var asyncDocumentsPaged = asyncDocuments.Skip(recordsToSkip).Take(pageSize);
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(asyncDocumentsPaged);

                httpContext.Response.ContentType = "Application/json";
                await httpContext.Response.WriteAsync(json);
            }

            else if(httpContext.Request.Path == "/ui") //UI --> Endpoint should be configurable
            {
                string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = System.IO.Path.Combine(currentDirectory,"FrontEnd","templete.html"); 

                string readContents;
                using (StreamReader streamReader = new StreamReader(filePath, Encoding.UTF8))
                {
                    readContents = streamReader.ReadToEnd();
                }

                httpContext.Response.ContentType = "text/html";
                await httpContext.Response.WriteAsync(readContents);
            }
        }
    }

    
    
    
    
    
    
    
    
    
    
    
    
    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class TracingMiddlewareExtensions
    {
        public static IApplicationBuilder UseTracingMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TracingMiddleware>();
        }
    }
}