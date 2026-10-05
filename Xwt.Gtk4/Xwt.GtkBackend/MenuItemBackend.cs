// 
// MenuItemBackend.cs
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
using Xwt.Drawing;


namespace Xwt.GtkBackend
{
	/// <summary>
	/// GTK 4 menus are models: each Xwt menu item is a GLib.MenuItem (rebuilt by the parent
	/// <see cref="MenuBackend"/> whenever its presentation changes) bound to a GLib.SimpleAction
	/// named "xwt.item&lt;N&gt;" in the shared <see cref="ActionGroup"/>.
	/// </summary>
	public class MenuItemBackend: IMenuItemBackend
	{
		/// <summary>Prefix under which <see cref="ActionGroup"/> is installed on widgets.</summary>
		public const string ActionGroupName = "xwt";

		static int lastItemId;
		static GLib.SimpleActionGroup actionGroup;

		/// <summary>The action group holding the actions of all the menu items.</summary>
		public static GLib.SimpleActionGroup ActionGroup {
			get {
				if (actionGroup == null)
					actionGroup = new GLib.SimpleActionGroup ();
				return actionGroup;
			}
		}

		IMenuItemEventSink eventSink;
		ApplicationContext context;
		readonly string actionName;
		readonly GLib.SimpleAction action;
		GLib.SimpleAction submenuAction;
		bool clickEnabled;

		string label = string.Empty;
		bool useMnemonic = true;
		string tooltip;
		bool sensitive = true;
		bool visible = true;
		FormattedText formattedText;
		GLib.IIcon icon;
		MenuBackend submenu;

		public MenuItemBackend ()
		{
			actionName = "item" + (++lastItemId);
			action = CreateAction (actionName);
			action.Activated += HandleActivated;
			ActionGroup.AddAction (action);
		}

		/// <summary>Creates the action bound to the item (stateful for check and radio items).</summary>
		protected virtual GLib.SimpleAction CreateAction (string name)
		{
			return new GLib.SimpleAction (name, null);
		}

		/// <summary>The target value of the item (radio items), or null.</summary>
		protected virtual GLib.Variant ActionTarget {
			get { return null; }
		}

		protected GLib.SimpleAction Action {
			get { return action; }
		}

		/// <summary>The action name including the group prefix ("xwt.item&lt;N&gt;").</summary>
		public string DetailedActionName {
			get { return ActionGroupName + "." + actionName; }
		}

		/// <summary>The parent menu (null while the item is not in a menu).</summary>
		internal MenuBackend ParentMenu { get; set; }

		internal virtual bool IsSeparator {
			get { return false; }
		}

		/// <summary>
		/// A GLib.MenuItem describing the current state of this item. GTK 4: there is no menu item widget;
		/// the menu model copies the item, so every call returns a new description.
		/// </summary>
		public GLib.MenuItem MenuItem {
			get { return CreateMenuItem (); }
		}

		internal GLib.MenuItem CreateMenuItem ()
		{
			string text;
			bool markup = formattedText != null;
			if (markup)
				text = FormattedTextMarkup.ToMarkup (formattedText, false);
			else
				text = label ?? string.Empty;
			// menu model labels always interpret '_' as a mnemonic
			if (!useMnemonic)
				text = text.Replace ("_", "__");

			var item = new GLib.MenuItem (text, (string) null);
			if (submenu != null) {
				item.Submenu = submenu.MenuModel;
				item.SetAttributeValue ("submenu-action", new GLib.Variant (ActionGroupName + "." + SubmenuAction.Name));
			} else
				item.SetActionAndTargetValue (DetailedActionName, ActionTarget);
			if (markup)
				item.SetAttributeValue ("use-markup", new GLib.Variant (true));
			if (icon != null)
				item.Icon = icon;
			if (!string.IsNullOrEmpty (tooltip))
				item.SetAttributeValue ("tooltip", new GLib.Variant (tooltip));
			return item;
		}

		/// <summary>
		/// Boolean stateful action GTK uses for submenu items (opened state, sensitivity).
		/// </summary>
		GLib.SimpleAction SubmenuAction {
			get {
				if (submenuAction == null) {
					submenuAction = new GLib.SimpleAction (actionName + "-submenu", null, new GLib.Variant (false));
					submenuAction.Enabled = sensitive;
					ActionGroup.AddAction (submenuAction);
				}
				return submenuAction;
			}
		}

		void NotifyChanged ()
		{
			ParentMenu?.Rebuild ();
		}

		public void Initialize (IMenuItemEventSink eventSink)
		{
			this.eventSink = eventSink;
		}

		public void InitializeBackend (object frontend, ApplicationContext context)
		{
			this.context = context;
		}

		public void SetSubmenu (IMenuBackend menu)
		{
			submenu = (MenuBackend)menu;
			NotifyChanged ();
		}

		public void SetImage (ImageDescription image)
		{
			icon = null;
			if (!image.IsNull && image.Backend is GtkImage gtkImage) {
				// GTK 4: menu model icons are GIcons; serialize the rendered image as PNG bytes
				var idesc = image.WithDefaultSize (Xwt.IconSize.Small);
				var texture = gtkImage.ToTexture (context, GtkWorkarounds.GetDefaultScaleFactor (), idesc);
				if (texture != null)
					icon = new GLib.BytesIcon (texture.SaveToPngBytes ());
			}
			NotifyChanged ();
		}

		public string Label {
			get {
				return label;
			}
			set {
				formattedText = null;
				label = value ?? string.Empty;
				NotifyChanged ();
			}
		}

		public string TooltipText {
			get {
				return tooltip;
			}
			set {
				// GTK 4: shown only by toolkits that honour the "tooltip" menu attribute
				tooltip = value;
				NotifyChanged ();
			}
		}

		public bool UseMnemonic {
			get { return useMnemonic; }
			set {
				useMnemonic = value;
				NotifyChanged ();
			}
		}

		public bool Sensitive {
			get {
				return sensitive;
			}
			set {
				sensitive = value;
				action.Enabled = value;
				if (submenuAction != null)
					submenuAction.Enabled = value;
			}
		}

		public bool Visible {
			get {
				return visible;
			}
			set {
				if (visible == value)
					return;
				visible = value;
				NotifyChanged ();
			}
		}

		/// <summary>Checked state of check and radio items (always false for normal items).</summary>
		public virtual bool Checked {
			get { return false; }
			set { }
		}

		public void SetFormattedText (FormattedText text)
		{
			formattedText = text;
			label = text?.Text ?? string.Empty;
			NotifyChanged ();
		}

		public void EnableEvent (object eventId)
		{
			if (eventId is MenuItemEvent && (MenuItemEvent)eventId == MenuItemEvent.Clicked)
				clickEnabled = true;
		}

		public void DisableEvent (object eventId)
		{
			if (eventId is MenuItemEvent && (MenuItemEvent)eventId == MenuItemEvent.Clicked)
				clickEnabled = false;
		}

		void HandleActivated (object sender, GLib.ActivatedArgs args)
		{
			OnActivated (args.Parameter);
			if (clickEnabled && eventSink != null)
				context.InvokeUserCode (eventSink.OnClicked);
		}

		/// <summary>Updates the action state on activation (check and radio items).</summary>
		protected virtual void OnActivated (GLib.Variant parameter)
		{
		}

		public void Dispose ()
		{
			if (actionGroup != null) {
				actionGroup.RemoveAction (actionName);
				if (submenuAction != null)
					actionGroup.RemoveAction (submenuAction.Name);
			}
			action.Activated -= HandleActivated;
			submenuAction = null;
			submenu = null;
		}
	}
}
