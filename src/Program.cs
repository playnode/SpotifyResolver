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
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stever.PlaySpot.json;
using Spotify;
using System.Reflection;
using System.Diagnostics;

#if DEBUG
using log4net;
#endif

namespace Stever.PlaySpot
{
    class Program
    {
#if DEBUG
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().ReflectedType);
#endif

        internal static string ApplicationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        const int MaxResults = 40;
        const string SpotifyResolverInfo = "{\"_msgtype\":\"settings\",\"name\":\"Spotify Resolver\",\"weight\":90,\"targettime\":15000,\"localonly\":true}";

#if DEBUG
        public static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (Log.IsErrorEnabled)
                Log.Error("Unhandled Exception!", (Exception) e.ExceptionObject);
        }
#endif

        static void Main()
        {
            try
            {
#if DEBUG
                AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
#endif

				/*
                // Expire by a given date.
                DateTime time = DateTime.Now;
                DateTime expires = new DateTime(2011, 6, 1);
                if (time > expires)
                {
#if DEBUG
                    if (Log.IsErrorEnabled)
                        Log.Error("Expired!");
#endif
                    return;
                }
                */

                // Ensure only one instance of application runs.
                bool notRunning;
                using (new Mutex(true, "SpotifyResolver", out notRunning))
                {
                    if (!notRunning)
                    {
#if DEBUG
                        if (Log.IsInfoEnabled)
                            Log.Info("Already running. Only one instance allowed.");
#endif
                    }
                    else
                    {
#if DEBUG
                        if (Log.IsInfoEnabled) Log.Info("Started.");
#endif

                        // Get the configuration from the Playdar etc folder.
                        string etc = Environment.GetEnvironmentVariable("PLAYDAR_ETC");
#if DEBUG
                        if (Log.IsDebugEnabled)
                            Log.Debug("PLAYDAR_ETC=\"" + etc + "\"");
#endif
                        if (string.IsNullOrEmpty(etc))
                            etc = "etc";

                        string configFilename = etc + "/spotify.conf";

                        if (!File.Exists(configFilename))
                        {
#if DEBUG
                            if (Log.IsErrorEnabled)
                                Log.Error("Spotify configuration file not found!");
#endif
                            return;
                        }

                        SimplePropertiesFile config = new SimplePropertiesFile(configFilename);
                        string username;
                        string password;
                        try
                        {
                            username = config.Sections["spotify"]["username"];
                            password = config.Sections["spotify"]["password"];
                        }
                        catch (Exception)
                        {
#if DEBUG
                            if (Log.IsErrorEnabled)
                                Log.Error("Couldn't get username or password from config file!");
#endif
                            return;
                        }

                        // Login to Spotify.
                        Spotify spotify = new Spotify();
                        bool loginOk = spotify.Login(username, password);
                        if (loginOk)
                        {

                            // Create a lookup table for Spotify links and UUIDs.
                            SpotifyLinkLookup spotifyLinkLookup = new SpotifyLinkLookup();

                            // Create an HTTP server for streaming mp3.
                            HttpStreamingServer streamServer = new HttpStreamingServer(spotify, spotifyLinkLookup);
                            streamServer.Start();

                            // Return resolver info.
                            SendJsonOutput(SpotifyResolverInfo);

                            // Read JSON queries.
                            Stream input = Console.OpenStandardInput();
                            do
                            {
                                string json = JsonMsgUtil.ExtractJsonInput(input);
                                if (String.IsNullOrEmpty(json) || json == "{}")
                                {
#if DEBUG
                                    if (Log.IsInfoEnabled)
                                        Log.Info("Empty message received. Leaving message receive loop.");
#endif
                                    break;
                                }

#if DEBUG
                                if (Log.IsDebugEnabled)
                                    Log.Debug(json);
#endif

                                JObject o = JObject.Parse(json);
                                string msgtype = (string)o["_msgtype"];
								if (String.IsNullOrEmpty(msgtype))
								{
#if DEBUG
                                    if (Log.IsWarnEnabled) 
                                    	Log.Warn("No message type provided. Leaving receive loop.");
#endif
                                    break;
								}
								else if (msgtype != "rq")
                                {
#if DEBUG
                                    if (Log.IsWarnEnabled)
										Log.Warn("Unexpected JSON _msgtype! " + msgtype);
#endif
                                    break;
                                }

#if DEBUG
                                if (Log.IsDebugEnabled)
                                    Log.Debug("Performing search...");
#endif

                                // Perform search.
                                string artist = (string)o["artist"];
                                string track = (string)o["track"];
                                string album = (string)o["album"];
                                List<Track> searchResults = spotify.Search(artist, track, album);

#if DEBUG
                                if (Log.IsDebugEnabled)
                                    Log.Debug("searchResults.Count = " + searchResults.Count);
#endif

                                // Build JSON results.
                                StringBuilder jsonBuild = new StringBuilder();
                                JsonTextWriter jsonWriter = new JsonTextWriter(new StringWriter(jsonBuild));
                                jsonWriter.WriteStartObject();
                                WriteProperty(jsonWriter, "_msgtype", "results");
                                WriteProperty(jsonWriter, "qid", (string) o["qid"]);
                                jsonWriter.WritePropertyName("results");
                                jsonWriter.WriteStartArray();
                                int i = 0;
                                foreach (Track r in searchResults)
                                {
                                    // Limit number of possible results.
                                    if (++i > MaxResults)
                                    {
#if DEBUG
                                        if (Log.IsWarnEnabled)
                                            Log.Warn("Only returning first " + MaxResults +
                                                " of " + searchResults.Count + " results.");
#endif

                                        break;
                                    }

                                    // Obtain a GUID for the stream request.
                                    string sid = spotifyLinkLookup.NewGuid(r.LinkString);

                                    // Build a URL for the track to be delivered.
                                    StringBuilder urlBuild = new StringBuilder();
                                    urlBuild.Append(streamServer.BaseUrl).Append(sid);
                                    string url = urlBuild.ToString();
#if DEBUG
                                    if (Log.IsDebugEnabled)
                                        Log.Debug("Stream URL = " + url);
#endif

                                    // Work out the content length.
#if DEBUG
                                    if (Log.IsDebugEnabled) Log.Debug("Duration: " + r.Duration);
#endif
                                    int seconds = r.Duration / 1000;

                                    // Add track result to JSON response.
                                    jsonWriter.WriteStartObject();
                                    WriteProperty(jsonWriter, "artist", r.Artists[0].Name);
                                    WriteProperty(jsonWriter, "track", r.Name);
                                    WriteProperty(jsonWriter, "album", r.Album.Name);
                                    WriteProperty(jsonWriter, "mimetype", "audio/mpeg");
                                    WriteProperty(jsonWriter, "source", "Spotify");
                                    WriteProperty(jsonWriter, "url", url);
                                    WriteProperty(jsonWriter, "duration", seconds);
                                    WriteProperty(jsonWriter, "score", CalculateScore(r, artist, track, album));
                                    WriteProperty(jsonWriter, "bitrate", LameProgram.BitRate);
                                    jsonWriter.WriteEndObject();
                                }
                                jsonWriter.WriteEnd();
                                jsonWriter.WriteEndObject();

                                // Send the JSON response to search query.
                                SendJsonOutput(jsonBuild.ToString());

                            } while (true);

                            // Tidy up after stream server.
                            streamServer.Shutdown();

                            // Logout of Spotify.
                            spotify.Logout();

                        }
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Log.IsErrorEnabled)
                    Log.Error("Exception", ex);
#endif
            }

            /* NOTE: This code was throwing a class-cast exception, due to being ProcessThread.
             * NOTE: ProcessThread does not have an Abort() method.
            try
            {
                foreach (ProcessThread t in Process.GetCurrentProcess().Threads)
                {
                    try
                    {
#if DEBUG
                        if (Log.IsWarnEnabled) Log.Warn("Aborting thread.");
#endif
                        t.Abort();
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        if (Log.IsErrorEnabled) Log.Error("Exception", ex);
#endif
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Log.IsErrorEnabled)
                    Log.Error("Exception", ex);
#endif
            }
            */

#if DEBUG
            if (Log.IsInfoEnabled) Log.Info("Finished.");
#endif

#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Calling Kill on current process.");
#endif
            Process.GetCurrentProcess().Kill();
        }

        #region Scoring

        static double CalculateScore(Track result, string artist, string track, string album)
        {
            double artistScore = SingleScore(artist.ToLower(), result.Artists[0].Name.ToLower());
            double trackScore = SingleScore(track.ToLower(), result.Name.ToLower());
            double albumScore = string.IsNullOrEmpty(album) ? 1.0 : SingleScore(album.ToLower(), result.Album.Name.ToLower());
            return (artistScore + trackScore + albumScore) / 3.0;
        }

        static double SingleScore(string a, string b)
        {
            double result;
            if (a == b) result = 1.0;
            else if (Soundex.ToSoundexCode(a) == Soundex.ToSoundexCode(b)) result = 1.0;
            else result = PercentageMatch(a, b);
            return result;
        }

        static double PercentageMatch(string a, string b)
        {
            double result;
            if (a.IndexOf(b) > 0) result = (b.Length * 1.0) / (a.Length * 1.0);
            else if (b.IndexOf(a) > 0) result = (a.Length * 1.0) / (b.Length * 1.0);
            else result = 0;
            return result;
        }

        #endregion

        #region Byte count conversions

        internal static string GetHumanByteString(double byteCount)
        {
            string result;
            if (byteCount >= 1073741824.0)
            {
                result = String.Format("{0:##.##}", byteCount / 1073741824.0) + " GB";
            }
            else if (byteCount >= 1048576.0)
            {
                result = String.Format("{0:##.##}", byteCount / 1048576.0) + " MB";
            }
            else if (byteCount >= 1024.0)
            {
                result = String.Format("{0:##.##}", byteCount / 1024.0) + " KB";
            }
            else if (byteCount > 0 && byteCount < 1024.0)
            {
                StringBuilder strBuild = new StringBuilder();
                strBuild.Append(byteCount.ToString()).Append(" Bytes");
                result = strBuild.ToString();
            }
            else
            {
                result = "0 Bytes";
            }
            return result;
        }

        internal static int GetExpectedBytesEncoded(int milliseconds)
        {
            return (((milliseconds / 1000) * LameProgram.BitRate) / 8) * 1024;
        }

        #endregion

        #region JSON support

        static void SendJsonOutput(string json)
        {
            Stream output = Console.OpenStandardOutput();
            byte[] bytes = JsonMsgUtil.WrapJsonOutput(json);
            output.Write(bytes, 0, bytes.Length);
            output.Flush();
        }

        static void WriteProperty(JsonWriter writer, string name, object value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        #endregion

        #region Debug support

        /// <summary>
        /// This method returns the string representation of a character.
        /// </summary>
        /// <param name="c">Character value to represent.</param>
        /// <returns>Label used to represent all characters in text logging.</returns>
        internal static string ToNameString(int c)
        {
            switch (c)
            {
                case 0x00: return "0x" + c.ToString("X2").ToUpper() + " <NUL>";
                case 0x01: return "0x" + c.ToString("X2").ToUpper() + " <SOH>";
                case 0x02: return "0x" + c.ToString("X2").ToUpper() + " <STX>";
                case 0x03: return "0x" + c.ToString("X2").ToUpper() + " <ETX>";
                case 0x04: return "0x" + c.ToString("X2").ToUpper() + " <EOT>";
                case 0x05: return "0x" + c.ToString("X2").ToUpper() + " <ENQ>";
                case 0x06: return "0x" + c.ToString("X2").ToUpper() + " <ACK>";
                case 0x07: return "0x" + c.ToString("X2").ToUpper() + " <BEL>";
                case 0x08: return "0x" + c.ToString("X2").ToUpper() + " <BS>";
                case 0x09: return "0x" + c.ToString("X2").ToUpper() + " <HT>";
                case 0x0A: return "0x" + c.ToString("X2").ToUpper() + " <LF>";
                case 0x0B: return "0x" + c.ToString("X2").ToUpper() + " <VT>";
                case 0x0C: return "0x" + c.ToString("X2").ToUpper() + " <FF>";
                case 0x0D: return "0x" + c.ToString("X2").ToUpper() + " <CR>";
                case 0x0E: return "0x" + c.ToString("X2").ToUpper() + " <SO>";
                case 0x0F: return "0x" + c.ToString("X2").ToUpper() + " <SI>";
                case 0x10: return "0x" + c.ToString("X2").ToUpper() + " <DLE>";
                case 0x11: return "0x" + c.ToString("X2").ToUpper() + " <DC1>";
                case 0x12: return "0x" + c.ToString("X2").ToUpper() + " <DC2>";
                case 0x13: return "0x" + c.ToString("X2").ToUpper() + " <DC3>";
                case 0x14: return "0x" + c.ToString("X2").ToUpper() + " <DC4>";
                case 0x15: return "0x" + c.ToString("X2").ToUpper() + " <NAK>";
                case 0x16: return "0x" + c.ToString("X2").ToUpper() + " <SYN>";
                case 0x17: return "0x" + c.ToString("X2").ToUpper() + " <ETB>";
                case 0x18: return "0x" + c.ToString("X2").ToUpper() + " <CAN>";
                case 0x19: return "0x" + c.ToString("X2").ToUpper() + " <EM>";
                case 0x1A: return "0x" + c.ToString("X2").ToUpper() + " <SUB>";
                case 0x1B: return "0x" + c.ToString("X2").ToUpper() + " <ESC>";
                case 0x1C: return "0x" + c.ToString("X2").ToUpper() + " <FS>";
                case 0x1D: return "0x" + c.ToString("X2").ToUpper() + " <GS>";
                case 0x1E: return "0x" + c.ToString("X2").ToUpper() + " <RS>";
                case 0x1F: return "0x" + c.ToString("X2").ToUpper() + " <US>";
                case 0x20: return "0x" + c.ToString("X2").ToUpper() + " <SP>";
                case 0x21:
                case 0x22:
                case 0x23:
                case 0x24:
                case 0x25:
                case 0x26:
                case 0x27:
                case 0x28:
                case 0x29:
                case 0x2A:
                case 0x2B:
                case 0x2C:
                case 0x2D:
                case 0x2E:
                case 0x2F:
                case 0x30:
                case 0x31:
                case 0x32:
                case 0x33:
                case 0x34:
                case 0x35:
                case 0x36:
                case 0x37:
                case 0x38:
                case 0x39:
                case 0x3A:
                case 0x3B:
                case 0x3C:
                case 0x3D:
                case 0x3E:
                case 0x3F:
                case 0x40:
                case 0x41:
                case 0x42:
                case 0x43:
                case 0x44:
                case 0x45:
                case 0x46:
                case 0x47:
                case 0x48:
                case 0x49:
                case 0x4A:
                case 0x4B:
                case 0x4C:
                case 0x4D:
                case 0x4E:
                case 0x4F:
                case 0x50:
                case 0x51:
                case 0x52:
                case 0x53:
                case 0x54:
                case 0x55:
                case 0x56:
                case 0x57:
                case 0x58:
                case 0x59:
                case 0x5A:
                case 0x5B:
                case 0x5C:
                case 0x5D:
                case 0x5E:
                case 0x5F:
                case 0x60:
                case 0x61:
                case 0x62:
                case 0x63:
                case 0x64:
                case 0x65:
                case 0x66:
                case 0x67:
                case 0x68:
                case 0x69:
                case 0x6A:
                case 0x6B:
                case 0x6C:
                case 0x6D:
                case 0x6E:
                case 0x6F:
                case 0x70:
                case 0x71:
                case 0x72:
                case 0x73:
                case 0x74:
                case 0x75:
                case 0x76:
                case 0x77:
                case 0x78:
                case 0x79:
                case 0x7A:
                case 0x7B:
                case 0x7C:
                case 0x7D:
                case 0x7E:
                    {
                        StringBuilder result = new StringBuilder();
                        result.Append((char) c);
                        return result.ToString();
                    }
                case 0x7F: return "0x" + c.ToString("X2").ToUpper() + " <DEL>";
                case -1: return "EOF";
                default:
                    {
                        StringBuilder result = new StringBuilder();
                        result.Append("0x");
                        result.Append(c.ToString("X2").ToUpper());
                        return result.ToString();
                    }
            }
        }

        #endregion
    }
}
