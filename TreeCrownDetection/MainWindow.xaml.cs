using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
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
        private Bitmap _processedImage;
        public bool Complete;
        private double[] _geoTransform;
        private Band _processingBand;
        private string WKT;

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
                _processedImage = background;
                var backgroundSource = Convert(background);
                var displayImage = new Image {Source = backgroundSource};
                ImageView.MouseWheel += Image_MouseWheel;
                ImageView.MouseMove += Image_MouseMove;
                ImageView.MouseLeftButtonDown += Image_MouseLeftButtonDown;
                ImageView.MouseLeftButtonUp += Image_MouseLeftButtonUp;
                ImageView.Source = displayImage.Source;
                ProgressBar.Value = 100;
            }
            else
            {
                MessageBox.Show(@"Input error, please provide single band raster image only..", "Tree Crow Detection",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (!Complete.Equals(true)) return;
            ProgressBar.Value = 0;
            ConvertButton.IsEnabled = true;
            SaveFileButton.IsEnabled = true;
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
                WKT = ds.GetProjectionRef();
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

                _geoTransform = new double[6];
                ds.GetGeoTransform(_geoTransform);

                progress.Report(30);
                _processingBand = ds.GetRasterBand(1);
                var rasterWidth = rasterCols;
                var rasterHeight = rasterRows;
                progress.Report(50);
                var rasterValues = new double[rasterWidth * rasterHeight];
                _processingBand.ReadRaster(0, 0, rasterWidth, rasterHeight, rasterValues, rasterWidth, rasterHeight, 0,
                    0);
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


        private static unsafe Bitmap ToBitmap(IReadOnlyList<double> rawImage, int width, int height)
        {
            var image = new Bitmap(width, height);
            var bitmapData = image.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadWrite,
                PixelFormat.Format32bppArgb
            );

            if (Application.Current.MainWindow != null)
            {
                var sliderA = Application.Current.MainWindow.FindName("SliderA") as Slider;
                var sliderB = Application.Current.MainWindow.FindName("SliderB") as Slider;

                const int cutOff = 15;
                if (sliderA != null)
                {
                    var a = System.Convert.ToInt32(sliderA.Value);
                    if (sliderB != null)
                    {
                        var b = System.Convert.ToInt32(sliderB.Value);

                        var tcd = new TreeCrownDetector(rawImage, width, height, cutOff, a, b);
                        var watch = Stopwatch.StartNew();
                        var labeledObj = tcd.GetLabeledTrees();
                        watch.Stop();
                        var elapsedMs = watch.ElapsedMilliseconds;
                        Console.WriteLine("Elapsed time: ");
                        Console.WriteLine(elapsedMs);

                        var rnd = new Random();
                        var colors = new Dictionary<int, ColorARGB>();

                        ColorARGB color;
                        color.A = 255;
                        color.R = 0;
                        color.G = 0;
                        color.B = 0;
                        colors.Add(0, color);

                        var startingPosition = (ColorARGB*) bitmapData.Scan0;
                        for (var i = 1; i < height - 1; i++)
                        for (var j = 1; j < width - 1; j++)
                        {
                            var label = labeledObj[i, j];

                            if (label == 0) continue;
                            if (!colors.ContainsKey(label))
                            {
                                while (colors.ContainsValue(color))
                                {
                                    color.A = 255;
                                    color.R = (byte) rnd.Next(256);
                                    color.G = (byte) rnd.Next(256);
                                    color.B = (byte) rnd.Next(256);
                                }

                                colors.Add(label, color);
                            }

                            *(startingPosition + j + i * width) = colors[label];
                        }
                    }
                }
            }

            image.UnlockBits(bitmapData);
            return image;
        }

        private void SliderA_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var val = System.Convert.ToInt32(e.NewValue);
            var msg = $"A value: {val}";
            var textBlockA = TextBlockA;
            if (textBlockA != null) textBlockA.Text = msg;
        }

        private void SliderB_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var val = System.Convert.ToInt32(e.NewValue);
            var msg = $"B value: {val}";
            var textBlockB = TextBlockB;
            if (textBlockB != null) textBlockB.Text = msg;
        }

        private void ConvertButton_OnClick(object sender, RoutedEventArgs e)
        {
            var background = ToBitmap(_loadedImage.RawImageData, _loadedImage.Width, _loadedImage.Height);
            _processedImage = background;
            var backgroundSource = Convert(background);
            var displayImage = new Image {Source = backgroundSource};
            ImageView.MouseWheel += Image_MouseWheel;
            ImageView.MouseMove += Image_MouseMove;
            ImageView.MouseLeftButtonDown += Image_MouseLeftButtonDown;
            ImageView.MouseLeftButtonUp += Image_MouseLeftButtonUp;
            ImageView.Source = displayImage.Source;
        }

        private async void SaveFileButton_OnClick(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                DefaultExt = ".geoTIFF",
                Filter =
                    "ESRI Shapefile (*.shp)|*.shp|geoTIFF Files(*.geoTIFF)|*.gTIFF"
            };

            var result = dlg.ShowDialog();
            if (result != true) return;

            Console.WriteLine("Saving to file: {0}", dlg.FileName);
            await SaveToFile(dlg.FileName);
        }

        private async Task SaveToFile(string fileName)
        {
            var fileExtension = fileName.Split('.').ToArray().Last();
            var shapefile = fileExtension == "shp";
            string fileNameGTiff = null;

            fileNameGTiff = shapefile ? $"{fileName}.tmp" : fileName;

            await Task.Run(() =>
            {
                string[] options = {"PHOTOMETRIC=RGB", "PROFILE=GeoTIFF"};

                var finalDataset = Gdal.GetDriverByName("GTiff").Create(fileNameGTiff, _processingBand.XSize,
                    _processingBand.YSize, 3, DataType.GDT_Byte, options);

                finalDataset.SetGeoTransform(_geoTransform);
                var spatialReference = new SpatialReference(WKT);

                spatialReference.ExportToWkt(out var exportedWkt);
                finalDataset.SetProjection(exportedWkt);

                var redBand = GetByteArrayFromRaster(_processedImage, _processingBand.XSize, _processingBand.YSize, 0);
                var greenBand =
                    GetByteArrayFromRaster(_processedImage, _processingBand.XSize, _processingBand.YSize, 1);
                var blueBand = GetByteArrayFromRaster(_processedImage, _processingBand.XSize, _processingBand.YSize, 2);

                finalDataset.GetRasterBand(1).WriteRaster(0, 0, _processingBand.XSize, _processingBand.YSize, redBand,
                    _processingBand.XSize, _processingBand.YSize, 0, 0);
                finalDataset.GetRasterBand(2).WriteRaster(0, 0, _processingBand.XSize, _processingBand.YSize, greenBand,
                    _processingBand.XSize, _processingBand.YSize, 0, 0);
                finalDataset.GetRasterBand(3).WriteRaster(0, 0, _processingBand.XSize, _processingBand.YSize, blueBand,
                    _processingBand.XSize, _processingBand.YSize, 0, 0);


                if (shapefile)
                {
                    string[] optionsShapefile = null;
                    var finalDatasource = Ogr.GetDriverByName("ESRI Shapefile")
                        .CreateDataSource(fileName, optionsShapefile);
                    var layer = finalDatasource.CreateLayer("trees", spatialReference, wkbGeometryType.wkbPolygon,
                        optionsShapefile);

                    Gdal.Polygonize(finalDataset.GetRasterBand(1), finalDataset.GetRasterBand(1), layer, -1, null, null,
                        null);
                    finalDatasource.FlushCache();
                }
                else
                {
                    finalDataset.FlushCache();
                }
            });
        }

        private byte[] GetByteArrayFromRaster(Bitmap bitmap, int sizeX, int sizeY, int band)
        {
            var outputByteArray = new byte[sizeX * sizeY];

            for (var i = 0; i < sizeY - 1; i++)
            for (var j = 0; j < sizeX - 1; j++)
                switch (band)
                {
                    case 0:
                        outputByteArray[i * sizeX + j] = bitmap.GetPixel(i, j).R;
                        break;
                    case 1:
                        outputByteArray[i * sizeX + j] = bitmap.GetPixel(i, j).G;
                        break;
                    case 2:
                        outputByteArray[i * sizeX + j] = bitmap.GetPixel(i, j).B;
                        break;
                    default:
                        outputByteArray[i * sizeX + j] = bitmap.GetPixel(i, j).R;
                        break;
                }

            return outputByteArray;
        }

        private void Image_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!(ImageView.RenderTransform is TransformGroup))
            {
                var st = new ScaleTransform();
                var tt = new TranslateTransform();
                var group = new TransformGroup();

                group.Children.Add(st);
                group.Children.Add(tt);
                ImageView.RenderTransform = group;
            }

            var transformGroup = (TransformGroup)ImageView.RenderTransform;
            var transform = (ScaleTransform)transformGroup.Children[0];

            var zoom = e.Delta > 0 ? .2 : -.2;
            if (zoom < 0)
            {
                if (transform.ScaleX > 0.4 && transform.ScaleY > 0.4)
                {
                    transform.ScaleX += zoom;
                    transform.ScaleY += zoom;
                }
            }
            else
            {
                transform.ScaleX += zoom;
                transform.ScaleY += zoom;
            }
        }

        private System.Windows.Point _start;
        private System.Windows.Point _origin;

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TranslateTransform tt;
            ImageView.CaptureMouse();

            if (!(ImageView.RenderTransform is TransformGroup))
            {
                var st = new ScaleTransform();
                tt = new TranslateTransform();
                var group = new TransformGroup();

                group.Children.Add(st);
                group.Children.Add(tt);
                ImageView.RenderTransform = group;
            }

            tt = (TranslateTransform)((TransformGroup)ImageView.RenderTransform).Children.First(tr => tr is TranslateTransform);
            _start = e.GetPosition(ImageDock);
            _origin = new System.Windows.Point(tt.X, tt.Y);
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (!ImageView.IsMouseCaptured) return;
            var tt = (TranslateTransform) ((TransformGroup) ImageView.RenderTransform)
                .Children.First(tr => tr is TranslateTransform);
            var v = _start - e.GetPosition(ImageDock);
            tt.X = _origin.X - v.X;
            tt.Y = _origin.Y - v.Y;
        }

        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ImageView.ReleaseMouseCapture();
        }

        public struct ColorARGB
        {
            public byte B;
            public byte G;
            public byte R;
            public byte A;
        }
    }
}