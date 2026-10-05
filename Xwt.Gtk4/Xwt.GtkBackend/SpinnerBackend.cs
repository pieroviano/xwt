//
// SpinnerBackend.cs
//
// Author:
//       Jérémie Laval <jeremie.laval@xamarin.com>
//
// Copyright (c) 2012 Xamarin, Inc.

using System;

using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class SpinnerBackend : WidgetBackend, ISpinnerBackend
	{
		public SpinnerBackend ()
		{
			Widget = new Gtk.Spinner ();
		}

		protected new Gtk.Spinner Widget {
			get { return (Gtk.Spinner)base.Widget; }
			set { base.Widget = value; }
		}

		public void StartAnimation ()
		{
			Widget.Spinning = true;
		}

		public void StopAnimation ()
		{
			Widget.Spinning = false;
		}

		public bool IsAnimating {
			get {
				return Widget.Spinning;
			}
		}
	}
}
