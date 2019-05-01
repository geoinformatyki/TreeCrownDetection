using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OSGeo.GDAL;
using Image = System.Windows.Controls.Image;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace TreeCrownDetection
{
    public class RawImage
    {
        public RawImage(double[] rawImageData, int width, int height)
        {
            Width = width;
            Height = height;
            RawImageData = rawImageData;
        }

        public int Width { get; }
        public int Height { get; }
        public double[] RawImageData { get; }
    }

    public partial class MainWindow : Window
    {
        private string _inputFilename;
        private RawImage _loadedImage;
        public bool Complete;

        public MainWindow()
        {
            InitializeComponent();
        }

        public bool BandCountError { get; set; }

        private async void OpenFileButtonClick(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                DefaultExt = ".geoTIFF",
                Filter =
                    "TIF Files (*.tif)|*.tif|geoTIFF Files(*.geoTIFF)|*.gTIFF|TIFF Files (*.tiff)|*.tiff"
            };

            var result = dlg.ShowDialog();
            if (result != true) return;

            _inputFilename = dlg.FileName;

            var progress = new Progress<int>(percent => { ProgressBar.Value = percent; });

            await ReadGeoTiffFiles(_inputFilename, progress);

            if (!BandCountError)
            {
                var background = ToBitmap(_loadedImage.RawImageData, _loadedImage.Width, _loadedImage.Height);
                var backgroundSource = Convert(background);
                var displayImage = new Image { Source = backgroundSource };
                ImageView.MouseWheel += Image_MouseWheel;
                ImageView.Source = displayImage.Source;
                ProgressBar.Value = 100;
            }
            else
            {
                MessageBox.Show(@"Input error, please provide single band raster image only..", "Tree Crow Detection",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (Complete.Equals(true))
            {
                ProgressBar.Value = 0;
            }
        }

        public async Task ReadGeoTiffFiles(string path, IProgress<int> progress)
        {
            GdalConfiguration.ConfigureGdal();
            if (null == path) return;

            await Task.Run(() =>
            {
                BandCountError = false;
                progress.Report(10);
                var ds = Gdal.Open(path, Access.GA_ReadOnly);
                Console.WriteLine(@"Raster data-set parameters:");
                Console.WriteLine(@"  Projection: " + ds.GetProjectionRef());
                Console.WriteLine(@"  RasterCount: " + ds.RasterCount);
                Console.WriteLine(@"  RasterSize (" + ds.RasterXSize + @"," + ds.RasterYSize + @")");

                progress.Report(20);
                var bandCount = ds.RasterCount;
                if (bandCount > 1)
                {
                    BandCountError = true;
                    Complete = true;
                    return;
                }

                var rasterCols = ds.RasterXSize;
                var rasterRows = ds.RasterYSize;

                var transformation = new double[6];
                ds.GetGeoTransform(transformation);

                progress.Report(30);
                var band = ds.GetRasterBand(1);
                var rasterWidth = rasterCols;
                var rasterHeight = rasterRows;
                progress.Report(50);
                var rasterValues = new double[rasterWidth * rasterHeight];
                band.ReadRaster(0, 0, rasterWidth, rasterHeight, rasterValues, rasterWidth, rasterHeight, 0, 0);
                progress.Report(70);
                _loadedImage = new RawImage(rasterValues, rasterWidth, rasterHeight);
                Complete = true;
                progress.Report(90);
            });
        }


        public BitmapSource Convert(Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                PixelFormats.Bgr32, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }

        public struct ColorARGB
        {
            public byte B;
            public byte G;
            public byte R;
            public byte A;
        }
        

        private static unsafe Bitmap ToBitmap(IReadOnlyList<double> rawImage, int width, int height)
        {
            var image = new Bitmap(width, height);
            var bitmapData = image.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadWrite,
                PixelFormat.Format32bppArgb
            );

            // TODO: read values from UI
            int cutOff = 15;
            int a = 3;
            int b = 5;

            var tcd = new TreeCrownDetector(rawImage, width, height, cutOff, a, b);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var labaledObj = tcd.GetLabeledTrees();
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            System.Console.WriteLine("Elapsed time: ");
            System.Console.WriteLine(elapsedMs);

            Random rnd = new Random();
            var colors = new Dictionary<int, ColorARGB>();

            ColorARGB color;
            color.A = 255;
            color.R = 0;
            color.G = 0;
            color.B = 0;
            colors.Add(0, color);

            var startingPosition = (ColorARGB*)bitmapData.Scan0;
            for (var i = 1; i < height - 1; i++)
            {
                for (var j = 1; j < width - 1; j++)
                {
                    var label = labaledObj[i, j];

                    if (label != 0)
                    {
                        if (!colors.ContainsKey(label))
                        {
                            while (colors.ContainsValue(color))
                            {
                                color.A = 255;
                                color.R = (byte)rnd.Next(256);
                                color.G = (byte)rnd.Next(256);
                                color.B = (byte)rnd.Next(256);
                            }
                            
                            colors.Add(label, color);
                        }

                        *(startingPosition + j + i * width) = colors[label];
                    }
                }
            }

            image.UnlockBits(bitmapData);
            return image;
        }

        private void Image_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var st = (ScaleTransform)ImageView.RenderTransform;
            double zoom = e.Delta > 0 ? .2 : -.2;
            st.ScaleX += zoom;
            st.ScaleY += zoom;
        }
    }
}