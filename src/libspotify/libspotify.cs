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
	internal static class libspotify
	{	
		internal static object Mutex = new object();        
		
		#region Constants
	
		internal const int SPOTIFY_API_VERSION = 4;
	
		#endregion

        #region Strings

        internal static string GetString(IntPtr ptr, string defaultValue)
        {
            if (ptr == IntPtr.Zero)
                return defaultValue;

            System.Collections.Generic.List<byte> l = new System.Collections.Generic.List<byte>();            
            byte read = 0;
            do
            {
                read = Marshal.ReadByte(ptr, l.Count);                
                l.Add(read);
            }
            while (read != 0);

            if (l.Count > 0)
                return System.Text.Encoding.UTF8.GetString(l.ToArray(), 0, l.Count - 1);
            else
                return string.Empty;
        }

        #endregion


        #region Error handling

        [DllImport ("libspotify")]
		internal static extern string sp_error_message(sp_error error);
		
		#endregion
		
		#region Session handling
		
		#region Methods
		
		[DllImport ("libspotify")]
		internal static extern sp_error sp_session_init(ref sp_session_config config, out IntPtr sessionPtr);
		
		[DllImport ("libspotify")]
		internal static extern sp_error sp_session_login(IntPtr sessionPtr, string username, string password);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_session_user(IntPtr sessionPtr);
		
		[DllImport ("libspotify")]
		internal static extern sp_error sp_session_logout(IntPtr sessionPtr);
		
		[DllImport ("libspotify")]
		internal static extern sp_connectionstate sp_session_connectionstate(IntPtr sessionPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_session_userdata(IntPtr sessionPtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_session_process_events(IntPtr sessionPtr, out int next_timeout);
		
		[DllImport ("libspotify")]
		internal static extern sp_error sp_session_player_load(IntPtr sessionPtr, IntPtr trackPtr);
		
		[DllImport ("libspotify")]
		internal static extern sp_error sp_session_player_seek(IntPtr sessionPtr, int offset);
			
		[DllImport ("libspotify")]
		internal static extern sp_error sp_session_player_play(IntPtr sessionPtr, bool play);
		
		[DllImport ("libspotify")]
		internal static extern void sp_session_player_unload(IntPtr sessionPtr);		
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_session_playlistcontainer(IntPtr sessionPtr);

        [DllImport("libspotify")]
        internal static extern void sp_session_preferred_bitrate(IntPtr sessionPtr, sp_bitrate bitrate);

        [DllImport("libspotify")]
        internal static extern IntPtr sp_session_starred_create(IntPtr sessionPtr);
		
		#endregion
	
		#region Structs
		
		internal struct sp_session_config
		{
			internal int api_version;
			internal string cache_location;
			internal string settings_location;
			internal IntPtr application_key;
			internal int application_key_size;
			internal string user_agent;
			internal IntPtr callbacks;
			internal IntPtr userdata;
		}		
		
		internal struct sp_session_callbacks
		{
			internal IntPtr logged_in;
			internal IntPtr logged_out;
			internal IntPtr metadata_updated;
			internal IntPtr connection_error;
			internal IntPtr message_to_user;
			internal IntPtr notify_main_thread;
			internal IntPtr music_delivery;
			internal IntPtr play_token_lost;
			internal IntPtr log_message;
			internal IntPtr end_of_track;
            internal IntPtr streaming_error;
            internal IntPtr userinfo_updated;
		}
		
		internal struct sp_audioformat
		{
			internal int sample_type;
			internal int sample_rate;
			internal int channels;
		}
		
		#endregion
		
		#endregion
		
		#region User handling
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_user_canonical_name(IntPtr userPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_user_display_name(IntPtr userPtr);
		
		[DllImport ("libspotify")]
		[return : MarshalAs(UnmanagedType.U1)]
		internal static extern bool sp_user_is_loaded(IntPtr userPtr);		 
		
		#endregion		
		
		#region Playlist subsystem
		
		#region Methods
		
		// Playlist
		
		[DllImport ("libspotify")]
		internal static extern bool sp_playlist_is_loaded(IntPtr playlistPtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_playlist_add_callbacks(IntPtr playlistPtr, IntPtr callbacksPtr, IntPtr userDataPtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_playlist_remove_callbacks(IntPtr playlistPtr, IntPtr callbacksPtr, IntPtr userDataPtr);
		
		[DllImport ("libspotify")]
		internal static extern int sp_playlist_num_tracks(IntPtr playlistPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_playlist_track(IntPtr playlistPtr, int index);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_playlist_name(IntPtr playlistPtr);
		
		[DllImport ("libspotify")]
		internal static extern sp_error sp_playlist_rename(IntPtr playlistPtr, string new_name);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_playlist_owner(IntPtr playlistPtr);
		
		[DllImport ("libspotify")]
		internal static extern bool sp_playlist_is_collaborative(IntPtr playlistPtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_playlist_set_collaborative(IntPtr playlistPtr, bool collaborative);
		
		[DllImport ("libspotify")]
		internal static extern bool sp_playlist_has_pending_changes(IntPtr playlistPtr);
		
		[DllImport ("libspotify")]
		internal static extern sp_error sp_playlist_add_tracks(IntPtr playlistPtr, IntPtr tracksArrayPtr, int num_tracks, int position, IntPtr sessionPtr);
		
		[DllImport ("libspotify")]
		internal static extern sp_error sp_playlist_remove_tracks(IntPtr playlistPtr, int[] trackIndicies, int num_tracks);
		
		[DllImport ("libspotify")]
		internal static extern sp_error sp_playlist_reorder_tracks(IntPtr playlistPtr, int[] trackIndicies, int num_tracks, int new_position);

        [DllImport ("libspotify")]
        internal static extern IntPtr sp_playlist_create(IntPtr sessionPtr, IntPtr linkPtr);
		
		// Playlist container
		
		[DllImport ("libspotify")]
		internal static extern void sp_playlistcontainer_add_callbacks(IntPtr containerPtr, IntPtr callbacksPtr, IntPtr userDataPtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_playlistcontainer_remove_callbacks(IntPtr containerPtr, IntPtr callbacksPtr, IntPtr userDataPtr);
		
		[DllImport ("libspotify")]
		internal static extern int sp_playlistcontainer_num_playlists(IntPtr containerPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_playlistcontainer_playlist(IntPtr containerPtr, int index);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_playlistcontainer_add_new_playlist(IntPtr containerPtr, string name);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_playlistcontainer_add_playlist(IntPtr containerPtr, IntPtr linkPtr);
		
		[DllImport ("libspotify")]
		internal static extern sp_error sp_playlistcontainer_remove_playlist(IntPtr containerPtr, int index);
		
		[DllImport ("libspotify")]
		internal static extern sp_error sp_playlistcontainer_move_playlist(IntPtr containerPtr, int index, int new_position);
		
	
		#endregion			
		
		#region Structs
		
		internal struct sp_playlist_callbacks
		{
			internal IntPtr tracks_added;
			internal IntPtr tracks_removed;
			internal IntPtr tracks_moved;
			internal IntPtr playlist_renamed;
			internal IntPtr playlist_state_changed;
			internal IntPtr playlist_update_in_progress;
			internal IntPtr playlist_metadata_updated;
		}
		
		internal struct sp_playlistcontainer_callbacks
		{
			internal IntPtr playlist_added;
			internal IntPtr playlist_removed;
			internal IntPtr playlist_moved;
			internal IntPtr container_loaded;
		}
	
		#endregion		
		
		#endregion
		
		#region Track subsystem
		
		[DllImport ("libspotify")]
		internal static extern bool sp_track_is_loaded(IntPtr trackPtr);
		
		[DllImport ("libspotify")]
		internal static extern bool sp_track_is_available(IntPtr trackPtr);
		
		[DllImport ("libspotify")]
		internal static extern sp_error sp_track_error(IntPtr trackPtr);
		
		[DllImport ("libspotify")]
		internal static extern int sp_track_num_artists(IntPtr trackPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_track_artist(IntPtr trackPtr, int index);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_track_album(IntPtr trackPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_track_name(IntPtr trackPtr);
		
		[DllImport ("libspotify")]
		internal static extern int sp_track_duration(IntPtr trackPtr);
		
		[DllImport ("libspotify")]
		internal static extern int sp_track_popularity(IntPtr trackPtr);
		
		[DllImport ("libspotify")]
		internal static extern int sp_track_disc(IntPtr trackPtr);
		
		[DllImport ("libspotify")]
		internal static extern int sp_track_index(IntPtr trackPtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_track_add_ref(IntPtr trackPtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_track_release(IntPtr trackPtr);

        [DllImport("libspotify")]
        internal static extern bool sp_track_is_starred(IntPtr trackPtr);

        [DllImport("libspotify")]
        internal static extern void sp_track_set_starred(IntPtr sessionPtr, IntPtr tracksArrayPtr, int num_tracks, bool star);
		
		#endregion
		
		#region Album subsystem
		
		[DllImport ("libspotify")]
		internal static extern bool sp_album_is_loaded(IntPtr albumPtr);
		
		[DllImport ("libspotify")]
		internal static extern bool sp_album_is_available(IntPtr albumPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_album_artist(IntPtr albumPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_album_cover(IntPtr albumPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_album_name(IntPtr albumPtr);
		
		[DllImport ("libspotify")]
		internal static extern int sp_album_year(IntPtr albumPtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_album_add_ref(IntPtr albumPtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_album_release(IntPtr albumPtr);
		
		[DllImport ("libspotify")]
		internal static extern sp_albumtype sp_album_type(IntPtr albumPtr);
		
		#endregion
		
		#region Artist subsystem
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_artist_name(IntPtr artistPtr);
		
		[DllImport ("libspotify")]
		internal static extern bool sp_artist_is_loaded(IntPtr artistPtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_artist_add_ref(IntPtr artistPtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_artist_release(IntPtr artistPtr);
		
		#endregion
		
		#region Links (Spotify URIs)
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_link_create_from_string(string link);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_link_create_from_track(IntPtr trackPtr, int offset);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_link_create_from_album(IntPtr albumPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_link_create_from_artist(IntPtr artistPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_link_create_from_search(IntPtr searchPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_link_create_from_playlist(IntPtr playlistPtr);
		
		[DllImport ("libspotify")]
		internal static extern int sp_link_as_string(IntPtr linkPtr, IntPtr bufferPtr, int buffer_size);
		
		[DllImport ("libspotify")]
		internal static extern sp_linktype sp_link_type(IntPtr linkPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_link_as_track(IntPtr linkPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_link_as_track_and_offset(IntPtr linkPtr, out IntPtr offsetPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_link_as_album(IntPtr linkPtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_link_as_artist(IntPtr linkPtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_link_add_ref(IntPtr linkPtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_link_release(IntPtr linkPtr);
		
		#endregion
		
		#region Album browsing
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_albumbrowse_create(IntPtr sessionPtr, IntPtr albumPtr, IntPtr callbackPtr, IntPtr userDataPtr);
		
		[DllImport ("libspotify")]
		internal static extern bool	sp_albumbrowse_is_loaded(IntPtr albumBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern sp_error sp_albumbrowse_error(IntPtr albumBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_albumbrowse_album(IntPtr albumBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_albumbrowse_artist(IntPtr albumBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern int sp_albumbrowse_num_copyrights(IntPtr albumBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_albumbrowse_copyright(IntPtr albumBrowsePtr, int index);
		
		[DllImport ("libspotify")]
		internal static extern int sp_albumbrowse_num_tracks(IntPtr albumBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_albumbrowse_track(IntPtr albumBrowsePtr, int index);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_albumbrowse_review(IntPtr albumBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_albumbrowse_add_ref(IntPtr albumBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_albumbrowse_release(IntPtr albumBrowsePtr);
		
		#endregion
		
		#region Artist browsing
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_artistbrowse_create(IntPtr sessionPtr, IntPtr artistPtr, IntPtr callbackPtr, IntPtr userDataPtr);
		
		[DllImport ("libspotify")]
		internal static extern bool	sp_artistbrowse_is_loaded(IntPtr artistBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern sp_error sp_artistbrowse_error(IntPtr artistBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_artistbrowse_artist(IntPtr artistBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern int sp_artistbrowse_num_portraits(IntPtr artistBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_artistbrowse_portrait(IntPtr artistBrowsePtr, int index);
		
		[DllImport ("libspotify")]
		internal static extern int sp_artistbrowse_num_tracks(IntPtr artistBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_artistbrowse_track(IntPtr artistBrowsePtr, int index);
		
		[DllImport ("libspotify")]
		internal static extern int sp_artistbrowse_num_albums(IntPtr artistBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_artistbrowse_album(IntPtr artistBrowsePtr, int index);
		
		[DllImport ("libspotify")]
		internal static extern int sp_artistbrowse_num_similar_artists(IntPtr artistBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_artistbrowse_similar_artist(IntPtr artistBrowsePtr, int index);
		
		[DllImport ("libspotify")]
		internal static extern IntPtr sp_artistbrowse_biography(IntPtr artistBrowsePtr);		
		
		[DllImport ("libspotify")]
		internal static extern void sp_artistbrowse_add_ref(IntPtr artistBrowsePtr);
		
		[DllImport ("libspotify")]
		internal static extern void sp_artistbrowse_release(IntPtr artistBrowsePtr);		
		
		
		#endregion
		
        #region Search subsystem

        [DllImport("libspotify")]
        internal static extern IntPtr sp_search_create(IntPtr sessionPtr, string query, int track_offset, int track_count, 
		                                               int album_offset, int album_count, int artist_offset, int artist_count,
		                                               IntPtr callbackPtr, IntPtr userDataPtr);

        [DllImport("libspotify")]
        internal static extern IntPtr sp_radio_search_create(IntPtr sessionPtr, int from_year, int to_year, sp_radio_genre genres,
                                                        IntPtr callbackPtr, IntPtr userDataPtr);

        [DllImport("libspotify")]
        internal static extern bool sp_search_is_loaded(IntPtr searchPtr);

        [DllImport("libspotify")]
        internal static extern sp_error sp_search_error(IntPtr searchPtr);

        [DllImport("libspotify")]
        internal static extern int sp_search_num_tracks(IntPtr searchPtr);

        [DllImport("libspotify")]
        internal static extern IntPtr sp_search_track(IntPtr searchPtr, int index);

        [DllImport("libspotify")]
        internal static extern int sp_search_num_albums(IntPtr searchPtr);

        [DllImport("libspotify")]
        internal static extern IntPtr sp_search_album(IntPtr searchPtr, int index);

        [DllImport("libspotify")]
        internal static extern int sp_search_num_artists(IntPtr searchPtr);

        [DllImport("libspotify")]
        internal static extern IntPtr sp_search_artist(IntPtr searchPtr, int index);

        [DllImport("libspotify")]
        internal static extern IntPtr sp_search_query(IntPtr searchPtr);

        [DllImport("libspotify")]
        internal static extern IntPtr sp_search_did_you_mean(IntPtr searchPtr);
        
        [DllImport("libspotify")]
        internal static extern int sp_search_total_tracks(IntPtr searchPtr);

        [DllImport("libspotify")]
        internal static extern void sp_search_add_ref(IntPtr searchPtr);

        [DllImport("libspotify")]
        internal static extern void sp_search_release(IntPtr searchPtr);

        #endregion
		
		#region Image subsystem
		
		[DllImport("libspotify")]
        internal static extern IntPtr sp_image_create(IntPtr sessionPtr, IntPtr idPtr);
		
		[DllImport("libspotify")]
		internal static extern void sp_image_add_load_callback(IntPtr imagePtr, IntPtr callbackPtr, IntPtr userDataPtr);
		
		[DllImport("libspotify")]
		internal static extern void sp_image_remove_load_callback(IntPtr imagePtr, IntPtr callbackPtr, IntPtr userDataPtr);
		
		[DllImport("libspotify")]
		internal static extern bool sp_image_is_loaded(IntPtr imagePtr);
		
		[DllImport("libspotify")]
		internal static extern sp_error sp_image_error(IntPtr imagePtr);
		
		[DllImport("libspotify")]
		internal static extern sp_imageformat sp_image_format(IntPtr imagePtr);
		
		[DllImport("libspotify")]		
		internal static extern IntPtr sp_image_data(IntPtr imagePtr, out IntPtr sizePtr);
		
		[DllImport("libspotify")]		
		internal static extern IntPtr sp_image_image_id(IntPtr imagePtr);
		
		[DllImport("libspotify")]
		internal static extern void sp_image_add_ref(IntPtr imagePtr);
		
		[DllImport("libspotify")]
		internal static extern void sp_image_release(IntPtr imagePtr);
		
		// strings are nicer than byte[]
		internal static string ImageIdToString(byte[] id)
		{
			if(id == null)
				return string.Empty;
			
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			foreach(byte b in id)
				sb.Append(b.ToString("X2"));
			
			return sb.ToString();
		}
		
		internal static string ImageIdToString(IntPtr idPtr)
		{
			if(idPtr == IntPtr.Zero)
				return string.Empty;
			
			byte[] id = new byte[20];
			Marshal.Copy(idPtr, id, 0, 20);
			
			return ImageIdToString(id);
		}
		
		internal static byte[] StringToImageId(string id)
		{
			if(string.IsNullOrEmpty(id) || id.Length != 40)
				return null;
			try
			{
				byte[] result = new byte[20];
				for(int i = 0; i < 20 ; i++)
				{
					result[i] = byte.Parse(id.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
				}
				
				return result;
			}
			catch
			{
			}
			return null;
		}
		
		#endregion

    }	
	
	#region Enums
	
	internal enum sp_error
	{
		OK = 0,
		BAD_API_VERSION = 1,
		API_INITIALIZATION_FAILED = 2,
		TRACK_NOT_PLAYABLE = 3,
		RESOURCE_NOT_LOADED = 4,
		APPLICATION_KEY = 5,
		BAD_USERNAME_OR_PASSWORD = 6,
		USER_BANNED = 7,
		UNABLE_TO_CONTACT_SERVER = 8,
		CLIENT_TOO_OLD = 9,
		OTHER_PERMANENT = 10,
		BAD_USER_AGENT = 11,
		MISSING_CALLBACK = 12,
		INVALID_INDATA = 13,
		INDEX_OUT_OF_RANGE = 14,
		USER_NEEDS_PREMIUM = 15,
		OTHER_TRANSIENT = 16,
		IS_LOADING = 17
	}
	
	internal enum sp_connectionstate
	{
		LOGGED_OUT = 0,
		LOGGED_IN = 1,
		DISCONNECTED = 2,
		UNDEFINED = 3
	}
	
	internal enum sp_sampletype
	{
		INT16_NATIVE_ENDIAN = 0
	}
	
	internal enum sp_linktype
	{
		INVALID = 0,
		TRACK = 1,
		ALBUM = 2,
  		ARTIST = 3,
  		SEARCH = 4,
  		PLAYLIST = 5
	}
	
	internal enum sp_imageformat : int
	{
		IMAGE_FORMAT_UNKNOWN,
		IMAGE_FORMAT_JPEG
	}
	
	internal enum sp_albumtype
	{
  		ALBUM = 0,
  		SINGLE = 1,
  		COMPILATION = 2,
  		UNKNOWN = 3
	}

    internal enum sp_bitrate
    {
        BITRATE_160k = 0,
        BITRATE_320k = 1
    }

    internal enum sp_radio_genre
    {
        ALT_POP_ROCK = 0x1,
        BLUES = 0x2,
        COUNTRY = 0x4,
        DISCO = 0x8,
        FUNK = 0x10,
        HARD_ROCK = 0x20,
        HEAVY_METAL = 0x40,
        RAP = 0x80,
        HOUSE = 0x100,
        JAZZ = 0x200,
        NEW_WAVE = 0x400,
        RNB = 0x800,
        POP = 0x1000,
        PUNK = 0x2000,
        REGGAE = 0x4000,
        POP_ROCK = 0x8000,
        SOUL = 0x10000,
        TECHNO = 0x20000
    }

    #endregion
}