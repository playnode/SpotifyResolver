LibSpotifySharp-Resolver
========================

Playdar resolver for Spotify.

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


Note
----

This resolver can only be used with a Spotify Premium accounts. You will need
also need a Spotify API key, which is available from the 
[Spotify Developers site](http://developer.spotify.com/).


Compiling
---------

Pre-requisites:

  * Common Language Runtime: Mono 2 on Mac & Linux, or .NET 2 on Windows.
  * IDE: MonoDevelop on Mac & Linux, or Visual Studio 2005 or newer on Windows.
  * libspotify for your platform, downloaded from the Spotify Developers site.
  * lame encoder for your platform.

This resolver is compiled using MonoDevelop on Mac and Linux, and Visual Studio
2005 or newer on Windows.

You need your own API key for this resolver to work. The API key should be
copied into "#region Key" in the Spotify.cs source file.

It's a good idea to obfuscate the build product, if you are distributing the
resolver, to avoid anyone easily obtaining your API key using a disassembler.


Configure & Run
---------------

First check you have mono on the path:

    mono --version

This should give you version information for the Mono compiler. Otherwise you
need to fix that first.

The resolver will look for a configuration file called `spotify.conf` using the
path defined by `PLAYDAR_ETC` and containing the following:

    [spotify]
    username=your_username
    password=your_password

The `bin` folder contains scripts that can be copied into the build output dir
to wrap the resolver and provide the `PLAYDAR_ETC` environment variable.

Test the resolver on the command-line to check it gives JSON resolver info, 
something like the following:

    a{"_msgtype":"settings","name":"Spotify Resolver","weight":90,"targettime":15000,"localonly":true}

If it doesn't provide this then either the login failed or there is another
issue preventing it from starting. Check the log file (written when using the
debug build) which is written to `SpotifyResolver.log`.

If you do get the JSON resolver info above it's ready to test using Playdar.

Check that you also get info from running `lame` which is also required.

Finally, the following files are typical in the product for each platform. You
might compile a release build and remove `log4net.dll`, log and debug files.

Mac OS

  * Newtonsoft.Json.Net20.dll
  * SpotifyResolver.exe*
  * SpotifyResolver.exe.config
  * SpotifyResolver.exe.mdb
  * SpotifyResolver.log
  * SpotifyResolver.sh*
  * lame*
  * libspotify
  * log4net.dll

Linux

  * Newtonsoft.Json.Net20.dll
  * SpotifyResolver.exe*
  * SpotifyResolver.exe.config
  * SpotifyResolver.exe.mdb
  * SpotifyResolver.log
  * SpotifyResolver.sh*
  * libspotify.so -> libspotify.so.4
  * libspotify.so.4 -> libspotify.so.4.0.400076
  * libspotify.so.4.0.400076
  * log4net.dll

*Assuming here that lame is already available somewhere on the system path.*

Windows

  * Newtonsoft.Json.Net20.dll*
  * SpotifyResolver.exe*
  * SpotifyResolver.exe.config*
  * SpotifyResolver.log*
  * SpotifyResolver.pdb*
  * lame.exe*
  * libspotify.dll*
  * log4net.dll*

*In all of the above, filenames appended with `*` are executable.*
*Those with `->` are symbolic links.*

Hopefully that helps get you up & running.