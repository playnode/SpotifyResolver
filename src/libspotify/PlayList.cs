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
using System.Runtime.InteropServices;
using System.Collections.Generic;


namespace Spotify
{
    internal delegate void TracksEventHandler(Playlist sender, TracksEventArgs e);
    internal delegate void PlaylistEventHandler(Playlist sender, PlaylistEventArgs e);

	internal class Playlist : IDisposable
	{
		#region Callbacks & static stuff
		
		private delegate void tracks_added_delegate(IntPtr playlistPtr, ref IntPtr tracksPtr, int num_tracks, int position, IntPtr userDataPtr);
		private delegate void tracks_removed_delegate(IntPtr playlistPtr, IntPtr trackIndicesPtr, int num_tracks, IntPtr userDataPtr);
		private delegate void tracks_moved_delegate(IntPtr playlistPtr, IntPtr trackIndicesPtr, int num_tracks, int new_position, IntPtr userDataPtr);
		private delegate void playlist_renamed_delegate(IntPtr playlistPtr, IntPtr userDataPtr);
		private delegate void playlist_state_changed_delegate(IntPtr playlistPtr, IntPtr userDataPtr);
		private delegate void playlist_update_in_progress_delegate(IntPtr playlistPtr, bool done, IntPtr userDataPtr);
		private delegate void playlist_metadata_updated_delegate(IntPtr playlistPtr, IntPtr userDataPtr);
		
		private static tracks_added_delegate tracks_added = new tracks_added_delegate(TracksAddedCallback);
		private static tracks_removed_delegate tracks_removed = new tracks_removed_delegate(TracksRemovedCallback);
		private static tracks_moved_delegate tracks_moved = new tracks_moved_delegate(TracksMovedCallback);
		private static playlist_renamed_delegate playlist_renamed = new playlist_renamed_delegate(PlaylistRenamedCallback);
		private static playlist_state_changed_delegate playlist_state_changed = new playlist_state_changed_delegate(PlaylistStateChangedCallback);
		private static playlist_update_in_progress_delegate playlist_update_in_progress = new playlist_update_in_progress_delegate(PlaylistUpdateInProgressCallback);
		private static playlist_metadata_updated_delegate metadata_updated = new playlist_metadata_updated_delegate(MetadataUpdatedCallback);
		
		
		private static Dictionary<IntPtr, Playlist> playlists = new Dictionary<IntPtr, Playlist>();
		private static libspotify.sp_playlist_callbacks callbacks;
		private static IntPtr callbacksPtr = IntPtr.Zero;
		
		#endregion
		
		#region Events
		
		internal event TracksEventHandler OnTracksAdded;
		internal event TracksEventHandler OnTracksRemoved;
		internal event TracksEventHandler OnTracksMoved;
		
		internal event PlaylistEventHandler OnRenamed;
		internal event PlaylistEventHandler OnStateChanged;		
		internal event PlaylistEventHandler OnUpdateInProgress;		
		internal event PlaylistEventHandler OnMetadataUpdated;
		
		#endregion	
		
		#region Private declarations
		
		internal IntPtr playlistPtr = IntPtr.Zero;	
		private Session owningSession = null;		
		
		#endregion

        #region Static

        internal static Playlist Create(Session session, Link link)
        {
            IntPtr ptr = libspotify.sp_playlist_create(session.sessionPtr, link.linkPtr);
            if (ptr != IntPtr.Zero)
                return new Playlist(ptr, session);
            else
                return null;
        }

        #endregion

        #region Ctor

        internal Playlist(IntPtr playlistPtr, Session owningSession)
		{
			if(playlistPtr == IntPtr.Zero)
				throw new ArgumentException("playlistPtr can not be zero");			
			
			lock(libspotify.Mutex)
			{
				this.playlistPtr = playlistPtr;
				this.owningSession = owningSession;

				libspotify.sp_playlist_add_callbacks(playlistPtr, callbacksPtr, IntPtr.Zero);

                playlists[playlistPtr] = this;
			}
		}
		
		#endregion
		
		#region Properties
		
		internal int TrackCount
		{
			get
			{
				CheckDisposed(true);
				lock(libspotify.Mutex)
					return libspotify.sp_playlist_num_tracks(playlistPtr);
			}
		}
		
		internal string Name
		{
			get
			{
				CheckDisposed(true);
				lock(libspotify.Mutex)
				{
                    return libspotify.GetString(libspotify.sp_playlist_name(playlistPtr), string.Empty);
				}
			}
			
			set
			{
				CheckDisposed(true);				
				if(!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(value.Trim()) && value.Length < 256)
				{
					lock(libspotify.Mutex)
					{
						if(libspotify.sp_playlist_is_loaded(playlistPtr))
						{
                            // TODO Char encoding?
							sp_error result = libspotify.sp_playlist_rename(playlistPtr, value);
							if(result != sp_error.OK)
							{
								throw new SpotifyException(result);								
							}
						}											
					}
				}
				else
					throw new SpotifyException(sp_error.INVALID_INDATA);
			}			
		}
		
		internal bool IsLoaded
		{
			get
			{
				CheckDisposed(true);
				lock(libspotify.Mutex)
					return libspotify.sp_playlist_is_loaded(playlistPtr);
			}
		}
		
		internal bool HasPendingChanges
		{
			get
			{
				CheckDisposed(true);
				lock(libspotify.Mutex)
					return libspotify.sp_playlist_has_pending_changes(playlistPtr);
			}
		}
		
		internal bool IsCollaborative
		{
			get
			{
				CheckDisposed(true);
				lock(libspotify.Mutex)
					return libspotify.sp_playlist_is_collaborative(playlistPtr);					
			}
			
			set				
			{
				CheckDisposed(true);
				lock(libspotify.Mutex)
					libspotify.sp_playlist_set_collaborative(playlistPtr, value);
			}
		}
		
		internal User Owner
		{
			get
			{
				lock(libspotify.Mutex)
					return new User(libspotify.sp_playlist_owner(playlistPtr));
			}
		}
		
		internal Track[] CurrentTracks
		{
			get
			{
                CheckDisposed(true);
                List<Track> tracks = new List<Track>();
                lock (libspotify.Mutex)
                {
                    tracks.Clear();
                    for (int i = 0; i < TrackCount; i++)
                    {
                        IntPtr trackPtr = libspotify.sp_playlist_track(playlistPtr, i);
                        Track t = new Track(trackPtr);
                        tracks.Add(t);
                    }
                }

                return tracks.ToArray();
			}
		}
		
		internal string LinkString
		{
			get
			{
				CheckDisposed(true);
				
				string linkString = string.Empty;
				using(Link l = CreateLink())
				{
					if( l != null)
						linkString = l.ToString();
				}
				
				return linkString;
			}
		}
		
		#endregion
		
		#region Callback & static methods
		
		static Playlist()
		{
			lock(libspotify.Mutex)
			{
				callbacks = new libspotify.sp_playlist_callbacks();
				
				callbacks.playlist_renamed = Marshal.GetFunctionPointerForDelegate(playlist_renamed);
				callbacks.playlist_state_changed = Marshal.GetFunctionPointerForDelegate(playlist_state_changed);
				callbacks.playlist_update_in_progress = Marshal.GetFunctionPointerForDelegate(playlist_update_in_progress);
				callbacks.tracks_added = Marshal.GetFunctionPointerForDelegate(tracks_added);
				callbacks.tracks_moved = Marshal.GetFunctionPointerForDelegate(tracks_moved);
				callbacks.tracks_removed = Marshal.GetFunctionPointerForDelegate(tracks_removed);
				callbacks.playlist_metadata_updated = Marshal.GetFunctionPointerForDelegate(metadata_updated);
					
					
				int size = Marshal.SizeOf(callbacks);
				callbacksPtr = Marshal.AllocHGlobal(size);
				Marshal.StructureToPtr(callbacks, callbacksPtr, true);
			}
		}
		
		private static Playlist GetPlaylist(IntPtr playlistPtr)
		{
			Playlist pl = null;
			
			if(playlists.TryGetValue(playlistPtr, out pl))
				return pl;
			else
				return null;
		}	
		
		private static void TracksAddedCallback(IntPtr playlistPtr, ref IntPtr tracksPtr, int num_tracks, int position, IntPtr userDataPtr)
		{
			
			Playlist pl = GetPlaylist(playlistPtr);
			
			if(pl != null)
			{
				Track[] tracks;
				Track[] currentTracks;
				int[] indices;
				
				lock(libspotify.Mutex)
				{
                    currentTracks = pl.CurrentTracks;
					tracks = new Track[num_tracks];
					indices = new int[num_tracks];

                    for (int i = 0; i < num_tracks; i++)
                    {
                        IntPtr trackPtr = libspotify.sp_playlist_track(playlistPtr, position + i);
                        Track t = new Track(trackPtr);
                        tracks[i] = t;
                        indices[i] = position + i;
                    }
				}
				
				pl.owningSession.EnqueueEventWorkItem(new EventWorkItem(pl.OnTracksAdded,
					new object[] {pl, new TracksEventArgs(tracks, indices, position, -1, currentTracks) }));
			}
		}
		
		private static void TracksRemovedCallback(IntPtr playlistPtr, IntPtr trackIndicesPtr, int num_tracks, IntPtr userDataPtr)
		{
			Playlist pl = GetPlaylist(playlistPtr);
			
			if(pl != null)
			{
				Track[] currentTracks;
				int[] indices;
				
				lock(libspotify.Mutex)
				{
                    currentTracks = pl.CurrentTracks;
					indices = new int[num_tracks];
					
					for(int i = 0; i < num_tracks; i++)
					{
						int index = Marshal.ReadInt32(trackIndicesPtr, i);
						indices[i] = index;
					}
				}
				
				pl.owningSession.EnqueueEventWorkItem(new EventWorkItem(pl.OnTracksRemoved,
					new object[] {pl, new TracksEventArgs(null, indices, -1, -1, currentTracks) }));
			}
		}
		
		private static void TracksMovedCallback(IntPtr playlistPtr, IntPtr trackIndicesPtr, int num_tracks, int new_position, IntPtr userDataPtr)
		{
			Playlist pl = GetPlaylist(playlistPtr);
			
			if(pl != null)
			{
				Track[] tracks;
				Track[] currentTracks;
				int[] indices;
				
				lock(libspotify.Mutex)
				{
                    currentTracks = pl.CurrentTracks;

					tracks = new Track[num_tracks];
					indices = new int[num_tracks];
					
					for(int i = 0; i < num_tracks; i++)
					{
						int index = Marshal.ReadInt32(trackIndicesPtr, i);
						indices[i] = index;
                        tracks[i] = currentTracks[index];
					}				
				}
				
				pl.owningSession.EnqueueEventWorkItem(new EventWorkItem(pl.OnTracksMoved,
					new object[] {pl, new TracksEventArgs(tracks, indices, -1, new_position, currentTracks) }));
			}
		}		
		
		private static void PlaylistRenamedCallback(IntPtr playlistPtr, IntPtr userDataPtr)
		{
			Playlist pl = GetPlaylist(playlistPtr);			
			
			if(pl != null)
			{
				pl.owningSession.EnqueueEventWorkItem(new EventWorkItem(pl.OnRenamed,
					new object[] {pl, new PlaylistEventArgs() }));
			}
		}
		
		private static void PlaylistStateChangedCallback(IntPtr playlistPtr, IntPtr userDataPtr)
		{
			Playlist pl = GetPlaylist(playlistPtr);
			
			if(pl != null)
			{
				pl.owningSession.EnqueueEventWorkItem(new EventWorkItem(pl.OnStateChanged,
					new object[] {pl, new PlaylistEventArgs() }));
			}
		}
		
		private static void PlaylistUpdateInProgressCallback(IntPtr playlistPtr, bool done, IntPtr userDataPtr)
		{	
			Playlist pl = GetPlaylist(playlistPtr);			
			
			if(pl != null)
			{
				pl.owningSession.EnqueueEventWorkItem(new EventWorkItem(pl.OnUpdateInProgress,
					new object[] {pl, new PlaylistEventArgs(done) }));
			}
		}
		
		private static void MetadataUpdatedCallback(IntPtr playlistPtr, IntPtr userDataPtr)
		{	
			Playlist pl = GetPlaylist(playlistPtr);
			
			if(pl != null)
			{
				pl.owningSession.EnqueueEventWorkItem(new EventWorkItem(pl.OnMetadataUpdated,
					new object[] {pl, new PlaylistEventArgs() }));
			}
		}
		
		#endregion
		
		#region Private methods

        private void LoadTracks()
        {
            
        }

		#endregion
		
		#region internal methods		
		
		internal sp_error AddTracks(Track[] tracks, int position)
		{
			sp_error result = sp_error.INVALID_INDATA;		
			
			if(tracks != null && tracks.Length > 0)
			{
				lock(libspotify.Mutex)
				{
					IntPtr arrayPtr = IntPtr.Zero;
					
					try
					{
						if(position < 0)
							position = 0;
						
						if(position > TrackCount)
							position = TrackCount;					
						
						int[] array = new int[tracks.Length];
						for(int i = 0; i < tracks.Length; i++)
						{
							array[i] = tracks[i].trackPtr.ToInt32();	
						}
						
						int size = Marshal.SizeOf(arrayPtr) * array.Length;
						arrayPtr = Marshal.AllocHGlobal(size);
						Marshal.Copy(array, 0, arrayPtr, array.Length);
						result = libspotify.sp_playlist_add_tracks(playlistPtr, arrayPtr, array.Length, position, owningSession.sessionPtr);
					}
					finally
					{
						if(arrayPtr != IntPtr.Zero)
						{
							Marshal.FreeHGlobal(arrayPtr);
						}
					}
				}
			}
			
			return result;
		}
		
		internal sp_error RemoveTracks(int[] indices)
		{
			sp_error result = sp_error.INVALID_INDATA;		
			
			if(indices != null && indices.Length > 0)
			{
				lock(libspotify.Mutex)
				{
					result = libspotify.sp_playlist_remove_tracks(playlistPtr, indices, indices.Length);
				}
			}
			
			return result;
		}
		
		internal sp_error ReorderTracks(int[] indices, int newPosition)
		{
			sp_error result = sp_error.INVALID_INDATA;		
			
			if(indices != null && indices.Length > 0)
			{
				lock(libspotify.Mutex)
				{
					if(newPosition < 0)
						newPosition = 0;
						
					if(newPosition > TrackCount)
						newPosition = TrackCount;
					
					result = libspotify.sp_playlist_reorder_tracks(playlistPtr, indices, indices.Length, newPosition);
				}
			}
			
			return result;
		}
		
		internal Link CreateLink()
		{
			CheckDisposed(true);
			
			lock(libspotify.Mutex)
			{
				IntPtr linkPtr = libspotify.sp_link_create_from_playlist(playlistPtr);
				if(linkPtr != IntPtr.Zero)
					return new Link(linkPtr);
				else
					return null;
			}
		}
		
		public override string ToString()
		{
			string linkString = LinkString;
			return string.Format("[Playlist: TrackCount={0}, Name={1}, IsLoaded={2}, HasPendingChanges={3}, IsCollaborative={4}, Owner={5}, LinkString={6}]",
				TrackCount, Name, IsLoaded, HasPendingChanges, IsCollaborative, Owner, linkString == null ? "null" : linkString);
		}

		
		#endregion
		
		#region Cleanup
		
		~Playlist()
		{
			Dispose(false);	
		}
		
		protected void Dispose(bool disposing)
		{
			try
			{
				if (disposing)
				{
		        	OnTracksAdded = null;
					OnTracksRemoved = null;
					OnTracksMoved = null;
					OnRenamed = null;
					OnStateChanged = null;
					OnUpdateInProgress = null;
		       	}
		       	
				if(playlistPtr == IntPtr.Zero)
					return;			
				
				playlists.Remove(playlistPtr);			
				
				if(playlistPtr != IntPtr.Zero)
				{
					if(owningSession != null && owningSession.ConnectionState == sp_connectionstate.LOGGED_IN)
					{
						libspotify.sp_playlist_remove_callbacks(playlistPtr, callbacksPtr, IntPtr.Zero);
					}
					
					playlistPtr = IntPtr.Zero;
				}
			}
			catch
			{
				
			}
		}

        public void Dispose()
		{
			if(playlistPtr == IntPtr.Zero)
				return;
			
			Dispose(true);
       		GC.SuppressFinalize(this);			
		}
		
		private bool CheckDisposed(bool throwOnDisposed)
		{
			lock(libspotify.Mutex)
			{
				bool result = playlistPtr == IntPtr.Zero;
				if(result && throwOnDisposed)
					throw new ObjectDisposedException("Playlist");
				
				return result;
			}
		}	
		
		#endregion		
	}
}
