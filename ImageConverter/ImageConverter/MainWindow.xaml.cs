﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using ImageConverter.BusinessLogic;
using ImageConverter.BusinessLogic.Enumerations;
using ImageConverter.Helpers;
using ImageConverter.Logging;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SizeConverter = ImageConverter.BusinessLogic.SizeConverter;

namespace ImageConverter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string> _files = new List<string>();
        private string _outputDirectory;
        private readonly IXMLLog _xmlLog = new XMLLog("logging.log");
        private readonly IConverter _converter;
        public MainWindow()
        {
            InitializeComponent();
            InitScreen();
            _converter = new Converter(new FormatConverter(new BitmapSourceLoader(), _xmlLog,new FormatEncoder()),
                new SizeConverter(new BitmapSourceLoader(), _xmlLog,new FormatEncoder()),
                _xmlLog);
        }
        private void InitScreen()
        {
            InitComboBoxes();
        }

        private void InitComboBoxes()
        {
            OutputFormatComboBox.ItemsSource = Enum.GetValues(typeof(Format));
            OutputFormatComboBox.SelectedIndex = 0;
            TextBoxJPEGCompression.Visibility = Visibility.Visible;
            LabelJPEGCompression.Visibility = Visibility.Visible;
            ComboBoxKeepAspectRatio.ItemsSource = Enum.GetValues(typeof(KeepAspectRatio));
            ComboBoxKeepAspectRatio.SelectedIndex = 0;
        }

        #region Event Handlers

        private void ButtonSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "JPEG Files (*.jpeg)|*.jpeg|JPG Files (*.jpg)|*.jpg|PNG Files (*.png)|*.png|Tiff Files (*.tif)|*.tiff|GIF Files (*.gif)|*.gif|BMP Files (*.bmp)|*.bmp";
            dialog.Multiselect = true;
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                _files.Clear();
                _files.AddRange(dialog.FileNames);
                SetNumberOfFiles();
            }
        }
        private void OutputDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog fb = new FolderBrowserDialog())
            {
                fb.ShowNewFolderButton = true;
                if (fb.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _outputDirectory = fb.SelectedPath;
                    LabelYourDirectory.Content = fb.SelectedPath;
                }
            }
        }
        private void ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ControlProperties())
                return;
            ButtonConvert.IsEnabled = false;
            if (FormatConversionRadioBox.IsChecked == true)
            {
                ConvertFormat();
            }
            else if (SizeConversionRadioBox.IsChecked == true)
            {
                ConvertSize();
            }
            else if (FormatAndSizeConversionRadioBox.IsChecked == true)
            {
                ConvertFormatAndSize();
            }
        }
        private void ButtonSelectDirectory_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog fb = new FolderBrowserDialog())
            {
                fb.ShowNewFolderButton = false;
                if (fb.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _files = Directory.GetFiles(fb.SelectedPath).ToList();
                    SetNumberOfFiles();
                }
            }
        }
        private void OutputFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OutputFormatComboBox.SelectedIndex == 0)
            {
                TextBoxJPEGCompression.Visibility = Visibility.Visible;
                LabelJPEGCompression.Visibility = Visibility.Visible;
            }
            else
            {
                TextBoxJPEGCompression.Visibility = Visibility.Hidden;
                LabelJPEGCompression.Visibility = Visibility.Hidden;
            }
        }
        private void ComboBoxKeepAspectRatio_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (ComboBoxKeepAspectRatio.SelectedIndex)
            {
                case 0:
                    WidthAndHeightSetVisibility(true, true);
                    break;
                case 1:
                    WidthAndHeightSetVisibility(true, false);
                    break;
                case 2:
                    WidthAndHeightSetVisibility(false, true);
                    break;
            }
        }
        private void FileNamePreview(object sender, TextCompositionEventArgs e)
        {
            bool result = FileHelper.IsTextAllowedInFileName(e.Text);
            e.Handled = !result;
            if (!result)
                MessageBox.Show("This character is there not allowed (" + e.Text + ")", "Cannot enter this character", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !FileHelper.IsTextAllowed(e.Text);
        }

        #region BackgroundWorker methods

        private void ConvertFormat()
        {
            string outputFileName = _outputDirectory + "\\" + TextBoxOutputFileName.Text;
            Format format = (Format)Enum.Parse(typeof(Format), OutputFormatComboBox.SelectedItem.ToString());
            _xmlLog.Info("Convert format " + FileHelper.GetFilesToString(_files) + "in directory " + Path.GetDirectoryName(_files.FirstOrDefault()) + " to " + format.ToString());
            switch (format)
            {
                case Format.JPEG:
                    outputFileName += ".jpeg";
                    break;
                case Format.PNG:
                    outputFileName += ".png";
                    break;
                case Format.Tiff:
                    outputFileName += ".tiff";
                    break;
                case Format.GIF:
                    outputFileName += ".gif";
                    break;
                case Format.BMP:
                    outputFileName += ".bmp";
                    break;
            }
            using (BackgroundWorker worker = new BackgroundWorker())
            {
                int compression = Int32.Parse(TextBoxJPEGCompression.Text);
                bool overwriteFiles = (bool)CheckBoxOverwriteExistingFiles.IsChecked;
                worker.WorkerReportsProgress = true;
                worker.DoWork += BgConvertType;
                worker.ProgressChanged += BgWorkerProgressChanged;
                worker.RunWorkerCompleted += BgWorkerCompleted;
                worker.RunWorkerAsync(Tuple.Create(_files, format, outputFileName,
                    compression, overwriteFiles));
            }
        }
        private void ConvertSize()
        {
            int width = 0;
            int height = 0;
            KeepAspectRatio ratio = (KeepAspectRatio)Enum.Parse(typeof(KeepAspectRatio), ComboBoxKeepAspectRatio.SelectedItem.ToString());
            switch (ratio)
            {
                case KeepAspectRatio.NONE:
                    width = Int32.Parse(TextBoxWidth.Text);
                    height = Int32.Parse(TextBoxHeight.Text);
                    _xmlLog.Info("Convert size " + FileHelper.GetFilesToString(_files) + "in directory " + Path.GetDirectoryName(_files.FirstOrDefault()) + " to given width: " + width + " and height " + height);
                    break;
                case KeepAspectRatio.WIDTH:
                    width = Int32.Parse(TextBoxWidth.Text);
                    _xmlLog.Info("Convert size " + FileHelper.GetFilesToString(_files) + "in directory " + Path.GetDirectoryName(_files.FirstOrDefault()) + " to given width: " + width);
                    break;
                case KeepAspectRatio.HEIGHT:
                    height = Int32.Parse(TextBoxHeight.Text);
                    _xmlLog.Info("Convert size " + FileHelper.GetFilesToString(_files) + "in directory " + Path.GetDirectoryName(_files.FirstOrDefault()) + " to given height " + height);
                    break;
            }
            string outputFileName = _outputDirectory + "\\" + TextBoxOutputFileName.Text;
            bool enlargeSmallerImages = CheckBoxEnlargeSmallerImages.IsChecked != null && (bool)  CheckBoxEnlargeSmallerImages.IsChecked;
            bool overWriteOutput = CheckBoxOverwriteExistingFiles.IsChecked != null && (bool) CheckBoxOverwriteExistingFiles.IsChecked;
            using (BackgroundWorker worker = new BackgroundWorker())
            {
                worker.WorkerReportsProgress = true;
                worker.DoWork += BgConvertSize;
                worker.ProgressChanged += BgWorkerProgressChanged;
                worker.RunWorkerCompleted += BgWorkerCompleted;
                worker.RunWorkerAsync(Tuple.Create(_files, width, height, outputFileName, ratio, enlargeSmallerImages,
                    overWriteOutput));
            }
        }
        private void ConvertFormatAndSize()
        {
            int width = 0;
            int height = 0;
            int compression = Int32.Parse(TextBoxJPEGCompression.Text);
            Format format = (Format)Enum.Parse(typeof(Format), OutputFormatComboBox.SelectedItem.ToString());
            KeepAspectRatio ratio = (KeepAspectRatio)Enum.Parse(typeof(KeepAspectRatio), ComboBoxKeepAspectRatio.SelectedItem.ToString());
            switch (ratio)
            {
                case KeepAspectRatio.NONE:
                    width = Int32.Parse(TextBoxWidth.Text);
                    height = Int32.Parse(TextBoxHeight.Text);
                    _xmlLog.Info("Convert format and size " + FileHelper.GetFilesToString(_files) + "in directory " + Path.GetDirectoryName(_files.FirstOrDefault()) + " with format " + format + " to given width: " + width + " and height " + height);
                    break;
                case KeepAspectRatio.WIDTH:
                    width = Int32.Parse(TextBoxWidth.Text);
                    _xmlLog.Info("Convert format and size " + FileHelper.GetFilesToString(_files) + "in directory " + Path.GetDirectoryName(_files.FirstOrDefault()) + " with format " + format + " to given width: " + width);
                    break;
                case KeepAspectRatio.HEIGHT:
                    height = Int32.Parse(TextBoxHeight.Text);
                    _xmlLog.Info("Convert format and size " + FileHelper.GetFilesToString(_files) + "in directory " + Path.GetDirectoryName(_files.FirstOrDefault()) + " with format " + format + " to given height " + height);
                    break;
            }
            string outputFileName = _outputDirectory + "\\" + TextBoxOutputFileName.Text;
            bool enlargeSmallerImages = CheckBoxEnlargeSmallerImages.IsChecked != null && (bool)CheckBoxEnlargeSmallerImages.IsChecked;
            bool overWriteOutput = CheckBoxOverwriteExistingFiles.IsChecked != null && (bool)CheckBoxOverwriteExistingFiles.IsChecked;
            using (BackgroundWorker worker = new BackgroundWorker())
            {
                worker.WorkerReportsProgress = true;
                worker.DoWork += BgConvertFormatAndSize;
                worker.ProgressChanged += BgWorkerProgressChanged;
                worker.RunWorkerCompleted += BgWorkerCompleted;
                worker.RunWorkerAsync(Tuple.Create(_files, format, width, height, outputFileName, ratio, Tuple.Create(enlargeSmallerImages, compression,overWriteOutput)));
            }
        }
        private void BgWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var result = (List<string>) e.Result;
            if (result.Count == 0)
            {
                MessageBox.Show("All selected files were converted","Conversion successful",MessageBoxButton.OK,MessageBoxImage.Information);
                _xmlLog.Debug("All files were converted");
            }
            else
            {
                MessageBox.Show("These files were not converted\n" + FileHelper.GetFilesToMessageBox(result),"Error in converting files",MessageBoxButton.OK,MessageBoxImage.Error);
                _xmlLog.Debug("Not converted files: " + FileHelper.GetFilesToString(result));
            }
            LabelProcessedFiles.Content = "No file";
            LabelPercents.Content = "0";
            ProgressBarProgress.Value = 0;
            ButtonConvert.IsEnabled = true;
        }

        private void BgWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            LabelProcessedFiles.Content = e.UserState as string;
            int progress = Math.Min(100, e.ProgressPercentage);
            LabelPercents.Content = progress;
            ProgressBarProgress.Value = progress;
        }

        private void BgConvertType(object sender, DoWorkEventArgs e)
        {
            var bw = sender as BackgroundWorker;
            var args = (Tuple<List<string>, Format, string, int, bool>) e.Argument;
            var files = args.Item1;
            var format = args.Item2;
            var outputFileName = args.Item3;
            var compression = args.Item4;
            var overwriteOutput = args.Item5;
            var result = _converter.ConvertFormat(files, format, outputFileName, compression, overwriteOutput,bw);
            e.Result = result;
        }

        private void BgConvertSize(object sender, DoWorkEventArgs e)
        {
            var bw = sender as BackgroundWorker;
            var args = (Tuple<List<string>, int, int, string, KeepAspectRatio, bool, bool>) e.Argument;
            var files = args.Item1;
            int width = args.Item2;
            int height = args.Item3;
            string outputFileName = args.Item4;
            KeepAspectRatio ratio = args.Item5;
            bool enlargeSmallerImages = args.Item6;
            bool overwriteOutput = args.Item7;
            var result = _converter.Resize(files, width, height, outputFileName, ratio, enlargeSmallerImages,
                overwriteOutput, bw);
            e.Result = result;
        }

        private void BgConvertFormatAndSize(object sender, DoWorkEventArgs e)
        {
            var bw = sender as BackgroundWorker;
            var args = (Tuple<List<string>, Format, int, int, string, KeepAspectRatio, Tuple<bool, int, bool>>)e.Argument;
            var files = args.Item1;
            Format format = args.Item2;
            int width = args.Item3;
            int height = args.Item4;
            string outputFileName = args.Item5;
            KeepAspectRatio ratio = args.Item6;
            bool enlargeSmallerImages = args.Item7.Item1;
            int compression = args.Item7.Item2;
            bool overwriteOutput = args.Item7.Item3;
            var result = _converter.ConvertFormatAndSize(files, format, width, height, outputFileName, ratio, enlargeSmallerImages,
                compression, overwriteOutput, bw);
            e.Result = result;
        }

        #endregion
        
        #endregion

        #region Other methods
        private bool ControlProperties()
        {
            if (FormatConversionRadioBox.IsChecked != true && SizeConversionRadioBox.IsChecked != true && FormatAndSizeConversionRadioBox.IsChecked != true)
            {
                MessageBox.Show("Select format, size or format and size conversion", "Select what to do", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (_files.Count() == 0)
            {
                MessageBox.Show("Select any files to convert", "No files are selected", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }
            if (_outputDirectory == null)
            {
                MessageBox.Show("Select directory for output files", "No output directory", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }
            if (FormatConversionRadioBox.IsChecked == true)
            {
                if (!FormatConversionCheckProperties())
                    return false;
                
            }
            if (SizeConversionRadioBox.IsChecked == true)
            {
                if (!SizeConversionCheckProperties())
                    return false;
                
            }
            if (FormatAndSizeConversionRadioBox.IsChecked == true)
            {
                if (!FormatConversionCheckProperties() || !SizeConversionCheckProperties())
                    return false;
            }
            if (TextBoxOutputFileName.Text.Length < 1)
            {
                MessageBox.Show("Output file name cannot be empty", "Wrong output file name", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }
            if (Path.GetDirectoryName(_files.FirstOrDefault()) == _outputDirectory)
            {
                MessageBox.Show("Input and output directory cannot be the same", "Output directory", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }
            return true;
        }

        private bool SizeConversionCheckProperties()
        {
            KeepAspectRatio ratio =
                    (KeepAspectRatio)
                        Enum.Parse(typeof(KeepAspectRatio), ComboBoxKeepAspectRatio.SelectedItem.ToString());

            int width;
            int height;
            switch (ratio)
            {
                case KeepAspectRatio.NONE:
                    if (TextBoxWidth.Text.Length < 1)
                    {
                        MessageBoxFillWidth();
                        return false;
                    }
                    if (TextBoxHeight.Text.Length < 1)
                    {
                        MessageBoxFillHeight();
                        return false;
                    }
                    width = Int32.Parse(TextBoxWidth.Text);
                    height = Int32.Parse(TextBoxHeight.Text);
                    if (width < 1)
                    {
                        ShowMessageBoxWidth();
                        return false;
                    }
                    if (height < 1)
                    {
                        ShowMessageBoxHeight();
                        return false;
                    }
                    break;
                case KeepAspectRatio.HEIGHT:
                    if (TextBoxHeight.Text.Length < 1)
                    {
                        MessageBoxFillHeight();
                        return false;
                    }
                    height = Int32.Parse(TextBoxHeight.Text);
                    if (height < 1)
                    {
                        ShowMessageBoxHeight();
                        return false;
                    }
                    break;
                case KeepAspectRatio.WIDTH:
                    if (TextBoxWidth.Text.Length < 1)
                    {
                        MessageBoxFillWidth();
                        return false;
                    }
                    width = Int32.Parse(TextBoxWidth.Text);
                    if (width < 1)
                    {
                        ShowMessageBoxWidth();
                        return false;
                    }
                    break;
            }
            return true;
        }

        private bool FormatConversionCheckProperties()
        {
            Format format = (Format)Enum.Parse(typeof(Format), OutputFormatComboBox.SelectedItem.ToString());
            if (format == Format.JPEG && TextBoxJPEGCompression.Text.Length < 1)
            {
                MessageBox.Show("Fill JPEG compression", "Empty compression", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }
            if (format == Format.JPEG && Int32.Parse(TextBoxJPEGCompression.Text) > 100)
            {
                MessageBox.Show("Compression has to be mostly 100%", "Wrong compression", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }
            return true;
        }

        private void MessageBoxFillWidth()
        {
            MessageBox.Show("Fill width", "Fill width", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void MessageBoxFillHeight()
        {
            MessageBox.Show("Fill height", "Fill height", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void ShowMessageBoxHeight()
        {
            MessageBox.Show("Height must be at least 1", "Wrong height value", MessageBoxButton.OK,
                                MessageBoxImage.Information);
        }
        private void ShowMessageBoxWidth()
        {
            MessageBox.Show("Width must be at least 1", "Wrong width value", MessageBoxButton.OK,
                                MessageBoxImage.Information);
        }
        private void SetNumberOfFiles()
        {
            LabelNumOfSelectedFiles.Content = _files.Count.ToString();
        }

        private void WidthAndHeightSetVisibility(bool width, bool height)
        {
            if (width)
            {
                LabelWidth.IsEnabled = true;
                TextBoxWidth.IsEnabled = true;
            }
            else
            {
                LabelWidth.IsEnabled = false;
                TextBoxWidth.IsEnabled = false;
            }
            if (height)
            {
                LabelHeight.IsEnabled = true;
                TextBoxHeight.IsEnabled = true;
            }
            else
            {
                LabelHeight.IsEnabled = false;
                TextBoxHeight.IsEnabled = false;
            }
        }
        #endregion
    }
}
