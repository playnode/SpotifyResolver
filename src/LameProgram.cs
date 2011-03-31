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
using System.Text;
using System.Threading;
using Spotify;

#if DEBUG
using System.Reflection;
using log4net;
#endif

namespace Stever.PlaySpot
{
    class LameProgram
    {
#if DEBUG
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().ReflectedType);
#endif

        internal const int StreamBufferSize = 1024 * 64; // 48 KB
        internal const int BitRate = 128;

        Queue<IAudioData> _outputQueue;

        internal Queue<IAudioData> OutputQueue
        {
            get { return _outputQueue; }
            private set { _outputQueue = value; }
        }

        readonly Process _process;
        readonly Thread _stdOutThread;
        readonly Thread _stdErrThread;
        readonly int _expectedSize = -1;

        bool _running = true;

        internal LameProgram(Track spotifyTrack)
        {
            // We need the expected size to pad the result to match the inexact time
            // that is specified by Spotify for the track.
            _expectedSize = Program.GetExpectedBytesEncoded(spotifyTrack.Duration);
#if DEBUG
            if (Log.IsDebugEnabled)
                Log.Debug("Expected size = " + _expectedSize);
#endif

            // Build up the arguments string.
            StringBuilder args = new StringBuilder();
            args.Append("-r "); // input is raw pcm
            args.Append("--cbr -b ").Append(BitRate).Append(' ');
            args.Append("- -"); // stdin & stdout

#if DEBUG
            if (Log.IsDebugEnabled)
                Log.Debug("Lame args: " + args);
#endif

            // Prepare the output data queue.
            OutputQueue = new Queue<IAudioData>();

            // "lame.exe" or "lame"
            string lame = "lame.exe";
            if (!File.Exists(Program.ApplicationPath + @"\lame.exe"))
                lame = "lame";

            // Prepare to start the process.
            _process = new Process();
            _process.StartInfo.WorkingDirectory = Program.ApplicationPath + '\\';
            _process.StartInfo.FileName = lame;
            _process.StartInfo.Arguments = args.ToString();
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
                Log.Debug("LameProgram started.");
#endif
        }

        internal void Write(byte[] bytes)
        {
            Stream output = _process.StandardInput.BaseStream;
            output.Write(bytes, 0, bytes.Length);
            output.Flush();
        }

        internal void Close()
        {
#if DEBUG
            if (Log.IsDebugEnabled)
                Log.Debug("Closing");
#endif

            // Standard output will stop expecting messages when _running is set to false.
            _running = false;

            // Stop the input stream.
            try
            {
                _process.StandardInput.Close();
            }
            catch
            {
#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("Couldn't flush the standard input stream.");
#endif
            }
            try
            {
                _process.StandardInput.Close();
            }
            catch
            {
#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("Couldn't close the standard input stream.");
#endif
            }

            // Stop the threads collecting the outputs.
            StopStandardOutputThread();
            StopStandardErrorThread();

            // Kill the LAME process if not exiting.
            if (_process.HasExited) return;
#if DEBUG
            if (Log.IsWarnEnabled) 
                Log.Warn("LameProgram closing but process has not yet exited. Giving it a little more time.");
#endif
            for (int i = 0; i < 5; i++)
            {
                if (!_process.HasExited) Thread.Sleep(1000);
                else break;
            }
            if (_process.HasExited) return;
#if DEBUG
            if (Log.IsWarnEnabled) 
                Log.Warn("Killing the LAME process now!");
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
                // Write bytes to a buffer to be delivered via HTTP.
                Stream input = _process.StandardOutput.BaseStream;
                byte[] buffer = new byte[StreamBufferSize];
                int byteCount = 0;
                int i = 0;
                while (_running)
                {
                    int c = input.ReadByte();
                    if (c == -1) break;

                    // Print periodic progress.
                    if (++byteCount % (1024 * 128) == 0)
                    {
#if DEBUG
                        if (Log.IsDebugEnabled)
                            Log.Debug("Progress: " + Program.GetHumanByteString(byteCount));
#endif
                    }

                    // Buffering output.
                    buffer[i++] = (byte) c;
                    if (i % StreamBufferSize != 0) continue;
                    AudioData data = new AudioData(buffer, buffer.Length);
                    i = 0; // Restart filling the buffer.
                    OutputQueue.Enqueue(data);
                }

                // Push any extra in buffer onto the queue.
                if (i > 0)
                {
                    AudioData data = new AudioData(buffer, i);
                    OutputQueue.Enqueue(data);
                }

#if DEBUG
                if (Log.IsDebugEnabled)
                    Log.Debug("Total bytes: " + byteCount + " (" + Program.GetHumanByteString(byteCount) + ")");
#endif

                // Send zeros for difference between expected size and the actual byte count.
                if (_expectedSize > byteCount)
                {
#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug("Byte count is less than expected. Padding with zeros.");
#endif

                    // Send zeros for difference between expected size and the actual byte count.
                    int diff = _expectedSize - byteCount;
                    byte[] extraBytes = new byte[diff];
                    for (int j = 0; j < diff; j++) extraBytes[j] = 0;
                    AudioData data = new AudioData(extraBytes, diff);
                    OutputQueue.Enqueue(data);
                }
                else if (_expectedSize < byteCount)
                {
#if DEBUG
                    if (Log.IsErrorEnabled)
                        Log.Error("Byte count is greater than expected size!");
#endif
                }
                else
                {
#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug("Byte count matched expected size.");
#endif
                }

                OutputQueue.Enqueue(NullAudioData.Instance);
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
                StringBuilder lineBuffer = new StringBuilder();
                do
                {
                    int c = _process.StandardError.Read();
#if DEBUG
                    if (Log.IsDebugEnabled)
                        Log.Debug(Program.ToNameString(c));
#endif
                    char ch = (char) c;
                    switch (ch)
                    {
                        case '\r':
                            // Ignore
                            break;

                        case '\n':

                            // Print out error line.
                            lineBuffer.Append(c);
#if DEBUG
                            if (Log.IsErrorEnabled)
                                Log.Error(lineBuffer.ToString());
#endif
                            lineBuffer = new StringBuilder();
                            break;

                        default:
                            lineBuffer.Append(c);
                            break;
                    }

                } while (_process != null && !_process.StandardError.EndOfStream);

                // Print out last line if there's any input in buffer.
#if DEBUG
                if (lineBuffer.Length > 0)
                    if (Log.IsErrorEnabled)
                        Log.Error(lineBuffer.ToString());
#endif
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
            for (int i = 0; i < 50; i++)
            {
                if (_stdOutThread == null || !_stdOutThread.IsAlive)
                    return;

                Thread.Sleep(100);
            }
#if DEBUG
            if (Log.IsWarnEnabled)
                Log.Warn("Aborting reader thread for standard output.");
#endif
            try
            {
                _stdOutThread.Interrupt();
                _stdOutThread.Abort();
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Log.IsErrorEnabled)
                    Log.Error("Exception", ex);
#endif
            }
        }

        void StopStandardErrorThread()
        {
            // Try to abort the standard error reader.
            for (int i = 0; i < 50; i++)
            {
                if (_stdErrThread == null || !_stdErrThread.IsAlive)
                    return;

                Thread.Sleep(100);
            }
#if DEBUG
            if (Log.IsWarnEnabled)
                Log.Warn("Aborting reader thread for error output.");
#endif
            try
            {
                _stdErrThread.Interrupt();
                _stdErrThread.Abort();
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Log.IsErrorEnabled)
                    Log.Error("Exception", ex);
#endif
            }
        }

        #endregion
    }
}
