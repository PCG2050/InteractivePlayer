// CustomWebViewRenderer.cs (Android)
using Android.Content;
using Android.Webkit;
using InteractivePlayer;
using InteractivePlayer.Droid;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;


[assembly: ExportRenderer(typeof(CustomWebView), typeof(CustomWebViewRenderer))]
namespace InteractivePlayer.Droid
{
    public class CustomWebViewRenderer : WebViewRenderer
    {
        public CustomWebViewRenderer(Context context) : base(context)
        {
        }

        protected override void OnElementChanged(ElementChangedEventArgs<Xamarin.Forms.WebView> e)
        {
            base.OnElementChanged(e);

            if (e.NewElement != null)
            {
                Control.SetWebViewClient(new CustomWebViewClient());
            }
        }

        private class CustomWebViewClient : WebViewClient
        {
            public override void OnPageFinished(Android.Webkit.WebView view, string url)
            {
                base.OnPageFinished(view, url);
                var height = (int)(view.ContentHeight * view.Scale);
                view.LayoutParameters.Height = height;
                view.RequestLayout();
            }
        }
    }
}
