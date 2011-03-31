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

namespace Spotify
{
	internal class TracksEventArgs
	{
		private Track[] tracks;
		private int[] trackIndices;
		private int position;
		private int newPosition;
		private Track[] currentTracks;
		
		internal TracksEventArgs(Track[] tracks, int[] trackIndices, int position, int newPosition, Track[] currentTracks)
		{
			this.tracks = tracks;
			this.trackIndices = trackIndices;
			this.position = position;
			this.newPosition = newPosition;
			this.currentTracks = currentTracks;
		}
		
		internal Track[] Tracks
		{
			get
			{
				return tracks;
			}
		}
		
		internal int[] TrackIndices
		{
			get
			{
				return trackIndices;
			}
		}
		
		internal int Position
		{
			get
			{
				return position;
			}
		}
		
		internal int NewPosition
		{
			get
			{
				return newPosition;
			}
		}
		
		internal Track[] CurrentTracks
		{
			get
			{
				return currentTracks;
			}
		}		
	}
}
