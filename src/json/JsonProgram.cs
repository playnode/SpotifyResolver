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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

#if DEBUG
using System.Reflection;
using log4net;
#endif

namespace Stever.PlaySpot.json
{
    /// <summary>
    /// Supports interactions with a program sending and receiving JSON messages.
    /// </summary>
    internal class JsonProgram
    {
#if DEBUG
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().ReflectedType);
#endif
        const int DefaultTimeout = 30000; // 30 seconds

        readonly Process _process;
        readonly Thread _stdOutThread;
        readonly Thread _stdErrThread;
        readonly AutoResetEvent _completed = new AutoResetEvent(false);
        readonly Queue<string> _msgQueue = new Queue<string>();

        bool _running = true;
        bool _runningAsynchronously;

        internal JsonProgram(string cmdPath)
        {
            // Split path and cmd into separate variables.
            int cmdPos = cmdPath.LastIndexOf('\\') + 1;
            int cmdLen = cmdPath.Length - cmdPos;
            string cmd = cmdPath.Substring(cmdPos, cmdLen);
            string path = cmdPath.Substring(0, cmdPath.Length - cmdLen);

            // Prepare to start the process.
            _process = new Process();
            _process.StartInfo.WorkingDirectory = path;
            _process.StartInfo.FileName = cmd;
            _process.StartInfo.Arguments = "";
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.CreateNoWindow = true;
            _process.StartInfo.RedirectStandardInput = true;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.RedirectStandardError = true;

            // Start the process.
            _process.Start();
            _process.StandardInput.AutoFlush = true;

            // Start reading from standard output.
            if (_process.StartInfo.RedirectStandardOutput)
            {
                _stdOutThread = new Thread(ReadStandardOutput);
                _stdOutThread.IsBackground = true;
                _stdOutThread.Start();
            }

            // Start reading from standard error.
            if (!_process.StartInfo.RedirectStandardError)
            {
                _stdErrThread = new Thread(ReadStandardError);
                _stdErrThread.IsBackground = true;
                _stdErrThread.Start();
            }

#if DEBUG
            if (Log.IsDebugEnabled)
                Log.Debug("jsonprogram started.");
#endif
        }

        /// <summary>
        /// Send a JSON message to the command. First 2 bytes is size of message.
        /// </summary>
        /// <remarks>
        /// This is used by both asynchronous and no result commands.
        /// </remarks>
        /// <param name="json">The JSON string to send.</param>
        internal void SendJsonCommand(IJson json)
        {
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Send: " + json.ToJson());
#endif
            Stream output = _process.StandardInput.BaseStream;
            byte[] msg = JsonMsgUtil.WrapJsonOutput(json);
            output.Write(msg, 0, msg.Length);
            output.Flush();
        }

        #region Synchronous message processing.

        /// <summary>
        /// This method provides a way to send a message via JSON and receive
        /// a JSON response with the result.
        /// </summary>
        /// <remarks>
        /// Uses default timeout setting.
        /// </remarks>
        /// <param name="json">The JSON command.</param>
        /// <returns>The JSON command response.</returns>
        internal string ExecuteJson(IJson json)
        {
            return ExecuteJson(json, DefaultTimeout);
        }

        /// <summary>
        /// This method provides a way to send a message via JSON and receive
        /// a JSON response with the result.
        /// </summary>
        /// <param name="json">The JSON command.</param>
        /// <param name="timeout">Timeout in milliseconds. If timeout is 0 then no timeout is used.</param>
        /// <returns>The JSON command response.</returns>
        internal string ExecuteJson(IJson json, int timeout)
        {
            if (_runningAsynchronously)
                throw new Exception("Can't run synchronously when already running asynchronously!");

            if (_msgQueue.Count > 0)
            {
#if DEBUG
                if (Log.IsErrorEnabled)
                    Log.Error("Message queue not empty! Discarding unread queue items.");
#endif

                while (_msgQueue.Count > 0)
                {
                    string item = _msgQueue.Dequeue();
#if DEBUG
                    if (Log.IsWarnEnabled)
                        Log.Warn("Discarding following message:\n" + item);
#endif
                }
            }

            // Going to wait on result.
            _completed.Reset();

            // Send the JSON command.
            SendJsonCommand(json);

            // Wait on result, with optional timeout.
            if (timeout == 0) _completed.WaitOne();
            else _completed.WaitOne(timeout);

            // Return the single message response.
            return _msgQueue.Dequeue();
        }

        #endregion

        #region Asynchronous message processing.

        /// <summary>
        /// Send a JSON message to the command. First 2 bytes is size of message.
        /// </summary>
        /// <param name="json">The JSON string to send.</param>
        internal void SendJsonForAsynchronousResults(IJson json)
        {
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Send: " + json);
#endif
            _runningAsynchronously = true;
            Stream output = _process.StandardInput.BaseStream;
            byte[] msg = JsonMsgUtil.WrapJsonOutput(json);
            output.Write(msg, 0, msg.Length);
            output.Flush();
        }

        internal string GetNextAsynchronousResult()
        {
            if (!_runningAsynchronously)
            {
#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("GetNextAsynchronousResult called when !_runningAsynchronously");
#endif

                return null;
            }

            const int waitTime = 250; // 1/4 second.
            const int maxTries = 120; // 120 * 250 = timeout after 30 seconds.
            int tries = 0;
            while (_msgQueue.Count == 0)
            {
                if (tries++ > maxTries)
                {
#if DEBUG
                    if (Log.IsErrorEnabled)
                        Log.Error("Max tries reached. Returning null on GetNextAsynchronousResult!");
#endif
                    break;
                }

#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("Queue empty. Waiting for next message in queue...");
#endif

                Thread.Sleep(waitTime);
            }

            return _msgQueue.Count == 0 ? null : _msgQueue.Dequeue();
        }

        internal void StopAsynchronousRead()
        {
            if (!_runningAsynchronously)
            {
#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("StopAsynchronousRead called when !_runningAsynchronously");
#endif

                return;
            }

            _runningAsynchronously = false;
        }

        #endregion

        internal void Close()
        {
            // Standard output will stop expecting messages when _running is set to false.
            _running = false;

            // Stop the threads collecting the outputs.
            StopStandardOutputThread();
            StopStandardErrorThread();

            // Kill the JsonProgram process if not exiting.
            if (_process.HasExited) return;
#if DEBUG
            if (Log.IsWarnEnabled) Log.Warn("JsonProgram closing but process has not yet exited. Giving it a little more time.");
#endif
            for (int i = 0; i < 5; i++)
            {
                if (!_process.HasExited) Thread.Sleep(1000);
                else break;
            }
            if (_process.HasExited) return;
#if DEBUG
            if (Log.IsWarnEnabled) Log.Warn("Killing the JsonProgram process now!");
#endif
            _process.Kill();
        }

        #region Standard output and error streams.

        void ReadStandardOutput()
        {
#if DEBUG
            if (Log.IsDebugEnabled)
                Log.Debug("Started reading from standard output.");
#endif
            try
            {
                // Read JSON messages.
                int retries = 0;
                const int maxRetries = 12; // 1/4 sec * 4 * 3 = 3 seconds.
                while (_running)
                {
                    Stream input = _process.StandardOutput.BaseStream;
                    string response = JsonMsgUtil.ExtractJsonInput(input);
                    if (response == null)
                    {
                        if (retries++ > maxRetries)
                        {
#if DEBUG
                            if (Log.IsErrorEnabled)
                                Log.Error("Null received from input stream. Maximum number of retries reached.");
#endif

                            _running = false;
                            Close();
                        }

#if DEBUG
                        if (Log.IsWarnEnabled)
                            Log.Warn("Null received from input stream. Waiting 250ms.");
#endif

                        Thread.Sleep(250);
                    }
                    else
                    {
                        retries = 0; // reset
                        _msgQueue.Enqueue(response);
                    }

                    _completed.Set();
                }
            }
            catch (ThreadAbortException)
            {
#if DEBUG
                if (Log.IsWarnEnabled)
                    Log.Warn("StandardOutput: Thread aborted.");
#endif
            }

#if DEBUG
            if (Log.IsDebugEnabled)
                Log.Debug("Finished reading from standard output.");
#endif
        }

        void ReadStandardError()
        {
            try
            {
                do
                {
                    //TODO: var c = 
                    _process.StandardError.Read();
                    //TODO: Something
                } while (_process != null && !_process.StandardError.EndOfStream);
            }
            catch (ThreadAbortException)
            {
#if DEBUG
                if (Log.IsWarnEnabled) Log.Warn("StandardError: Thread aborted.");
#endif
            }
        }

        void StopStandardOutputThread()
        {
            // Try to abort the standard output reader.
            if (_stdOutThread == null || !_stdOutThread.IsAlive) return;
            for (int i = 0; i < 50; i++)
            {
                // ReSharper disable ConditionIsAlwaysTrueOrFalse
                if (_stdOutThread == null || !_stdOutThread.IsAlive) return;
                // ReSharper restore ConditionIsAlwaysTrueOrFalse
                Thread.Sleep(100);
            }
#if DEBUG
            if (Log.IsWarnEnabled) Log.Warn("Aborting reader thread for standard output.");
#endif
            try
            {
                _stdOutThread.Interrupt();
                _stdOutThread.Abort();
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Log.IsErrorEnabled) Log.Error("Exception", ex);
#endif
            }
        }

        void StopStandardErrorThread()
        {
            // Try to abort the standard error reader.
            if (_stdErrThread == null || !_stdErrThread.IsAlive) return;
            for (int i = 0; i < 50; i++)
            {
                if (_stdOutThread == null || !_stdOutThread.IsAlive) return;
                Thread.Sleep(100);
            }
#if DEBUG
            if (Log.IsWarnEnabled) Log.Warn("Aborting reader thread for error output.");
#endif
            try
            {
                _stdErrThread.Interrupt();
                _stdErrThread.Abort();
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Log.IsErrorEnabled) Log.Error("Exception", ex);
#endif
            }
        }

        #endregion
    }
}
