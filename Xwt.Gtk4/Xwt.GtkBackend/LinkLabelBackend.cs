//
// LinkLabelBackend.cs
//
// Author:
//       Jérémie Laval <jeremie.laval@xamarin.com>
//
// Copyright (c) 2012 Xamarin, Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using Xwt;
using Xwt.Backends;


namespace Xwt.GtkBackend
{
	class LinkLabelBackend : LabelBackend, ILinkLabelBackend
	{
		Uri uri;
		string text = string.Empty;

		bool ClickEnabled {
			get; set;
		}

		new ILinkLabelEventSink EventSink {
			get { return (ILinkLabelEventSink)base.EventSink; }
		}

		public Uri Uri {
			get {
				return uri;
			}
			set {
				uri = value;
				SetMarkup ();
			}
		}

		public override string Text {
			get { return text; }
			set {
				text = value ?? string.Empty;
				SetMarkup ();
			}
		}

		public override void EnableEvent (object eventId)
		{
			base.EnableEvent (eventId);
			if (eventId is LinkLabelEvent) {
				switch ((LinkLabelEvent) eventId) {
				case LinkLabelEvent.NavigateToUrl:
					ClickEnabled = true;
					break;
				}
			}
		}

		public override void DisableEvent (object eventId)
		{
			base.DisableEvent (eventId);
			if (eventId is LinkLabelEvent) {
				switch ((LinkLabelEvent) eventId) {
				case LinkLabelEvent.NavigateToUrl:
					ClickEnabled = false;
					break;
				}
			}
		}

		void SetMarkup ()
		{
			Label.Attributes = null;
			Label.Markup = string.Format ("<a href=\"{1}\">{0}</a>", FormattedTextMarkup.Escape (text), uri != null ? FormattedTextMarkup.Escape (uri.ToString ()) : "");
		}

		protected override bool OnActivateLink (string link)
		{
			if (ClickEnabled) {
				var target = !string.IsNullOrEmpty (link) ? new Uri (link, UriKind.RelativeOrAbsolute) : null;
				ApplicationContext.InvokeUserCode (() => {
					EventSink.OnNavigateToUrl (target);
				});
			}
			// the frontend opens the URL (NavigateToUrl default action); never let GTK open it too
			return true;
		}
	}
}
