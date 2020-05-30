//
// Copyright (C) 2006-2016 Kody Brown (kody@bricksoft.com).
//
// MIT License:
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;
using System.Text;

public class EncodingType
{
    public static System.Text.Encoding GetType( string FILE_NAME )
    {
        FileStream fs = new FileStream(FILE_NAME, FileMode.Open, FileAccess.Read);
        Encoding r = GetType(fs);
        fs.Close();
        return r;
    }

    public static System.Text.Encoding GetType( FileStream fs )
    {
        byte[] Unicode = new byte[] { 0xFF, 0xFE, 0x41 };
        byte[] UnicodeBIG = new byte[] { 0xFE, 0xFF, 0x00 };
        byte[] UTF8 = new byte[] { 0xEF, 0xBB, 0xBF }; //with BOM

        Encoding reVal;
        byte[] ss;
        int i;

        using (BinaryReader r = new BinaryReader(fs, System.Text.Encoding.Default)) {
            int.TryParse(fs.Length.ToString(), out i);
            ss = r.ReadBytes(i);

            if (IsUTF8Bytes(ss) || (ss[0] == 0xEF && ss[1] == 0xBB && ss[2] == 0xBF)) {
                reVal = Encoding.UTF8;
            } else if (ss[0] == 0xFE && ss[1] == 0xFF && ss[2] == 0x00) {
                reVal = Encoding.BigEndianUnicode;
            } else if (ss[0] == 0xFF && ss[1] == 0xFE && ss[2] == 0x41) {
                reVal = Encoding.Unicode;
            } else {
                reVal = Encoding.Default;
            }

            r.Close();
        }

        return reVal;
    }

    private static bool IsUTF8Bytes( byte[] data )
    {
        int charByteCounter = 1;
        byte curByte;
        for (int i = 0; i < data.Length; i++) {
            curByte = data[i];
            if (charByteCounter == 1) {
                if (curByte >= 0x80) {
                    while (((curByte <<= 1) & 0x80) != 0) {
                        charByteCounter++;
                    }

                    if (charByteCounter == 1 || charByteCounter > 6) {
                        return false;
                    }
                }
            } else {
                if ((curByte & 0xC0) != 0x80) {
                    return false;
                }
                charByteCounter--;
            }
        }
        if (charByteCounter > 1) {
            throw new Exception("Error byte format");
        }
        return true;
    }
}
