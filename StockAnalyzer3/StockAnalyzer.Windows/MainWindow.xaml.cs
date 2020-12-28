using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using Newtonsoft.Json;
using StockAnalyzer.Core.Domain;
using StockAnalyzer.Windows.Services;

namespace StockAnalyzer.Windows
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        CancellationTokenSource cancellationTokenSource = null;

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            #region Before loading stock data
            var watch = new Stopwatch();
            watch.Start();
            StockProgress.Visibility = Visibility.Visible;
            StockProgress.IsIndeterminate = true;

            Search.Content = "Cancel";
            #endregion

            //cancellation logic (validate user input)
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource = null;
                return;
            }

            //showing that the procedure has already fired
            cancellationTokenSource = new CancellationTokenSource();

            cancellationTokenSource.Token.Register(() =>
            {
                Notes.Text = "Cancellation requested";
            });

            try
            {
                var service = new StockService();
                var data = await service.GetStockPricesFor(Ticker.Text);

                Stocks.ItemsSource = data;
            }
            catch (Exception ex)
            {

                Notes.Text = ex.Message;
            }

            #region After stock data is loaded
            StocksStatus.Text = $"Loaded stocks for {Ticker.Text} in {watch.ElapsedMilliseconds}ms";
            StockProgress.Visibility = Visibility.Hidden;
            Search.Content = "Search";
            #endregion

            //var loadLinesTask = SearchForStocks(cancellationTokenSource.Token);
            //var processStocksTask = loadLinesTask.ContinueWith(t =>
            //{
            //    var lines = t.Result;
            //    var data = new List<StockPrice>();

            //    foreach (var line in lines.Skip(1))
            //    {
            //        var segments = line.Split(',');

            //        for (var i = 0; i < segments.Length; i++) segments[i] = segments[i].Trim('\'', '"');
            //        var price = new StockPrice
            //        {
            //            Ticker = segments[0],
            //            TradeDate = DateTime.ParseExact(segments[1], "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture),
            //            Volume = Convert.ToInt32(segments[6], CultureInfo.InvariantCulture),
            //            Change = Convert.ToDecimal(segments[7], CultureInfo.InvariantCulture),
            //            ChangePercent = Convert.ToDecimal(segments[8], CultureInfo.InvariantCulture),
            //        };
            //        data.Add(price);
            //    }

            //    Dispatcher.Invoke(() =>
            //    {
            //        Stocks.ItemsSource = data.Where(price => price.Ticker == Ticker.Text);
            //    });
            //},
            //    cancellationTokenSource.Token
            //    ,TaskContinuationOptions.OnlyOnRanToCompletion
            //    ,TaskScheduler.Current);

            ////continue on failure
            //loadLinesTask.ContinueWith(t =>
            //{
            //    Dispatcher.Invoke(() =>
            //    {
            //        Notes.Text = t.Exception.InnerException.Message;
            //    });
            //}, TaskContinuationOptions.OnlyOnFaulted);

            //processStocksTask.ContinueWith(_ =>
            //{
            //    Dispatcher.Invoke(() =>
            //    {
            //        #region After stock data is loaded
            //        StocksStatus.Text = $"Loaded stocks for {Ticker.Text} in {watch.ElapsedMilliseconds}ms";
            //        StockProgress.Visibility = Visibility.Hidden;
            //        Search.Content = "Search";
            //        #endregion
            //    });
            //});
        }


        /// <summary>
        /// Get the stocks from the file if <see cref="CancellationTokenSource"/> is not set
        /// </summary>
        /// <param name="cancellationToken">Checks to see if the call has already been made based on <see cref="CancellationTokenSource"/>  </param>
        /// <returns>Stocks unformatted</returns>
        private Task<List<string>> SearchForStocks(CancellationToken cancellationToken)
        {

            //load in the file
            var loadLinesTask = Task.Run(async () =>
            {
                var lines = new List<string>();
                using (var stream = new StreamReader(File.OpenRead(@"StockPrices_Small.csv")))
                {
                    string line;
                    while ((line = await stream.ReadLineAsync()) != null)
                    {
                        if (cancellationToken.IsCancellationRequested) return lines;                    
                        lines.Add(line);
                    }
                }
                return lines;
                //var lines = File.ReadAllLines(@"StockPrices_Small.csv");//StockPrices_Small.csv

                //return lines;
                //var data = new List<StockPrice>();

                //foreach (var line in lines.Skip(1))
                //{
                //    var segments = line.Split(',');

                //    for (var i = 0; i < segments.Length; i++) segments[i] = segments[i].Trim('\'', '"');
                //    var price = new StockPrice
                //    {
                //        Ticker = segments[0],
                //        TradeDate = DateTime.ParseExact(segments[1], "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture),
                //        Volume = Convert.ToInt32(segments[6], CultureInfo.InvariantCulture),
                //        Change = Convert.ToDecimal(segments[7], CultureInfo.InvariantCulture),
                //        ChangePercent = Convert.ToDecimal(segments[8], CultureInfo.InvariantCulture),
                //    };
                //    data.Add(price);
                //}

                //Dispatcher.Invoke(() =>
                //{
                //    Stocks.ItemsSource = data.Where(price => price.Ticker == Ticker.Text);
                //});
            }, cancellationToken);
            return loadLinesTask;
        }

        /// <summary>
        /// Set the stocks in memory correctly formatted from <see cref="SearchForStocks"/> 
        /// </summary>
        /// <param name="loadLinesTask">records from <see cref="SearchForStocks"/> </param>
        /// <returns></returns>
        private Task ProcessStocks(Task<List<string>> loadLinesTask)
        {

            //continue on success
            return loadLinesTask.ContinueWith(t =>
            {
                var lines = t.Result;
                var data = new List<StockPrice>();

                foreach (var line in lines.Skip(1))
                {
                    var segments = line.Split(',');

                    for (var i = 0; i < segments.Length; i++) segments[i] = segments[i].Trim('\'', '"');
                    var price = new StockPrice
                    {
                        Ticker = segments[0],
                        TradeDate = DateTime.ParseExact(segments[1], "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture),
                        Volume = Convert.ToInt32(segments[6], CultureInfo.InvariantCulture),
                        Change = Convert.ToDecimal(segments[7], CultureInfo.InvariantCulture),
                        ChangePercent = Convert.ToDecimal(segments[8], CultureInfo.InvariantCulture),
                    };
                    data.Add(price);
                }

                Dispatcher.Invoke(() =>
                {
                    Stocks.ItemsSource = data.Where(price => price.Ticker == Ticker.Text);
                });
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));

            e.Handled = true;
        }

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
