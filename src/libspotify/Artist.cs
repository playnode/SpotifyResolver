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
using System.Runtime.InteropServices;

namespace Spotify
{	
	internal class Artist
	{
		#region Declarations
		
		internal IntPtr artistPtr = IntPtr.Zero;		
		
		#endregion
		
		#region Ctor
		
		internal Artist(IntPtr artistPtr)
		{
			if(artistPtr == IntPtr.Zero)
				throw new ArgumentException("artistPtr can not be zero");
			
			this.artistPtr = artistPtr;
			
			lock(libspotify.Mutex)
				libspotify.sp_artist_add_ref(artistPtr);
		}
		
		#endregion
		
		#region Static methods
		
		internal static Artist CreateFromLink(Link link)
		{
			Artist result = null;
			
			if(link.linkPtr != IntPtr.Zero)
			{
				lock(libspotify.Mutex)
				{
					IntPtr artistPtr = libspotify.sp_link_as_artist(link.linkPtr);
					if(artistPtr != IntPtr.Zero)
						result = new Artist(artistPtr);
				}
			}
			
			return result;
		}
		
		internal static string ArtistsToString(Artist[] artists)
		{
			if(artists == null)
				return string.Empty;
			
			List<string> names = new List<string>();
			foreach(Artist a in artists)
				names.Add(a.Name);
			
			return string.Join(", ", names.ToArray());
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
					return libspotify.sp_artist_is_loaded(artistPtr);	
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
                    return libspotify.GetString(libspotify.sp_artist_name(artistPtr), string.Empty);
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
		
		#endregion
		
		#region internal methods
		
		internal Link CreateLink()
		{
			CheckDisposed(true);
			
			lock(libspotify.Mutex)
			{
				IntPtr linkPtr = libspotify.sp_link_create_from_artist(artistPtr);
				if(linkPtr != IntPtr.Zero)
					return new Link(linkPtr);
				else
					return null;
			}
		}
		
		public override string ToString()
		{
			if(IsLoaded)
				return string.Format("[Artist: Name={0}, LinkString={1}]", Name, LinkString);
			else
				return "[Artist: Not loaded]";
		}

		
		#endregion
		
		#region Cleanup
		
		~Artist()
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
				
				if(artistPtr != IntPtr.Zero)
				{
					libspotify.sp_artist_release(artistPtr);
					artistPtr = IntPtr.Zero;
				}
			}
			catch
			{
				
			}
		}		
		
		internal void Dispose()
		{
			if(artistPtr == IntPtr.Zero)
				return;
			
			Dispose(true);
       		GC.SuppressFinalize(this);			
		}
		
		private bool CheckDisposed(bool throwOnDisposed)
		{
			lock(libspotify.Mutex)
			{
				bool result = artistPtr == IntPtr.Zero;
				if(result && throwOnDisposed)
					throw new ObjectDisposedException("Artist");
				
				return result;
			}
		}
		
		#endregion	
	}
}
