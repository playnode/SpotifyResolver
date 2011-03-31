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
	internal class User
	{
		private IntPtr userPtr = IntPtr.Zero;
		
		internal User(IntPtr userPtr)
		{
			this.userPtr = userPtr;			
		}
		
		internal string CanonicalName
		{
			get
			{
				if(userPtr == IntPtr.Zero)
					return "Unknown";
				else
				{
					lock(libspotify.Mutex)
					{
                        return libspotify.GetString(libspotify.sp_user_canonical_name(userPtr), "Unknown");
					}
				}
			}
		}
		
		internal string DisplayName
		{
			get
			{
				if(userPtr == IntPtr.Zero)
					return "Unknown";
				else
				{
					lock(libspotify.Mutex)
					{
                        return libspotify.GetString(libspotify.sp_user_display_name(userPtr), "Unknown");
					}
				}
			}
		}
		
		internal bool IsLoaded
		{
			get
			{
				if(userPtr == IntPtr.Zero)
					return false;
				else
				{
					lock(libspotify.Mutex)
						return libspotify.sp_user_is_loaded(userPtr);
				}
			}
		}
		
		public override string ToString()
		{
			return string.Format("[User: CanonicalName={0}, DisplayName={1}, IsLoaded={2}]", CanonicalName, DisplayName, IsLoaded);
		}

	}
}
