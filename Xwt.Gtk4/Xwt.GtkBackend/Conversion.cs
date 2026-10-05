using System;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public static class Conversion
	{
		/// <summary>Pixel size of an Xwt icon size (GTK 4 has no named icon sizes beyond normal/large).</summary>
		public static int ToPixelSize (this Xwt.IconSize size)
		{
			switch (size) {
			case IconSize.Small:
				return 16;
			case IconSize.Medium:
				return 24;
			case IconSize.Large:
				return 48;
			}
			return 48;
		}

		public static Pango.Color ToPangoColor (this Xwt.Drawing.Color color)
		{
			var c = new Pango.Color ();
			c.Red = (ushort)(color.Red * ushort.MaxValue);
			c.Green = (ushort)(color.Green * ushort.MaxValue);
			c.Blue = (ushort)(color.Blue * ushort.MaxValue);
			return c;
		}

		public static Color ToXwtValue (this Pango.Color color)
		{
			return new Color ((double)color.Red / (double)ushort.MaxValue, (double)color.Green / (double)ushort.MaxValue, (double)color.Blue / (double)ushort.MaxValue);
		}

		/// <summary>CSS color literal, e.g. for a per-widget CssProvider.</summary>
		public static string ToCssValue (this Xwt.Drawing.Color color)
		{
			return string.Format (System.Globalization.CultureInfo.InvariantCulture, "rgba({0},{1},{2},{3})",
				(int)(color.Red * 255), (int)(color.Green * 255), (int)(color.Blue * 255), color.Alpha);
		}

		public static Gdk.RGBA ToGtkRgbaValue (this Xwt.Drawing.Color color)
		{
			var rgba = new Gdk.RGBA ();
			rgba.Red = (float)color.Red;
			rgba.Green = (float)color.Green;
			rgba.Blue = (float)color.Blue;
			rgba.Alpha = (float)color.Alpha;
			return rgba;
		}

		public static Color ToXwtValue (this Gdk.RGBA color)
		{
			return new Color (color.Red, color.Green, color.Blue, color.Alpha);
		}

		public static Pango.EllipsizeMode ToGtkValue (this EllipsizeMode value)
		{
			switch (value) {
			case Xwt.EllipsizeMode.None: return Pango.EllipsizeMode.None;
			case Xwt.EllipsizeMode.Start: return Pango.EllipsizeMode.Start;
			case Xwt.EllipsizeMode.Middle: return Pango.EllipsizeMode.Middle;
			case Xwt.EllipsizeMode.End: return Pango.EllipsizeMode.End;
			}
			throw new NotSupportedException ();
		}

		public static EllipsizeMode ToXwtValue (this Pango.EllipsizeMode value)
		{
			switch (value) {
			case Pango.EllipsizeMode.None: return Xwt.EllipsizeMode.None;
			case Pango.EllipsizeMode.Start: return Xwt.EllipsizeMode.Start;
			case Pango.EllipsizeMode.Middle: return Xwt.EllipsizeMode.Middle;
			case Pango.EllipsizeMode.End: return Xwt.EllipsizeMode.End;
			}
			throw new NotSupportedException ();
		}

		public static ScrollPolicy ToXwtValue (this Gtk.PolicyType p)
		{
			switch (p) {
			case Gtk.PolicyType.Always:
				return ScrollPolicy.Always;
			case Gtk.PolicyType.Automatic:
				return ScrollPolicy.Automatic;
			case Gtk.PolicyType.Never:
				return ScrollPolicy.Never;
			}
			throw new InvalidOperationException ("Invalid policy value:" + p);
		}

		public static Gtk.PolicyType ToGtkValue (this ScrollPolicy p)
		{
			switch (p) {
			case ScrollPolicy.Always:
				return Gtk.PolicyType.Always;
			case ScrollPolicy.Automatic:
				return Gtk.PolicyType.Automatic;
			case ScrollPolicy.Never:
				return Gtk.PolicyType.Never;
			}
			throw new InvalidOperationException ("Invalid policy value:" + p);
		}

		public static ScrollDirection ToXwtValue(this Gdk.ScrollDirection d)
		{
			switch(d) {
			case Gdk.ScrollDirection.Up:
				return Xwt.ScrollDirection.Up;
			case Gdk.ScrollDirection.Down:
				return Xwt.ScrollDirection.Down;
			case Gdk.ScrollDirection.Left:
				return Xwt.ScrollDirection.Left;
			case Gdk.ScrollDirection.Right:
				return Xwt.ScrollDirection.Right;
			}
			throw new InvalidOperationException("Invalid mouse scroll direction value: " + d);
		}

		public static Gdk.ScrollDirection ToGtkValue(this ScrollDirection d)
		{
			switch (d) {
			case ScrollDirection.Up:
				return Gdk.ScrollDirection.Up;
			case ScrollDirection.Down:
				return Gdk.ScrollDirection.Down;
			case ScrollDirection.Left:
				return Gdk.ScrollDirection.Left;
			case ScrollDirection.Right:
				return Gdk.ScrollDirection.Right;
			}
			throw new InvalidOperationException("Invalid mouse scroll direction value: " + d);
		}

		public static ModifierKeys ToXwtValue (this Gdk.ModifierType s)
		{
			ModifierKeys m = ModifierKeys.None;
			if ((s & Gdk.ModifierType.ShiftMask) != 0)
				m |= ModifierKeys.Shift;
			if ((s & Gdk.ModifierType.ControlMask) != 0)
				m |= ModifierKeys.Control;
			if ((s & Gdk.ModifierType.AltMask) != 0)
				m |= ModifierKeys.Alt;
			if ((s & (Gdk.ModifierType.MetaMask | Gdk.ModifierType.SuperMask)) != 0)
				m |= ModifierKeys.Command;
			return m;
		}

		public static Gtk.TreeViewGridLines ToGtkValue (this GridLines value)
		{
			switch (value)
			{
				case GridLines.Both:
					return Gtk.TreeViewGridLines.Both;
				case GridLines.Horizontal:
					return Gtk.TreeViewGridLines.Horizontal;
				case GridLines.Vertical:
					return Gtk.TreeViewGridLines.Vertical;
				case GridLines.None:
					return Gtk.TreeViewGridLines.None;
			}
			throw new InvalidOperationException("Invalid GridLines value: " + value);
		}

		public static GridLines ToXwtValue (this Gtk.TreeViewGridLines value)
		{
			switch (value)
			{
				case Gtk.TreeViewGridLines.Both:
					return GridLines.Both;
				case Gtk.TreeViewGridLines.Horizontal:
					return GridLines.Horizontal;
				case Gtk.TreeViewGridLines.Vertical:
					return GridLines.Vertical;
				case Gtk.TreeViewGridLines.None:
					return GridLines.None;
			}
			throw new InvalidOperationException("Invalid TreeViewGridLines value: " + value);
		}

		public static float ToGtkAlignment(this Alignment alignment)
		{
			switch(alignment) {
				case Alignment.Start: return 0.0f;
				case Alignment.Center: return 0.5f;
				case Alignment.End: return 1.0f;
			}
			throw new InvalidOperationException("Invalid alignment value: " + alignment);
		}

		public static Pango.Alignment ToPangoAlignment (this Alignment alignment)
		{
			switch(alignment) {
				case Alignment.Start: return Pango.Alignment.Left;
				case Alignment.Center: return Pango.Alignment.Center;
				case Alignment.End: return Pango.Alignment.Right;
			}
			throw new InvalidOperationException("Invalid alignment value: " + alignment);
		}

		public static Gtk.ResponseType ToResponseType (this Xwt.Command command)
		{
			if (command.Id == Command.Ok.Id)
				return Gtk.ResponseType.Ok;
			if (command.Id == Command.Cancel.Id)
				return Gtk.ResponseType.Cancel;
			if (command.Id == Command.Yes.Id)
				return Gtk.ResponseType.Yes;
			if (command.Id == Command.No.Id)
				return Gtk.ResponseType.No;
			if (command.Id == Command.Close.Id)
				return Gtk.ResponseType.Close;
			if (command.Id == Command.Delete.Id)
				return Gtk.ResponseType.DeleteEvent;
			if (command.Id == Command.Apply.Id)
				return Gtk.ResponseType.Accept;
			if (command.Id == Command.Stop.Id)
				return Gtk.ResponseType.Reject;
			return Gtk.ResponseType.None;
		}

		public static Gtk.AccessibleRole ToGtkRole (this Accessibility.Role role)
		{
			switch (role) {
			case Accessibility.Role.Button:
			case Accessibility.Role.ButtonClose:
			case Accessibility.Role.ButtonMinimize:
			case Accessibility.Role.ButtonMaximize:
			case Accessibility.Role.ButtonFullscreen:
			case Accessibility.Role.MenuButton:
			case Accessibility.Role.Disclosure:
				return Gtk.AccessibleRole.Button;
			case Accessibility.Role.Calendar:
				return Gtk.AccessibleRole.Grid;
			case Accessibility.Role.Cell:
				return Gtk.AccessibleRole.GridCell;
			case Accessibility.Role.CheckBox:
				return Gtk.AccessibleRole.Checkbox;
			case Accessibility.Role.ColorChooser:
			case Accessibility.Role.Custom:
				return Gtk.AccessibleRole.Widget;
			case Accessibility.Role.Column:
				return Gtk.AccessibleRole.ColumnHeader;
			case Accessibility.Role.ComboBox:
				return Gtk.AccessibleRole.ComboBox;
			case Accessibility.Role.Filler:
				return Gtk.AccessibleRole.Generic;
			case Accessibility.Role.Group:
			case Accessibility.Role.Paned:
			case Accessibility.Role.ScrollView:
				return Gtk.AccessibleRole.Group;
			case Accessibility.Role.Image:
				return Gtk.AccessibleRole.Img;
			case Accessibility.Role.Label:
				return Gtk.AccessibleRole.Label;
			case Accessibility.Role.LevelIndicator:
				return Gtk.AccessibleRole.Meter;
			case Accessibility.Role.Link:
				return Gtk.AccessibleRole.Link;
			case Accessibility.Role.List:
				return Gtk.AccessibleRole.List;
			case Accessibility.Role.Menu:
			case Accessibility.Role.Popup:
				return Gtk.AccessibleRole.Menu;
			case Accessibility.Role.MenuBar:
				return Gtk.AccessibleRole.MenuBar;
			case Accessibility.Role.MenuBarItem:
			case Accessibility.Role.MenuItem:
				return Gtk.AccessibleRole.MenuItem;
			case Accessibility.Role.MenuItemCheckBox:
				return Gtk.AccessibleRole.MenuItemCheckbox;
			case Accessibility.Role.MenuItemRadio:
				return Gtk.AccessibleRole.MenuItemRadio;
			case Accessibility.Role.Notebook:
				return Gtk.AccessibleRole.TabList;
			case Accessibility.Role.NotebookTab:
				return Gtk.AccessibleRole.Tab;
			case Accessibility.Role.PanedSplitter:
			case Accessibility.Role.Separator:
				return Gtk.AccessibleRole.Separator;
			case Accessibility.Role.ProgressBar:
				return Gtk.AccessibleRole.ProgressBar;
			case Accessibility.Role.RadioButton:
				return Gtk.AccessibleRole.Radio;
			case Accessibility.Role.RadioGroup:
				return Gtk.AccessibleRole.RadioGroup;
			case Accessibility.Role.Row:
				return Gtk.AccessibleRole.Row;
			case Accessibility.Role.ScrollBar:
				return Gtk.AccessibleRole.Scrollbar;
			case Accessibility.Role.Slider:
				return Gtk.AccessibleRole.Slider;
			case Accessibility.Role.SpinButton:
				return Gtk.AccessibleRole.SpinButton;
			case Accessibility.Role.Table:
				return Gtk.AccessibleRole.Table;
			case Accessibility.Role.TextArea:
			case Accessibility.Role.TextEntry:
			case Accessibility.Role.TextEntryPassword:
				return Gtk.AccessibleRole.TextBox;
			case Accessibility.Role.TextEntrySearch:
				return Gtk.AccessibleRole.SearchBox;
			case Accessibility.Role.ToggleButton:
				return Gtk.AccessibleRole.ToggleButton;
			case Accessibility.Role.ToolBar:
				return Gtk.AccessibleRole.Toolbar;
			case Accessibility.Role.ToolTip:
				return Gtk.AccessibleRole.Tooltip;
			case Accessibility.Role.Tree:
				return Gtk.AccessibleRole.TreeGrid;
			case Accessibility.Role.None:
				return Gtk.AccessibleRole.None;
			default:
				throw new ArgumentOutOfRangeException ();
			}
		}

		public static Accessibility.Role ToXwtRole (this Gtk.AccessibleRole role)
		{
			switch (role) {
			case Gtk.AccessibleRole.Button: return Accessibility.Role.Button;
			case Gtk.AccessibleRole.Checkbox: return Accessibility.Role.CheckBox;
			case Gtk.AccessibleRole.ColumnHeader: return Accessibility.Role.Column;
			case Gtk.AccessibleRole.ComboBox: return Accessibility.Role.ComboBox;
			case Gtk.AccessibleRole.Generic: return Accessibility.Role.Filler;
			case Gtk.AccessibleRole.Group: return Accessibility.Role.Group;
			case Gtk.AccessibleRole.GridCell:
			case Gtk.AccessibleRole.Cell:
			case Gtk.AccessibleRole.ListItem: return Accessibility.Role.Cell;
			case Gtk.AccessibleRole.Img: return Accessibility.Role.Image;
			case Gtk.AccessibleRole.Label: return Accessibility.Role.Label;
			case Gtk.AccessibleRole.Link: return Accessibility.Role.Link;
			case Gtk.AccessibleRole.List:
			case Gtk.AccessibleRole.ListBox: return Accessibility.Role.List;
			case Gtk.AccessibleRole.Menu: return Accessibility.Role.Menu;
			case Gtk.AccessibleRole.MenuBar: return Accessibility.Role.MenuBar;
			case Gtk.AccessibleRole.MenuItem: return Accessibility.Role.MenuItem;
			case Gtk.AccessibleRole.MenuItemCheckbox: return Accessibility.Role.MenuItemCheckBox;
			case Gtk.AccessibleRole.MenuItemRadio: return Accessibility.Role.MenuItemRadio;
			case Gtk.AccessibleRole.Meter: return Accessibility.Role.LevelIndicator;
			case Gtk.AccessibleRole.ProgressBar: return Accessibility.Role.ProgressBar;
			case Gtk.AccessibleRole.Radio: return Accessibility.Role.RadioButton;
			case Gtk.AccessibleRole.RadioGroup: return Accessibility.Role.RadioGroup;
			case Gtk.AccessibleRole.Row:
			case Gtk.AccessibleRole.RowHeader: return Accessibility.Role.Row;
			case Gtk.AccessibleRole.Scrollbar: return Accessibility.Role.ScrollBar;
			case Gtk.AccessibleRole.SearchBox: return Accessibility.Role.TextEntrySearch;
			case Gtk.AccessibleRole.Separator: return Accessibility.Role.Separator;
			case Gtk.AccessibleRole.Slider: return Accessibility.Role.Slider;
			case Gtk.AccessibleRole.SpinButton: return Accessibility.Role.SpinButton;
			case Gtk.AccessibleRole.Tab: return Accessibility.Role.NotebookTab;
			case Gtk.AccessibleRole.TabList: return Accessibility.Role.Notebook;
			case Gtk.AccessibleRole.Table:
			case Gtk.AccessibleRole.Grid: return Accessibility.Role.Table;
			case Gtk.AccessibleRole.TextBox: return Accessibility.Role.TextEntry;
			case Gtk.AccessibleRole.ToggleButton: return Accessibility.Role.ToggleButton;
			case Gtk.AccessibleRole.Toolbar: return Accessibility.Role.ToolBar;
			case Gtk.AccessibleRole.Tooltip: return Accessibility.Role.ToolTip;
			case Gtk.AccessibleRole.Tree:
			case Gtk.AccessibleRole.TreeGrid: return Accessibility.Role.Tree;
			case Gtk.AccessibleRole.Widget: return Accessibility.Role.Custom;
			default: return Accessibility.Role.None;
			}
		}
	}
}
