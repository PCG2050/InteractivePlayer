// CustomWebViewRenderer.cs (iOS)
using Foundation;
using InteractivePlayer;
using InteractivePlayer.iOS;
using System;
using WebKit;
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;

[assembly: ExportRenderer(typeof(CustomWebView), typeof(CustomWebViewRenderer))]
namespace InteractivePlayer.iOS
{
    public class CustomWebViewRenderer : WkWebViewRenderer
    {
        protected override void OnElementChanged(VisualElementChangedEventArgs e)
        {
            base.OnElementChanged(e);

            if (e.NewElement != null)
            {
                NavigationDelegate = new CustomWebViewNavigationDelegate();
            }
        }

        private class CustomWebViewNavigationDelegate : WKNavigationDelegate
        {
            public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
            {
                webView.EvaluateJavaScript("document.body.scrollHeight", (result, error) =>
                {
                    if (result != null)
                    {
                        var height = Convert.ToDouble(result);
                        webView.Frame = new CoreGraphics.CGRect(webView.Frame.X, webView.Frame.Y, webView.Frame.Width, height);
                    }
                });
            }
        }
    }
}




