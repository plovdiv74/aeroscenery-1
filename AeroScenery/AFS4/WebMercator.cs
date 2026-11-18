using System;

namespace AeroScenery.AFS4
{
    /// <summary>
    /// The Web Mercator tile scheme of Bing and Google: at zoom z the world is 256 * 2^z pixels each way, x from
    /// longitude -180 eastwards, y from latitude +85.05 southwards. Pixel (px, py) covers [px, px + 1) x [py, py + 1),
    /// so its centre is at (px + 0.5, py + 0.5). Tile (tx, ty) holds pixels 256 tx .. 256 tx + 255.
    /// </summary>
    public static class WebMercator
    {
        public const int TileSize = 256;

        public static double WorldPixels(int zoom)
        {
            return TileSize * Math.Pow(2, zoom);
        }

        public static double PixelX(double lon, int zoom)
        {
            return WorldPixels(zoom) * (lon + 180) / 360;
        }

        public static double PixelY(double lat, int zoom)
        {
            double s = Math.Sin(lat * Math.PI / 180);
            return WorldPixels(zoom) * (0.5 - Math.Log((1 + s) / (1 - s)) / (4 * Math.PI));
        }
    }
}
