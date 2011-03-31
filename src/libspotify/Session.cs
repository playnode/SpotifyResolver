/*
Copyright (c) 2009 Jonas Larsson, jonas@hallerud.se

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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spotify
{
	internal delegate void SessionEventHandler(Session sender, SessionEventArgs e);
	internal delegate void AlbumBrowseEventHandler(Session sender, AlbumBrowseEventArgs e);
	internal delegate void ArtistBrowseEventHandler(Session sender, ArtistBrowseEventArgs e);
	internal delegate void SearchEventHandler(Session sender, SearchEventArgs e);
	internal delegate void MusicDeliveryEventHandler(Session sender, MusicDeliveryEventArgs e);
	internal delegate void ImageEventHandler(Session sender, ImageEventArgs e);
	
	/* FIXME
	 * 
	 * Handle dispose of resources when Spotify makes Sessions disposable.
	 * Make callbacks non static
	 * 
	 */
	internal class Session
	{
		#region Static declarations
		
		private static Dictionary<IntPtr, Session> sessions = new Dictionary<IntPtr, Session>();		
		private static libspotify.sp_session_callbacks callbacks;
	
		#endregion
		
		#region Callback stuff
		
		private delegate void logged_in_delegate(IntPtr sessionPtr, sp_error error);
		private delegate void logged_out_delegate(IntPtr sessionPtr);
		private delegate void metadata_updated_delegate(IntPtr sessionPtr);
		private delegate void connection_error_delegate(IntPtr sessionPtr, sp_error error);
		private delegate void message_to_user_delegate(IntPtr sessionPtr, string message);
		private delegate void notify_main_thread_delegate(IntPtr sessionPtr);
		private delegate int music_delivery_delegate(IntPtr sessionPtr, IntPtr formatPtr, IntPtr framesPtr, int num_frames);
		private delegate void play_token_lost_delegate(IntPtr sessionPtr);
		private delegate void log_message_delegate(IntPtr sessionPtr, string message);
		private delegate void end_of_track_delegate(IntPtr sessionPtr);
        private delegate void streaming_error_delegate(IntPtr sessionPtr, sp_error error);
        private delegate void userinfo_updated_delegate(IntPtr sessionPtr);
		
		private delegate void albumbrowse_complete_cb_delegate(IntPtr albumBrowsePtr, IntPtr userDataPtr);		
		
		private delegate void artistbrowse_complete_cb_delegate(IntPtr albumBrowsePtr, IntPtr userDataPtr);

		private delegate void search_complete_cb_delegate(IntPtr searchPtr, IntPtr userDataPtr);
		
		private delegate void image_loaded_cb_delegate(IntPtr imagePtr, IntPtr userDataPtr);
		
		private static logged_in_delegate logged_in = new logged_in_delegate(LoggedInCallback);
		private static logged_out_delegate logged_out = new logged_out_delegate(LoggedOutCallback);
		private static metadata_updated_delegate metadata_updated = new metadata_updated_delegate(MetadataUpdatedCallback);
		private static connection_error_delegate connection_error = new connection_error_delegate(ConnectionErrorCallback);
		private static message_to_user_delegate message_to_user = new message_to_user_delegate(MessageToUserCallback);
		private static notify_main_thread_delegate notify_main_thread = new notify_main_thread_delegate(NotifyMainThreadCallback);
		private static music_delivery_delegate music_delivery = new music_delivery_delegate(MusicDeliveryCallback);
		private static play_token_lost_delegate play_token_lost = new play_token_lost_delegate(PlayTokenLostCallback);
		private static log_message_delegate log_message = new log_message_delegate(LogMessageCallback);
		private static end_of_track_delegate end_of_track = new end_of_track_delegate(EndOfTrackCallback);
        private static streaming_error_delegate streaming_error = new streaming_error_delegate(StreamingErrorCallback);
        private static userinfo_updated_delegate userinfo_updated = new userinfo_updated_delegate(UserinfoUpdatedCallback);


		
		#endregion
		
		#region Events
		
		internal event SessionEventHandler OnLoginComplete;
		internal event SessionEventHandler OnLoggedOut;
		internal event SessionEventHandler OnMetaDataUpdated;
		internal event SessionEventHandler OnConnectionError;
		internal event SessionEventHandler OnMessageToUser;
		internal event SessionEventHandler OnPlayTokenLost;
		internal event SessionEventHandler OnLogMessage;
		internal event AlbumBrowseEventHandler OnAlbumBrowseComplete;
		internal event ArtistBrowseEventHandler OnArtistBrowseComplete;
        internal event SearchEventHandler OnSearchComplete;
		internal event ImageEventHandler OnImageLoaded;
		internal event SessionEventHandler OnEndOfTrack;
		internal event SessionEventHandler OnPlaylistContainerLoaded;
        internal event SessionEventHandler OnStreamingError;
        internal event SessionEventHandler OnUserinfoUpdated;

        internal event SessionEventHandler OnException;
		
		/* NOTE
		 * 
		 * Do _NOT_ call / access anything that calls back into libspotify when handling this
		 * Accessing current Track is OK, anything else is not.
		 * 
		 */
		internal event MusicDeliveryEventHandler OnMusicDelivery;
		
		#endregion
		
		#region Declarations
		
		internal IntPtr sessionPtr = IntPtr.Zero;
		private Thread mainThread = null;
		private Thread eventThread = null;

		private AutoResetEvent mainThreadNotification = new AutoResetEvent(false);
		private AutoResetEvent eventThreadNotification = new AutoResetEvent(false);		
		private Queue<EventWorkItem> eventQueue = new Queue<EventWorkItem>();
		
		private PlaylistContainer playlistContainer = null;
		
		private albumbrowse_complete_cb_delegate albumbrowse_complete_cb;
		private artistbrowse_complete_cb_delegate artistbrowse_complete_cb;
		private search_complete_cb_delegate search_complete_cb;
		private image_loaded_cb_delegate image_loaded_cb;
		
		private ManualResetEvent loginHandle = null;
		private sp_error loginResult = sp_error.IS_LOADING;
		
		private ManualResetEvent logoutHandle = null;
		
		private Dictionary<int, object> states = new Dictionary<int, object>();
		
		private ushort userStateCtr = 1;
		private ushort internalStateCtr = 1;
			

		#endregion
		
		#region Properties
		
		internal sp_connectionstate ConnectionState
		{
			get
			{
				try
				{
					if(sessionPtr != IntPtr.Zero)
					{
						lock(libspotify.Mutex)
							return libspotify.sp_session_connectionstate(sessionPtr);
					}
					else
						return sp_connectionstate.UNDEFINED;
				}
				catch
				{
					return sp_connectionstate.UNDEFINED;
				}
			}
		}
		
		internal User User
		{
			get
			{
				lock(libspotify.Mutex)
					return new User(libspotify.sp_session_user(sessionPtr));	
			}
		}
		
		internal PlaylistContainer PlaylistContainer
		{
			get
			{
				return playlistContainer;
			}
		}
		
		#endregion
		
		#region Ctor
		
		private Session(byte[] applicationKey, string cacheLocation, string settingsLocation, string userAgent)
		{
			libspotify.sp_session_config config = new libspotify.sp_session_config();
			
			config.api_version = libspotify.SPOTIFY_API_VERSION;
			config.cache_location = cacheLocation;
			config.settings_location = settingsLocation;
			config.user_agent = userAgent;
			
			int size = Marshal.SizeOf(callbacks);
			config.callbacks = Marshal.AllocHGlobal(size);
			Marshal.StructureToPtr(callbacks, config.callbacks, true);
				
			config.application_key = IntPtr.Zero;
			
			try
			{
				config.application_key = Marshal.AllocHGlobal(applicationKey.Length);
				Marshal.Copy(applicationKey, 0, config.application_key, applicationKey.Length);
				
				lock(libspotify.Mutex)
					config.application_key_size = applicationKey.Length;
				
				sessionPtr = IntPtr.Zero;
				sp_error res = libspotify.sp_session_init(ref config, out sessionPtr);
				
				if(res != sp_error.OK)
				{
					throw new SpotifyException(res);
				}				
				
				albumbrowse_complete_cb = new albumbrowse_complete_cb_delegate(AlbumBrowseCompleteCallback);
				artistbrowse_complete_cb = new artistbrowse_complete_cb_delegate(ArtistBrowseCompleteCallback);
				search_complete_cb = new search_complete_cb_delegate(SearchCompleteCallback);
				image_loaded_cb = new image_loaded_cb_delegate(ImageLoadedCallback);
				
				
				mainThread = new Thread(new ThreadStart(MainThread));
				mainThread.IsBackground = true;
				mainThread.Start();
				
				eventThread = new Thread(new ThreadStart(EventThread));
				eventThread.IsBackground = true;
				eventThread.Start();
			}
			finally
			{
				if(config.application_key != IntPtr.Zero)
				{
                    Marshal.FreeHGlobal(config.application_key);
				}
			}
		}
		
		#endregion
		
		#region Static methods
		
		static Session()
		{
			lock(libspotify.Mutex)
			{
				callbacks = new libspotify.sp_session_callbacks();
				callbacks.connection_error = Marshal.GetFunctionPointerForDelegate(connection_error);
				callbacks.logged_in = Marshal.GetFunctionPointerForDelegate(logged_in);
				callbacks.logged_out = Marshal.GetFunctionPointerForDelegate(logged_out);
				callbacks.log_message = Marshal.GetFunctionPointerForDelegate(log_message);
				callbacks.message_to_user = Marshal.GetFunctionPointerForDelegate(message_to_user);
				callbacks.metadata_updated = Marshal.GetFunctionPointerForDelegate(metadata_updated);
				callbacks.music_delivery = Marshal.GetFunctionPointerForDelegate(music_delivery);
				callbacks.notify_main_thread = Marshal.GetFunctionPointerForDelegate(notify_main_thread);
				callbacks.play_token_lost = Marshal.GetFunctionPointerForDelegate(play_token_lost);				
				callbacks.end_of_track = Marshal.GetFunctionPointerForDelegate(end_of_track);
                callbacks.streaming_error = Marshal.GetFunctionPointerForDelegate(streaming_error);
                callbacks.userinfo_updated = Marshal.GetFunctionPointerForDelegate(userinfo_updated);
			}
		}
					
		internal static Session CreateInstance(byte[] applicationKey, string cacheLocation, string settingsLocation, string userAgent)
		{
			lock(libspotify.Mutex)
			{
				if(sessions.Count > 0)
				{
					throw new InvalidOperationException("libspotify can only handle one session at the moment");
				}
				else
				{
					Session instance = new Session(applicationKey, cacheLocation, settingsLocation, userAgent);
					sessions.Add(instance.sessionPtr, instance);
					return instance;
				}
			}
		}
		
		private static Session GetSession(IntPtr sessionPtr)
		{
			Session s;
			if(sessions.TryGetValue(sessionPtr, out s))
				return s;
			else
				return null;
		}
		
		#region Callbacks
		
		private static void LoggedInCallback(IntPtr sessionPtr, sp_error error)
		{
			Session s = GetSession(sessionPtr);
			if (s == null)
				return;
			
			if(s.ConnectionState == sp_connectionstate.LOGGED_IN && error == sp_error.OK)
				s.playlistContainer = new PlaylistContainer(libspotify.sp_session_playlistcontainer(sessionPtr), s);
			
			if (s.loginHandle == null)
				s.EnqueueEventWorkItem(new EventWorkItem(s.OnLoginComplete, new object[] { s, new SessionEventArgs(error) }));
			else
			{
				try
				{
					s.loginResult = error;
					s.loginHandle.Set();
				}
				catch
				{
					
				}
			}
		}
		
		private static void LoggedOutCallback(IntPtr sessionPtr)
		{
			Session s = GetSession(sessionPtr);
			if (s == null)
				return;			
			
			if (s.playlistContainer != null)
			{
				s.playlistContainer.Dispose();
				s.playlistContainer = null;
			}
		
			if (s.loginHandle == null && s.logoutHandle == null)
			{
				s.EnqueueEventWorkItem(new EventWorkItem(s.OnLoggedOut, new object[] { s, new SessionEventArgs() }));			
			}
			else
			{
				try
				{
					if (s.loginHandle != null)
						s.loginHandle.Set();				
					if (s.logoutHandle != null)
						s.logoutHandle.Set();
				}
				catch
				{
				}
			}
		}
		
		private static void MetadataUpdatedCallback(IntPtr sessionPtr)
		{
			Session s = GetSession(sessionPtr);
			if (s == null)
				return;
			
			s.EnqueueEventWorkItem(new EventWorkItem(s.OnMetaDataUpdated, new object[] { s, new SessionEventArgs() }));
		}
		
		private static void ConnectionErrorCallback(IntPtr sessionPtr, sp_error error)
		{
			Session s = GetSession(sessionPtr);
			if (s == null)
				return;
			
			s.EnqueueEventWorkItem(new EventWorkItem(s.OnConnectionError, new object[] { s, new SessionEventArgs(error) }));			
		}
		
		private static void MessageToUserCallback(IntPtr sessionPtr, string message)
		{
			Session s = GetSession(sessionPtr);
			if (s == null)
				return;
			
			s.EnqueueEventWorkItem(new EventWorkItem(s.OnMessageToUser, new object[] { s, new SessionEventArgs(message) }));
		}
		
		private static void PlayTokenLostCallback(IntPtr sessionPtr)
		{
			Session s = GetSession(sessionPtr);
			if (s == null)
				return;
			
			s.EnqueueEventWorkItem(new EventWorkItem(s.OnPlayTokenLost, new object[] { s, new SessionEventArgs() }));
		}
		
		private static void LogMessageCallback(IntPtr sessionPtr, string message)
		{
			Session s = GetSession(sessionPtr);
			if (s == null)
				return;
			
			// Spotify log msgs can contain unprintable chars. Guessing that they control text color on Win32 or something
			message = System.Text.RegularExpressions.Regex.Replace(message, "[\u0000-\u001F]", string.Empty);
			s.EnqueueEventWorkItem(new EventWorkItem(s.OnLogMessage, new object[] { s, new SessionEventArgs(message) }));
		}
		
		private static void NotifyMainThreadCallback(IntPtr sessionPtr)
		{
			Session s = GetSession(sessionPtr);
			if (s == null)
				return;
			
			s.mainThreadNotification.Set();
		}
		
		private static int MusicDeliveryCallback(IntPtr sessionPtr, IntPtr formatPtr, IntPtr framesPtr, int num_frames)
		{
			Session s = GetSession(sessionPtr);
			if (s == null)
				return 0;
			
			int consumed = 0;
			
			byte[] samplesBytes = null;
			libspotify.sp_audioformat format = (libspotify.sp_audioformat)Marshal.PtrToStructure(formatPtr, typeof(libspotify.sp_audioformat));
			
			if(num_frames > 0)
			{
				samplesBytes = new byte[num_frames * format.channels * 2];					
				Marshal.Copy(framesPtr, samplesBytes, 0, samplesBytes.Length);
			}
			else
				samplesBytes = new byte[0];
							
			if(s.OnMusicDelivery != null)
			{
				MusicDeliveryEventArgs e = new MusicDeliveryEventArgs(format.channels, format.sample_rate, samplesBytes, num_frames);
				s.OnMusicDelivery(s, e);
				
				consumed = e.ConsumedFrames;
			}
			
			return consumed;
		}
		
		
		private static void EndOfTrackCallback(IntPtr sessionPtr)
		{
			Session s = GetSession(sessionPtr);
			if (s == null)
				return;
			
			s.EnqueueEventWorkItem(new EventWorkItem(s.OnEndOfTrack, new object[] { s, new SessionEventArgs() }));
		}

        private static void StreamingErrorCallback(IntPtr sessionPtr, sp_error error)
        {
            Session s = GetSession(sessionPtr);
            if (s == null)
                return;

            s.EnqueueEventWorkItem(new EventWorkItem(s.OnStreamingError, new object[] { s, new SessionEventArgs(error) }));
        }

        private static void UserinfoUpdatedCallback(IntPtr sessionPtr)
        {
            Session s = GetSession(sessionPtr);
            if (s == null)
                return;

            s.EnqueueEventWorkItem(new EventWorkItem(s.OnUserinfoUpdated, new object[] { s, new SessionEventArgs() }));
        }
		
		
		#endregion
		
		#endregion		
	
		#region Private metods
		
		private void MainThread()
		{			
			// FIXME Fix this when sessions can be destroyed
			
			int waitTime = 0;
			
			while(true)
			{
				mainThreadNotification.WaitOne(waitTime, false);
				do
				{
					lock(libspotify.Mutex)
					{
                        try
                        {
                            libspotify.sp_session_process_events(sessionPtr, out waitTime);
                        }
                        catch
                        {
                            waitTime = 1000;
                        }
					}
				}
				while(waitTime == 0);				
			}
		}		
		
		private void EventThread()
		{
			List<EventWorkItem> localList = new List<EventWorkItem>();
			
			// FIXME Fix this when sessions can be destroyed
			while(true)
			{				
				eventThreadNotification.WaitOne();
				
				lock(eventQueue)
				{
					while(eventQueue.Count > 0)
						localList.Add(eventQueue.Dequeue());
				}
				
				foreach(EventWorkItem eventWorkItem in localList)
				{
					try
					{
						eventWorkItem.Execute();					
					}
					catch(Exception ex)
					{
                        if (OnException != null)
                            OnException(this, new SessionEventArgs(ex.ToString()));
					}
				}

				localList.Clear();
			}
		}
		
		private int GetUserStateId()
		{
			int result;
			lock (libspotify.Mutex)
			{
				result = userStateCtr;
				userStateCtr++;
			}
			
			return result;
		}
		
		private int GetInternalStateId()
		{
			int result;
			lock (libspotify.Mutex)
			{
				result = internalStateCtr;
				internalStateCtr++;
			}
			
			return result + ushort.MaxValue;
		}
		
		private object GetSyncResponse(int id, WaitHandle waitHandle, TimeSpan timeout)
		{
			bool signal = waitHandle.WaitOne(timeout, false);		
			waitHandle.Close();			
			
			lock(libspotify.Mutex)
			{
				try
				{
					if (signal && states.ContainsKey(id))
						return states[id];
					else
						return null;
				}
				finally
				{
					states.Remove(id);	
				}
			}
		}
		
		// Callbacks are called on our own thread that already has lock -> no locking needed here		
		private void AlbumBrowseCompleteCallback(IntPtr albumBrowsePtr, IntPtr userDataPtr)
		{
			AlbumBrowse albumBrowse = new AlbumBrowse(albumBrowsePtr);
			int id = userDataPtr.ToInt32();
			
			object state = states.ContainsKey(id) ? states[id] : null;
				
			if (id <= short.MaxValue)
			{
				states.Remove(id);
				EnqueueEventWorkItem(new EventWorkItem(OnAlbumBrowseComplete, new object[] { this, new AlbumBrowseEventArgs(albumBrowse, state) }));
			}
			else
			{
				if (state != null && state is ManualResetEvent)
				{
					states[id] = albumBrowse;
					(state as ManualResetEvent).Set();
				}	
			}
		}
		
		private void ArtistBrowseCompleteCallback(IntPtr artistBrowsePtr, IntPtr userDataPtr)
		{
			try
			{
			
				ArtistBrowse artistBrowse = new ArtistBrowse(artistBrowsePtr);
				int id = userDataPtr.ToInt32();
				
				object state = states.ContainsKey(id) ? states[id] : null;
				
				if (id <= short.MaxValue)
				{
					states.Remove(id);
					EnqueueEventWorkItem(new EventWorkItem(OnArtistBrowseComplete, new object[] { this, new ArtistBrowseEventArgs(artistBrowse, state) }));			
				}
				else
				{
					if (state != null && state is ManualResetEvent)
					{
						states[id] = artistBrowse;
						(state as ManualResetEvent).Set();
					}
				}	
			}
			catch(Exception ex)
			{
				EnqueueEventWorkItem(new EventWorkItem(OnLogMessage, new object[] { this, new SessionEventArgs("E " + ex.Message) }));
			}
		}

        private void SearchCompleteCallback(IntPtr searchPtr, IntPtr userDataPtr)
        {
            Search search = new Search(searchPtr);
			
			int id = userDataPtr.ToInt32();
			
			object state = states.ContainsKey(id) ? states[id] : null;
			
			if (id <= short.MaxValue)
			{
				states.Remove(id);
				EnqueueEventWorkItem(new EventWorkItem(OnSearchComplete, new object[] { this, new SearchEventArgs(search, state) }));
			}
			else
			{
				if (state != null && state is ManualResetEvent)
				{
					states[id] = search;
					(state as ManualResetEvent).Set();
				}
			}
			
        }
		
		private void ImageLoadedCallback(IntPtr imagePtr, IntPtr userDataPtr)
        {
			
			int id = userDataPtr.ToInt32();
			object state = states.ContainsKey(id) ? states[id] : null;
			ManualResetEvent wh = null;
			bool isSync = id > short.MaxValue;
			if (isSync)
			{
				if (state == null || !(state is ManualResetEvent))
					return;
				wh = state as ManualResetEvent;
			}
			
			try
			{	
				// No locking needed since this is called on our own thread
				// that already holds the lock.
				
				/*
				 * libspotify was _REALLY_ stupidly written regarding image format.
				 * 
				 * The first versions gave you pointers to decoded image data instead
				 * of raw image data. libspotify devs seems to have listened to 
				 * the complaints about this. Yay for them.
				 */
				
				IntPtr lengthPtr = IntPtr.Zero;				
				IntPtr dataPtr = libspotify.sp_image_data(imagePtr, out lengthPtr);
				
				int length = lengthPtr.ToInt32();
				
				byte[] imageData = new byte[length];
				Marshal.Copy(dataPtr, imageData, 0, imageData.Length);
				
				Bitmap bmp = (Bitmap) Bitmap.FromStream(new MemoryStream(imageData));
				
				if (!isSync)
				{
					EnqueueEventWorkItem(new EventWorkItem(OnImageLoaded,
				    	new object[] { this, new ImageEventArgs(bmp, libspotify.ImageIdToString(libspotify.sp_image_image_id(imagePtr)), state) }));
				}
				else if (wh != null)
				{
					states[id] = bmp;
					wh.Set();
				}
					
			}
			catch(Exception ex)
			{
				if (!isSync)
				{
					EnqueueEventWorkItem(new EventWorkItem(OnImageLoaded,
					    new object[] { this, new ImageEventArgs(ex.Message, libspotify.ImageIdToString(libspotify.sp_image_image_id(imagePtr)), state) }));
				}
				else if (wh != null)
				{
					states[id] = null;
					wh.Set();
				}
			}
			finally
			{	
				libspotify.sp_image_release(imagePtr);
			}
            
        }
		
		
		#endregion
		
		#region Internal methods
		
		internal void EnqueueEventWorkItem(EventWorkItem eventWorkItem)
		{
			lock(eventQueue)
			{
				eventQueue.Enqueue(eventWorkItem);
			}			
			
			eventThreadNotification.Set();
		}
		
		internal void PlaylistContainerLoaded()
		{
			EnqueueEventWorkItem(new EventWorkItem(OnPlaylistContainerLoaded,
					new object[] {this, new SessionEventArgs() }));
		}
		
		#endregion		
		
		#region internal methods
		
		internal void LogIn(string username, string password)
		{
			lock(libspotify.Mutex)
			{
				sp_error res = libspotify.sp_session_login(sessionPtr, username, password);	
				
				if(res != sp_error.OK)
					throw new SpotifyException(res);			
			}
		}
		
		internal sp_error LogInSync(string username, string password, TimeSpan timeout)
		{
			lock(libspotify.Mutex)
			{
				if( loginHandle != null)
					return sp_error.IS_LOADING;
				else
				{
					sp_error res = libspotify.sp_session_login(sessionPtr, username, password);	
					if(res != sp_error.OK)
						return res;
					else
						loginHandle = new ManualResetEvent(false);
				}
			}
			
			bool signal = loginHandle.WaitOne(timeout, false);
			
			if (loginHandle != null)
			{
				lock (libspotify.Mutex)
				{
					loginHandle.Close();
					loginHandle = null;
				}
			}
			
			if (signal)
				return loginResult;
			else
				return sp_error.OTHER_TRANSIENT;
		}
		
		internal void LogOut()
		{
			lock(libspotify.Mutex)
			{
				if (ConnectionState == sp_connectionstate.LOGGED_IN)
				{
					sp_error res = libspotify.sp_session_logout(sessionPtr);				
				
					if(res != sp_error.OK)
						throw new SpotifyException(res);
				}
				else
					EnqueueEventWorkItem(new EventWorkItem(OnLoggedOut, new object[] { this, new SessionEventArgs() }));
			}
		}
		
		internal void LogOutSync(TimeSpan timeout)
		{
			lock(libspotify.Mutex)
			{
				if (ConnectionState == sp_connectionstate.LOGGED_IN)
				{
					logoutHandle = new ManualResetEvent(false);
					sp_error res = libspotify.sp_session_logout(sessionPtr);
				
					if(res != sp_error.OK)					
						return; // Ignore network errors etc.
				}
				else
					return; 
			}
			
			logoutHandle.WaitOne(timeout, false);
			if (logoutHandle != null)
			{
				logoutHandle.Close();
				logoutHandle = null;
			}
		}
		
		internal bool BrowseAlbum(Album album, object state)
		{	
			lock(libspotify.Mutex)
			{
				int id = GetUserStateId(); 
				states[id] = state;
				IntPtr browsePtr = libspotify.sp_albumbrowse_create(sessionPtr, album.albumPtr,
					Marshal.GetFunctionPointerForDelegate(albumbrowse_complete_cb), new IntPtr(id));
				return browsePtr != IntPtr.Zero;
			}
		}
		
		internal AlbumBrowse BrowseAlbumSync(Album album, TimeSpan timeout)
		{
			ManualResetEvent waitHandle = new ManualResetEvent(false);
			int id = GetInternalStateId();
			
			lock(libspotify.Mutex)
			{
				states[id] = waitHandle;
				if(libspotify.sp_albumbrowse_create(sessionPtr, album.albumPtr, 
					Marshal.GetFunctionPointerForDelegate(albumbrowse_complete_cb), new IntPtr(id)) == IntPtr.Zero)
				{
					waitHandle.Close();
					states.Remove(id);
					return null;
				}				
			}			

			return GetSyncResponse(id, waitHandle, timeout) as AlbumBrowse;
		}	
		
		
		internal bool BrowseArtist(Artist artist, object state)
		{
			lock(libspotify.Mutex)
			{
				int id = GetUserStateId(); 
				states[id] = state;
				IntPtr browsePtr = libspotify.sp_artistbrowse_create(sessionPtr, artist.artistPtr, 
					Marshal.GetFunctionPointerForDelegate(artistbrowse_complete_cb), new IntPtr(id));
				return browsePtr != IntPtr.Zero;
			}			
		}
		
		internal ArtistBrowse BrowseArtistSync(Artist artist, TimeSpan timeout)
		{
			ManualResetEvent waitHandle = new ManualResetEvent(false);
			int id = GetInternalStateId();
			
			lock(libspotify.Mutex)
			{
				states[id] = waitHandle;
				if(libspotify.sp_artistbrowse_create(sessionPtr, artist.artistPtr,
					Marshal.GetFunctionPointerForDelegate(artistbrowse_complete_cb), new IntPtr(id)) == IntPtr.Zero)
				{
					waitHandle.Close();
					states.Remove(id);
					return null;
				}				
			}
			
			return GetSyncResponse(id, waitHandle, timeout) as ArtistBrowse;
		}
		
		
        internal bool Search(string query, int trackOffset, int trackCount, int albumOffset, int albumCount, int artistOffset, int artistCount, object state)
        {
			lock(libspotify.Mutex)
			{
				int id = GetUserStateId(); 				
				states[id] = state;				
				IntPtr browsePtr = libspotify.sp_search_create(sessionPtr, query, trackOffset, trackCount, albumOffset, albumCount, artistOffset, artistCount,
					Marshal.GetFunctionPointerForDelegate(search_complete_cb), new IntPtr(id));				
				return browsePtr != IntPtr.Zero;
			}
        }
		
		internal Search SearchSync(string query, int trackOffset, int trackCount, int albumOffset, int albumCount, int artistOffset, int artistCount, TimeSpan timeout)
		{
			ManualResetEvent waitHandle = new ManualResetEvent(false);
			int id = GetInternalStateId();
			
			lock(libspotify.Mutex)
			{
				states[id] = waitHandle;
				if(libspotify.sp_search_create(sessionPtr, query, trackOffset, trackCount, albumOffset, albumCount, artistOffset, artistCount,
					Marshal.GetFunctionPointerForDelegate(search_complete_cb), new IntPtr(id)) == IntPtr.Zero)
				{
					waitHandle.Close();
					states.Remove(id);
					return null;
				}				
			}
			
			return GetSyncResponse(id, waitHandle, timeout) as Search;
		}

        internal bool RadioSearch(int fromYear, int toYear, sp_radio_genre genre, object state)
        {
            lock (libspotify.Mutex)
            {
                int id = GetUserStateId();
                states[id] = state;
                IntPtr browsePtr = libspotify.sp_radio_search_create(sessionPtr, fromYear, toYear, genre, Marshal.GetFunctionPointerForDelegate(search_complete_cb), new IntPtr(id));
                return browsePtr != IntPtr.Zero;
            }
        }


        internal Search RadioSearchSync(int fromYear, int toYear, sp_radio_genre genre, TimeSpan timeout)
        {
            ManualResetEvent waitHandle = new ManualResetEvent(false);
            int id = GetInternalStateId();

            lock (libspotify.Mutex)
            {
                states[id] = waitHandle;
                if (libspotify.sp_radio_search_create(sessionPtr, fromYear, toYear, genre,
                    Marshal.GetFunctionPointerForDelegate(search_complete_cb), new IntPtr(id)) == IntPtr.Zero)
                {
                    waitHandle.Close();
                    states.Remove(id);
                    return null;
                }	
            }

            return GetSyncResponse(id, waitHandle, timeout) as Search;
        }
		
		internal bool LoadImage(string id, object state)
        {
			if (id == null)
				throw new ArgumentNullException("id");
			
			if (id.Length != 40)
				throw new ArgumentException("invalid id");
			
			byte[] idArray = libspotify.StringToImageId(id);
			
			if(idArray.Length != 20)
				throw new Exception("Internal error in LoadImage");
			
            lock (libspotify.Mutex)
            {
				IntPtr idPtr = IntPtr.Zero;
				try
				{
					idPtr = Marshal.AllocHGlobal(idArray.Length);
					Marshal.Copy(idArray, 0, idPtr, idArray.Length);
					
					int stateId = GetUserStateId(); 
					states[stateId] = state;
					
					IntPtr imagePtr = libspotify.sp_image_create(sessionPtr, idPtr);
					if (libspotify.sp_image_is_loaded(imagePtr))
						ImageLoadedCallback(imagePtr, new IntPtr(stateId));
					else
						libspotify.sp_image_add_load_callback(imagePtr, Marshal.GetFunctionPointerForDelegate(image_loaded_cb), new IntPtr(stateId));
					
					return idPtr != IntPtr.Zero;
				}
				finally
				{
					if (idPtr != IntPtr.Zero)
						Marshal.FreeHGlobal(idPtr);
				}
            }
        }
		
		internal Bitmap LoadImageSync(string id, TimeSpan timeout)
		{
			if (id == null)
				throw new ArgumentNullException("id");
			
			if (id.Length != 40)
				throw new ArgumentException("invalid id");
			
			byte[] idArray = libspotify.StringToImageId(id);
			
			if(idArray.Length != 20)
				throw new Exception("Internal error in LoadImage");
			
			ManualResetEvent waitHandle = new ManualResetEvent(false);
			int stateId = GetInternalStateId();
			
			lock (libspotify.Mutex)
            {
				IntPtr idPtr = IntPtr.Zero;
				try
				{
					idPtr = Marshal.AllocHGlobal(idArray.Length);
					Marshal.Copy(idArray, 0, idPtr, idArray.Length);				
					
					states[stateId] = waitHandle;
					
					IntPtr imagePtr = libspotify.sp_image_create(sessionPtr, idPtr);
					if (libspotify.sp_image_is_loaded(imagePtr))
						ImageLoadedCallback(imagePtr, new IntPtr(stateId));
					else
						libspotify.sp_image_add_load_callback(imagePtr, Marshal.GetFunctionPointerForDelegate(image_loaded_cb), new IntPtr(stateId));
				}
				finally
				{
					if (idPtr != IntPtr.Zero)
						Marshal.FreeHGlobal(idPtr);
				}
            }			
			
			return GetSyncResponse(stateId, waitHandle, timeout) as Bitmap;
		}
		
		
		internal sp_error PlayerLoad(Track track)
		{
			PlayerUnload();
						
			lock(libspotify.Mutex)
			{
				sp_error err = libspotify.sp_session_player_load(sessionPtr, track.trackPtr);
				if (err == sp_error.OK)
					track.CheckLoaded();
				
				return err;
			}
		}
		
		internal void PlayerUnload()
		{
			lock(libspotify.Mutex)
			{
				libspotify.sp_session_player_unload(sessionPtr);
			}
		}
		
		internal sp_error PlayerSeek(int offset)
		{
			lock(libspotify.Mutex)
			{
				return libspotify.sp_session_player_seek(sessionPtr, offset);
			}	
		}
		
		internal sp_error PlayerPlay(bool play)
		{
			lock(libspotify.Mutex)
			{
				return libspotify.sp_session_player_play(sessionPtr, play);
			}	
		}

        internal void PreferredBitrate(sp_bitrate bitrate)
        {
            lock (libspotify.Mutex)
            {
                libspotify.sp_session_preferred_bitrate(sessionPtr, bitrate);
            }
        }

        internal Playlist StarredPlaylistCreate()
        {
            lock (libspotify.Mutex)
            {
                return new Playlist(libspotify.sp_session_starred_create(sessionPtr), this);
            }
        }
		
		#endregion
	}
}
