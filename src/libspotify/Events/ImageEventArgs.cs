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
using System.Drawing;

namespace Spotify
{
    internal class ImageEventArgs : EventArgs
	{
		private Bitmap image = null;
		private string error = string.Empty;
		private object state;
		private string id = string.Empty;

        internal ImageEventArgs(Bitmap image, string id, object state)
		{
			this.state = state;
			this.image = image;
			this.id = id;
		}
		
		internal ImageEventArgs(string error, string id, object state)
		{
			this.state = state;
			this.error = error;
			this.id = id;
		}
		
		internal object State
		{
			get
			{
				return state;
			}
		}
		
		internal Bitmap Image
		{
			get
			{
				return image;
			}
		}
		
		internal string Error
		{
			get
			{
				return error;
			}
		}
		
		internal string Id
		{
			get
			{
				return id;
			}
		}
	}
}
