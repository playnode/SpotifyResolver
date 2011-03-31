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
	internal class Album
	{
		#region Declarations
		
		internal IntPtr albumPtr = IntPtr.Zero;
		
		#endregion
		
		#region Ctor
		
		internal Album(IntPtr albumPtr)
		{
			if(albumPtr == IntPtr.Zero)
				throw new ArgumentException("albumPtr can not be zero");
			
			this.albumPtr = albumPtr;
			
			lock(libspotify.Mutex)
				libspotify.sp_album_add_ref(albumPtr);
		}
		
		#endregion
		
		#region Static methods
		
		internal static Album CreateFromLink(Link link)
		{
			Album result = null;
			
			if(link.linkPtr != IntPtr.Zero)
			{
				lock(libspotify.Mutex)
				{
					IntPtr albumPtr = libspotify.sp_link_as_album(link.linkPtr);
					if(albumPtr != IntPtr.Zero)
						result = new Album(albumPtr);
				}
			}
			
			return result;
		}
		
		#endregion
		
		#region Properties
		
		internal bool IsLoaded
		{
			get
			{
				CheckDisposed(true);
				
				lock(libspotify.Mutex)
				{
					return libspotify.sp_album_is_loaded(albumPtr);	
				}
			}
		}
		
		internal bool IsAvailable
		{
			get
			{
				CheckDisposed(true);
				
				lock(libspotify.Mutex)
				{
					return libspotify.sp_album_is_available(albumPtr);
				}
			}
		}
		
		internal Artist Artist
		{
			get
			{
				CheckDisposed(true);
				
				lock(libspotify.Mutex)
				{
					return new Artist(libspotify.sp_album_artist(albumPtr));
				}
			}
		}
		
		internal string Name
		{
			get
			{
				CheckDisposed(true);
				
				lock(libspotify.Mutex)
				{
                    return libspotify.GetString(libspotify.sp_album_name(albumPtr), string.Empty);
				}
			}
		}
		
		internal int Year
		{
			get
			{
				CheckDisposed(true);
				
				lock(libspotify.Mutex)
				{
					return libspotify.sp_album_year(albumPtr);
				}
			}
		}
		
		internal string CoverId
		{
			get
			{
				CheckDisposed(true);
				
				lock(libspotify.Mutex)
				{
					IntPtr coverIdPtr = libspotify.sp_album_cover(albumPtr);
					if (coverIdPtr == IntPtr.Zero)
						return null;
					
					byte[] coverId = new byte[20];
					Marshal.Copy(coverIdPtr, coverId, 0, coverId.Length);
					return libspotify.ImageIdToString(coverId);
				}
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
		
		internal sp_albumtype Type
		{
			get
			{
				CheckDisposed(true);
				
				lock(libspotify.Mutex)
				{
					return libspotify.sp_album_type(albumPtr);
				}
			}
		}
		
		#endregion
		
		#region internal methods
		
		internal Link CreateLink()
		{
			CheckDisposed(true);
			
			lock(libspotify.Mutex)
			{
				IntPtr linkPtr = libspotify.sp_link_create_from_album(albumPtr);
				if(linkPtr != IntPtr.Zero)
					return new Link(linkPtr);
				else
					return null;
			}
		}
		
		public override string ToString()
		{
			if(IsLoaded)
			{
				return string.Format("[Album: Artist={0}, Name={1}, Year={2}, CoverId={3}, LinkString={4}]",
					Artist, Name, Year, CoverId, LinkString);
			}
			else
			{
				return "[Album: Not loaded]";
			}
		}

		
		#endregion
		
		#region Cleanup
		
		~Album()
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
				
				if(albumPtr != IntPtr.Zero)
				{
					libspotify.sp_album_release(albumPtr);
					albumPtr = IntPtr.Zero;
				}
			}
			catch
			{
				
			}
		}		
		
		internal void Dispose()
		{
			if(albumPtr == IntPtr.Zero)
				return;
			
			Dispose(true);
       		GC.SuppressFinalize(this);			
		}
		
		private bool CheckDisposed(bool throwOnDisposed)
		{
			lock(libspotify.Mutex)
			{
				bool result = albumPtr == IntPtr.Zero;
				if(result && throwOnDisposed)
					throw new ObjectDisposedException("Album");
				
				return result;
			}
		}
		
		#endregion	
	}
}
