﻿using System.Collections;
using System;
using System.IO;
using ESC_POS_USB_NET.Interfaces.Command;
//#if NETSTANDARD2_0
using System.Drawing;
//#else
//using System.Drawing;
//#endif

namespace ESC_POS_USB_NET.EpsonCommands
{
    public class Image : IImage
    {


        private static BitmapData GetBitmapData(Bitmap bmp, int dotsInLine=576)
        {
  
                var threshold = 127;
                var index = 0;
                double multiplier = dotsInLine * 1.0; // this depends on your printer model.
                double scale = (double)(multiplier / (double)bmp.Width);
                int xheight = (int)(bmp.Height * scale);
                int xwidth = (int)(bmp.Width * scale);
                var dimensions = xwidth * xheight;
                var dots = new BitArray(dimensions);

                for (var y = 0; y < xheight; y++)
                {
                    for (var x = 0; x < xwidth; x++)
                    {
                        var _x = (int)(x / scale);
                        var _y = (int)(y / scale);
                        var color = bmp.GetPixel(_x, _y);
                        var luminance = (int)(color.R * 0.3 + color.G * 0.59 + color.B * 0.11);
                        dots[index] = (luminance < threshold);
                        index++;
                    }
                }

                return new BitmapData()
                {
                    Dots = dots,
                    Height = (int)(bmp.Height * scale),
                    Width = (int)(bmp.Width * scale)
                };

        }


        /// DotsInLine:
        /// This depends on your printer head, most common printers have 203dpi that is 8dot/1mm.Usually printer have about 5mm margins too.
        /// High Resolution print heads have 300dpi, that is about 12dots/1mm but those are much more expensive and less popular.
        /// Usually this is in Printer specification table in your printeer manual.
        /// So if you have printer for 58mm ribbon(57mm), then: 58mm - (2x5mm margin) = 48mm active area, then knowing 8dots/1mm: 48*8=384 dots

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <param name="DotsInLine">This depends on your printer head</param>
        /// <returns></returns>
        byte[] IImage.Print(Bitmap image, int DotsInLine)
        {
            var data = GetBitmapData(image, DotsInLine);
            BitArray dots = data.Dots;
            byte[] width = BitConverter.GetBytes(data.Width);

            int offset = 0;
            MemoryStream stream = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(stream);

            // reset printer settings
            bw.Write((char)0x1B);
            bw.Write('@');
            // set line height to 24dots - because we're using 24dots height mode
            bw.Write((char)0x1B);
            bw.Write('3');
            bw.Write((byte)24);

            while (offset < data.Height)
            {
                bw.Write((char)0x1B);
                bw.Write('*');         // bit-image mode
                bw.Write((byte)33);    // 24-dot double-density ; some printers have 24-dot single-width
                bw.Write(width[0]);  // width low byte
                bw.Write(width[1]);  // width high byte

                for (int x = 0; x < data.Width; ++x)
                {
                    for (int k = 0; k < 3; ++k)
                    {
                        byte slice = 0;
                        for (int b = 0; b < 8; ++b)
                        {
                            int y = (((offset / 8) + k) * 8) + b;
                            // Calculate the location of the pixel we want in the bit array.
                            // It'll be at (y * width) + x.
                            int i = (y * data.Width) + x;

                            // If the image is shorter than 24 dots, pad with zero.
                            bool v = false;
                            if (i < dots.Length)
                            {
                                v = dots[i];
                            }
                            slice |= (byte)((v ? 1 : 0) << (7 - b));
                        }

                        bw.Write(slice);
                    }
                }
                offset += 24;
                bw.Write((char)0x0A);
            }
            // Restore the line spacing to the default of 30 dots.
            bw.Write((char)0x1B);
            bw.Write('3');
            bw.Write((byte)30);

            bw.Flush();
            byte[] bytes = stream.ToArray();
            bw.Dispose();
            return bytes;
        }
    }

    public class BitmapData
    {
        public BitArray Dots { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
    }
}

