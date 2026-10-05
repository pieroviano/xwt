// 
// DialogBackend.cs
//  
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
// 
// Copyright (c) 2011 Xamarin Inc
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
using Xwt.Backends;
using System.Collections.Generic;
using System.Linq;


namespace Xwt.GtkBackend
{
	public class DialogBackend: WindowBackend, IDialogBackend
	{
		DialogButton[] dialogButtons;
		DialogButton defaultButton;
		Gtk.Button[] buttons;
		Gtk.Box actionArea;
		NestedLoop runLoop;

		public DialogBackend ()
		{
		}

		public override void Initialize ()
		{
			// GTK 4: GtkDialog is deprecated; the dialog is a window with its own button row.
			Window = new Gtk.Window ();
			var box = new Gtk.Box (Gtk.Orientation.Vertical, 0);
			var content = CreateMainLayout ();
			content.Vexpand = true;
			box.Append (content);
			actionArea = new Gtk.Box (Gtk.Orientation.Horizontal, 6);
			actionArea.Halign = Gtk.Align.End;
			actionArea.MarginStart = actionArea.MarginEnd = actionArea.MarginTop = actionArea.MarginBottom = 6;
			actionArea.AddCssClass ("dialog-action-area");
			actionArea.Visible = false;
			box.Append (actionArea);
			Window.Child = box;
		}

		/// <summary>The box holding the dialog buttons.</summary>
		public Gtk.Box ActionArea {
			get { return actionArea; }
		}

		new IDialogEventSink EventSink {
			get { return (IDialogEventSink) base.EventSink; }
		}

		public DialogButton DefaultButton {
			get {
				return defaultButton;
			}
			set {
				defaultButton = value;
				SetButtons (dialogButtons);
			}
		}

		public void SetButtons (IEnumerable<DialogButton> newButtons)
		{
			if (buttons != null) {
				foreach (var b in buttons) {
					b.Clicked -= HandleButtonClicked;
					actionArea.Remove (b);
				}
			}
			dialogButtons = (newButtons ?? Enumerable.Empty<DialogButton> ())
				.OrderBy (b => b.PackOrigin)
				.OrderBy (b => b == DefaultButton).ToArray ();
			buttons = new Gtk.Button [dialogButtons.Length];

			for (int n=0; n<dialogButtons.Length; n++) {
				var db = dialogButtons[n];
				Gtk.Button b = new Gtk.Button ();
				actionArea.Append (b);
				UpdateButton (db, b);
				buttons[n] = b;
				buttons[n].Clicked += HandleButtonClicked;
				if (db == DefaultButton) {
					Window.DefaultWidget = b;
					b.AddCssClass ("suggested-action");
				}
			}
			UpdateActionAreaVisibility ();
		}

		void UpdateActionAreaVisibility ()
		{
			actionArea.Visible = buttons != null && buttons.Any (c => c.Visible);
		}

		void UpdateButton (DialogButton btn, Gtk.Button b)
		{
			if (!string.IsNullOrEmpty (btn.Label) && btn.Image == null) {
				b.Label = btn.Label;
			} else if (string.IsNullOrEmpty (btn.Label) && btn.Image != null) {
				var pix = btn.Image.ToImageDescription (ApplicationContext);
				b.Child = new ImageBox (ApplicationContext, pix);
			} else if (!string.IsNullOrEmpty (btn.Label)) {
				Gtk.Box box = new Gtk.Box (Gtk.Orientation.Horizontal, 3);
				var pix = btn.Image.ToImageDescription (ApplicationContext);
				box.Append (new ImageBox (ApplicationContext, pix));
				var label = new Gtk.Label (btn.Label);
				label.Hexpand = true;
				box.Append (label);
				b.Child = box;
			}
			b.Visible = btn.Visible;
			b.Sensitive = btn.Sensitive;
			UpdateActionAreaVisibility ();
		}

		void HandleButtonClicked (object o, EventArgs a)
		{
			int i = Array.IndexOf (buttons, (Gtk.Button) o);
			ApplicationContext.InvokeUserCode (delegate {
				EventSink.OnDialogButtonClicked (dialogButtons[i]);
			});
		}

		public void UpdateButton (DialogButton btn)
		{
			int i = Array.IndexOf (dialogButtons, btn);
			UpdateButton (btn, buttons[i]);
		}

		/// <summary>
		/// Shows the dialog modal to <paramref name="parent"/> and runs a nested main loop until
		/// <see cref="EndLoop"/> is called or the dialog is closed.
		/// </summary>
		public void RunLoop (IWindowFrameBackend parent)
		{
			Gtk.Window p = parent != null ? ApplicationContext.Toolkit.GetNativeWindow (parent) as Gtk.Window : null;
			var loop = new NestedLoop ();
			runLoop = loop;
			try {
				MessageService.RunModal (Window, p, loop);
			} finally {
				if (runLoop == loop)
					runLoop = null;
			}
		}

		public void EndLoop ()
		{
			runLoop?.Quit ();
			Window.Visible = false;
		}

		protected override bool OnCloseRequest ()
		{
			if (runLoop == null)
				return base.OnCloseRequest ();
			// closing a running dialog ends the loop if the frontend allows it; the window is kept
			if (PerformClose (false))
				runLoop?.Quit ();
			return true;
		}

		public override bool Close ()
		{
			if (PerformClose (false)) {
				runLoop?.Quit ();
				return true;
			} else
				return false;
		}

		public override void GetMetrics (out Size minSize, out Size decorationSize)
		{
			base.GetMetrics (out minSize, out decorationSize);
			if (!actionArea.Visible)
				return;
			int minW, natW, minH, natH, b1, b2;
			actionArea.Measure (Gtk.Orientation.Horizontal, -1, out minW, out natW, out b1, out b2);
			actionArea.Measure (Gtk.Orientation.Vertical, -1, out minH, out natH, out b1, out b2);
			minW += actionArea.MarginStart + actionArea.MarginEnd;
			natH += actionArea.MarginTop + actionArea.MarginBottom;
			if (minW > minSize.Width)
				minSize.Width = minW;
			decorationSize.Height += natH;
		}
	}
}
