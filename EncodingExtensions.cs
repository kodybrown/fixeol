using System;
using System.IO;
using System.Text;

namespace Bricksoft.PowerCode
{
	public static class EncodingExtensions
	{
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
		public static Encoding DetectEncoding( this Stream stream )
		{
			if (!stream.CanSeek || !stream.CanRead) {
				throw new Exception("DetectEncoding() requires a seekable and readable Stream");
			}

			// Try to read 4 bytes. If the stream is shorter, less bytes will be read.
			Byte[] u8_Buf = new Byte[4];
			int s32_Count = stream.Read(u8_Buf, 0, 4);

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
			return Encoding.Default;
		}
	}
}
