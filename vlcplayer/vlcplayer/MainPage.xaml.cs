using LibVLCSharp.Forms.Shared;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Newtonsoft.Json;
using System.Linq;

namespace vlcplayer
{
    public partial class MainPage : ContentPage
    {
        LibVLC _libVLC;
        MediaPlayer _mediaPlayer;
        VideoView _videoView;
        float _position;
        private static readonly HttpClient Client = new HttpClient();

        private Dictionary<TimeSpan, List<Question>> _questionGroups;
        private Dictionary<TimeSpan, int> _currentQuestionIndex;
        private List<int> questionPositions = new List<int>();
       
        public static readonly BindableProperty QuestionPopupWidthProperty = BindableProperty.Create(nameof(QuestionPopupWidth), typeof(double), typeof(MainPage), 300.0);
        public static readonly BindableProperty QuestionPopupHeigthProperty = BindableProperty.Create(nameof(QuestionPopupHeight), typeof(double), typeof(MainPage), 500.0);
        public static readonly BindableProperty QuestionWebViewPopupHeightProperty = BindableProperty.Create(nameof(QuestionWebViewPopupHeight), typeof(double), typeof(MainPage), 250.0);
        public double QuestionPopupWidth
        {
            get => (double)GetValue(QuestionPopupWidthProperty);
            set => SetValue(QuestionPopupWidthProperty, value);
        }
        public double QuestionPopupHeight
        {
            get => (double)GetValue(QuestionPopupHeigthProperty);
            set => SetValue(QuestionPopupHeigthProperty, value);
        }
        public double QuestionWebViewPopupHeight
        {
            get => (double)GetValue(QuestionWebViewPopupHeightProperty);
            set => SetValue(QuestionWebViewPopupHeightProperty, value);
        }


        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
            _ = LoadQuestionsFromApi();  // ✅ Load questions at startup
            Debug.WriteLine("✅ Questions loaded at startup.");

            //Calculate height of the quesiton popup
            var deviceHeight = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density;
            var deviceWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
            QuestionPopupHeight = deviceHeight * 0.7;
            QuestionPopupWidth = deviceWidth * 0.8;
            QuestionWebViewPopupHeight = QuestionPopupHeight * 0.6;

            MessagingCenter.Subscribe<string>(this, "OnPause", app =>
            {
                Debug.WriteLine("⏸️ App went to background. Saving video position...");
                VideoView.MediaPlayerChanged -= MediaPlayerChanged;
                if (_videoView != null)
                {
                    _videoView.MediaPlayerChanged -= MediaPlayerChanged;
                    _videoView = null;
                }
                _mediaPlayer.Pause();
                _position = _mediaPlayer.Position;
                _mediaPlayer.Stop();
                MainGrid.Children.Clear();
                Debug.WriteLine($"📌 Saved media player position: {_position}");
            });

            MessagingCenter.Subscribe<string>(this, "OnRestart", app =>
            {
                Debug.WriteLine("🔄 Restarting video...");

                try
                {
                    if (_videoView == null)
                    {
                        _videoView = new VideoView { HorizontalOptions = LayoutOptions.FillAndExpand, VerticalOptions = LayoutOptions.FillAndExpand };
                        MainGrid.Children.Add(_videoView);
                    }
                    _videoView.MediaPlayerChanged += MediaPlayerChanged;
                    _videoView.MediaPlayer = _mediaPlayer;

                    if (_mediaPlayer != null)
                    {
                        _mediaPlayer.Position = _position;
                        _mediaPlayer.Play();
                        Debug.WriteLine($"🎥 Resuming video at position: {_position}");
                        //_position = 0;
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            DurationSlider.Value = _mediaPlayer.Time / 1000;
                            //DurationSlider.Maximum = _mediaPlayer.Length / 1000;
                            ElapsedTimeLabel.Text = TimeSpan.FromMilliseconds(_mediaPlayer.Time).ToString(@"hh\:mm\:ss");
                            DurationLabel.Text = TimeSpan.FromMilliseconds(_mediaPlayer.Length).ToString(@"hh\:mm\:ss");
                            UpdateSliderHighlights();
                        });
                    }
                    MainGrid.Children.Add(QuestionPopup);
                    MainGrid.Children.Add(TimeSlider);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"⚠️The restart exception : {ex}");
                }

            });
        }



        protected override void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("🟢 App appeared. Setting up video player...");

            VideoView.MediaPlayerChanged += MediaPlayerChanged;
            Core.Initialize();

            _libVLC = new LibVLC();
            var media = new Media(_libVLC, new Uri("https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4"));

            _mediaPlayer = new MediaPlayer(_libVLC)
            {
                Media = media
            };

            VideoView.MediaPlayer = _mediaPlayer;
            _mediaPlayer.Play();
            Debug.WriteLine("🎬 Video started playing.");        
            Device.BeginInvokeOnMainThread(StartSliderUpdate);

        }
        private async void StartSliderUpdate()
        {
            while (true)
            {
                if (_mediaPlayer != null && _mediaPlayer.IsPlaying)
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        DurationSlider.Maximum = _mediaPlayer.Length / 1000;  // Convert ms to sec
                        DurationSlider.Value = _mediaPlayer.Time / 1000;      // Update slider position

                        //// Update Labels
                        ElapsedTimeLabel.Text = TimeSpan.FromMilliseconds(_mediaPlayer.Time).ToString(@"hh\:mm\:ss");
                        DurationLabel.Text = TimeSpan.FromMilliseconds(_mediaPlayer.Length).ToString(@"hh\:mm\:ss");
                        UpdateSliderHighlights();

                    });
                }
                await Task.Delay(500);  // Update every 0.5 seconds
            }
        }

        //Add quesiton Markers for questions
        private void SetQuestionMarkers(List<int> questionTime)
        {
            questionPositions = questionTime;
            UpdateSliderHighlights();
        }

        private void UpdateSliderHighlights()
        {
            double sliderWidth = DurationSlider.Width;
            if (sliderWidth > 0 && DurationSlider.Maximum > 0)
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    MarkerLayout.Children.Clear();
                    MarkerLayout.Children.Add(DurationSlider);
                    foreach (var timeSec in questionPositions)
                    {
                        double markerPosition = (timeSec / DurationSlider.Maximum) * 1.15;

                        Image marker = new Image
                        {
                            Source = "question_marker.png",
                            WidthRequest = 20,
                            HeightRequest = 20,
                        };
                        AbsoluteLayout.SetLayoutBounds(marker, new Rectangle(markerPosition, -0.5, 20, 20));
                        AbsoluteLayout.SetLayoutFlags(marker, AbsoluteLayoutFlags.PositionProportional);
                        MarkerLayout.Children.Add(marker);
                    }
                });
            }
        }


        private void MediaPlayerChanged(object sender, EventArgs e)
        {
            _mediaPlayer.Play();
        }
        //static string[] state = new string[] { "visible", "hidden" };
        private async void StartQuestionCheck()
        {
            Debug.WriteLine("🔍 Starting periodic question check...");
            while (true)
            {
                //Console.Out.WriteLine($"Popup state {(QuestionPopup.IsVisible ? state[0]:state[1])}");
                if (_mediaPlayer != null && _mediaPlayer.IsPlaying)
                {
                    TimeSpan currentTime = TimeSpan.FromSeconds(_mediaPlayer.Time / 1000);
                    Debug.WriteLine($"⏳ Current Video Time: {currentTime}");

                    Device.BeginInvokeOnMainThread(() =>
                    {
                        ElapsedTimeLabel.Text = currentTime.ToString(@"hh\:mm\:ss");
                        DurationSlider.Value = currentTime.TotalSeconds;
                    });

                    foreach (var timestamp in _questionGroups.Keys)
                    {
                        if (currentTime >= timestamp && _currentQuestionIndex[timestamp] < _questionGroups[timestamp].Count)
                        {
                            Debug.WriteLine($"🛑 Pausing video to show question at {timestamp}...");
                            ShowNextQuestion(timestamp);
                            break;
                        }
                    }
                }
                await Task.Delay(1000); // Check every second
            }
        }
        //private void GetVideoStatus(object sender,EventArgs args)
        //{
        //    Console.WriteLine(MainGrid.Children);
        //}

        private async Task LoadQuestionsFromApi()
        {
            _questionGroups = new Dictionary<TimeSpan, List<Question>>();
            _currentQuestionIndex = new Dictionary<TimeSpan, int>();
            string videoId = "4a4662c5a313410a9e52d1e9ab99aba6";
            string questionUrl = $"https://prolalmsqaenv.azurewebsites.net/webapi/videos/{videoId}/questions";
            string authToken = $"eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJkNTdjMzAwZi1lMzdlLTRiNjYtOTcwZS1hMTBiOWI1NTdiOTUiLCJSb2xlIjoiU3R1ZGVudCIsImluc3RJZCI6ImViNmEwYmJjLTk4NGMtNDY3ZS05OWEwLTI0YmQ5M2IwNTBkMiIsInZlciI6IjMiLCJpc3MiOiJodHRwczovL3Byb2xhbG1zcWFlbnYuYXp1cmV3ZWJzaXRlcy5uZXQvIiwiYXVkIjoiaHR0cHM6Ly9wcm9sYWxtc3FhZW52LmF6dXJld2Vic2l0ZXMubmV0LyIsImV4cCI6MTc0MTg1Mjc3MiwibmJmIjoxNzQxODUwOTcyfQ.mNsyKfIeqNJ1_kJZZsezZP9kCBdJUS3A9JnY-SDN3gs";
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, questionUrl))
                {
                    request.Headers.Clear();
                    request.Headers.Add("Authorization", $"Bearer {authToken}");

                    HttpResponseMessage response = await Client.SendAsync(request);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    List<ApiQuestion> apiQuestions = JsonConvert.DeserializeObject<List<ApiQuestion>>(responseBody);
                    foreach (var apiQuestion in apiQuestions)
                    {
                        TimeSpan position = TimeSpan.FromSeconds(apiQuestion.PositionSec);
                        var question = new Question
                        {
                            QuestionText = apiQuestion.QuestionText,
                            Options = new List<Option>()
                        };
                        foreach (var option in apiQuestion.AnswerOption)
                        {
                            question.Options.Add(new Option
                            {
                                OptionText = option.OptionText,
                                IsCorrect = option.IsCorrect
                            });
                        }
                        if (!_questionGroups.ContainsKey(position))
                        {
                            _questionGroups[position] = new List<Question>();
                            _currentQuestionIndex[position] = 0;
                        }
                        _questionGroups[position].Add(question);
                    }
                    SetQuestionMarkers(apiQuestions.Select(q=>q.PositionSec).ToList());
                }
                Debug.WriteLine($"Questions Loaded :{_questionGroups.Count} timestamps detected.");
                Device.BeginInvokeOnMainThread(StartQuestionCheck);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Failed to load questions: {ex}");
            }



        }

        private void ShowNextQuestion(TimeSpan timestamp)
        {
            int completedQuestionsCount = _currentQuestionIndex[timestamp];

            if (completedQuestionsCount < _questionGroups[timestamp].Count)
            {
                Debug.WriteLine($"📢 Showing question {completedQuestionsCount + 1} at {timestamp}...");
                Question question = _questionGroups[timestamp][completedQuestionsCount];
                ShowQuestionPopup(question, timestamp);
            }
            else
            {
                Debug.WriteLine("✅ All questions answered. Resuming video...");
                _mediaPlayer.Play();
            }
        }

        private void ShowQuestionPopup(Question question, TimeSpan timestamp)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                Debug.WriteLine($"🛑 Pausing video and displaying question: {question.QuestionText}");
                _mediaPlayer.Pause();

                QuestionWebView.Source = new HtmlWebViewSource { Html = question.QuestionText };
                OptionsContainer.Children.Clear();

                foreach (var option in question.Options)
                {
                    Label optionLabel = new Label
                    {
                        Text = HtmlToPlainText(option.OptionText),
                        FontAttributes = FontAttributes.Bold,
                        BackgroundColor = Color.LightGray,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center,
                        Padding = new Thickness(15)
                    };
                    TapGestureRecognizer tapGesture = new TapGestureRecognizer();
                    tapGesture.Tapped += async (sender, args) =>
                    {
                        if (option.IsCorrect)
                        {
                            optionLabel.BackgroundColor = Color.Green;
                            await Task.Delay(2000);
                            _currentQuestionIndex[timestamp]++;
                            QuestionPopup.IsVisible = false;
                            ShowNextQuestion(timestamp);
                        }
                        else
                        {
                            optionLabel.BackgroundColor = Color.Red;
                            await Task.Delay(1000);
                            optionLabel.BackgroundColor = Color.LightGray;
                        }
                    };
                    optionLabel.GestureRecognizers.Add(tapGesture);
                    OptionsContainer.Children.Add(optionLabel);
                }
                QuestionPopup.IsVisible = true;
            });
        }


        private string HtmlToPlainText(string html)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);
            return doc.DocumentNode.InnerText;
        }

    }

    public class ApiQuestion
    {
        public string QuestionId { get; set; }
        public string QuestionText { get; set; }
        public int PositionSec { get; set; }
        public List<ApiOption> AnswerOption { get; set; }
    }

    public class ApiOption
    {
        public string OptionId { get; set; }
        public string OptionText { get; set; }
        public bool IsCorrect { get; set; }
    }

    public class Question
    {
        public string QuestionText { get; set; }
        public List<Option> Options { get; set; }
    }

    public class Option
    {
        public string OptionText { get; set; }
        public bool IsCorrect { get; set; }
    }
}
