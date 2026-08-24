/*!
	Copyright (C) 2008-2026 Kody Brown (kody@bricksoft.com).

	MIT License:

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to
	deal in the Software without restriction, including without limitation the
	rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
	sell copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in
	all copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
	FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
	DEALINGS IN THE SOFTWARE.
*/

namespace PowerCode;

using System;
using System.IO;
using System.Text;

public static class EncodingHelper
{
  public static Encoding ConvertEncoding( string text )
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(text);
    var name = text.Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    switch (name) {
      case "ascii":
        return Encoding.ASCII;
      case "ansi":
      case "windows1252":
      case "win1252":
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
      case "utf32":
        return new UTF32Encoding(false, false);
      case "utf32bom":
        return new UTF32Encoding(false, true);
      case "utf7":
#pragma warning disable SYSLIB0001
        return Encoding.UTF7;
#pragma warning restore SYSLIB0001
      case "utf8":
        return new UTF8Encoding(false);
      case "utf8bom":
        return new UTF8Encoding(true);
      case "unicode":
      case "utf16":
        return Encoding.Unicode;
      case "os":
      case "default":
        return Encoding.Default;
      default:
        throw new ArgumentException($"Invalid or unknown encoding specified '{text}'.", nameof(text));
    }
  }

  public static Encoding? DetectEncoding( string fileName, Encoding? defaultEncoding = null )
  {
    using var stream = File.OpenRead(fileName);
    return DetectEncoding(stream, defaultEncoding);
  }

  /// <summary>
  /// UTF8    : EF BB BF
  /// UTF16 BE: FE FF
  /// UTF16 LE: FF FE
  /// UTF32 BE: 00 00 FE FF
  /// UTF32 LE: FF FE 00 00
  /// </summary>
  /// <remarks>
  /// https://stackoverflow.com/a/40511863/139793
  /// </remarks>
  public static Encoding? DetectEncoding( this Stream stream, Encoding? defaultEncoding = null )
  {
    if (!stream.CanSeek || !stream.CanRead) {
      throw new Exception("DetectEncoding() requires a seekable and readable Stream");
    }

    // Try to read 4 bytes. If the stream is shorter, less bytes will be read.
    var u8_Buf = new byte[4];
    var s32_Count = stream.Read(u8_Buf, 0, 4);

    if (s32_Count >= 2) {
      if (u8_Buf[0] == 0xFE && u8_Buf[1] == 0xFF) {
        stream.Position = 2;
        return new UnicodeEncoding(true, true);
      }

      if (u8_Buf[0] == 0xFF && u8_Buf[1] == 0xFE) {
        if (s32_Count >= 4 && u8_Buf[2] == 0 && u8_Buf[3] == 0) {
          stream.Position = 4;
          return new UTF32Encoding(false, true);
        } else {
          stream.Position = 2;
          return new UnicodeEncoding(false, true);
        }
      }

      if (s32_Count >= 3 && u8_Buf[0] == 0xEF && u8_Buf[1] == 0xBB && u8_Buf[2] == 0xBF) {
        stream.Position = 3;
        return Encoding.UTF8;
      }

      if (s32_Count >= 4 && u8_Buf[0] == 0 && u8_Buf[1] == 0 && u8_Buf[2] == 0xFE && u8_Buf[3] == 0xFF) {
        stream.Position = 4;
        return new UTF32Encoding(true, true);
      }
    }

    stream.Position = 0;
    return defaultEncoding;
  }
}
