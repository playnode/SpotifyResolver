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
	internal class Link : IDisposable
	{
		#region Declarations
		
		internal IntPtr linkPtr = IntPtr.Zero;
		
		#endregion
		
		#region Ctor
		
		internal Link(IntPtr linkPtr)
		{
			if(linkPtr == IntPtr.Zero)
				throw new ArgumentException("linkPtr can not be zero");	
			
			this.linkPtr = linkPtr;
		}
		
		#endregion
		
		#region Static methods
		
		internal static Link Create(string linkString)
		{
			lock(libspotify.Mutex)
			{		
				IntPtr linkPtr = libspotify.sp_link_create_from_string(linkString);			
				if(linkPtr != IntPtr.Zero)
					return new Link(linkPtr);
				else
					return null;
			}
		}		
		
		#endregion
		
		#region Properties
		
		internal sp_linktype LinkType
		{
			get
			{
				CheckDisposed(true);
				lock(libspotify.Mutex)
					return libspotify.sp_link_type(linkPtr);
			}
		}
		
		#endregion
		
		#region internal methods
		
		public override string ToString()
		{
			CheckDisposed(true);
			
			string result = string.Empty;
			int bufSize = 256;
			
			while(true)
			{
				int strlen = bufSize;
				IntPtr bufferPtr = IntPtr.Zero;
				
				try
				{
					bufferPtr = Marshal.AllocHGlobal(bufSize);
					
					lock(libspotify.Mutex)
					{
						strlen = libspotify.sp_link_as_string(linkPtr, bufferPtr, bufSize);	
					}
					
					if(strlen < 0)
					{
						result = "ERROR";
						break;
					}
					else if(strlen < bufSize)
					{
                        result = libspotify.GetString(bufferPtr, string.Empty);
						break;					
					}
					else
					{
						bufSize *=2;
					}
				}
				finally
				{
					if(bufferPtr != IntPtr.Zero)
						Marshal.FreeHGlobal(bufferPtr);
				}		                    
			}
			
			return result;
		}

		#endregion
		
		#region Cleanup
		
		~Link()
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
				
				if(linkPtr != IntPtr.Zero)
				{
					libspotify.sp_link_release(linkPtr);
					linkPtr = IntPtr.Zero;
				}			
			}
			catch
			{
				
			}
		}		
		
		public void Dispose()
		{
			if(linkPtr == IntPtr.Zero)
				return;
			
			Dispose(true);
       		GC.SuppressFinalize(this);			
		}
		
		private bool CheckDisposed(bool throwOnDisposed)
		{
			lock(libspotify.Mutex)
			{
				bool result = linkPtr == IntPtr.Zero;
				if(result && throwOnDisposed)
					throw new ObjectDisposedException("Link");
				
				return result;
			}
		}
		
		#endregion
	}
}
