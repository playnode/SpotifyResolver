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
using System.Text;

namespace Spotify
{	
	internal class ArtistBrowse
	{
		#region Declarations
		
		private sp_error error;
		private Artist artist;		
		private List<string> portraitIds;
		private Track[] tracks;
		private Album[] albums;
		private Artist[] similarArtists;
		private string biography;		
		
		#endregion
		
		#region Ctor
		
		internal ArtistBrowse(IntPtr artistBrowsePtr)
		{
			if(artistBrowsePtr == IntPtr.Zero)
				throw new ArgumentException("artistBrowsePtr can not be zero");
			
			lock(libspotify.Mutex)
			{
				IntPtr strPtr = IntPtr.Zero;
				
				error = libspotify.sp_artistbrowse_error(artistBrowsePtr);
				artist = new Artist(libspotify.sp_artistbrowse_artist(artistBrowsePtr));
				
				portraitIds = new List<string>(libspotify.sp_artistbrowse_num_portraits(artistBrowsePtr));
				for(int i = 0; i < portraitIds.Count; i++)
				{
					IntPtr portraitIdPtr = libspotify.sp_artistbrowse_portrait(artistBrowsePtr, i);
					byte[] portraitId = new byte[20];
					Marshal.Copy(portraitIdPtr, portraitId, 0, portraitId.Length);
					portraitIds.Add(libspotify.ImageIdToString(portraitId));
				}
				
				tracks = new Track[libspotify.sp_artistbrowse_num_tracks(artistBrowsePtr)];
				for(int i = 0; i < tracks.Length; i++)
				{
					IntPtr trackPtr = libspotify.sp_artistbrowse_track(artistBrowsePtr, i);
					tracks[i] = new Track(trackPtr);
				}
				
				albums = new Album[libspotify.sp_artistbrowse_num_albums(artistBrowsePtr)];
				for(int i = 0; i < albums.Length; i++)
				{
					IntPtr albumPtr = libspotify.sp_artistbrowse_album(artistBrowsePtr, i);
					albums[i] = new Album(albumPtr);
				}
				
				similarArtists = new Artist[libspotify.sp_artistbrowse_num_similar_artists(artistBrowsePtr)];
				for(int i = 0; i < similarArtists.Length; i++)
				{
					IntPtr artistPtr = libspotify.sp_artistbrowse_similar_artist(artistBrowsePtr, i);
					similarArtists[i] = new Artist(artistPtr);
				}
				
				strPtr = libspotify.sp_albumbrowse_review(artistBrowsePtr);
                biography = libspotify.GetString(strPtr, string.Empty);
				
				libspotify.sp_artistbrowse_release(artistBrowsePtr);
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
		
		internal Artist Artist
		{
			get
			{
				return artist;	
			}
		}
		
		internal string[] PortraitIds
		{
			get
			{
				return portraitIds.ToArray();
			}
		}
		
		internal Track[] Tracks
		{
			get
			{
				return tracks;
			}
		}
		
		internal Artist[] SimilarArtists
		{
			get
			{
				return similarArtists;
			}
		}		
		
		internal string Biography
		{
			get
			{
				return biography;
			}
		}
		
		#endregion	
		
		#region internal methods
		
		public override string ToString()
		{
			
			
			
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("[ArtistBrowse]");
			sb.AppendLine("Error=" + Error);			
			sb.AppendLine(Artist.ToString());			
			sb.AppendLine("PortraitsIds.Length=" + PortraitIds.Length);			
			foreach(string portraitId in PortraitIds)
				sb.AppendLine(portraitId);		 
			             
			sb.AppendLine("Tracks.Length=" + Tracks.Length);
			foreach(Track t in Tracks)
				sb.AppendLine(t.ToString());			
			sb.AppendLine("SimilarArtists.Length=" + SimilarArtists.Length);
			foreach(Artist a in SimilarArtists)
				sb.AppendLine(a.ToString());
			
			sb.AppendFormat("Biography:{0}{1}", Environment.NewLine, Biography);
			
			return sb.ToString();
		}

		
		#endregion

	}		
}
