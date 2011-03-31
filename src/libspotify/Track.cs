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

namespace Spotify
{	
	/*
	 * Accessors in this class easily seems to be way too complex.
	 * It is the way it is because of libspotify internal threading.
	 * If accessing Name calls into libspotify every time, there where
	 * problems when accessing Track properties from OnMusicDelivery
	 * callback. I try to preload all properties before a track is
	 * played. That way it's safe to call Track.Duration from playing
	 * callback.
	 */ 
	
	internal class Track
	{
		#region Static methods
		
		internal static Track CreateFromLink(Link link)
		{
			Track result = null;
			
			if(link.linkPtr != IntPtr.Zero)
			{
				lock(libspotify.Mutex)
				{
					IntPtr trackPtr = libspotify.sp_link_as_track(link.linkPtr);
					if(trackPtr != IntPtr.Zero)
						result = new Track(trackPtr);
				}
			}
			
			return result;
		}
		
		internal static Track CreateFromLink(Link link, out int offset)
		{
			Track result = null;
			offset = 0;
			
			if(link.linkPtr != IntPtr.Zero)
			{
				IntPtr offsetPtr = IntPtr.Zero;
				
				lock(libspotify.Mutex)
				{
					IntPtr trackPtr = libspotify.sp_link_as_track_and_offset(link.linkPtr, out offsetPtr);
					if(trackPtr != IntPtr.Zero)
						result = new Track(trackPtr);
				}
				
				offset = offsetPtr.ToInt32();
			}
			
			return result;
		}
		
		#endregion
		
		#region Declarations
		
		internal IntPtr trackPtr = IntPtr.Zero;
		
		private bool isLoaded = false;
		private bool isAvailable = false;
		private sp_error error = sp_error.RESOURCE_NOT_LOADED;
		private Album album = null;
		private Artist[] artists = null;
		private string name = string.Empty;
		private int duration = 0;
		private int popularity = 0;
		private int disc = 0;
		private int index = 0;
		private string linkString = string.Empty;
		
		#endregion
		
		#region Ctor
		
		internal Track(IntPtr trackPtr)
		{
			if(trackPtr == IntPtr.Zero)
				throw new ArgumentException("trackPtr can not be zero");
			
			this.trackPtr = trackPtr;			
			
			lock(libspotify.Mutex)
				libspotify.sp_track_add_ref(trackPtr);
			
			CheckLoaded();
		}
		
		#endregion
		
		#region Internal methods
		
		internal void CheckLoaded()
		{
			CheckDisposed(true);
			
			if(isLoaded)
				return;
			
			lock(libspotify.Mutex)
				isLoaded = libspotify.sp_track_is_loaded(trackPtr);
			
			if(!isLoaded)
				return;
			
			
			lock(libspotify.Mutex)
			{
				isAvailable = libspotify.sp_track_is_available(trackPtr);
				
				error = libspotify.sp_track_error(trackPtr);
                IntPtr albumPtr = libspotify.sp_track_album(trackPtr);
                if (albumPtr != IntPtr.Zero)
                    album = new Album(albumPtr);

				artists = new Artist[libspotify.sp_track_num_artists(trackPtr)];
				for(int i = 0; i < artists.Length; i++)
					artists[i] = new Artist(libspotify.sp_track_artist(trackPtr, i));
				
                name = libspotify.GetString(libspotify.sp_track_name(trackPtr), string.Empty);
				
				duration = libspotify.sp_track_duration(trackPtr);
				popularity = libspotify.sp_track_popularity(trackPtr);
				disc = libspotify.sp_track_disc(trackPtr);
				index = libspotify.sp_track_index(trackPtr);
				
				using(Link l = CreateLink(0))
				{
					linkString = l.ToString();	
				}				
			}
		}
		
		#endregion
		
		#region Properties
		
		internal bool IsLoaded
		{
			get
			{
				CheckLoaded();
				return isLoaded;
			}
		}

        internal bool IsStarred
        {
            get
            {
                CheckLoaded();
                lock (libspotify.Mutex)
                {
                    bool result = libspotify.sp_track_is_starred(trackPtr);
                    return result;
                }
            }
        }
		
		internal bool IsAvailable
		{
			get
			{
				CheckLoaded();
				return isAvailable;
			}
		}
		
		internal sp_error Error
		{
			get
			{
				CheckLoaded();				
				return error;
			}
		}
		
		
		internal Album Album
		{
			get
			{
				CheckLoaded();				
				return album;
			}
		}
		
		internal Artist[] Artists
		{
			get
			{
				CheckLoaded();				
				return artists;
			}
		}	
		
		internal string Name
		{
			get
			{
				CheckLoaded();				
				return name;
			}
		}
		
		internal int Duration
		{
			get
			{
				CheckLoaded();				
				return duration;
			}
		}
		
		internal int Popularity
		{
			get
			{
				CheckLoaded();				
				return popularity;
			}
		}
		
		internal int Disc
		{
			get
			{
				CheckLoaded();				
				return disc;
			}
		}
		
		internal int Index
		{
			get
			{
				CheckLoaded();				
				return index;
			}
		}
		
		internal string LinkString
		{
			get
			{
				CheckLoaded();				
				return linkString;
			}
		}
		
		#endregion
		
		#region internal methods
		
		internal Link CreateLink(int offset)
		{
			CheckDisposed(true);
			
			lock(libspotify.Mutex)
			{
				IntPtr linkPtr = libspotify.sp_link_create_from_track(trackPtr, offset);
				if(linkPtr != IntPtr.Zero)
					return new Link(linkPtr);
				else
					return null;
			}
		}

        internal void SetStarred(Session session, bool starred)
        {
            CheckLoaded();
            if (!isLoaded)
                return;

            IntPtr arrayPtr = IntPtr.Zero;

            try
            {
                int[] array = new int[] { trackPtr.ToInt32() };
                int size = Marshal.SizeOf(arrayPtr) * array.Length;
                arrayPtr = Marshal.AllocHGlobal(size);
                Marshal.Copy(array, 0, arrayPtr, array.Length);
                libspotify.sp_track_set_starred(session.sessionPtr, arrayPtr, 1, starred);
            }
            finally
            {
                if (arrayPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(arrayPtr);
                }
            }
        }
		
		public override string ToString ()
		{
			if(IsLoaded)			
			{
                return string.Format("[Track: Error={0}, Album.Name={1}, Artists={2}, Name={3}, Duration={4}, Popularity={5}, Disc={6}, Index={7}, LinkString={8} IsStarred={9}]",
					Error, Album == null ? "null" : Album.Name, Artist.ArtistsToString(Artists), Name, Duration, Popularity, Disc, Index, LinkString, IsStarred);
			}
			else
				return "[Track: Not loaded]";
		}	
		
		
		#endregion
		
		#region Cleanup
		
		~Track()
		{
			Dispose(false);
		}
		
		protected void Dispose(bool disposing)
		{
			try
			{
				if (disposing)
				{
		        	
		       	}
				
				if(trackPtr != IntPtr.Zero)
				{
					libspotify.sp_track_release(trackPtr);
					trackPtr = IntPtr.Zero;
				}			
			}
			catch
			{
				
			}
		}		
		
		internal void Dispose()
		{
			if(trackPtr == IntPtr.Zero)
				return;
			
			Dispose(true);
       		GC.SuppressFinalize(this);			
		}
		
		private bool CheckDisposed(bool throwOnDisposed)
		{
			lock(libspotify.Mutex)
			{
				bool result = trackPtr == IntPtr.Zero;
				if(result && throwOnDisposed)
					throw new ObjectDisposedException("Track");
				
				return result;
			}
		}
		
		#endregion	
	}
}
