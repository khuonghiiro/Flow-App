using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Utils
{
    /// <summary>
    /// Các thuật toán xử lý và bộ lọc ảnh dùng thư viện System.Drawing (GDI+) tích hợp sẵn.
    /// </summary>
    public static class ImageAdjustments
    {
        // Chuyển đổi WriteableBitmap sang System.Drawing.Bitmap
        public static Bitmap WriteableBitmapToBitmap(WriteableBitmap wbitmap)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BmpBitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(wbitmap));
                enc.Save(outStream);
                using (var temp = new Bitmap(outStream))
                {
                    return new Bitmap(temp);
                }
            }
        }

        // Chuyển đổi System.Drawing.Bitmap sang WriteableBitmap (ghi đè đè pixel)
        public static void BitmapToWriteableBitmap(Bitmap bitmap, WriteableBitmap wbitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                wbitmap.WritePixels(new Int32Rect(0, 0, width, height), data.Scan0, data.Stride * height, data.Stride);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        // 1. Grayscale (Đen trắng)
        public static void ApplyGrayscale(Bitmap bmp)
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                ColorMatrix colorMatrix = new ColorMatrix(new float[][]
                {
                    new float[] {.3f, .3f, .3f, 0, 0},
                    new float[] {.59f, .59f, .59f, 0, 0},
                    new float[] {.11f, .11f, .11f, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {0, 0, 0, 0, 1}
                });
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(colorMatrix);
                    g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height),
                        0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attributes);
                }
            }
        }

        // 2. Invert (Đảo màu)
        public static void ApplyInvert(Bitmap bmp)
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                ColorMatrix colorMatrix = new ColorMatrix(new float[][]
                {
                    new float[] {-1, 0, 0, 0, 0},
                    new float[] {0, -1, 0, 0, 0},
                    new float[] {0, 0, -1, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {1, 1, 1, 0, 1}
                });
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(colorMatrix);
                    g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height),
                        0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attributes);
                }
            }
        }

        // 3. Sepia (Hoài cổ)
        public static void ApplySepia(Bitmap bmp)
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                ColorMatrix colorMatrix = new ColorMatrix(new float[][]
                {
                    new float[] {.393f, .349f, .272f, 0, 0},
                    new float[] {.769f, .686f, .534f, 0, 0},
                    new float[] {.189f, .168f, .131f, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {0, 0, 0, 0, 1}
                });
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(colorMatrix);
                    g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height),
                        0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attributes);
                }
            }
        }

        // 4. Brightness (Tăng giảm sáng)
        public static void ApplyBrightness(Bitmap bmp, float value)
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                ColorMatrix colorMatrix = new ColorMatrix(new float[][]
                {
                    new float[] {1, 0, 0, 0, 0},
                    new float[] {0, 1, 0, 0, 0},
                    new float[] {0, 0, 1, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {value, value, value, 0, 1}
                });
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(colorMatrix);
                    g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height),
                        0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attributes);
                }
            }
        }

        // 5. Contrast (Tăng giảm tương phản)
        public static void ApplyContrast(Bitmap bmp, float value)
        {
            float t = (1.0f - value) / 2.0f;
            using (Graphics g = Graphics.FromImage(bmp))
            {
                ColorMatrix colorMatrix = new ColorMatrix(new float[][]
                {
                    new float[] {value, 0, 0, 0, 0},
                    new float[] {0, value, 0, 0, 0},
                    new float[] {0, 0, value, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {t, t, t, 0, 1}
                });
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(colorMatrix);
                    g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height),
                        0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attributes);
                }
            }
        }

        // 6. Blur (Làm mờ dùng tích chập hộp đơn giản) - Safe version
        public static void ApplyBlur(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            Bitmap src = (Bitmap)bmp.Clone();
            BitmapData srcData = src.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dstData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                int stride = srcData.Stride;
                int bytes = stride * height;
                byte[] srcPixels = new byte[bytes];
                byte[] dstPixels = new byte[bytes];

                System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, srcPixels, 0, bytes);
                System.Runtime.InteropServices.Marshal.Copy(dstData.Scan0, dstPixels, 0, bytes);

                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        int r = 0, g = 0, b = 0, a = 0;
                        for (int ky = -1; ky <= 1; ky++)
                        {
                            int rowOffset = (y + ky) * stride;
                            for (int kx = -1; kx <= 1; kx++)
                            {
                                int pixelIndex = rowOffset + (x + kx) * 4;
                                b += srcPixels[pixelIndex];
                                g += srcPixels[pixelIndex + 1];
                                r += srcPixels[pixelIndex + 2];
                                a += srcPixels[pixelIndex + 3];
                            }
                        }

                        int dstOffset = y * stride + x * 4;
                        dstPixels[dstOffset] = (byte)(b / 9);
                        dstPixels[dstOffset + 1] = (byte)(g / 9);
                        dstPixels[dstOffset + 2] = (byte)(r / 9);
                        dstPixels[dstOffset + 3] = (byte)(a / 9);
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(dstPixels, 0, dstData.Scan0, bytes);
            }
            finally
            {
                src.UnlockBits(srcData);
                bmp.UnlockBits(dstData);
                src.Dispose();
            }
        }

        // 7. Sharpen (Ma trận nhân chập) - Safe version
        public static void ApplySharpen(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            Bitmap src = (Bitmap)bmp.Clone();
            BitmapData srcData = src.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dstData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            float[] filter = {
                0, -1,  0,
               -1,  5, -1,
                0, -1,  0
            };

            try
            {
                int stride = srcData.Stride;
                int bytes = stride * height;
                byte[] srcPixels = new byte[bytes];
                byte[] dstPixels = new byte[bytes];

                System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, srcPixels, 0, bytes);
                System.Runtime.InteropServices.Marshal.Copy(dstData.Scan0, dstPixels, 0, bytes);

                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        float r = 0, g = 0, b = 0;
                        int a = srcPixels[y * stride + x * 4 + 3];

                        int filterIndex = 0;
                        for (int ky = -1; ky <= 1; ky++)
                        {
                            int rowOffset = (y + ky) * stride;
                            for (int kx = -1; kx <= 1; kx++)
                            {
                                int pixelIndex = rowOffset + (x + kx) * 4;
                                float f = filter[filterIndex++];
                                b += srcPixels[pixelIndex] * f;
                                g += srcPixels[pixelIndex + 1] * f;
                                r += srcPixels[pixelIndex + 2] * f;
                            }
                        }

                        int dstOffset = y * stride + x * 4;
                        dstPixels[dstOffset] = (byte)Math.Clamp(b, 0, 255);
                        dstPixels[dstOffset + 1] = (byte)Math.Clamp(g, 0, 255);
                        dstPixels[dstOffset + 2] = (byte)Math.Clamp(r, 0, 255);
                        dstPixels[dstOffset + 3] = (byte)a;
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(dstPixels, 0, dstData.Scan0, bytes);
            }
            finally
            {
                src.UnlockBits(srcData);
                bmp.UnlockBits(dstData);
                src.Dispose();
            }
        }
    }
}
