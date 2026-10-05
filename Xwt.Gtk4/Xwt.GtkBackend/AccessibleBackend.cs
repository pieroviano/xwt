//
// AccessibleBackend.cs
//
// Author:
//       Vsevolod Kukol <sevoku@microsoft.com>
//
// Copyright (c) 2017 Microsoft Corporation
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
using System.Globalization;
using System.Linq;
using Xwt.Accessibility;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	/// <summary>
	/// Xwt accessibility on top of the GTK 4 GtkAccessible interface (ATK is gone).
	/// GTK 4 has no getters for accessible properties, so the values set through Xwt are
	/// cached here and pushed to GTK with gtk_accessible_update_property/relation.
	/// </summary>
	public class AccessibleBackend : IAccessibleBackend
	{
		protected Gtk.Widget widget;
		IAccessibleEventSink eventSink;
		ApplicationContext context;

		string description, label, identifier, roleDescription, value;

		public virtual event EventHandler AccessibilityInUseChanged;
		protected virtual void OnAccessibilityInUseChanged (object sender, EventArgs eventArgs)
		{
			AccessibilityInUseChanged?.Invoke (sender, eventArgs);
		}

		public AccessibleBackend ()
		{
		}

		public void Initialize (IAccessibleEventSink eventSink)
		{
			this.eventSink = eventSink;
		}

		public void Initialize (IWidgetBackend parentWidget, IAccessibleEventSink eventSink)
		{
			var backend = parentWidget as WidgetBackend;
			if (backend is TableViewBackend && backend.Widget is Gtk.ScrolledWindow) {
				// the accessible information belongs to the tree, not to its scrolled window
				Initialize ((backend.Widget as Gtk.ScrolledWindow)?.Child, eventSink);
			} else
				Initialize (backend?.Widget, eventSink);
		}

		public void Initialize (IPopoverBackend parentPopover, IAccessibleEventSink eventSink)
		{
			// Not currently supported
			this.eventSink = eventSink;
		}

		public void Initialize (IMenuBackend parentMenu, IAccessibleEventSink eventSink)
		{
			this.eventSink = eventSink;
			// GTK 4: a menu is rendered by a GtkPopoverMenu; annotate it when the backend exposes one
			widget = (object)(parentMenu as MenuBackend)?.Menu as Gtk.Widget;
		}

		public void Initialize (IMenuItemBackend parentMenuItem, IAccessibleEventSink eventSink)
		{
			// GTK 4: menu items are GMenuItem data, not accessibles: the properties are only stored.
			this.eventSink = eventSink;
		}

		public void Initialize (object parentWidget, IAccessibleEventSink eventSink)
		{
			this.eventSink = eventSink;
			widget = parentWidget as Gtk.Widget;
			if (widget == null)
				throw new ArgumentException ("The widget is not a Gtk.Widget.", nameof (parentWidget));
		}

		public void InitializeBackend (object frontend, ApplicationContext context)
		{
			this.context = context;
		}

		void UpdateProperty (Gtk.AccessibleProperty property, string text)
		{
			if (widget == null)
				return;
			if (text == null) {
				widget.ResetProperty (property);
				return;
			}
			var val = new GLib.Value (text);
			try {
				Gtk.AccessibleExtensions.UpdateProperty (widget, property, val);
			} finally {
				val.Dispose ();
			}
		}

		public virtual Rectangle Bounds {
			get {
				if (widget == null || !widget.GetBounds (out int x, out int y, out int w, out int h))
					return Rectangle.Zero;
				// GTK 4: accessible bounds are relative to the accessible parent; Xwt expects screen coordinates
				var parent = widget.Parent;
				var origin = parent != null ? GtkWorkarounds.ConvertToScreenCoordinates (parent, new Point (x, y)) : new Point (x, y);
				return new Rectangle (origin.X, origin.Y, w, h);
			}
			set {
				// GTK 4: accessible bounds are computed from the widget allocation and cannot be overridden
			}
		}

		public string Description {
			get {
				return description;
			}
			set {
				description = value;
				UpdateProperty (Gtk.AccessibleProperty.Description, value);
			}
		}

		public virtual string Label {
			get {
				return label;
			}
			set {
				label = value;
				UpdateProperty (Gtk.AccessibleProperty.Label, value);
			}
		}

		public string Identifier {
			get {
				return identifier;
			}
			set {
				// GTK 4: the accessible id is the GtkBuildable id and is read-only; the identifier is only kept here
				identifier = value;
			}
		}

		public Role Role {
			get {
				return widget != null ? widget.AccessibleRole.ToXwtRole () : Role.None;
			}

			set {
				// GTK 4: the accessible role can only be changed before the AT context is realized
				if (widget != null && !widget.IsRealized)
					widget.AccessibleRole = value.ToGtkRole ();
			}
		}

		public virtual string RoleDescription {
			get {
				return roleDescription;
			}
			set {
				roleDescription = value;
				UpdateProperty (Gtk.AccessibleProperty.RoleDescription, value);
			}
		}

		public virtual string Title { get; set; }

		public string Value {
			get {
				if (widget is Gtk.Range range)
					return range.Value.ToString (CultureInfo.InvariantCulture);
				if (widget is Gtk.IEditable editable)
					return editable.Text;
				return value;
			}
			set {
				if (widget is Gtk.Range range) {
					if (double.TryParse (value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
						range.Value = d;
				} else if (widget is Gtk.IEditable editable) {
					editable.Text = value ?? string.Empty;
				} else {
					this.value = value;
					UpdateProperty (Gtk.AccessibleProperty.ValueText, value);
				}
			}
		}

		public virtual Widget LabelWidget {
			set {
				if (widget == null)
					return;
				var labelWidget = value != null ? (Toolkit.GetBackend (value) as IGtkWidgetBackend)?.Widget : null;
				if (labelWidget != null)
					Gtk.AccessibleExtensions.UpdateRelation (widget, Gtk.AccessibleRelation.LabelledBy, labelWidget);
				else
					widget.ResetRelation (Gtk.AccessibleRelation.LabelledBy);
			}
		}

		public virtual Uri Uri { get; set; }

		public virtual bool IsAccessible {
			get {
				return widget != null && widget.AccessibleRole != Gtk.AccessibleRole.None && widget.AccessibleRole != Gtk.AccessibleRole.Presentation;
			}
			set {
				// TODO
			}
		}

		public virtual bool AccessibilityInUse => false;

		public virtual void AddChild (object nativeChild)
		{
			// TODO: custom child accessibles need a managed GtkAccessible implementation
		}

		public virtual void RemoveChild (object nativeChild)
		{
			// TODO
		}

		public virtual void RemoveAllChildren ()
		{
			// TODO
		}

		public virtual IEnumerable<object> GetChildren ()
		{
			// TODO
			return Enumerable.Empty<object>();
		}

		public void DisableEvent (object eventId)
		{
		}

		public void EnableEvent (object eventId)
		{
		}

		public virtual void MakeAnnouncement (string message, bool polite = false)
		{
			if (widget == null || string.IsNullOrEmpty (message))
				return;
			widget.Announce (message, polite ? Gtk.AccessibleAnnouncementPriority.Medium : Gtk.AccessibleAnnouncementPriority.High);
		}
	}
}
