//
// NativeHostWidget.cs
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
using System.Runtime.InteropServices;
using Xwt.GtkBackend;
using SWF = System.Windows.Forms;

namespace Xwt.Gtk.Windows
{
	/// <summary>
	/// Placeholder GTK 4 widget that keeps a WinForms control (a child HWND of the toplevel surface
	/// HWND) positioned over its allocation. GTK 4 cannot embed foreign windows; the control is an
	/// overlay, so GTK content cannot be drawn above it.
	/// </summary>
	class NativeHostWidget : global::Gtk.Widget
	{
		readonly SWF.Control control;
		Gdk.FrameClock clock;
		IntPtr parentHwnd;
		Gdk.Rectangle lastBounds;

		[DllImport ("user32.dll")]
		static extern IntPtr SetParent (IntPtr child, IntPtr newParent);

		[DllImport ("user32.dll")]
		static extern bool SetWindowPos (IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

		[DllImport ("user32.dll")]
		static extern bool ShowWindow (IntPtr hwnd, int cmd);

		const uint SWP_NOZORDER = 0x0004;
		const uint SWP_NOACTIVATE = 0x0010;
		const int SW_HIDE = 0;
		const int SW_SHOWNA = 8;

		public NativeHostWidget (SWF.Control control)
		{
			this.control = control;
			Focusable = true;
		}

		protected override void OnMapped ()
		{
			base.OnMapped ();
			clock = FrameClock;
			if (clock != null)
				clock.AfterPaint += HandleAfterPaint;
			UpdatePlacement ();
		}

		protected override void OnUnmapped ()
		{
			if (clock != null) {
				clock.AfterPaint -= HandleAfterPaint;
				clock = null;
			}
			if (control.IsHandleCreated)
				ShowWindow (control.Handle, SW_HIDE);
			base.OnUnmapped ();
		}

		protected override void OnSizeAllocate (int width, int height, int baseline)
		{
			base.OnSizeAllocate (width, height, baseline);
			UpdatePlacement ();
		}

		void HandleAfterPaint (object sender, EventArgs e)
		{
			// the widget may move without being reallocated (e.g. scrolling)
			UpdatePlacement ();
		}

		void UpdatePlacement ()
		{
			if (!IsMapped)
				return;
			var native = this.GetNativeWidget ();
			var surface = Native?.Surface;
			if (native == null || surface == null)
				return;
			var hwnd = GtkWorkarounds.GetNativeHandle (surface);
			if (hwnd == IntPtr.Zero)
				return;

			double x, y, sx, sy;
			if (!TranslateCoordinates (native, 0, 0, out x, out y))
				return;
			Native.GetSurfaceTransform (out sx, out sy);
			double scale = surface.Scale;
			var bounds = new Gdk.Rectangle ((int)Math.Round ((x + sx) * scale), (int)Math.Round ((y + sy) * scale),
				(int)Math.Round (Width * scale), (int)Math.Round (Height * scale));

			var child = control.Handle;
			if (parentHwnd != hwnd) {
				SetParent (child, hwnd);
				parentHwnd = hwnd;
				lastBounds = new Gdk.Rectangle ();
			}
			if (!bounds.Equals (lastBounds)) {
				lastBounds = bounds;
				SetWindowPos (child, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, SWP_NOZORDER | SWP_NOACTIVATE);
			}
			ShowWindow (child, SW_SHOWNA);
		}
	}
}
