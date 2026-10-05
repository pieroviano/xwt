//
// MessageService.cs
//
// Author:
//   Mike Krüger <mkrueger@novell.com>
//
// Copyright (C) 2008 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;

namespace Xwt.GtkBackend
{
	public static class MessageService
	{
		public static Gtk.Window RootWindow { get; set; }

		/// <summary>
		/// Places, runs and destroys a transient dialog.
		/// </summary>
		public static int ShowCustomDialog (Gtk.Dialog dialog)
		{
			return ShowCustomDialog (dialog, null);
		}

		public static int ShowCustomDialog (Gtk.Dialog dialog, Gtk.Window parent)
		{
			try {
				return RunCustomDialog (dialog, parent);
			} finally {
				if (dialog != null)
					dialog.Destroy ();
			}
		}

		public static int RunCustomDialog (Gtk.Dialog dialog)
		{
			return RunCustomDialog (dialog, null);
		}

		/// <summary>
		/// Places and runs a transient dialog in a nested main loop until it emits a response or
		/// is closed (<see cref="Gtk.ResponseType.DeleteEvent"/>). Does not destroy it, so values
		/// can be retrieved from its widgets.
		/// </summary>
		public static int RunCustomDialog (Gtk.Dialog dialog, Gtk.Window parent)
		{
			int result = (int) Gtk.ResponseType.None;
			var loop = new NestedLoop ();
			Gtk.ResponseHandler onResponse = (o, args) => {
				result = (int) args.ResponseId;
				loop.Quit ();
			};
			Gtk.CloseRequestHandler onClose = (o, args) => {
				result = (int) Gtk.ResponseType.DeleteEvent;
				args.RetVal = true;
				loop.Quit ();
			};
			dialog.Response += onResponse;
			dialog.CloseRequest += onClose;
			try {
				RunModal (dialog, parent, loop);
			} finally {
				dialog.Response -= onResponse;
				dialog.CloseRequest -= onClose;
			}
			return result;
		}

		/// <summary>
		/// Shows <paramref name="window"/> modal to <paramref name="parent"/> (or the default parent)
		/// and runs <paramref name="loop"/> until it is quit or the window is hidden.
		/// </summary>
		internal static void RunModal (Gtk.Window window, Gtk.Window parent, NestedLoop loop)
		{
			window.Modal = true;
			if (parent == null)
				parent = window.TransientFor ?? GetDefaultParent (window);
			if (parent != window)
				window.TransientFor = parent;
			window.DestroyWithParent = true;
			EventHandler onHidden = (o, e) => loop.Quit ();
			window.Hidden += onHidden;
			try {
				window.Present ();
				loop.Run ();
			} finally {
				window.Hidden -= onHidden;
			}
		}

		//make sure modal children are parented on top of other modal children
		static Gtk.Window GetDefaultParent (Gtk.Window child)
		{
			if (child.Modal) {
				return GetDefaultModalParent ();
			} else {
				return RootWindow;
			}
		}

		/// <summary>
		/// Gets a default parent for modal dialogs.
		/// </summary>
		public static Gtk.Window GetDefaultModalParent ()
		{
			foreach (Gtk.Window w in Gtk.Window.ListToplevels ())
				if (w.Visible && w.IsActive && w.Modal)
					return w;
			return RootWindow;
		}
	}

	/// <summary>
	/// A nested <see cref="GLib.MainLoop"/> used to make the asynchronous GTK 4 dialogs synchronous.
	/// <see cref="Quit"/> may be called before <see cref="Run"/> (e.g. by an async callback
	/// completing immediately), in which case <see cref="Run"/> returns at once.
	/// </summary>
	sealed class NestedLoop
	{
		readonly GLib.MainLoop loop = new GLib.MainLoop ();
		bool done;

		public void Run ()
		{
			if (!done)
				loop.Run ();
		}

		public void Quit ()
		{
			done = true;
			if (loop.IsRunning)
				loop.Quit ();
		}
	}
}
