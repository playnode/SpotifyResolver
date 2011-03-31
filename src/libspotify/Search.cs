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
    internal class Search : IDisposable
    {
        #region Declarations
		
		internal IntPtr searchPtr = IntPtr.Zero;

        private sp_error error;
        private Track[] tracks;
        private Album[] albums;
        private Artist[] artists;
        private string query;
        private string didYouMean;
        private int totalTracks;

        #endregion

        #region Ctor

        internal Search(IntPtr searchPtr)
        {
            if (searchPtr == IntPtr.Zero)
                throw new ArgumentException("searchPtr can not be zero");

			this.searchPtr = searchPtr;
			
            lock (libspotify.Mutex)
            {
                IntPtr strPtr = IntPtr.Zero;

                error = libspotify.sp_search_error(searchPtr);

                tracks = new Track[libspotify.sp_search_num_tracks(searchPtr)];
                for (int i = 0; i < tracks.Length; i++)
                {
                    IntPtr trackPtr = libspotify.sp_search_track(searchPtr, i);
                    tracks[i] = new Track(trackPtr);
                }

                albums = new Album[libspotify.sp_search_num_albums(searchPtr)];
                for (int i = 0; i < albums.Length; i++)
                {
                    IntPtr albumPtr = libspotify.sp_search_album(searchPtr, i);
                    albums[i] = new Album(albumPtr);
                }

                artists = new Artist[libspotify.sp_search_num_artists(searchPtr)];
                for (int i = 0; i < artists.Length; i++)
                {
                    IntPtr artistPtr = libspotify.sp_search_artist(searchPtr, i);
                    artists[i] = new Artist(artistPtr);
                }

                strPtr = libspotify.sp_search_query(searchPtr);
                query = libspotify.GetString(strPtr, string.Empty);

                strPtr = libspotify.sp_search_did_you_mean(searchPtr);
                didYouMean = libspotify.GetString(strPtr, string.Empty);

                totalTracks = libspotify.sp_search_total_tracks(searchPtr);                
            }
        }

        #endregion

        #region Properties

        internal sp_error Error
        {
            get
            {
				CheckDisposed(true);
                return error;
            }
        }

        internal Track[] Tracks
        {
            get
            {
				CheckDisposed(true);
                return tracks;
            }
        }

        internal Album[] Albums
        {
            get
            {
				CheckDisposed(true);
                return albums;
            }
        }

        internal Artist[] Artists
        {
            get
            {
				CheckDisposed(true);
                return artists;
            }
        }

        internal string Query
        {
            get
            {
				CheckDisposed(true);
                return query;
            }
        }

        internal string DidYouMean
        {
            get
            {
				CheckDisposed(true);
                return didYouMean;
            }
        }

        internal int TotalTracks
        {
            get
            {
				CheckDisposed(true);
                return totalTracks;
            }
        }

        #endregion

        #region internal methods
		
		internal Link CreateLink()
		{
			CheckDisposed(true);
			
			lock(libspotify.Mutex)
			{
				IntPtr linkPtr = libspotify.sp_link_create_from_search(searchPtr);
				if(linkPtr != IntPtr.Zero)
					return new Link(linkPtr);
				else
					return null;
			}
		}

        public override string ToString()
        {
			CheckDisposed(true);
			
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[Search]");
            sb.AppendLine("Error=" + Error);
            sb.AppendLine("Tracks.Length=" + Tracks.Length);
            sb.AppendLine("Albums.Length=" + Albums.Length);
            sb.AppendLine("Artists.Length=" + Artists.Length);
            sb.AppendLine("Query=" + Query);
            sb.AppendLine("DidYouMean=" + DidYouMean);
            sb.AppendLine("TotalTracks=" + TotalTracks);            

            return sb.ToString();
        }

        #endregion
		
		#region Cleanup
		
		~Search()
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
				
				if(searchPtr != IntPtr.Zero)
				{
					
					libspotify.sp_search_release(searchPtr);
					searchPtr = IntPtr.Zero;
				}			
			}
			catch
			{
				
			}
		}

        public void Dispose()
		{
			if(searchPtr == IntPtr.Zero)
				return;
			
			Dispose(true);
       		GC.SuppressFinalize(this);			
		}
		
		private bool CheckDisposed(bool throwOnDisposed)
		{
			lock(libspotify.Mutex)
			{
				bool result = searchPtr == IntPtr.Zero;
				if(result && throwOnDisposed)
					throw new ObjectDisposedException("Search");
				
				return result;
			}
		}
		
		#endregion

    }
}
