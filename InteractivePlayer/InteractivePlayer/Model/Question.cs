using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace InteractivePlayer.Model
{
    public class Question
    {
        public string QuestionText { get; set; }
        public List<String> Options { get; set; }
        public string CorrectAnswer { get; set; }
        public TimeSpan TimeStamp { get; set; }

    }
}
