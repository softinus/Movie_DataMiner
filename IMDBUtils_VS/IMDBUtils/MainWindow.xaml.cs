﻿using IMDBUtils.Models;
using IMDBUtils.ViewModel;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Parse;
using SmartXLS;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using System.Windows.Threading;

namespace IMDBUtils
{
    public enum EMode
    {
        None = 0,
        Idle = 1,
        OpeningFile = 2,
        ReadDataFile = 3,
        WritingFile = 4,
        ClosingFile = 5
    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private EMode m_eWorking;
        public EMode EStatus
        {
            get { return m_eWorking; }
            set
            {
                this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate ()
                {
                    switch (value)
                    {
                        case EMode.Idle:
                            btnExportToXLS.Content = "Export to XLS";
                            btnExportToXLS.IsEnabled = true;
                            btnLoad.IsEnabled = true;
                            break;
                        case EMode.OpeningFile:
                            btnExportToXLS.Content = "Creating...";
                            btnExportToXLS.IsEnabled = false;
                            btnLoad.IsEnabled = false;
                            break;
                        case EMode.ReadDataFile:
                            btnExportToXLS.Content = "Reading...";
                            btnExportToXLS.IsEnabled = false;
                            btnLoad.IsEnabled = false;
                            break;
                        case EMode.WritingFile:
                            btnExportToXLS.Content = "Writing...";
                            btnExportToXLS.IsEnabled = false;
                            btnLoad.IsEnabled = false;
                            break;
                        case EMode.ClosingFile:
                            btnExportToXLS.Content = "Saving...";
                            btnExportToXLS.IsEnabled = false;
                            btnLoad.IsEnabled = false;
                            break;
                    }
                }));

                m_eWorking = value;
            }
        }
        private static string strSelectedFile = string.Empty;
        ObservableCollection<Gross> lstGross = new ObservableCollection<Gross>();
        ObservableCollection<Models.Task> TaskList= new ObservableCollection<Models.Task>();

        public MainWindow()
        {
            InitializeComponent();

        }
        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".txt";
            dlg.Filter = "TEXT Files (*.txt)|*.txt";
            dlg.Multiselect = true;

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                foreach (var FN in dlg.FileNames)
                {
                    //var onlyFN = System.IO.Path.GetFileName(FN);
                    lstFiles.Items.Add(FN);
                    #region validation
                    //if(lstFiles.Items.Count == 0)
                    //{
                    //    lstFiles.Items.Add(onlyFN);
                    //}
                    //else
                    //{
                    //    foreach (string existFN in lstFiles.Items)
                    //    {
                    //        if(existFN.Equals(onlyFN) == false)
                    //        {
                    //            lstFiles.Items.Add(onlyFN);
                    //        }
                    //    }

                    //}
                    #endregion
                }
            }
        }

        private readonly BackgroundWorker worker = new BackgroundWorker();

        private void btnExportToXLS_Click(object sender, RoutedEventArgs e)
        {
            worker.DoWork += Do_ExportWork;
            worker.RunWorkerCompleted += Done_ExportWork;

            EStatus = EMode.OpeningFile;
            worker.RunWorkerAsync();
        }

        private void ParseStringArray(ref List<string[]> arrStrings)
        {
            string line = string.Empty;

            this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate ()
            {
                prgExport.Maximum = lstFiles.Items.Count;
            }));

            foreach (string existFN in lstFiles.Items)
            {
                // Read the file line by line.
                EStatus = EMode.ReadDataFile;
                System.IO.StreamReader file = new System.IO.StreamReader(existFN);
                while ((line = file.ReadLine()) != null)
                {
                    arrStrings.Add(line.Split('|'));
                }
                file.Close();
            }
        }

        /// <summary>
        /// being in export
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Do_ExportWork(object sender, DoWorkEventArgs e)
        {
            int nRes = 0;
            int nFileCounter = 0;
            List<string[]> arrStrings = new List<string[]>();

            this.ParseStringArray(ref arrStrings);

            this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate ()
            {
                lstFiles.SelectedIndex = nFileCounter;
            }));

            EStatus = EMode.WritingFile;
            nRes = AddToWorkbook(@".\\Test.xlsx", arrStrings);
            if (nRes == -1)
            {
                MessageBox.Show("error!");
            }
            else
            {
                this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate ()
                {
                    prgExport.Value = ++nFileCounter;
                }));
            }

        }

        /// <summary>
        /// When export is done
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Done_ExportWork(object sender, RunWorkerCompletedEventArgs e)
        {
            worker.DoWork -= Do_ExportWork;
            worker.RunWorkerCompleted -= Done_ExportWork;
            //update ui once worker complete his work
            EStatus = EMode.Idle;
        }

        /// <summary>
        /// create or write data to xlsx file.
        /// </summary>
        /// <param name="strPath"></param>
        /// <param name="arrStrings"></param>
        /// <returns></returns>
        private int AddToWorkbook(string strPath, List<string[]> arrStrings)
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate ()
            {
                prgExport_Single.Value = 0;
            }));

            int nRowOffset = 0;
            WorkBook m_book = new WorkBook();
            if (File.Exists(strPath) == true)    // if it already exists change into read mode.
            {
                m_book.readXLSX(strPath);
                nRowOffset = m_book.LastRow;
            }
            this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate ()
            {
                prgExport_Single.Maximum = arrStrings.Count;
            }));


            int nMaxRow = 0;
            int nMaxCol = 0;

            try
            {
                m_book.setSheetName(0, "IMDB");
                m_book.Sheet = 0;

                for (int row = 0; row < arrStrings.Count; ++row)
                {
                    nMaxRow = Math.Max(nMaxRow, row);
                    this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate ()
                    {
                        prgExport_Single.Value = row;
                    }));
                    for (int col = 0; col < arrStrings[row].Length; ++col)
                    {
                        nMaxCol = Math.Max(nMaxCol, col);
                        m_book.setText(row + nRowOffset, col, arrStrings[row][col]);
                    }
                }

                #region Apply Style
                RangeStyle rangeStyle = m_book.getRangeStyle(0, 0, nMaxRow + nRowOffset, nMaxCol);//get format from range B2:C3
                rangeStyle.FontName = "Arial";
                m_book.setRangeStyle(rangeStyle, 0, 0, nMaxRow + nRowOffset, nMaxCol); //set format for range B2:C3
                #endregion

                //m_book.AutoRecalc = false;
                EStatus = EMode.ClosingFile;
                m_book.recalc();
                m_book.writeXLSX(strPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return -1;
            }
            finally
            {
                m_book = null;
                arrStrings = null;
            }

            return nMaxRow;
        }


        private void Do_ShowSheet(object sender, DoWorkEventArgs e)
        {
            List<string[]> strArrays = new List<string[]>();
            EStatus = EMode.ReadDataFile;
            this.ParseStringArray(ref strArrays);

            //string[] currMovieStringArr = null;
            this.Dispatcher.Invoke(new Action(delegate ()
            {
                //currMovieStringArr = strArrays[lstFiles.SelectedIndex];
                prgPresent.Maximum = strArrays.Count;
            }));

            List<Movie> arrMovie = new List<Movie>();
            // splite string arrays
            for (int i = 0; i < strArrays.Count; ++i)
            {
                var currMovie = new Movie(strArrays[i]);
                arrMovie.Add(currMovie);
                this.Dispatcher.Invoke(new Action(delegate ()
                {
                    prgPresent.Value = i;
                }));
            }


        }

        private void Done_ShowSheet(object sender, RunWorkerCompletedEventArgs e)
        {
            //MessageBox.Show("complete!");
            worker.DoWork -= Do_ShowSheet;
            worker.RunWorkerCompleted -= Done_ShowSheet;

            EStatus = EMode.Idle;
        }



        private void lstFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            strSelectedFile = lstFiles.Items[lstFiles.SelectedIndex] as string;
            var onlyFN = System.IO.Path.GetFileName(strSelectedFile);
            btnLoadExcelData.Content = "Show data of " + onlyFN;
        }

        private void btnLoadExcelData_Click(object sender, RoutedEventArgs e)
        {
            worker.DoWork += Do_ShowSheet;
            worker.RunWorkerCompleted += Done_ShowSheet;
            worker.RunWorkerAsync();
            //MessageBox.Show(lstFiles.Items[lstFiles.SelectedIndex] as string);
        }

        private void btnSplit_Click(object sender, RoutedEventArgs e)
        {
            this.SplitGrossText(txtBefore.Text, true);
        }

        private void SplitGrossText(string strTarget, bool bViewing)
        {
            string strPattern = @"(\€|\$|\£| FRF | DEM )";
            List<string> substrings = Regex.Split(strTarget, strPattern).ToList();
            List<string> arrResult = new List<string>();
            string strSet = string.Empty;

            substrings.RemoveAt(0);
            for (int i = 0; i < substrings.Count; ++i)
            {
                if (i % 2 == 0)
                {
                    strSet = substrings[i];
                }
                else
                {
                    strSet += substrings[i];
                    arrResult.Add(strSet);
                }
            }

            if(bViewing)
            {
                this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate ()
                {
                    lstGross.Clear();
                }));
            }
            

            foreach (string str in arrResult)
            {
                try
                {
                    var gross = new Gross();
                    string[] arrGross = str.Split('(');
                    gross.Amount = arrGross[0].Replace(')', ' ').Trim();
                    gross.Country = arrGross[1].Replace(')', ' ').Trim();
                    gross.SetReleaseDate(arrGross[2].Replace(')', ' ').Trim());
                    gross.Else = arrGross[3].Replace(')', ' ').Trim();
                    lstGross.Add(gross);
                }
                catch (Exception ex)
                {
                }
            }

            if(bViewing)
            {
                this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate ()
                {
                    dgAfter.ItemsSource = null;
                    dgAfter.ItemsSource = lstGross;
                }));
            }
            else
            {

            }
            
        }

        private void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            var lstFilteredGross = FilterByCountry(txtFilter.Text);
                
            if (txtFilter.Text.Trim().Equals(""))
            {
                lstFilteredGross = lstGross;
            }

            //lstGross = lstFilteredGross;
            dgAfter.ItemsSource = null;
            dgAfter.ItemsSource = lstFilteredGross;
            dgAfter.Items.Refresh();

        }

        private ObservableCollection<Gross> FilterByCountry(string str)
        {
            var lstFilteredGross = new ObservableCollection<Gross>();
            foreach (Gross g in lstGross)
            {
                if (g.Country.Equals(str))
                {
                    lstFilteredGross.Add(g);
                }
            }
            return lstFilteredGross;
        }

        private List<Gross> FilterByReleaseDate(int nFirstN)
        {
            var lstFilteredGross = new List<Gross>();
            lstFilteredGross = lstGross.ToList();
            lstFilteredGross.Sort((x, y) => DateTime.Compare(x.Releasedate, y.Releasedate));
            lstFilteredGross = lstFilteredGross.Take(nFirstN).ToList();

            return lstFilteredGross;
        }

        private string strTargetAutomationFile = string.Empty;
        private void btnLoadForAuto_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".xlsx";
            dlg.Filter = "XLSX Files (*.xlsx)|*.xlsx";

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                strTargetAutomationFile= txtAutoFilePath.Text = dlg.FileName;
            }
        }

        private void btnRunForAuto_Click(object sender, RoutedEventArgs e)
        {
            worker.DoWork += Do_GrossAutomation;
            worker.RunWorkerCompleted += Done_GrossAutomation;
            worker.RunWorkerAsync();
        }

        private void Do_GrossAutomation(object sender, DoWorkEventArgs e)
        {
            var wbWrite = new WorkBook();
            wbWrite.setSheetName(0, "Gross");
            wbWrite.Sheet = 0;

            var wbRead = new WorkBook();
            if (File.Exists(strTargetAutomationFile) == true)    // if it already exists change into read mode.
            {
                wbRead.readXLSX(strTargetAutomationFile);
                this.Dispatcher.Invoke(new Action(delegate ()
                {
                    prgGrossAuto.Maximum = wbRead.LastRow;
                }));
            }
            else
            {
                MessageBox.Show("cannot find file.");
                return;
            }

            for(int row=0; row<wbRead.LastRow; ++row)
            {
                wbRead.Sheet = 0;
                string strGrossWorld= wbRead.getText(row, 1);

                if(strGrossWorld.Trim().Equals("") == false)
                {
                    SplitGrossText(strGrossWorld, false);
                    lstGross= this.FilterByCountry("USA");
                    var lstFiltered= this.FilterByReleaseDate(4);
                    for (int item=0; item< lstFiltered.Count; ++item)
                    {
                        wbWrite.setText(row, item, lstFiltered[item].AsString());
                        
                    }
                    this.Dispatcher.Invoke(new Action(delegate ()
                    {
                        lstGross.Clear();
                        prgGrossAuto.Value = row;
                    }));
                }
                else
                {
                    wbWrite.setText(row, 0, "null");
                }
            }
            
            wbWrite.writeXLSX(@".\\Gross.xlsx");
            MessageBox.Show("file was written");

        }

        private void Done_GrossAutomation(object sender, RunWorkerCompletedEventArgs e)
        {
            worker.DoWork -= Do_GrossAutomation;
            worker.RunWorkerCompleted -= Done_GrossAutomation;
        }

        public async void RefreshRemoteTable()
        {
            prgRing.IsActive = true;
            try
            {
                var query = ParseObject.GetQuery("Parsing_range");
                IEnumerable<ParseObject> lstParsingRange = await query.FindAsync();

                TaskList.Clear();
                foreach (var PO in lstParsingRange)
                {
                    var task = new Models.Task();
                    task.Progress = Convert.ToDouble(PO["done_count"]);
                    task.ProgressMax = Convert.ToDouble(PO["quantity"]);
                    task.Range = PO["range"] as string;
                    task.RangeEnd = PO["range_end"] as string;
                    task.Status = PO["status"] as string;
                    task.Progress_server = Convert.ToString(Convert.ToDouble(PO["progress"]));

                    task.LastPage = PO["last_page"] as string;

                    if (task.LastPage.Equals(""))
                        task.LastPage = "about:blank";

                    task.StartedAt = PO["starting_time"] as string;
                    task.FinishedAt = PO["ending_time"] as string;

                    task.ProgressCaption = task.Progress + " / " + task.ProgressMax;
                    //task.rawDataURI = PO["rawdata"] as string;
                    //var applicantResumeFile = anotherApplication.Get<ParseFile>("applicantResumeFile");
                    //string resumeText = await new HttpClient().GetStringAsync(applicantResumeFile.Url);
                    TaskList.Add(task);
                }
                lstWorks.ItemsSource = null;
                lstWorks.ItemsSource = TaskList;
                prgRing.IsActive = false;

            }
            catch (Exception ex)
            {
                MessageBox.Show("Got connection pbm\n" + ex.Message);
            }
        }
        
        public void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl)
            {
                if (TabRemote.IsSelected == false)
                    return;

                RefreshRemoteTable();
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void btnRefreshRemote_Click(object sender, RoutedEventArgs e)
        {
            RefreshRemoteTable();
        }
    }
}