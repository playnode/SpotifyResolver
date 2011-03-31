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
using System.Threading;

namespace Spotify
{
    internal delegate void PlaylistContainerEventHandler(PlaylistContainer sender, PlaylistContainerEventArgs e);

	internal class PlaylistContainer : IDisposable
	{
		#region Callbacks & static stuff
		
		private delegate void playlist_added_delegate(IntPtr containerPtr, IntPtr playlistPtr, int position, IntPtr userDataPtr);
		private delegate void playlist_removed_delegate(IntPtr containerPtr, IntPtr playlistPtr, int position, IntPtr userDataPtr);
		private delegate void playlist_moved_delegate(IntPtr containerPtr, IntPtr playlistPtr, int position, int new_position, IntPtr userDataPtr);
		private delegate void container_loaded_delegate(IntPtr containerPtr, IntPtr userDataPtr);
		
		private static playlist_added_delegate playlist_added = new playlist_added_delegate(PlaylistAddedCallback);
		private static playlist_removed_delegate playlist_removed = new playlist_removed_delegate(PlaylistRemovedCallback);
		private static playlist_moved_delegate playlist_moved = new playlist_moved_delegate(PlaylistMovedCallback);		
		private static container_loaded_delegate container_loaded = new container_loaded_delegate(ContainerLoadedCallback);
		
		private static Dictionary<IntPtr, PlaylistContainer> containers = new Dictionary<IntPtr, PlaylistContainer>();
		private static libspotify.sp_playlistcontainer_callbacks callbacks;
		private static IntPtr callbacksPtr = IntPtr.Zero;
		
		#endregion
		
		#region Events

		internal event PlaylistContainerEventHandler OnPlaylistAdded;
		internal event PlaylistContainerEventHandler OnPlaylistRemoved;
		internal event PlaylistContainerEventHandler OnPlaylistMoved;
		internal event PlaylistContainerEventHandler OnContainerLoaded;
		
		#endregion	
		
		#region Private declarations
		
		private IntPtr containerPtr = IntPtr.Zero;		
		private List<Playlist> playlists = new List<Playlist>();
		private Dictionary<IntPtr, int> playlistIndexByPtr = new Dictionary<IntPtr, int>();		
		
		private Session owningSession = null;
		
		#endregion
		
		#region Ctor

		internal PlaylistContainer(IntPtr containerPtr, Session owningSession)
		{
			if(containerPtr == IntPtr.Zero)
				throw new ArgumentException("containerPtr can not be zero");			
			
			lock(libspotify.Mutex)
			{
				if(containers.ContainsKey(containerPtr))
				   throw new Exception("libspotify-sharp internal error, creating playlist container for the second time");
				   
				libspotify.sp_playlistcontainer_add_callbacks(containerPtr, callbacksPtr, IntPtr.Zero);	
				
				this.owningSession = owningSession;
				this.containerPtr = containerPtr;
				containers.Add(containerPtr, this);
			
				for(int i = 0; i < PlaylistCount; i++)
				{
					IntPtr playlistPtr = IntPtr.Zero;
					playlistPtr = libspotify.sp_playlistcontainer_playlist(containerPtr, i);
					Playlist p = new Playlist(playlistPtr, owningSession);
					playlists.Add(p);
					playlistIndexByPtr.Add(playlistPtr, i);
				}
			}
		}
		
		#endregion
		
		#region Properties
		
		internal int PlaylistCount
		{
			get
			{
				CheckDisposed(true);
				lock(libspotify.Mutex)
					return libspotify.sp_playlistcontainer_num_playlists(containerPtr);
			}
		}
		
		internal Playlist[] CurrentLists
		{
			get
			{
				lock(libspotify.Mutex)
				{
					CheckDisposed(true);
					return playlists.ToArray();
				}
			}
		}
		
		#endregion		
		
		#region Callback & static methods
		
		static PlaylistContainer()
		{
			lock(libspotify.Mutex)
			{
				callbacks = new libspotify.sp_playlistcontainer_callbacks();
			
				callbacks.playlist_added = Marshal.GetFunctionPointerForDelegate(playlist_added);
				callbacks.playlist_moved = Marshal.GetFunctionPointerForDelegate(playlist_moved);			
				callbacks.playlist_removed = Marshal.GetFunctionPointerForDelegate(playlist_removed);
				callbacks.container_loaded = Marshal.GetFunctionPointerForDelegate(container_loaded);
				
				int size = Marshal.SizeOf(callbacks);
				callbacksPtr = Marshal.AllocHGlobal(size);
				Marshal.StructureToPtr(callbacks, callbacksPtr, true);
			}
		}
		
		private static PlaylistContainer GetContainer(IntPtr containerPtr)
		{
			PlaylistContainer pc = null;
			
			if(containers.TryGetValue(containerPtr, out pc))
				return pc;
			else
				return null;
		}
		
		private static void PlaylistAddedCallback(IntPtr containerPtr, IntPtr playlistPtr, int position, IntPtr userDataPtr)
		{	
			PlaylistContainer pc = GetContainer(containerPtr);
			
			if(pc != null)
			{
				Playlist pl;
				Playlist[] currentLists;
				
				lock(libspotify.Mutex)
				{
					pl = new Playlist(playlistPtr, pc.owningSession);
					pc.playlists.Insert(position, pl);
					pc.playlistIndexByPtr[playlistPtr] = position;
					currentLists = pc.CurrentLists;
				}
				
				pc.owningSession.EnqueueEventWorkItem(new EventWorkItem(pc.OnPlaylistAdded,
					new object[] {pc, new PlaylistContainerEventArgs(pl, position, -1, currentLists) }));
			}
		}
		
		private static void PlaylistRemovedCallback(IntPtr containerPtr, IntPtr playlistPtr, int position, IntPtr userDataPtr)
		{
			PlaylistContainer pc = GetContainer(containerPtr);
			
			if(pc != null)
			{
				Playlist pl;
				Playlist[] currentLists;
				
				lock(libspotify.Mutex)
				{
					if(!pc.playlists[position].playlistPtr.Equals(playlistPtr))
						throw new Exception("libspotify-sharp internal error, playlist position and pointer is inconsistent on remove");
				
					pl = pc.playlists[position];
					pc.playlists.RemoveAt(position);
					pc.playlistIndexByPtr.Remove(playlistPtr);
					pl.Dispose();
					
					currentLists = pc.CurrentLists;
				}
				
				pc.owningSession.EnqueueEventWorkItem(new EventWorkItem(pc.OnPlaylistRemoved,
					new object[] {pc, new PlaylistContainerEventArgs(pl, position, -1, currentLists) }));
			}
		}
		
		private static void PlaylistMovedCallback(IntPtr containerPtr, IntPtr playlistPtr, int position, int new_position, IntPtr userDataPtr)
		{
			PlaylistContainer pc = GetContainer(containerPtr);
			
			if(pc != null)
			{
				Playlist pl;
				Playlist[] currentLists;
				
				lock(libspotify.Mutex)
				{
				
					// when moving a playlist "up", the indices are re-arranged so need to adjust the new_position
					// when moving a playlist "down" all is good...
					if(position < new_position){
						new_position--;
					}
					
					pl = pc.playlists[position];
					pc.playlists.RemoveAt(position);
					pc.playlists.Insert(new_position, pl);
					
					currentLists = pc.CurrentLists;
				}
				
				pc.owningSession.EnqueueEventWorkItem(new EventWorkItem(pc.OnPlaylistMoved,
					new object[] {pc, new PlaylistContainerEventArgs(pl, position, new_position, currentLists) }));
			}
			
		}
		
		private static void ContainerLoadedCallback(IntPtr containerPtr, IntPtr userDataPtr)
		{
			PlaylistContainer pc = GetContainer(containerPtr);
			
			if(pc != null)
			{
				Playlist[] currentLists;
				
				lock(libspotify.Mutex)
				{
					currentLists = pc.CurrentLists;
				}
				
				// It's more practical to have this event on Session IMHO. Let's have both.
				
				pc.owningSession.EnqueueEventWorkItem(new EventWorkItem(pc.OnContainerLoaded,
					new object[] {pc, new PlaylistContainerEventArgs(null, -1, -1, currentLists) }));
				
				pc.owningSession.PlaylistContainerLoaded();
				
			}
			
		}
		
		#endregion
		
		#region Private methods	
		
		#endregion
		
		#region internal methods
		
		internal Playlist AddNewPlaylist(string name)
		{
			CheckDisposed(true);
			
			Playlist result = null;			
			
			if(string.IsNullOrEmpty(name) || string.IsNullOrEmpty(name.Trim()) || name.Length >= 256)
				throw new ArgumentException("invalid playlist name");
			
			lock(libspotify.Mutex)
			{
				IntPtr playlistPtr = libspotify.sp_playlistcontainer_add_new_playlist(containerPtr, name);
				
				int index;
				if(playlistIndexByPtr.TryGetValue(playlistPtr, out index))
					result = playlists[index];
				else
					result = null;
			}
			
			return result;
		}
		
		internal Playlist AddPlaylist(Link link)
		{
			CheckDisposed(true);
			
			Playlist result = null;
			
			lock(libspotify.Mutex)
			{
				IntPtr playlistPtr = libspotify.sp_playlistcontainer_add_playlist(containerPtr, link.linkPtr);
				
				int index;
				if(playlistIndexByPtr.TryGetValue(playlistPtr, out index))
					result = playlists[index];
				else
					result = null;
			}
			
			return result;			
		}
		
		internal sp_error RemovePlaylist(Playlist playlist)
		{
			CheckDisposed(true);
			
			sp_error result = sp_error.INVALID_INDATA;
					
			int index = -1;
			lock(libspotify.Mutex)
			{
				if(playlistIndexByPtr.TryGetValue(playlist.playlistPtr, out index))
				{
					if(!playlist.playlistPtr.Equals(playlists[index].playlistPtr))
						throw new Exception("libspotify-sharp internal error, playlist position and pointer is inconsistent on remove");
					
					libspotify.sp_playlistcontainer_remove_playlist(containerPtr, index);
				}
			}
			
			return result;
		}
		
		internal sp_error MovePlaylist(Playlist playlist, int newPosition)
		{
			CheckDisposed(true);
			
			sp_error result = sp_error.INVALID_INDATA;
			
			int index = -1;
			lock(libspotify.Mutex)
			{
				if(playlistIndexByPtr.TryGetValue(playlist.playlistPtr, out index))
				{
					if(!playlist.playlistPtr.Equals(playlists[index].playlistPtr))
						throw new Exception("libspotify-sharp internal error, playlist position and pointer is inconsistent on move");
					
					result = libspotify.sp_playlistcontainer_move_playlist(containerPtr, index, newPosition);					
				}
			}		
			
			return result;
		}
		
		#endregion
		
		#region Clean up
		
		~PlaylistContainer()
		{
			Dispose(false);	
		}	
		
		private void Dispose(bool disposing)
		{
			try
			{
				if (disposing)
				{
		        	OnPlaylistAdded = null;
					OnPlaylistMoved = null;
					OnPlaylistRemoved = null;
		       	}
		       	
				if(containerPtr == IntPtr.Zero)
					return;
				
				containers.Remove(containerPtr);
				
				if(containerPtr != IntPtr.Zero)
				{
					libspotify.sp_playlistcontainer_remove_callbacks(containerPtr, callbacksPtr, IntPtr.Zero);			
					containerPtr = IntPtr.Zero;
					
					foreach(Playlist p in playlists.ToArray())
					{
						p.Dispose();
					}
					
					playlists.Clear();
					playlistIndexByPtr.Clear();
				}
			}
			catch
			{
				
			}
		}

        public void Dispose()
		{
			if(containerPtr == IntPtr.Zero)
				return;
			
			Dispose(true);			
       		GC.SuppressFinalize(this);			
		}
		
		private bool CheckDisposed(bool throwOnDisposed)
		{
			lock(libspotify.Mutex)
			{
				bool result = containerPtr == IntPtr.Zero;
				if(result && throwOnDisposed)
					throw new ObjectDisposedException("PlaylistContainer");
				
				return result;
			}
		}
		
		#endregion
	}
}
