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
using System.Text;

namespace Spotify
{	
	internal class AlbumBrowse
	{
		#region Declarations
		
		private sp_error error;
		private Album album;
		private Artist artist;
		private string[] copyrights;
		private Track[] tracks;
		private string review;
				
		#endregion
		
		#region Ctor
		
		internal AlbumBrowse(IntPtr albumBrowsePtr)
		{
			if(albumBrowsePtr == IntPtr.Zero)
				throw new ArgumentException("albumBrowsePtr can not be zero");
			
			lock(libspotify.Mutex)
			{
				IntPtr strPtr = IntPtr.Zero;
				
				error = libspotify.sp_albumbrowse_error(albumBrowsePtr);
				album = new Album(libspotify.sp_albumbrowse_album(albumBrowsePtr));
				artist = new Artist(libspotify.sp_albumbrowse_artist(albumBrowsePtr));
				
				copyrights = new string[libspotify.sp_albumbrowse_num_copyrights(albumBrowsePtr)];
				for(int i = 0; i < copyrights.Length; i++)
				{
					strPtr = libspotify.sp_albumbrowse_copyright(albumBrowsePtr, i);
					copyrights[i] = libspotify.GetString(strPtr, string.Empty);
				}
				
				tracks = new Track[libspotify.sp_albumbrowse_num_tracks(albumBrowsePtr)];
				for(int i = 0; i < tracks.Length; i++)
				{
					IntPtr trackPtr = libspotify.sp_albumbrowse_track(albumBrowsePtr, i);
					tracks[i] = new Track(trackPtr);
				}
				
				strPtr = libspotify.sp_albumbrowse_review(albumBrowsePtr);
                review = libspotify.GetString(strPtr, string.Empty);
				
				libspotify.sp_albumbrowse_release(albumBrowsePtr);
			}
		}
		
		#endregion
		
		#region Properties
		
		internal sp_error Error
		{
			get
			{
				return error;	
			}
		}
		
		internal Album Album
		{
			get
			{
				return album;	
			}
		}
		
		internal Artist Artist
		{
			get
			{
				return artist;	
			}
		}
		
		internal Track[] Tracks
		{
			get
			{
				return tracks;
			}
		}
		
		internal string[] Copyrights
		{
			get
			{
				return copyrights;
			}
		}
		
		internal string Review
		{
			get
			{
				return review;
			}
		}
		
		#endregion	
		
		#region internal methods
		
		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("[AlbumBrowse]");
			sb.AppendLine("Error=" + Error);
			sb.AppendLine(Album.ToString());
			sb.AppendLine(Artist.ToString());			
			sb.AppendLine("Copyrights=" + string.Join(",", Copyrights));
			sb.AppendLine("Tracks.Length=" + Tracks.Length);
			foreach(Track t in Tracks)
				sb.AppendLine(t.ToString());
			
			sb.AppendFormat("Review:{0}{1}", Environment.NewLine, Review);
			
			return sb.ToString();
		}
		
		#endregion
		
	}
}
