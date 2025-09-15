// <copyright file="HttpServer.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareData2
{
    /// <summary>
    ///     Simple HTTP server for sharing game data.
    /// </summary>
    public sealed class HttpServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Func<HttpListenerRequest, string> _responderMethod;
        private bool _isRunning;
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="HttpServer" /> class.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        /// <param name="responderMethod">The method to handle requests.</param>
        public HttpServer(int port, Func<HttpListenerRequest, string> responderMethod)
        {
            if (!HttpListener.IsSupported)
            {
                throw new NotSupportedException("HTTP Listener is not supported on this platform.");
            }

            _responderMethod = responderMethod ?? throw new ArgumentNullException(nameof(responderMethod));
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:{port}/");
        }

        /// <summary>
        ///     Starts the HTTP server.
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                return;
            }

            try
            {
                _listener.Start();
                _isRunning = true;
                _ = Task.Run(ListenAsync);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to start HTTP server: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     Stops the HTTP server.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            _listener?.Stop();
        }

        /// <summary>
        ///     Gets a value indicating whether the server is running.
        /// </summary>
        public bool IsRunning => _isRunning;

        private async Task ListenAsync()
        {
            while (_isRunning && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context));
                }
                catch (ObjectDisposedException)
                {
                    // Server was stopped
                    break;
                }
                catch (HttpListenerException)
                {
                    // Server was stopped
                    break;
                }
                catch (Exception ex)
                {
                    // Log error but continue listening
                    Console.WriteLine($"HTTP Server error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                var responseString = _responderMethod(request);
                var buffer = Encoding.UTF8.GetBytes(responseString);

                response.ContentLength64 = buffer.Length;
                response.ContentType = "application/json";
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling request: {ex.Message}");
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch
                {
                    // Ignore errors when closing response
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _listener?.Close();
            _disposed = true;
        }
    }
}
