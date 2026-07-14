using ImageMagick;
using System.IO;
using System.Windows.Media.Imaging;

namespace FlowMy.Extensions
{
    /// <summary>
    /// Extension methods cho BitmapSource để làm việc với ImageMagick.
    /// </summary>
    public static class BitmapSourceExtensions
    {
        /// <summary>
        /// Convert BitmapSource sang MagickImage.
        /// </summary>
        public static MagickImage ToMagickImage(this BitmapSource source)
        {
            if (source == null)
                throw new System.ArgumentNullException(nameof(source));

            // Convert sang PNG stream
            using (var stream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(stream);
                stream.Position = 0;

                return new MagickImage(stream);
            }
        }

        /// <summary>
        /// Convert MagickImage sang BitmapSource.
        /// </summary>
        public static BitmapSource ToBitmapSource(this MagickImage magick)
        {
            if (magick == null)
                throw new System.ArgumentNullException(nameof(magick));

            using (var stream = new MemoryStream())
            {
                magick.Write(stream, MagickFormat.Png32);
                stream.Position = 0;

                var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var bitmap = decoder.Frames[0];
                bitmap.Freeze();
                return bitmap;
            }
        }

        /// <summary>
        /// Convert WriteableBitmap sang BitmapSource (helper).
        /// </summary>
        public static BitmapSource ToBitmapSource(this WriteableBitmap writeable)
        {
            if (writeable == null)
                throw new System.ArgumentNullException(nameof(writeable));

            // WriteableBitmap IS a BitmapSource, just freeze it
            if (!writeable.IsFrozen && writeable.CanFreeze)
            {
                var clone = writeable.Clone();
                clone.Freeze();
                return clone;
            }
            
            return writeable;
        }
    }
}
