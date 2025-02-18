using InteractivePlayer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace InteractivePlayer
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class MediaPlayerElementPage : ContentPage
    {
        public MediaPlayerElementPage()
        {
            InitializeComponent();
        }
        protected override void OnAppearing()
        {
            ((MediaPlayerElementPageViewModel)BindingContext).OnAppearing();
            base.OnAppearing();
        }
    }   
}