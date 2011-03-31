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
using Spotify;

#if DEBUG
using System.Reflection;
using log4net;
#endif

namespace Stever.PlaySpot
{
    class Spotify
    {
#if DEBUG
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().ReflectedType);
#endif
        
        Session _session;
        LameProgram _lameEncoder;

        internal Session Session
        {
            get { return _session; }
            private set { _session = value; }
        }

        internal LameProgram LameEncoder
        {
            get { return _lameEncoder; }
            private set { _lameEncoder = value; }
        }

        // NOTE: Bit rate = (sampling rate) x (bit depth) x (number of channels)
        // For a recording with a 44.1 kHz sampling rate, a 16 bit depth, and 2 channels (stereo):
        // 44100 x 16 x 2 = 1411200 bits per second, or 1411.2 kbit/s

        #region Key

        static readonly byte[] Key = new byte[]
			{
                // NOTE: Your API key from Spotify goes here!
			};

        #endregion

        readonly AutoResetEvent _loggedIn = new AutoResetEvent(false);
        readonly AutoResetEvent _loggedOut = new AutoResetEvent(false);
        readonly AutoResetEvent _searchComplete = new AutoResetEvent(false);
        readonly AutoResetEvent _playbackDone = new AutoResetEvent(false);

        bool _loginOk;
        List<Track> _searchResults;

        internal Spotify()
        {
            // Temp data path.
            StringBuilder path = new StringBuilder();
            path.Append(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            path.Append(@"/PlaySpot/libspotify");
            string tempPath = path.ToString();
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Temp path: " + tempPath);
#endif
			// Always start with a fresh temp folder.
			if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
            Directory.CreateDirectory(tempPath);

            // Returning the session instance with the preferred bitrate set.
            Session = Session.CreateInstance(Key, tempPath, tempPath, "libspotify-sharp-cmd");
            Session.PreferredBitrate(sp_bitrate.BITRATE_320k);

            // Set event handlers.
            Session.OnLoginComplete += HandleOnLoginComplete;
            Session.OnLoggedOut += HandleOnLoggedOut;
            Session.OnSearchComplete += HandleOnSearchComplete;
            Session.OnMessageToUser += HandleOnMessageToUser;
            Session.OnLogMessage += HandleOnLogMessage;
            Session.OnConnectionError += HandleOnConnectionError;
            Session.OnMusicDelivery += HandleOnMusicDelivery;
            Session.OnEndOfTrack += HandleOnEndOfTrack;
            Session.OnPlayTokenLost += HandleOnPlayTokenLost;
        }

        #region Login

        internal bool Login(string username, string password)
        {
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Logging in...");
#endif
            Session.LogIn(username, password);
            _loggedIn.WaitOne();
            return _loginOk;
        }

        void HandleOnLoginComplete(Session sender, SessionEventArgs e)
        {
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("HandleOnLoginComplete");
#endif
            try
            {
#if DEBUG
                if (Log.IsDebugEnabled) Log.Debug("Login result: " + e.Status);
#endif
                _loginOk = Session.ConnectionState == sp_connectionstate.LOGGED_IN;
                _loggedIn.Set();
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

        #region Logout

        internal void Logout()
        {
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Logging out..");
#endif
            Session.LogOut();
            _loggedOut.WaitOne(5000, false);
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Logged out");
#endif
        }

        void HandleOnLoggedOut(Session sender, SessionEventArgs e)
        {
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("HandleOnLoggedOut");
#endif
            try
            {
                _playbackDone.Set();
                _loggedOut.Set();
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

        #region Search

        internal List<Track> Search(string artist, string track, string album)
        {
            // Prepare search query.
            StringBuilder queryBuild = new StringBuilder();
            queryBuild.Append(artist);
            queryBuild.Append(' ').Append(track);
            if (!string.IsNullOrEmpty(album)) queryBuild.Append(' ').Append(album);
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Searching for \"" + queryBuild + '"');
#endif
            string query = FilterSearchQuery(queryBuild.ToString());
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Searching for \"" + query + '"');
#endif

            // Do search and wait for results.
            _searchResults = new List<Track>();
            _searchComplete.Reset();
            Session.Search(query, 0, 500, 0, 500, 0, 500, null);
            _searchComplete.WaitOne();

            // If there's no results, try search without album name.
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Trying search without album name.");
#endif
            queryBuild = new StringBuilder();
            queryBuild.Append(artist);
            queryBuild.Append(' ').Append(track);
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Searching for \"" + queryBuild + '"');
#endif
            query = FilterSearchQuery(queryBuild.ToString());
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Searching for \"" + query + '"');
#endif

            // Do search and wait for results.
            _searchResults = new List<Track>();
            _searchComplete.Reset();
            Session.Search(query, 0, 500, 0, 500, 0, 500, null);
            _searchComplete.WaitOne();

            // Return results.
            List<Track> result = _searchResults;
            _searchResults = null;
            return result;
        }

        /// <summary>
        /// Remove newlines and - characters. Compress spaces.
        /// </summary>
        /// <param name="query">Search query string.</param>
        /// <returns>Filtered search query string.</returns>
        static string FilterSearchQuery(string query)
        {
            StringBuilder temp = new StringBuilder();
            char lastChar = ' ';
            foreach (char c in query)
            {
                int ival = (int) c;

                char useChar;
                if (ival >= 65 && ival <= 90) useChar = c; // A-Z
                else if (ival >= 97 && ival <= 122) useChar = c; // a-z
                else if (ival >= 48 && ival <= 57) useChar = c; // 0-9
                else useChar = ' ';

                if (useChar != ' ' || lastChar != ' ')
                    temp.Append(useChar);

                lastChar = useChar;
            }
            return temp.ToString().Trim();
        }

        void HandleOnSearchComplete(Session sender, SearchEventArgs e)
        {
#if DEBUG
            if (Log.IsDebugEnabled)
                Log.Debug("HandleOnSearchComplete");
#endif

            try
            {
#if DEBUG
                if (Log.IsDebugEnabled) Log.Debug("Search returned: " + e.Result.TotalTracks + " tracks.");
#endif
                foreach (Track track in e.Result.Tracks) _searchResults.Add(track);
                _searchComplete.Set();
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

        #region Stream Track

        internal void StreamTrackAsync(Track track)
        {
#if DEBUG
            if (Log.IsDebugEnabled) 
                Log.Debug("StreamTrackAsync");
#endif

            // Prepare to play the track.
#if DEBUG
            if (Log.IsDebugEnabled) 
                Log.Debug("Playing track: " + track.Name);
#endif

            // Configure the LAME MP3 encoder.
            LameEncoder = new LameProgram(track);

            // Reset the variable used to block until playback done.
            _playbackDone.Reset();

            // Load the track.
            sp_error loadResult = Session.PlayerLoad(track);
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Load: " + loadResult);
#endif

            // Play the track.
            sp_error playResult = Session.PlayerPlay(true);
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Play: " + playResult);
#endif
        }

        void HandleOnMusicDelivery(Session sender, MusicDeliveryEventArgs e)
        {
            try
            {
                // Write to LAME.
                LameEncoder.Write(e.Samples);

                // Don't forget to set how many frames we consumed
                e.ConsumedFrames = e.Frames;
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Log.IsErrorEnabled)
                    Log.Error("Exception", ex);
#endif
            }
        }

        void HandleOnEndOfTrack(Session sender, SessionEventArgs e)
        {
#if DEBUG
            if (Log.IsDebugEnabled)
                Log.Debug("End of music delivery.");
#endif
            try
            {
                LameEncoder.Close();
                LameEncoder = null;
                _playbackDone.Set();
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Log.IsErrorEnabled)
                    Log.Error("Exception", ex);
#endif
            }
        }

        internal void CancelPlayback()
        {
            Session.PlayerPlay(false);
            Session.PlayerUnload();
            if (LameEncoder != null)
                LameEncoder.Close();
        }

        #endregion

        #region Spotify Session Event Handlers

        static void HandleOnMessageToUser(Session sender, SessionEventArgs e)
        {
#if DEBUG
            if (Log.IsInfoEnabled)
                Log.Info("Spotify says: " + e.Message);
#endif
        }

        static void HandleOnLogMessage(Session sender, SessionEventArgs e)
        {
#if DEBUG
            if (Log.IsInfoEnabled)
                Log.Info("Spotify: " + e.Message);
#endif
        }

        static void HandleOnConnectionError(Session sender, SessionEventArgs e)
        {
#if DEBUG
            if (Log.IsErrorEnabled)
                Log.Error("Connection error: " + e.Status);
#endif
        }

        void HandleOnPlayTokenLost(Session sender, SessionEventArgs e)
        {
#if DEBUG
            if (Log.IsWarnEnabled) Log.Warn("Play token lost");
#endif
            _playbackDone.Set();
        }

        #endregion
    }
}
