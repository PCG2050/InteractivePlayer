﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.Forms.Shared;
using System.IO;
using InteractivePlayer.Model;

namespace InteractivePlayer
{
    public partial class MainPage : ContentPage
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;

        private Dictionary<TimeSpan, List<Question>> _questionGroups = new Dictionary<TimeSpan, List<Question>>();
        private Dictionary<TimeSpan, int> _currentQuestionIndex;

        private TimeSpan _lastPlaybackPosition;
        private TimeSpan _lastQuestionTimestamp;
        private bool _isQuestionVisible;

        #region MoviePosition
        public static readonly BindableProperty MoviePositionProperty = BindableProperty.Create(nameof(MoviePosition), typeof(float), typeof(MainPage), propertyChanged: (obj, old, newV) =>
        {
            var me = obj as MainPage;
            if (newV != null && !(newV is float)) return;
            var oldMoviePosition = (float)old;
            var newMoviePosition = (float)newV;
            me?.MoviePositionChanged(oldMoviePosition, newMoviePosition);
        });

        private void MoviePositionChanged(float oldMoviePosition, float newMoviePosition)
        {

        }

        /// <summary>
        /// A bindable property
        /// </summary>
        public float MoviePosition
        {
            get => (float)GetValue(MoviePositionProperty);
            set => SetValue(MoviePositionProperty, value);
        }
        #endregion

        public MainPage()
        {
            InitializeComponent();
            MessagingCenter.Subscribe<String>(this, "OnPause", OnAppSuspending);
            MessagingCenter.Subscribe<String>(this, "OnResume", OnAppResuming);
            Core.Initialize();
            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadQuestions();

            if (_mediaPlayer.Media == null) // Only set media if not already set
            {
                _mediaPlayer.Media = new Media(_libVLC, new Uri("http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ElephantsDream.mp4"));
                VideoView.MediaPlayer = _mediaPlayer;
            }

            _mediaPlayer.PositionChanged += MediaPlayerPositionChanged;
            _mediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
            _mediaPlayer.EncounteredError += MediaPlayer_EncounteredError;
            _mediaPlayer.EndReached += MediaPlayer_EndReached;
            _mediaPlayer.Playing += MediaPlayer_Playing;

            if (_lastPlaybackPosition != TimeSpan.Zero)
            {
                SeekTo(_lastPlaybackPosition);
                if (_isQuestionVisible)
                {
                    ShowNextQuestion(_lastQuestionTimestamp);
                }
                else
                {
                    _mediaPlayer.Play();
                }
            }
            else
            {
                _mediaPlayer.Play();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Store the last playback position
            _lastPlaybackPosition = TimeSpan.FromMilliseconds(_mediaPlayer.Time);

            // Pause the media player
            _mediaPlayer.Pause();
        }
        private void OnAppSuspending(string sender)
        {
            _lastPlaybackPosition = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
            _isQuestionVisible = QuestionPopup.IsVisible;
            if (_isQuestionVisible)
            {
                _lastQuestionTimestamp = _questionGroups.Keys.FirstOrDefault(ts => _currentQuestionIndex[ts] < _questionGroups[ts].Count);
            }
            _mediaPlayer.Pause();
        }

        private void OnAppResuming(string sender)
        {
            if (_lastPlaybackPosition != TimeSpan.Zero)
            {
                // Recreate Media and attach to VideoView to fix black screen issue
                _mediaPlayer.Media = new Media(_libVLC, new Uri("http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ElephantsDream.mp4"));
                VideoView.MediaPlayer = _mediaPlayer; // 🔴 IMPORTANT: Reassign MediaPlayer

                SeekTo(_lastPlaybackPosition);

                if (_isQuestionVisible)
                {
                    ShowNextQuestion(_lastQuestionTimestamp);
                }
                else
                {
                    _mediaPlayer.Play();
                }
            }
        }

        // Removed duplicate OnAppResuming method

        public async Task PlayStreamWithHeaders()
        {
            var stream = await GetStreamFromUrl("http://mediathatrequireauth", new Dictionary<string, string>
            {
                { "Authentication", "Bearer {Token goes here}"}
            });
            var mediaInput = new StreamMediaInput(stream);
            var media = new Media(_libVLC, mediaInput);
            _mediaPlayer.Play(media);
        }

        private async Task<Stream> GetStreamFromUrl(string url, Dictionary<string, string> headers)
        {
            byte[] data;

            using (var client = new System.Net.Http.HttpClient())
            {
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }

                data = await client.GetByteArrayAsync(url);
            }

            return new MemoryStream(data);
        }

        private void MediaPlayer_Playing(object sender, EventArgs e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                DurationLabel.Text = string.Format("{0:mm\\:ss}", TimeSpan.FromMilliseconds(_mediaPlayer.Length));
            });
        }

        private DateTime _lastUpdateTime = DateTime.MinValue;
        private void MediaPlayer_TimeChanged(object sender, MediaPlayerTimeChangedEventArgs e)
        {
            var now = DateTime.Now;
            if ((now - _lastUpdateTime).TotalMilliseconds < 500) // Update UI every 500ms
                return;
            _lastUpdateTime = now;

            Device.BeginInvokeOnMainThread(() =>
            {
                ElapsedTimeLabel.Text = string.Format("{0:mm\\:ss}", TimeSpan.FromMilliseconds(_mediaPlayer.Time));
                // Check if it's time for questions
                foreach (var timestamp in _questionGroups.Keys)
                {
                    if (TimeSpan.FromMilliseconds(_mediaPlayer.Time) >= timestamp && _currentQuestionIndex[timestamp] < _questionGroups[timestamp].Count)
                    {
                        _mediaPlayer.SetPause(false);
                        ShowNextQuestion(timestamp);
                        break;
                    }
                }
            });
        }

        private void MediaPlayer_EncounteredError(object sender, EventArgs e)
        {
            // Called when an error occurs while playing the media item.
        }

        private void MediaPlayer_EndReached(object sender, EventArgs e)
        {
            // Called when the media player terminates the current media
            // Note: When this is called, if you have a queue to manage, call the next item on the queue.
        }

        private void MediaPlayerPositionChanged(object sender, MediaPlayerPositionChangedEventArgs e)
        {
            MoviePosition = e.Position * 100;
        }

        private void StopButton_Clicked(object sender, EventArgs e)
        {
            _mediaPlayer.Stop();
        }

        void SeekTo(TimeSpan seconds)
        {
            _mediaPlayer.Time = (long)seconds.TotalMilliseconds;
        }

        private void PlayPauseButton_Clicked(object sender, EventArgs e)
        {
            // Note: Use the set pause option to resume the player from pause state
            // Since using the play option will play as if it was a new media we added
            _mediaPlayer.SetPause(_mediaPlayer.IsPlaying);
            PlayPauseButton.Text = _mediaPlayer.IsPlaying ? "Play" : "Pause";
        }

        private void Back10SecsButton_Clicked(object sender, EventArgs e)
        {
            SeekTo(TimeSpan.FromMilliseconds(_mediaPlayer.Time) - TimeSpan.FromSeconds(10));
        }

        private void Forward10SecsButton_Clicked(object sender, EventArgs e)
        {
            SeekTo(TimeSpan.FromMilliseconds(_mediaPlayer.Time) + TimeSpan.FromSeconds(10));
        }

        private void DurationSlider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (e.NewValue != Math.Round(_mediaPlayer.Position * 100, 2))
            {
                var val = e.NewValue;
                _mediaPlayer.Position = (float)(val / 100);
            }
        }

        // Questions
        private void LoadQuestions()
        {
            _questionGroups = new Dictionary<TimeSpan, List<Question>>();
            _currentQuestionIndex = new Dictionary<TimeSpan, int>();

            var group1Time = TimeSpan.FromSeconds(10);
            var group2Time = TimeSpan.FromSeconds(20);

            _questionGroups[group1Time] = new List<Question>
            {
                new Question
                {
                    QuestionText = "What is the capital of France?",
                    Options = new List<string> { "Paris", "London", "Berlin", "Madrid" },
                    CorrectAnswer = "Paris"
                },
                new Question
                {
                    QuestionText = "Who won the 2018 FIFA World Cup?",
                    Options = new List<string> { "Brazil", "Germany", "Spain", "Argentina" },
                    CorrectAnswer = "Germany"
                },
                new Question
                {
                    QuestionText = "What is the name of the famous painting by Leonardo da Vinci?",
                    Options = new List<string> { "Mona Lisa", "The Last Supper", "The Starry Night", "The Creation of Adam" },
                    CorrectAnswer = "Mona Lisa"
                }
            };

            _questionGroups[group2Time] = new List<Question>
            {
                new Question
                {
                    QuestionText = "Who is the current Prime Minister of Australia?",
                    Options = new List<string> { "Joe Biden", "George Osborne", "David Cameron", "Australian Labor Party Leader" },
                    CorrectAnswer = "David Cameron"
                },
                new Question
                {
                    QuestionText = "What is the capital city of Sweden?",
                    Options = new List<string> { "Stockholm", "Copenhagen", "London", "Berlin" },
                    CorrectAnswer = "Stockholm"
                },
            };
            foreach (var timestamp in _questionGroups.Keys)
            {
                _currentQuestionIndex[timestamp] = 0;
            }
        }

        private void ShowNextQuestion(TimeSpan timestamp)
        {
            int index = _currentQuestionIndex[timestamp];

            if (index < _questionGroups[timestamp].Count)
            {
                Question question = _questionGroups[timestamp][index];

                // Display question pop-up
                ShowQuestionPopup(question, timestamp);
            }
            else
            {
                // All questions at this timestamp are answered, resume the video
                _mediaPlayer.Play();
            }
        }

        private void ShowQuestionPopup(Question question, TimeSpan timestamp)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                // Stop video playback
                _mediaPlayer.Pause();

                // Set question text
                QuestionTextLabel.Text = question.QuestionText;

                // Clear previous options
                OptionsContainer.Children.Clear();

                // Reset CorrectAnswerLabel
                CorrectAnswerLabel.IsVisible = false;
                CorrectAnswerLabel.Text = "";

                // Create answer buttons
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
                            // Show correct answer
                            optionButton.BackgroundColor = Color.Green;
                            CorrectAnswerLabel.Text = "Correct Answer: " + question.CorrectAnswer;
                            CorrectAnswerLabel.TextColor = Color.Green;
                            CorrectAnswerLabel.IsVisible = true;

                            await Task.Delay(2000); // Wait 2 seconds

                            // Move to the next question in the group
                            _currentQuestionIndex[timestamp]++;
                            QuestionPopup.IsVisible = false;
                            ShowNextQuestion(timestamp);
                        }
                        else
                        {
                            // Show incorrect answer
                            optionButton.BackgroundColor = Color.Red;
                            CorrectAnswerLabel.Text = "Wrong Answer! Try again.";
                            CorrectAnswerLabel.TextColor = Color.Red;
                            CorrectAnswerLabel.IsVisible = true;
                        }
                    };

                    OptionsContainer.Children.Add(optionButton);
                }

                // Show popup
                QuestionPopup.IsVisible = true;
            });
        }
    }
}
