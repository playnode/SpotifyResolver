/*
Copyright (c) 2011 Steven Robertson, steverobertson@gmail.com

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
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

#if DEBUG
using System.Reflection;
using log4net;
#endif

namespace Stever.PlaySpot.json
{
    internal static class JsonMsgUtil
    {
#if DEBUG
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().ReflectedType);
#endif

        /// <summary>
        /// Extract JSON string from the next message on the input stream.
        /// </summary>
        /// <returns>Message from input stream.</returns>
        internal static string ExtractJsonInput(Stream inputStream)
        {
            // Read first 4 bytes for message length.
            byte[] lenBytes = ReadBytes(4, inputStream);
            byte[] bigendian = new byte[4];
            bigendian[0] = lenBytes[3];
            bigendian[1] = lenBytes[2];
            bigendian[2] = lenBytes[1];
            bigendian[3] = lenBytes[0];
            int len = BitConverter.ToInt32(bigendian, 0);
            if (len == 0 || len == -1) return null; // Invalid values.
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Extracting length: " + len);
#endif

            // Read the content of the message.
            byte[] bytes = ReadBytes(len, inputStream);
            string content = Encoding.UTF8.GetString(bytes);
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Extracted JSON: " + content);
#endif

            return content;
        }

        static byte[] ReadBytes(int n, Stream inputStream)
        {
            byte[] bytes = new byte[n];
            for (int i = 0; i < n; i++)
                bytes[i] = (byte) inputStream.ReadByte();
            return bytes;
        }

        internal static byte[] WrapJsonOutput(IJson json)
        {
            return WrapJsonOutput(json.ToJson());
        }

        internal static byte[] WrapJsonOutput(JObject json)
        {
            return WrapJsonOutput(json.ToString());
        }

        internal static byte[] WrapJsonOutput(string json)
        {
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Wrapping JSON: " + json);
#endif
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            int len = jsonBytes.Length;
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Wrapped length: " + len);
#endif
            byte[] lenBytes = BitConverter.GetBytes(len);
            byte[] bytes = new byte[4 + jsonBytes.Length];
            bytes[0] = lenBytes[3];
            bytes[1] = lenBytes[2];
            bytes[2] = lenBytes[1];
            bytes[3] = lenBytes[0];
            int i = 4;
            foreach (byte b in jsonBytes) bytes[i++] = b;
            return bytes;
        }
    }
}
