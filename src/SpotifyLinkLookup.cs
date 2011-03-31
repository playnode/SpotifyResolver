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
using System.Collections.Generic;

namespace Stever.PlaySpot
{
    class SpotifyLinkLookup
    {
        readonly Dictionary<string, string> _lookupTable;
        readonly Queue<string> _removalQueue;

        const int MaxQueueSize = 10000;

        internal SpotifyLinkLookup()
        {
            _lookupTable = new Dictionary<string, string>();
            _removalQueue = new Queue<string>();
        }

        internal string NewGuid(string link)
        {
            // Limit number of GUIDs listed in table.
            while (_removalQueue.Count >= MaxQueueSize - 1)
            {
                string guid = _removalQueue.Dequeue();
                _lookupTable.Remove(guid);
            }

            // Add a new GUID to the lookup table.
            string newGuid = Guid.NewGuid().ToString().ToUpper();
            _lookupTable.Add(newGuid, link);

            // Add the new GUID to the queue to be removed.
            _removalQueue.Enqueue(newGuid);

            // Return the new GUID.
            return newGuid;
        }

        internal string FindLink(string guid)
        {
            string result = null;
            if (_lookupTable.ContainsKey(guid))
                result = _lookupTable[guid];
            return result;
        }
    }
}
