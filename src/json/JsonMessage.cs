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
using Newtonsoft.Json;

namespace Stever.PlaySpot.json
{
    /// <summary>
    /// This generic base class provides methods to serialise and deserialise
    /// a JSON string to object and back, and its class name.
    /// </summary>
    /// <typeparam name="T">The class for the JSON object.</typeparam>
    internal abstract class JsonMessage<T> where T : new()
    {
        internal string ClassName
        {
            get
            {
                Type type = typeof(T);
                return type.Name;
            }
        }

        internal static T CreateFrom(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        internal string ToJson()
        {
            return JsonConvert.SerializeObject(this).TrimEnd();
        }
    }
}
