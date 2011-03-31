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

using System.Collections.Generic;
using System.IO;
using System.Text;

#if DEBUG
using System.Reflection;
using log4net;
#endif

namespace Stever.PlaySpot
{
    public class SimplePropertiesFile
    {
#if DEBUG
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().ReflectedType);
#endif

        Dictionary<string, Dictionary<string, string>> _sections;

        public Dictionary<string, Dictionary<string, string>> Sections
        {
            get { return _sections; }
            set { _sections = value; }
        }

        readonly Dictionary<string, string> _section;
        readonly string _filename;

        public SimplePropertiesFile(string filename)
        {
            _filename = filename;
#if DEBUG
            if (Log.IsDebugEnabled) Log.Debug("Filename = " + _filename);
#endif
            Sections = new Dictionary<string, Dictionary<string, string>>();

            if (!File.Exists(_filename)) return;

            foreach (string row in File.ReadAllLines(_filename))
            {
                if (row.Length <= 0) continue;
                bool skip = false;
                switch (row[0])
                {
                    case '[':
                        string section = row.Substring(1, row.IndexOf(']') - 1);
#if DEBUG
                        if (Log.IsDebugEnabled) Log.Debug("Section name = " + section);
#endif
                        _section = new Dictionary<string, string>();
                        Sections.Add(section, _section);
                        skip = true;
                        break;
                    case '#':
                    case ';':
#if DEBUG
                        if (Log.IsDebugEnabled) Log.Debug("Ignoring: " + row);
#endif
                        skip = true;
                        break;
                }
                if (skip) continue;
                if (_section == null)
                {
                    _section = new Dictionary<string, string>();
                    Sections.Add("undefined", _section);
                }
                string[] spit = row.Split('=');
                string name = spit[0].Trim();
                string value = spit[1].Trim();
#if DEBUG
                if (Log.IsDebugEnabled) Log.Debug(name + " = " + value);
#endif
                _section.Add(name, value);
            }
        }

        public void Save()
        {
            StringBuilder str = new StringBuilder();
            foreach (KeyValuePair<string, Dictionary<string, string>> section in Sections)
            {
                str.Append('[').Append(section.Key).Append("]\n");
                foreach (KeyValuePair<string, string> p in section.Value)
                    str.Append(p.Key).Append(" = ").Append(p.Value).Append('\n');
            }

            // Write the configuration file.
            if (File.Exists(_filename)) File.Delete(_filename);
            FileStream file = new FileStream(_filename, FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(file);
            sw.Write(str.ToString());
            sw.Close();
            file.Close();
        }
    }
}
