using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MomondoBot
{
    public partial class MomondoForm : Form
    {
        public string SaveLocation { get; set; }
        public bool FlyReturn { get; set; }
        public bool ShowNodes { get; set; }
        public Stack<ScrapeRequest> _bunch;
        private System.Windows.Forms.Timer timer { get; set; }
        public int Counter {get; set;}
        public ProgressBar progressas;
        public Label perc;
        public Label es;
        CancellationTokenSource cts;
        bool isValidAPI { get; set; }

        public MomondoForm()
        {
            InitializeComponent();
            apiInput.Text = Properties.Settings.Default.captchakey;
            nodesComboBox.DataSource = new List<int> {1,2,3,4,5,6,7,8,9,10};
        }

        private async void scanButton_Click(object sender, EventArgs e)
        {
            await apiCheck();
            if (isValidAPI)
            {
                cts = new CancellationTokenSource();
                progressas = progressBar1;
                perc = percentLabel;
                es = estimatedLabel;
                progressBar1.Value = 0;
                if (withReturnRadio.Checked)
                {
                    FlyReturn = true;
                }
                else
                {
                    FlyReturn = false;
                }
                if (showNodesCheckBox.Checked)
                {
                    ShowNodes = true;
                }
                else
                {
                    ShowNodes = false;
                }
                int tripDurations;
                if (FlyReturn)
                {
                    tripDurations = Int32.Parse(tripDurationRangeEnd.Text) - Int32.Parse(tripDurationRangeStart.Text) + 1;
                }
                else
                {
                    tripDurations = 1;
                }
                var departureDatesAmmount = (departureDateIntervalEnd.Value - departureDateIntervalStart.Value).Days + 2;
                timer = new System.Windows.Forms.Timer();
                int duration = 0;
                timer.Interval = 1000;
                timer.Tick += (s, o) =>
                {
                    duration++;
                    secondsLabel.Text = duration.ToString();
                };
                timer.Start();
                if (FlyReturn)
                {
                    progressBar1.Maximum = departureDatesAmmount * tripDurations;
                }
                else
                {
                    progressBar1.Maximum = departureDatesAmmount;
                }
                var answer = Task.Run(() =>
                {
                    Stack<ScrapeRequest> requests = new Stack<ScrapeRequest>();
                    int a = 0;
                    if (FlyReturn)
                    {
                        for (int i = 0; i < departureDatesAmmount; i++)
                        {
                            for (var j = Int32.Parse(tripDurationRangeStart.Text); j <= Int32.Parse(tripDurationRangeEnd.Text); j++)
                            {
                                ScrapeRequest request = new ScrapeRequest(apiInput.Text, ShowNodes, FlyReturn, this, departureAirport.Text, arrivalAirport.Text, departureDateIntervalStart.Value.AddDays(i).ToString("yyyy-MM-dd"), departureDateIntervalStart.Value.AddDays(i).AddDays(j).ToString("yyyy-MM-dd"), domainValue.Text, flightDuration.Text, ++a, Int32.Parse(maxPrice.Text));
                                requests.Push(request);
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < departureDatesAmmount; i++)
                        {
                            ScrapeRequest request = new ScrapeRequest(apiInput.Text, ShowNodes, FlyReturn, this, departureAirport.Text, arrivalAirport.Text, departureDateIntervalStart.Value.AddDays(i).ToString("yyyy-MM-dd"), null, domainValue.Text, flightDuration.Text, ++a, Int32.Parse(maxPrice.Text));
                            requests.Push(request);
                        }

                    }

                    return requests;
                });

                var check = await answer;
                _bunch = check;
                Counter = Int32.Parse(nodesComboBox.SelectedValue.ToString());

                if (answer.Result.Count < Counter)
                {
                    Counter = answer.Result.Count;
                }

                int inversible = Counter;
                List<Task> tasks1 = new List<Task>();
                for (int i = 0; i < Counter; i++)
                {
                    new Thread(() =>
                    {
                        check.Pop().ParseWeb(cts.Token);
                    }).Start();
                }
            } else
            {
                MessageBox.Show("Please enter Correct 2captcha.com API key or check balance");
            }
            //timer.Stop();
            //MessageBox.Show("Totally " + duration + " seconds passed");
            //duration = 0;
        }

        private void label6_Click(object sender, EventArgs e)
        {

        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            // Prepare a dummy string, thos would appear in the dialog
            string dummyFileName = "Save Here";

            SaveFileDialog sf = new SaveFileDialog();
            // Feed the dummy name to the save dialog
            sf.FileName = dummyFileName;

            if (sf.ShowDialog() == DialogResult.OK)
            {
                // Now here's our save folder
                SaveLocation = sf.FileName + sf.DefaultExt;
            }
        }

        private void oneWayRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (oneWayRadio.Checked)
            {
                tripDurationRangeEnd.Enabled = false;
                tripDurationRangeStart.Enabled = false;
            } else
            {
                tripDurationRangeEnd.Enabled = true;
                tripDurationRangeStart.Enabled = true;
            }
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            if (cts != null)
            {
                cts.Cancel();
            }

            timer.Stop();
            secondsLabel.Text = "0";
            percentLabel.Text = "0%";
            _bunch.Clear();
            progressas.Value = 0;
        }

        private void withReturnRadio_CheckedChanged(object sender, EventArgs e)
        {

        }

        private async void SaveAPI_Click(object sender, EventArgs e)
        {
            if (!String.IsNullOrEmpty(apiInput.Text))
            {
                Properties.Settings.Default.captchakey = apiInput.Text;
                Properties.Settings.Default.Save();
            }

            await apiCheck();
        }
        private async Task apiCheck()
        {
            using (var http = new HttpClient())
            {
                var response = await http.GetStringAsync("http://2captcha.com/in.php?key=" + apiInput.Text + "&s_s_c_user_id=10&s_s_c_session_id=493e52c37c10c2bcdf4a00cbc9ccd1e8&s_s_c_web_server_sign=9006dc725760858e4c0715b835472f22-pz-&s_s_c_web_server_sign2=2ca3abe86d90c6142d5571db98af6714&method=keycaptcha&pageurl=https://www.keycaptcha.ru/demo-magnetic/");
                if (response.StartsWith("O"))
                {
                    isValidAPI = true;
                }
                else
                {
                    isValidAPI = false;
                }
            }
        }
    }
}
