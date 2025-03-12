using LibVLCSharp.Forms.Shared;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace vlcplayer
{
    public partial class MainPage : ContentPage
    {
        LibVLC _libVLC;
        MediaPlayer _mediaPlayer;
        VideoView _videoView;
        float _position;
        private string _videoId = "4a4662c5a313410aa9e52d1e52d1e9ab99aba6";


        private Dictionary<TimeSpan, List<Question>> _questionGroups;
        private Dictionary<TimeSpan, int> _currentQuestionIndex;
        private TimeSpan group1Time = TimeSpan.FromSeconds(10);
        private TimeSpan group2Time = TimeSpan.FromSeconds(20);

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
            LoadQuestions();  // ✅ Load questions at startup
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

            StartQuestionCheck();  // ✅ Start checking for questions
            StartSliderUpdate();
            Device.StartTimer(TimeSpan.FromMilliseconds(500), () =>
            {
                UpdateSliderHighlights();
                return false;
            });

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

        private void UpdateSliderHighlights()
        {
            double sliderWidth = DurationSlider.Width;
            if (sliderWidth > 0 && DurationSlider.Maximum > 0)
            {
                // Get positions of question times relative to the slider
                double marker1Pos = (group1Time.TotalSeconds / DurationSlider.Maximum) * 1.3;
                double marker2Pos = (group2Time.TotalSeconds / DurationSlider.Maximum) * 1.3;

                // Update AbsoluteLayout positions 
                Device.BeginInvokeOnMainThread(() =>
                {
                    AbsoluteLayout.SetLayoutBounds(Marker1, new Rectangle(marker1Pos, 0, 20, 15));
                    AbsoluteLayout.SetLayoutBounds(Marker2, new Rectangle(marker2Pos, 0, 20, 15));

                    Marker1.IsVisible = true;
                    Marker2.IsVisible = true;
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

        private void LoadQuestions()
        {
            _questionGroups = new Dictionary<TimeSpan, List<Question>>();
            _currentQuestionIndex = new Dictionary<TimeSpan, int>();



            _questionGroups[group1Time] = new List<Question>
             {
             new Question
             {
                 QuestionText = "<p>What is the <b>capital</b> of France kjsdhfjaskdjbfdamf,fkghlakfngaklgkdfsng lalfjdngadkjgldfbgandfgalgnbfganldfga mfndkg alfgafdg?</p>",
                 Options = new List<string> { "Paris", "London is the capital of the world and is the world's' responsibility for protecting the environment", "Berlin", "Madrid" },
                 CorrectAnswer = "Paris"
             },
             new Question
             {
                 QuestionText ="<p>What is the <b>capital</b> of France?</p><img src='https://cdn.pixabay.com/photo/2025/02/07/18/31/peacock-9390809_1280.jpg' alt='Paris' width='200' height='150' />",
                 Options = new List<string> { "Brazil", "Germany", "Spain", "Argentina" },
                 CorrectAnswer = "Germany"
             },
             new Question
             {
                 QuestionText = "<p>What is the name of the famous painting by <u>Leonardo da Vinci</u>?</p>",
                 Options = new List<string> { "Mona Lisa", "The Last Supper", "The Starry Night", "The Creation of Adam" },
                 CorrectAnswer = "Mona Lisa"
             }
             };

            _questionGroups[group2Time] = new List<Question>
             {
                 new Question
                 {
                     QuestionText = "<p>What is the name of the famous painting by <u>Leonardo da Vinci</u>?</p>",
                     Options = new List<string> { "Joe Biden", "George Osborne", "David Cameron", "Australian Labor Party Leader" },
                     CorrectAnswer = "David Cameron"
                 },
                 new Question
                 {
                     QuestionText = "<p>Who won the <i>2018 FIFA World Cup</i>?</p>",
                     Options = new List<string> { "Stockholm is the biggest capital in the world", "Copenhagen", "London", "Berlin" },
                     CorrectAnswer = "London"
                 },
             };

            foreach (var timestamp in _questionGroups.Keys)
            {
                _currentQuestionIndex[timestamp] = 0;
                Debug.WriteLine($"📝 Questions added for timestamp {timestamp}: {_questionGroups[timestamp].Count} questions.");
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

                var htmlContent = $@"
                    <html>
                    <head>
                        <style> body {{ font-family: Arial; font-size: 16px; color: black; text-align: center; }} </style>
                    </head>
                    <body> {question.QuestionText} </body>
                    </html>";

                QuestionWebView.Source = new HtmlWebViewSource { Html = htmlContent };
                OptionsContainer.Children.Clear();
                CorrectAnswerLabel.IsVisible = false;
                CorrectAnswerLabel.Text = "";

                foreach (var option in question.Options)
                {
                    Button optionButton = new Button
                    {
                        Text = option,
                        FontSize = 16,
                        BackgroundColor = Color.LightGray,
                        TextColor = Color.Black
                    };

                    optionButton.Clicked += async (sender, args) =>
                    {
                        if (option == question.CorrectAnswer)
                        {
                            Debug.WriteLine("✅ Correct answer selected.");
                            optionButton.BackgroundColor = Color.Green;
                            CorrectAnswerLabel.Text = "Correct!";
                            CorrectAnswerLabel.TextColor = Color.Green;
                            CorrectAnswerLabel.IsVisible = true;
                            await Task.Delay(2000);

                            _currentQuestionIndex[timestamp]++;
                            QuestionPopup.IsVisible = false;
                            ShowNextQuestion(timestamp);
                        }
                        else
                        {
                            Debug.WriteLine("❌ Incorrect answer selected.");
                            optionButton.BackgroundColor = Color.Red;
                            CorrectAnswerLabel.Text = "Wrong! Try again.";
                            CorrectAnswerLabel.TextColor = Color.Red;
                            CorrectAnswerLabel.IsVisible = true;
                        }
                    };

                    OptionsContainer.Children.Add(optionButton);
                }

                QuestionPopup.IsVisible = true;
                Console.WriteLine("Completed Question Setup");
            });
        }


    }

    public class Question
    {
        public string QuestionText { get; set; }
        public List<string> Options { get; set; }
        public string CorrectAnswer { get; set; }
    }
}