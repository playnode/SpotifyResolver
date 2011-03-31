/*
Copyright (c) 2011 Steven Robertson, steverobertson@gmail.com

Permission is hereby granted, free of charge, to any person
obtaining a copy of this software and associated documentation
files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use,
copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using Spotify;

#if DEBUG
using System.Reflection;
using log4net;
#endif

namespace Stever.PlaySpot
{
    class HttpStreamingServer
    {
#if DEBUG
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().ReflectedType);
#endif

        const int ThreadAbortDelayTimes = 6;
        const int ThreadAbortDelay = 500;
        const int SocketSendTimeout = 10000; // 10 seconds.
        const int CreateTrackFromLinkRetryLimit = 10; // 1/2 second in total.
        const int CreateTrackFromLinkRetryDelay = 50; // 50 milliseconds.

        static bool _activeRequest;

        #region URL

        const string Protocol = "http://";
        const string Host = "localhost";
        internal const int Port = 60218;
        const string BasePath = "/sid/";

        string _baseUrl;

        internal string BaseUrl
        {
            get { return _baseUrl; }
            set { _baseUrl = value; }
        }

        #endregion

        readonly Spotify _spotify;
        readonly SpotifyLinkLookup _spotifyLinkLookup;
        readonly TcpListener _listener;

        Thread _serverThread;
        Thread _clientThread;
        Socket _clientSocket;
        Socket _newRequestSocket;

        bool _runServer = true;
        bool _runClient = true;

        internal HttpStreamingServer(Spotify spotify, SpotifyLinkLookup spotifyLinkLookup)
        {
#if DEBUG
            if (Log.IsDebugEnabled)
                Log.Debug("HttpStreamingServer");
#endif

            _spotify = spotify;
            _spotifyLinkLookup = spotifyLinkLookup;

            // Build the base URL.
            StringBuilder urlBuild = new StringBuilder();
            urlBuild.Append(Protocol);
            urlBuild.Append(Host);
            urlBuild.Append(':').Append(Port);
            urlBuild.Append(BasePath);
            BaseUrl = urlBuild.ToString();

            // TCP listener for the HTTP server.
            _listener = new TcpListener(IPAddress.Any, Port);
        }

        internal void Start()
        {
            if (_serverThread != null)
                throw new Exception("Start method cannot be called more than once.");

            // Start the TCP listener.
            _listener.Start();
#if DEBUG
            if (Log.IsInfoEnabled)
                Log.Debug("HTTP streaming server started on port " + Port);
#endif

            // Start the HTTP server thread.
            _serverThread = new Thread(ServerThread);
            _serverThread.Start();
        }

        internal void Shutdown()
        {
#if DEBUG
            if (Log.IsDebugEnabled)
                Log.Debug("Shutting down HTTP streaming server.");
#endif

            _runClient = false;
            _runServer = false;

            if (_spotify != null)
            {
#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("Cancelling any current Spotify playback.");
#endif
                _spotify.CancelPlayback();
            }

            if (_newRequestSocket != null)
            {
                if (_newRequestSocket.Connected)
                {
#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug("Closing new request socket.");
#endif

                    _newRequestSocket.Close();
                }

                _newRequestSocket = null;
            }

            if (_clientSocket != null)
            {
                if (_clientSocket.Connected)
                {
#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug("Closing client socket.");
#endif

                    _clientSocket.Close();
                }

                _clientSocket = null;
            }

            if (_serverThread != null)
            {
#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("Ending server thread.");
#endif

                // Allow time for server thread to shutdown.
                for (int i = 0; i < ThreadAbortDelayTimes; i++)
                {
                    if (!_serverThread.IsAlive) break;

#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug("Waiting for server thread.");
#endif

                    Thread.Sleep(ThreadAbortDelay);
                }

                // Abort the server thread.
                if (_serverThread.IsAlive)
                {
#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug("Aborting server thread.");
#endif

                    _serverThread.Abort();
                }
            }

            if (_clientThread != null)
            {
#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("Ending client thread.");
#endif

                // Allow time for client thread to shutdown.
                for (int i = 0; i < ThreadAbortDelayTimes; i++)
                {
                    if (!_clientThread.IsAlive) break;

#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug("Waiting for client thread.");
#endif

                    Thread.Sleep(ThreadAbortDelay);
                }

                // Abort the client thread.
                if (_clientThread.IsAlive)
                {
#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug("Aborting client thread.");
#endif

                    _clientThread.Abort();
                }
            }

            if (_listener != null)
            {
#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("Stopping the TCP listener.");
#endif

                _listener.Stop();
            }
        }

        void ServerThread()
        {
            while (_runServer)
            {
                try
                {
                    // Check if there's a pending request.
                    if (!_listener.Pending())
                    {
                        Thread.Sleep(30);
                        continue;
                    }

                    // Socket information.
                    _newRequestSocket = _listener.AcceptSocket();
                    IPEndPoint clientInfo = (IPEndPoint)_newRequestSocket.RemoteEndPoint;
#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug("Client: " + clientInfo.Address + ":" + clientInfo.Port);
#endif

                    // Only accept local requests.
                    if (!clientInfo.Address.Equals(new IPAddress(new byte[] { 127, 0, 0, 1 })))
                    {
#if DEBUG
                        if (Log.IsWarnEnabled)
                            Log.Warn("Invalid client! " + clientInfo.Address);
#endif

                        return;
                    }

                    // Only serve one request at a time.
                    if (!_activeRequest) _activeRequest = true;
                    else
                    {
#if DEBUG
                        if (Log.IsWarnEnabled)
                            Log.Warn("Already active request. Cancelling previous request.");

                        if (Log.IsDebugEnabled)
                            Log.Debug("Cancelling playback.");
#endif

                        _spotify.CancelPlayback();

                        if (_clientThread != null)
                        {
#if DEBUG
                            if (Log.IsDebugEnabled)
                                Log.Debug("Shutting down the existing client thread.");
#endif

                            // Try to shutdown the client thread.
                            _runClient = false;

                            // Allow time for the thread to shutdown.
                            for (int i = 0; i < ThreadAbortDelayTimes; i++)
                            {
                                if (!_clientThread.IsAlive) break;

#if DEBUG
                                if (Log.IsDebugEnabled)
                                    Log.Debug("Waiting for client thread.");
#endif

                                Thread.Sleep(ThreadAbortDelay);
                            }

                            // Abort the thread.
                            if (_clientThread.IsAlive)
                            {
#if DEBUG
                                if (Log.IsDebugEnabled)
                                    Log.Debug("Aborting the client thread.");
#endif

                                _clientThread.Abort();
                            }

                            _clientThread = null;
                        }
                    }

                    // Deal with the request on another thread.
                    _runClient = true;
                    _clientThread = new Thread(ClientRequestThread);
                    _clientThread.Start();
                }
                catch (ThreadAbortException)
                {
#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug("Thread being aborted.");
#endif
                }
                catch (Exception ex)
                {
#if DEBUG
                    if (Log.IsErrorEnabled)
                        Log.Error("Exception", ex);
#endif
                }
            }

            if (_newRequestSocket != null)
            {
                if (_newRequestSocket.Connected)
                    _newRequestSocket.Close();
            }

#if DEBUG
            if (Log.IsDebugEnabled)
                Log.Debug("Server thread ends.");
#endif
        }

        void ClientRequestThread()
        {
            // Copy the socket reference.
            _clientSocket = _newRequestSocket;
            _newRequestSocket = null;
            try
            {
                // Receive HTTP Request from Web Browser
                byte[] recvBytes = new byte[1025];
                int bytes = 0;
				int retries = 0;
				while (bytes == 0) {
					bytes = _clientSocket.Receive(recvBytes, 0, _clientSocket.Available, SocketFlags.None);

#if DEBUG
	                if (Log.IsDebugEnabled)
	                    Log.Debug("Received " + bytes + " bytes.");
#endif

					if (retries++ > 100)
						throw new Exception("Exceeded maximum number of retries for receiving HTTP request.");

					if (bytes == 0) Thread.Sleep(10);
				}
				
				string htmlReq = Encoding.ASCII.GetString(recvBytes, 0, bytes);

#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("HTTP Request: " + htmlReq.Trim());
#endif

                // Ensure that GET method is used.
                string[] strArray = htmlReq.Trim().Split(' ');
                string method = strArray[0].Trim().ToUpper();
                if (method != "GET")
                {
#if DEBUG
                    if (Log.IsWarnEnabled)
                    {
                        if (method.Trim().Length == 0)
                            Log.Warn("Request method not found.");
                        else
                            Log.Warn("Not GET method. Method = " + method);
                    }
#endif
                    
                    SendForbiddenResponse();
                    goto EndResponse;
                }

                // Put the HTTP headers into a hashtable.
                string headers = htmlReq.Replace("\n\r", "\n");
                string[] lines = headers.Split('\n');
                Dictionary<string, string> table = new Dictionary<string, string>();
                int i = 0;
                foreach (string s in lines)
                {
					// Ignore first line and empty lines.
                    if (i++ == 0 || s.Length == 0) 
                        continue;
                    try
                    {
#if DEBUG
                        if (Log.IsDebugEnabled)
                            Log.Debug("Line = \"" + s + "\"");
#endif
						
						int pos = s.IndexOf(':');
						if (pos >= 0) {
							string name = s.Substring(0, pos).Trim();
                            string val = s.Substring(pos + 1).Trim();
	                    	table.Add(name, val);
						} else {
#if DEBUG
	                        if (Log.IsWarnEnabled)
	                            Log.Warn("Unexpected header string \"" + s + "\"");
#endif
						}
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        if (Log.IsErrorEnabled)
                            Log.Error("Exception", ex);
#endif
                    }
                }
				
                // Check user-agent.
				// It's ok if there isn't one (Playdar doesn't provide one, unusually).
                if (table.ContainsKey("user-agent"))
                {
					// Get the name of the agent, without version and other information.
                    string userAgent = table["user-agent"];
					String agent = userAgent.IndexOf('/') < 0 ? userAgent : userAgent.Substring(0, userAgent.IndexOf('/'));

#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug("Agent = \"" + agent + "\".");
#endif
					
					// TODO: Allow certain user-agent, but not others.
                    /*
					if (agent != "Something") {
	                    SendForbiddenResponse();
	                    goto EndResponse;
					}
                    */
                }

                // Get request URI.
                if (strArray.Length == 1)
                {
#if DEBUG
                    if (Log.IsWarnEnabled)
                        Log.Warn("Request URI not found.");
#endif

                    SendForbiddenResponse();
                    goto EndResponse;
                }

                string uri = strArray[1].Trim();
#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("URI = " + uri);
#endif

                if (uri.Length != BasePath.Length + 36)
                {
#if DEBUG
                    if (Log.IsErrorEnabled)
                        Log.Error("URI length isn't correct. URI = \"" + uri + '"');
#endif

                    SendForbiddenResponse();
                    goto EndResponse;
                }

                // Get stream GUID from request.
                string path = uri.Substring(0, BasePath.Length);
#if DEBUG
                if (Log.IsDebugEnabled) Log.Debug("path = " + path);
#endif
                if (path != BasePath)
                {
#if DEBUG
                    if (Log.IsErrorEnabled)
                        Log.Error("Path doesn't match. Path = \"" + path + '"');
#endif

                    SendForbiddenResponse();
                    goto EndResponse;
                }

                string sid = uri.Substring(BasePath.Length, 36);
#if DEBUG
                if (Log.IsDebugEnabled) Log.Debug("sid = " + sid);
#endif
                string link = _spotifyLinkLookup.FindLink(sid);
                if (link == null)
                {
#if DEBUG
                    if (Log.IsErrorEnabled)
                        Log.Error("Requested stream not found. SID = " + sid);
#endif

                    SendForbiddenResponse();
                    goto EndResponse;
                }

#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("Spotify Link = " + link);
#endif

                // Get track object from Spotify.
                // Note: There's a timing issue which is resolved here with a retry delay.
                Track track = null;
                int retryCount = CreateTrackFromLinkRetryLimit;
                bool success = false;
                while (!success && retryCount > 0)
                {
#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug("Going to try and create track instance from link. retryCount = " + retryCount);
#endif

                    Link spotifyLink = Link.Create(link);
                    track = Track.CreateFromLink(spotifyLink);
                    if (track.Duration == 0)
                    {
#if DEBUG
                        if (Log.IsErrorEnabled)
                            Log.Error("Duration is 0");
#endif
                        retryCount--;
                        Thread.Sleep(CreateTrackFromLinkRetryDelay);
                    }
                    else
                    {
                        success = true;
                    }
                }

                if (!success)
                {
                    SendForbiddenResponse();
                    goto EndResponse;
                }

                // Work out the content length.
#if DEBUG
                if (Log.IsDebugEnabled) Log.Debug("Duration: " + track.Duration);
#endif
                int expectedSize = Program.GetExpectedBytesEncoded(track.Duration);
#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("Calculated size: " + expectedSize + " (" + Program.GetHumanByteString(expectedSize) + ")");
#endif

                // Send the response header.
                StringBuilder headerBuild = new StringBuilder();
                headerBuild.Append("HTTP/1.1 200 OK\r\n");
                headerBuild.Append("Date: ").Append(DateTime.Now.ToUniversalTime().ToString("r")).Append("\r\n");
                headerBuild.Append("Server: SpotifyResolver\r\n");
                headerBuild.Append("Content-Type: audio/mpeg\r\n");
                headerBuild.Append("Content-Length: ").Append(expectedSize).Append("\r\n");
                headerBuild.Append("Keep-Alive: timeout=1, max=1\r\n");
                headerBuild.Append("Connection: Keep-Alive\r\n");
                headerBuild.Append("\r\n");
                byte[] headerBytes = Encoding.ASCII.GetBytes(headerBuild.ToString());
                SendToSocket(_clientSocket, headerBytes, 0, headerBytes.Length, SocketSendTimeout);

                // Start streaming the track from Spotify.
#if DEBUG
                if (Log.IsDebugEnabled) Log.Debug("Streaming track from Spotify.");
#endif
                _spotify.StreamTrackAsync(track);

                // Wait for bytes to appear in the queue.
#if DEBUG
                if (Log.IsDebugEnabled) Log.Debug("Looking for bytes in the queue.");
#endif
                while (SpotifyQueueEmpty())
                    Thread.Sleep(30);

                // Stream the bytes from Spotify via HTTP.
#if DEBUG
                if (Log.IsDebugEnabled) Log.Debug("Streaming audio via HTTP.");
#endif
                int byteCount = 0;
                while (_runClient)
                {
                    //TODO: Ensure that the client hasn't disconnected else cancel stream from Spotify.
                    //TODO: How do we detect if a client has disconnected? This doesn't seem to work.
                    if (!_clientSocket.Connected)
                    {
#if DEBUG
                        if (Log.IsInfoEnabled)
                            Log.Info("Client disconnected. Cancelling stream from Spotify.");
#endif

                        _spotify.CancelPlayback();
                        SendForbiddenResponse();
                        goto EndResponse;
                    }

                    // Send the next packet of data.
                    IAudioData data = _spotify.LameEncoder.OutputQueue.Dequeue();
                    if (data is NullAudioData) break;
                    SendToSocket(_clientSocket, ((AudioData) data).Data, 0, ((AudioData) data).Length, SocketSendTimeout);
                    byteCount += ((AudioData) data).Length;

                    // If necessary wait for more items in the queue.
                    while (_runClient && SpotifyEncoderAvailable() && SpotifyQueueEmpty())
                        Thread.Sleep(1);

                    // Quit if the encoder became unavailable.
                    if (!SpotifyEncoderAvailable())
                        break;
                }

#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("Total bytes: " + byteCount + " (" + Program.GetHumanByteString(byteCount) + ")");
#endif

                EndResponse:
                Thread.Sleep(1); // Goto label requires something.
#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug("Stream ending.");
#endif
            }
            catch (ThreadAbortException)
            {
#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("Thread being aborted.");
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Log.IsErrorEnabled)
                    Log.Error("Exception", ex);
#endif
            }
            finally
            {
#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("Request thread ending.");
#endif

                if (_clientSocket != null && _clientSocket.Connected)
                {
#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug("Closing socket.");
#endif

                    _clientSocket.Shutdown(SocketShutdown.Both);
                    _clientSocket.Close();
                    _clientSocket = null;
                }

                _activeRequest = false;
            }
        }

        void SendForbiddenResponse()
        {
            StringBuilder headerBuild = new StringBuilder();
            headerBuild.Append("HTTP/1.1 403 Forbidden\r\n");
            headerBuild.Append("\r\n");
            byte[] headerBytes = Encoding.ASCII.GetBytes(headerBuild.ToString());
            SendToSocket(_clientSocket, headerBytes, 0, headerBytes.Length, SocketSendTimeout);
        }

        static void SendToSocket(Socket socket, byte[] buffer, int offset, int size, int timeout)
        {
            int startTickCount = Environment.TickCount;
            int sent = 0; // The number of bytes already sent.
            do
            {
                if (Environment.TickCount > startTickCount + timeout)
                {
                    throw new Exception("SendToSocket Timeout!");
                }
                try
                {
                    sent += socket.Send(buffer, offset + sent, size - sent, SocketFlags.None);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.WouldBlock ||
                        ex.SocketErrorCode == SocketError.IOPending ||
                        ex.SocketErrorCode == SocketError.NoBufferSpaceAvailable)
                    {
                        // Socket buffer is probably full, wait and try again.

#if DEBUG
                        if (Log.IsErrorEnabled)
                            Log.Error("SocketException. Delaying and will try again.", ex);
#endif

                        Thread.Sleep(30);
                    }
                    else
                    {
                        throw;
                    }
                }
            } while (sent < size);
        }

        bool SpotifyEncoderAvailable()
        {
            return _spotify != null &&
                   _spotify.LameEncoder != null;
        }

        bool SpotifyQueueEmpty()
        {
            return _spotify.LameEncoder.OutputQueue.Count == 0;
        }
    }
}
