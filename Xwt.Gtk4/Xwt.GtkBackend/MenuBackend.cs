// 
// MenuBackend.cs
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
using System.Collections.Generic;
using System.Linq;
using Xwt.Backends;


namespace Xwt.GtkBackend
{
	/// <summary>
	/// GTK 4 menu: a GLib.Menu model (items split into sections at separators, hidden items omitted)
	/// shown by a Gtk.PopoverMenu (context menus) or a Gtk.PopoverMenuBar (window menu bar). Item
	/// actions live in <see cref="MenuItemBackend.ActionGroup"/>, installed as "xwt" on those widgets.
	/// </summary>
	public class MenuBackend: IMenuBackend
	{
		readonly GLib.Menu model = new GLib.Menu ();
		readonly List<MenuItemBackend> items = new List<MenuItemBackend> ();
		Pango.FontDescription customFont;
		Gtk.PopoverMenu popover;
		Gtk.PopoverMenuBar menuBar;
		readonly List<Gtk.Widget> styledWidgets = new List<Gtk.Widget> ();

		public void InitializeBackend (object frontend, ApplicationContext context)
		{
		}

		/// <summary>The menu model (kept up to date with the items).</summary>
		public GLib.MenuModel MenuModel {
			get { return model; }
		}

		/// <summary>The popover used to show this menu as a context menu.</summary>
		public Gtk.PopoverMenu Menu {
			get {
				if (popover == null) {
					popover = new Gtk.PopoverMenu (model);
					popover.HasArrow = false;
					popover.Halign = Gtk.Align.Start;
					popover.Position = Gtk.PositionType.Bottom;
					popover.Closed += HandlePopoverClosed;
					InsertActions (popover);
					ApplyFont (popover);
				}
				return popover;
			}
		}

		/// <summary>A menu bar showing this menu (created on first use; see <see cref="CreateMenuBar"/>).</summary>
		public Gtk.PopoverMenuBar MenuBar {
			get {
				if (menuBar == null)
					menuBar = CreateMenuBar ();
				return menuBar;
			}
		}

		/// <summary>Creates a new menu bar bound to this menu's model and actions.</summary>
		public Gtk.PopoverMenuBar CreateMenuBar ()
		{
			var bar = new Gtk.PopoverMenuBar (model);
			InsertActions (bar);
			ApplyFont (bar);
			return bar;
		}

		/// <summary>
		/// Installs the menu item actions (group "xwt") on <paramref name="target"/>, so that a
		/// PopoverMenu or PopoverMenuBar inside it can activate the items.
		/// </summary>
		public void InsertActions (Gtk.Widget target)
		{
			target.InsertActionGroup (MenuItemBackend.ActionGroupName, MenuItemBackend.ActionGroup);
		}

		public virtual object Font {
			get {
				if (customFont != null)
					return customFont;
				var w = (Gtk.Widget) popover ?? menuBar;
				if (w != null)
					return w.PangoContext.FontDescription;
				var name = Gtk.Settings.Default?.GtkFontName;
				return Pango.FontDescription.FromString (string.IsNullOrEmpty (name) ? "Sans 10" : name);
			}
			set {
				customFont = (Pango.FontDescription) value;
				foreach (var w in styledWidgets.ToArray ())
					w.SetStyle ("font", customFont != null ? customFont.ToCssValue () : null);
			}
		}

		void ApplyFont (Gtk.Widget w)
		{
			styledWidgets.Add (w);
			if (customFont != null)
				w.SetStyle ("font", customFont.ToCssValue ());
		}

		public void InsertItem (int index, IMenuItemBackend menuItem)
		{
			var item = (MenuItemBackend)menuItem;
			if (index < 0 || index > items.Count)
				index = items.Count;
			items.Insert (index, item);
			item.ParentMenu = this;
			Rebuild ();
		}

		public void RemoveItem (IMenuItemBackend menuItem)
		{
			var item = (MenuItemBackend)menuItem;
			if (items.Remove (item)) {
				item.ParentMenu = null;
				Rebuild ();
			}
		}

		/// <summary>
		/// Regenerates the model from the items. The GLib.Menu instance is kept, so menu bars, popovers
		/// and parent menus linking to it update automatically.
		/// </summary>
		internal void Rebuild ()
		{
			model.RemoveAll ();
			var section = new GLib.Menu ();
			int sectionCount = 0;
			foreach (var item in items) {
				if (!item.Visible)
					continue;
				if (item.IsSeparator) {
					if (sectionCount > 0) {
						model.AppendSection (null, section);
						section = new GLib.Menu ();
						sectionCount = 0;
					}
					continue;
				}
				section.AppendItem (item.CreateMenuItem ());
				sectionCount++;
			}
			if (sectionCount > 0)
				model.AppendSection (null, section);
		}

		public void EnableEvent (object eventId)
		{
		}

		public void DisableEvent (object eventId)
		{
		}

		public void Popup ()
		{
			// GTK 4 cannot pop up at screen coordinates: anchor the popover to the content of the
			// active window, at the last known pointer position in that window (or its centre).
			var window = Gtk.Window.ListToplevels ().FirstOrDefault (w => w.IsActive && w.Visible)
				?? Gtk.Window.ListToplevels ().FirstOrDefault (w => w.Visible);
			if (window == null)
				return;
			var anchor = window.Child ?? window;

			double x = anchor.Width / 2, y = anchor.Height / 2;
			var pointer = Gdk.Display.Default?.DefaultSeat?.Pointer;
			var windowSurface = window.Surface;
			if (pointer != null && windowSurface != null) {
				double sx, sy;
				var surface = pointer.GetSurfaceAtPosition (out sx, out sy);
				if (surface != null && surface.Handle == windowSurface.Handle) {
					double tx, ty, ax, ay;
					window.GetSurfaceTransform (out tx, out ty);
					if (window.TranslateCoordinates (anchor, sx - tx, sy - ty, out ax, out ay)) {
						x = ax;
						y = ay;
					}
				}
			}
			ShowPopover (anchor, x, y);
		}

		public void Popup (IWidgetBackend widget, double x, double y)
		{
			var target = widget as WidgetBackend;
			if (target == null)
				throw new ArgumentException ("Widget belongs to an unsupported Toolkit", nameof (widget));
			ShowPopover (target.Widget, x, y);
		}

		void ShowPopover (Gtk.Widget parent, double x, double y)
		{
			var pm = Menu;
			if (pm.Parent != parent) {
				if (pm.Parent != null)
					pm.Unparent ();
				pm.Parent = parent;
			}
			pm.PointingTo = new Gdk.Rectangle ((int)x, (int)y, 1, 1);
			pm.Popup ();
		}

		void HandlePopoverClosed (object sender, EventArgs e)
		{
			// Detach the popover from the target widget once the activated item (if any) has run, so
			// the target does not keep a child it doesn't know about.
			GLib.Idle.Add (delegate {
				if (popover != null && !popover.Visible && popover.Parent != null)
					popover.Unparent ();
				return false;
			});
		}
	}
}
