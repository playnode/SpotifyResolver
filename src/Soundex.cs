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
using System.Text;

namespace Stever.PlaySpot
{
    /// <summary>
    /// Utility class for performing soundex algorithm.
    ///
    /// The Soundex algorithm is used to convert a word to a
    /// code based upon the phonetic sound of the word.
    ///
    /// The soundex algorithm is outlined below:
    /// Rule 1. Keep the first character of the name.
    /// Rule 2. Perform a transformation on each remaining characters:
    /// A,E,I,O,U,Y = A
    /// H,W = S
    /// B,F,P,V = 1
    /// C,G,J,K,Q,S,X,Z = 2
    /// D,T = 3
    /// L = 4
    /// M,N = 5
    /// R = 6
    /// Rule 3. If a character is the same as the previous, do not include in the code.
    /// Rule 4. If character is "A" or "S" do not include in the code.
    /// Rule 5. If a character is blank, then do not include in the code.
    /// Rule 6. A soundex code must be exactly 4 characters long. If the
    /// code is too short then pad with zeros, otherwise truncate.
    ///
    /// Jeff Guitard
    /// October 2002
    /// </summary>
    internal class Soundex
    {

        private Soundex()
        {
        }

        /// <summary>
        /// Return the soundex code for a given string.
        /// </summary>
        internal static String ToSoundexCode(String aString)
        {
            string word = aString.ToUpper();
            StringBuilder soundexCode = new StringBuilder();
            int wordLength = word.Length;

            // Rule 1. Keep the first character of the word
            soundexCode.Append(word.Substring(0, 1));

            // Rule 2. Perform a transformation on each remaining characters
            for (int i = 1; i < wordLength; i++)
            {
                string transformedChar = Transform(word.Substring(i, 1));

                // Rule 3. If a character is the same as the previous, do not include in code
                if (!transformedChar.Equals(soundexCode.ToString().Substring(soundexCode.Length - 1)))
                {

                    // Rule 4. If character is "A" or "S" do not include in code
                    if (!transformedChar.Equals("A") && !transformedChar.Equals("S"))
                    {

                        // Rule 5. If a character is blank, then do not include in code
                        if (!transformedChar.Equals(" "))
                        {
                            soundexCode.Append(transformedChar);
                        }
                    }
                }
            }

            // Rule 6. A soundex code must be exactly 4 characters long. If the
            // code is too short then pad with zeros, otherwise truncate.
            soundexCode.Append("0000");

            return soundexCode.ToString().Substring(0, 4);
        }

        /// <summary>
        /// Transform the A-Z alphabetic characters to the appropriate soundex code.
        /// </summary>
        private static String Transform(String aString)
        {

            switch (aString)
            {
                case "A":
                case "E":
                case "I":
                case "O":
                case "U":
                case "Y":
                    return "A";
                case "H":
                case "W":
                    return "S";
                case "B":
                case "F":
                case "P":
                case "V":
                    return "1";
                case "C":
                case "G":
                case "J":
                case "K":
                case "Q":
                case "S":
                case "X":
                case "Z":
                    return "2";
                case "D":
                case "T":
                    return "3";
                case "L":
                    return "4";
                case "M":
                case "N":
                    return "5";
                case "R":
                    return "6";
            }

            return " ";
        }
    }
}
