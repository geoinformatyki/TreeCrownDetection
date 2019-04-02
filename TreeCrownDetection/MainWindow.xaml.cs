using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OSGeo.GDAL;

namespace TreeCrownDetection
{
    public partial class MainWindow : Window
    {
        private string _inputFilename;
        public bool Complete = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void OpenFileButtonClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".geoTIFF",
                Filter =
                    "geoTIFF Files(*.geoTIFF)|*.gTIFF|TIFF Files (*.tiff)|*.tiff|TIF Files (*.tif)|*.tif"
            };
            
            var result = dlg.ShowDialog();
            if (result != true) return;

            _inputFilename = dlg.FileName;
          
            var progress = new Progress<int>(percent =>
            {
                ProgressBar.Value = percent;
            });

            await ReadGeoTiffFiles(_inputFilename, progress);
        }

        public async Task ReadGeoTiffFiles(string path, IProgress<int> progress)
        {
            if (null == path)
            {
                return;
            }
       
            await Task.Run((() =>
                    {
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
                            Console.WriteLine(@"Input error, please provide single band raster image only..");
                            System.Environment.Exit(-1);
                        }
                        var rasterCols = ds.RasterXSize;
                        var rasterRows = ds.RasterYSize;

                        var transformation = new double[6];
                        ds.GetGeoTransform(transformation);

                        var originX = transformation[0];
                        var originY = transformation[3];
                        var pixelWidth = transformation[1];
                        var pixelHeight = transformation[5];
                        progress.Report(60);
                        var band = ds.GetRasterBand(1);
                        var rasterWidth = rasterCols;
                        var rasterHeight = rasterRows;
                        progress.Report(80);
                        var rasterValues = new double[rasterWidth * rasterHeight];
                        band.ReadRaster(0, 0, rasterWidth, rasterHeight, rasterValues, rasterWidth, rasterHeight, 0, 0);
                        progress.Report(100);
                        Complete = true;
                    }
                ));
        }

    }
}
