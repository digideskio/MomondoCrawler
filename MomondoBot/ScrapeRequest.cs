using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using System.Net.Http;
using OpenQA.Selenium.Support.UI;
using System.IO;
using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using OpenQA.Selenium.Interactions;
using System.Threading;
using System.Net;
using System.Windows.Forms;

namespace MomondoBot
{
    public class ScrapeRequest : IDisposable
    {
        public string RequestUrl { get; set; }
        public int Id { get; set; }
        private static HttpClient client = new HttpClient();
        private static MomondoForm _form;
        private string _departureDate;
        private string _arrivalDate;
        private string _departureAirport;
        private string _arrivalAirport;
        private string _domainValue;
        private int _flightDuration;
        private int _maxPrice;
        public Boolean Finished { get; set; }
        public IWebDriver driver {get; set;}
        private bool _flightReturn;
        private bool _showNodes;
        public Boolean Started { get; set; }
        private System.Windows.Forms.Timer _timer;
        private int _counter;
        private string _apiKey { get; set; } 

        public ScrapeRequest(string apiKey, bool showNodes, bool flightReturn, MomondoForm form, string departureAirport, string arrivalAirport, string departureDate, string arrivalDate, string domainValue, string flightDuration, int id, int maxPrice)
        {
            if (_form == null)
            {
                _form = form;
            }
            _departureDate = departureDate;
            Id = id;
            _arrivalDate = arrivalDate;
            _apiKey = apiKey;
            _flightReturn = flightReturn;
            _departureAirport = departureAirport;
            _arrivalAirport = arrivalAirport;
            _domainValue = domainValue;
            _maxPrice = maxPrice;
            _showNodes = showNodes;
            _flightDuration = Int32.Parse(flightDuration) * 60;
            Finished = false;

            _timer = new System.Windows.Forms.Timer();
            _counter = 0;
            _timer.Interval = 1000;
            _timer.Tick += (s, o) =>
            {
                _counter++;
            };

            if (_flightReturn)
            {
                RequestUrl = "https://momondo." + _domainValue + "/flight-search/" + departureAirport + "-" + arrivalAirport + "/" + departureDate + "/" + arrivalDate + "?fs=legdur=-" + _flightDuration;
            } else
            {
                RequestUrl = "https://momondo." + _domainValue + "/flight-search/" + departureAirport + "-" + arrivalAirport + "/" + departureDate + "/" + "?fs=legdur=-" + _flightDuration;

            }
        }

        public async Task ParseWeb(CancellationToken cts)
        {
            try
            { 
            Started = true;
            _timer.Start();
            if (driver == null)
            {
                var chromeDriverService = ChromeDriverService.CreateDefaultService();
                chromeDriverService.HideCommandPromptWindow = true;
                var timespan = TimeSpan.FromMinutes(5);
                var options = new ChromeOptions();
                if (!_showNodes)
                options.AddArgument("headless");
                options.AddArguments("--incognito");
                driver = new ChromeDriver(chromeDriverService,options,timespan);
                driver.Url = RequestUrl;
                System.Net.ServicePointManager.Expect100Continue = false;
            }
            bool indicator = false;

            while (!Finished)
            {
                    cts.ThrowIfCancellationRequested();
                try
                {
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(1));
                    wait.Until(ExpectedConditions.ElementExists(By.ClassName("Common-Results-SpinnerWithProgressBar")));

                    var searchCompleted = driver.FindElement(By.ClassName("Common-Results-SpinnerWithProgressBar"));
                    var searchCompletedText = searchCompleted.Text;
                    var results = searchCompleted.FindElement(By.ClassName("count")).Text;

                    if (searchCompletedText.Contains("Search complete") || searchCompletedText.Contains("Sökningen är klar") || searchCompletedText.Contains("Søket er fullført") || searchCompletedText.Contains("Haku valmis") || searchCompletedText.Contains("Поиск завершен") || searchCompletedText.Contains("Zoektocht compleet") || searchCompletedText.Contains("Suche beendet") || searchCompletedText.Contains("Søgning afsluttet") || searchCompletedText.Contains("Búsqueda finalizada") || searchCompletedText.Contains("Recherche terminée"))
                        {
                            if (results.Contains("0"))
                            {
                                indicator = true;
                                driver.Quit();
                                Finished = true;
                                break;
                            }
                            indicator = true;

                            cts.ThrowIfCancellationRequested();
                            var bookingValues = driver.FindElements(By.ClassName("resultWrapper")).ToList();

                            ScrapeRequest.FileWriter(driver, this, bookingValues);
                            Finished = true;
                            driver.Quit();
                            break;
                        }
                        cts.ThrowIfCancellationRequested();
                }
                catch (WebDriverTimeoutException)
                {
                    try
                    {
                        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(1));
                        wait.Until(ExpectedConditions.ElementExists(By.ClassName("g-recaptcha")));
                        await this.CheckRecaptcha(driver,cts);
                    }
                    catch (WebDriverTimeoutException)
                    {
                        try
                        {
                            var prediction = driver.FindElement(By.ClassName("predictionContainer"));
                            indicator = true;
                            var bookingValues = driver.FindElements(By.ClassName("resultWrapper")).ToList();
                            ScrapeRequest.FileWriter(driver, this, bookingValues);
                            driver.Quit();
                            Finished = true;
                            break;

                        }
                        catch (NoSuchElementException)
                        {
                            continue;
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                        //throw new Exception("LAABAI BLOGAI");
                    }
                }
                catch (StaleElementReferenceException)
                {
                    continue;
                }
                catch (NoSuchElementException)
                {
                    continue;
                }
            }

            _form.Counter -= 1;
            _form.progressas.Invoke((System.Windows.Forms.MethodInvoker)delegate
            {
            {
                _form.progressas.Increment(1);
                var reiksme = ((double)((double)_form.progressas.Value / (double)_form.progressas.Maximum)*100).ToString("0.00") + "%";
                _form.perc.Text = reiksme;
                _timer.Stop();
                double est = (double)((double) _counter * (double)_form._bunch.Count)/60;
                _form.es.Text = est.ToString() + " minutes remaining";
            }
            });
            if (_form._bunch.Count != 0)
            {
                _form._bunch.Pop().ParseWeb(cts);
            }

            }
            catch (OperationCanceledException)
            {
                _form.progressas.Invoke((MethodInvoker)delegate
                {
                    _form.progressas.Value = 0;
                    _form.perc.Text = "0";
                });
                driver.Close();
                this.driver.Quit();
            }
        }

        public async Task CheckRecaptcha(IWebDriver driver, CancellationToken cts)
            {
                bool captchaCompleted = false;
                while (!captchaCompleted)
                {
                    try
                    {
                    cts.ThrowIfCancellationRequested();
                        //var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
                        //wait.Until(ExpectedConditions.ElementExists(By.ClassName("g-recaptcha")));
                        var recaptcha = driver.FindElement(By.ClassName("g-recaptcha"));
                        var responseField = driver.FindElement(By.Id("g-recaptcha-response"));
                        var sitekey = recaptcha.GetAttribute("data-sitekey");
                        var values = new Dictionary<string, string>
                    {
                       { "key", _apiKey },
                       { "method", "userrecaptcha" },
                       { "googlekey", sitekey },
                       {"pageurl", driver.Url }
                    };
                        var content = new FormUrlEncodedContent(values);
                        var postResultId = await ScrapeRequest.PostRequest(client, content,cts);
                        postResultId = postResultId.Substring(3);
                        var token = await ScrapeRequest.GetRequest(_apiKey,client, postResultId,cts);
                        token = token.Substring(3);
                        try
                        {
                            var cap = driver.FindElement(By.Id("g-recaptcha-response"));
                            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                            js.ExecuteScript("document.getElementById('g-recaptcha-response').setAttribute('style', 'display:block;')");
                            responseField.SendKeys(token);
                            js.ExecuteScript("handleCaptcha(arguments[0])", token);
                            break;
                        } catch (NoSuchElementException)
                        {
                            continue;
                        }

                    }
                    catch (NoSuchElementException)
                    {
                        captchaCompleted = true;
                    }
                    catch (StaleElementReferenceException)
                    {
                        captchaCompleted = true;
                    }
                    catch (OperationCanceledException)
                {
                    captchaCompleted = true;
                }
                }
        }

        public static async Task<string> PostRequest(HttpClient clientas, FormUrlEncodedContent data, CancellationToken cts)
        {
            try
            {
                cts.ThrowIfCancellationRequested();
                var response = await clientas.PostAsync("http://2captcha.com/in.php", data);

                return await response.Content.ReadAsStringAsync();
            }
            catch (OperationCanceledException ex)
            {
                throw new OperationCanceledException();
            }
        }

        public static async Task<string> GetRequest(string apiKey, HttpClient clientas, string id, CancellationToken cts)
        {
            try
            {
                string responseUrl = "http://2captcha.com/res.php?key="+ apiKey + "&action=get&id=" + id;

                string responseString = "";
                string final = String.Empty;


                while (!responseString.StartsWith("O"))
                {
                    try
                    {
                        responseString = await clientas.GetStringAsync(responseUrl);
                        cts.ThrowIfCancellationRequested();
                        Thread.Sleep(10000);

                    }
                    catch (HttpRequestException)
                    {
                        continue;
                    }
                }
                final = await clientas.GetStringAsync(responseUrl);


                return final;
            }
            catch (OperationCanceledException ex)
            {
                throw new OperationCanceledException();
            }
        }

        public static void FileWriter(IWebDriver driver, ScrapeRequest request, List<IWebElement> foundResults)
        {
            List<string> providers = new List<string>();
            List<string> prices = new List<string>();
            KeyValuePair<string, int> finalValue;

            foreach (var value in foundResults)
            {
                var providerGroup = value.FindElements(By.ClassName("col-price"))
                                         .ToList()
                                         .Select(x => x.FindElement(By.ClassName("above-button")))
                                         .ToList()
                                         .Select(y => y.FindElement(By.ClassName("providerName")))
                                         .ToList()
                                         .Select(z => z.Text)
                                         .ToList();
                var priceGroup = value.FindElements(By.ClassName("col-price"))
                                         .ToList()
                                         .Select(x => x.FindElement(By.ClassName("above-button")))
                                         .ToList()
                                         .Select(y => y.FindElement(By.ClassName("price")))
                                         .ToList()
                                         .Select(z => z.Text)
                                         .ToList();

                providers.AddRange(providerGroup);
                prices.AddRange(priceGroup);
            }

            List<int> convertedPrices = new List<int>();
            foreach(var price in prices)
            {
                var trimmed = price.Substring(0, price.IndexOf(" "));
                if (trimmed.Contains("."))
                {
                    trimmed = trimmed.Remove(trimmed.IndexOf('.'),1);
                }
                if (trimmed.Contains(","))
                {
                    trimmed = trimmed.Remove(trimmed.IndexOf(','), 1);
                }
                var newPrice = Int32.Parse(trimmed);
                convertedPrices.Add(newPrice);
            }

            try
            {


                List<KeyValuePair<string, int>> values = new List<KeyValuePair<string, int>>();
                foreach (var provider in providers)
                {
                    values.Add(new KeyValuePair<string, int>(provider, convertedPrices[providers.IndexOf(provider)]));
                }
                finalValue = values.OrderBy(x => x.Value).First();
                driver.Close();
                string location = String.Empty;
                if (_form.SaveLocation == null)
                {
                    location = "results.txt";
                }
                else
                {
                    location = @_form.SaveLocation;
                }
                using (var stream = new StreamWriter(location, true))
                {
                    if (finalValue.Value <= request._maxPrice)
                    {
                        if (request._flightReturn)
                        {
                            stream.Write("\\\\\\\\\\ " + "#" + request.Id + " " + request._departureDate + "-" + request._arrivalDate + " \\\\\\\\\\" + Environment.NewLine);
                        }
                        else
                        {
                            stream.Write("\\\\\\\\\\ " + "#" + request.Id + " " + request._departureDate + "\\\\\\\\\\" + Environment.NewLine);

                        }
                        stream.Write("\\\\\\\\\\ Skrydis iš: " + request._departureAirport + " į: " + request._arrivalAirport + " \\\\\\\\\\\\" + Environment.NewLine);
                        stream.Write("Kompanija: " + finalValue.Key + Environment.NewLine + "Kaina: " + finalValue.Value + Environment.NewLine);
                        if (request._flightReturn)
                        {
                            stream.Write("\\\\\\\\\\ " + request._departureDate + "-" + request._arrivalDate + " \\\\\\\\\\ Pabaiga" + Environment.NewLine + Environment.NewLine);
                        }
                        else
                        {
                            stream.Write("\\\\\\\\\\ " + request._departureDate + " \\\\\\\\\\ Pabaiga" + Environment.NewLine + Environment.NewLine);

                        }
                    }
                }
            } catch (InvalidOperationException)
            {
                request.Finished = true;
            }
            request.Finished = true;
        }

        public void Dispose()
        {
            Finished = true;
            if (this.driver != null)
            this.driver.Quit();
        }
    }
}
