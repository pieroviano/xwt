// 
// CellUtil.cs
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
using System.Runtime.InteropServices;
using Gtk;
using Xwt.Backends;
using TreeModel = Gtk.ITreeModel;

namespace Xwt.GtkBackend
{
	public static class CellUtil
	{
		class CellDataSource: ICellDataSource
		{
			TreeIter iter;
			TreeModel treeModel;

			public CellDataSource (TreeIter iter, TreeModel treeModel)
			{
				this.iter = iter;
				this.treeModel = treeModel;
			}

			public object GetValue (IDataField field)
			{
				return treeModel.GetValue (iter, field.Index);
			}
		}

		public static CellViewBackend CreateCellRenderer (ApplicationContext actx, Widget widget, ICellRendererTarget col, object target, ICellViewFrontend view)
		{
			CellViewBackend crd;

			if (view is ITextCellViewFrontend) {
				crd = new CustomCellRendererText ();
			}
			else if (view is ICheckBoxCellViewFrontend || view is IRadioButtonCellViewFrontend) {
				crd = new CustomCellRendererToggle ();
			}
			else if (view is IImageCellViewFrontend) {
				crd = new CustomCellRendererImage ();
			}
			else if (view is ICanvasCellViewFrontend) {
				crd = new CustomCellRenderer ();
			}
			else if (view is IComboBoxCellViewFrontend) {
				crd = new CustomCellRendererComboBox ();
			} else
				throw new NotSupportedException ("Unknown cell view type: " + view.GetType ());

			crd.Initialize (view, col, target);
			col.PackStart (target, crd.CellRenderer, view.Expands);
			col.SetCellDataFunc (target, crd.CellRenderer, (cellLayout, cell, treeModel, iter) => {
				crd.LoadData (treeModel, iter);
				col.OnCellDataLoaded (target, crd.CellRenderer, iter);
			});
			view.AttachBackend (widget, crd);
			return crd;
		}
		
		public static Gtk.Widget CreateCellRenderer (ApplicationContext actx, ICollection<CellView> views)
		{
			if (views.Count > 1) {
				var box = new Gtk.Box (Gtk.Orientation.Horizontal, 0);
				foreach (var v in views) {
					var w = CreateCellRenderer (actx, v);
					w.Hexpand = v.Expands;
					box.Append (w);
				}
				return box;
			}
			else
				return CreateCellRenderer (actx, views.First ());
		}
		
		public static Gtk.Widget CreateCellRenderer (ApplicationContext actx, CellView view)
		{
			if (view is TextCellView) {
				Gtk.Label lab = new Gtk.Label ();
				lab.Xalign = 0;
//				lab.Text = ((TextCellView)view).TextField;
				return lab;
			}
			throw new NotImplementedException ();
		}

		public static void SetModelValue (TreeModel store, Gtk.TreeIter it, int column, Type type, object value)
		{
			if (type == typeof(ObjectWrapper) && value != null)
				store.SetValue (it, column, new ObjectWrapper (value));
			else if (value is string)
				store.SetValue (it, column, (string)value);
			else
				store.SetValue (it, column, value ?? DBNull.Value);
		}

		public static object GetModelValue (TreeModel store, Gtk.TreeIter it, int column)
		{
			object val = store.GetValue (it, column);
			if (val is DBNull)
				return null;
			else if (val is ObjectWrapper)
				return ((ObjectWrapper)val).Object;
			else
				return val;
		}

		public static void SetCurrentEventRow (TreeViewBackend treeBackend, string path)
		{
			if (treeBackend != null) {
				var treeFrontend = (TreeView)treeBackend.Frontend;

				TreePosition toggledItem = null;

				var pathParts = path.Split (':').Select (int.Parse);

				foreach (int pathPart in pathParts) {
					toggledItem = treeFrontend.DataSource.GetChild (toggledItem, pathPart);
				}

				treeBackend.CurrentEventRow = toggledItem;
			}
		}

	}
	
	public interface ICellRendererTarget
	{
		void PackStart (object target, Gtk.CellRenderer cr, bool expand);
		void PackEnd (object target, Gtk.CellRenderer cr, bool expand);
		void AddAttribute (object target, Gtk.CellRenderer cr, string field, int column);
		void SetCellDataFunc (object target, Gtk.CellRenderer cr, Gtk.CellLayoutDataFunc dataFunc);
		Rectangle GetCellBounds (object target, Gtk.CellRenderer cr, Gtk.TreeIter iter);
		Rectangle GetCellBackgroundBounds (object target, Gtk.CellRenderer cr, Gtk.TreeIter iter);
		void SetCurrentEventRow (string path);
		Gtk.Widget EventRootWidget { get; }
		/// <summary>
		/// Finds the row under (ex, ey), given in <see cref="EventRootWidget"/> coordinates, if the point
		/// is inside renderer r. cx/cy are the same point in widget coordinates.
		/// </summary>
		bool GetCellPosition (Gtk.CellRenderer r, int ex, int ey, out int cx, out int cy, out Gtk.TreeIter iter);
		/// <summary>Called after a cell view loaded the data of a row, before the renderer draws it.</summary>
		void OnCellDataLoaded (object target, Gtk.CellRenderer cr, Gtk.TreeIter iter);
		void QueueDraw (object target, Gtk.TreeIter iter);
		void QueueResize (object target, Gtk.TreeIter iter);
		TreeModel Model { get; }
		Gtk.TreeIter PressedIter { get; set; }
		CellViewBackend PressedCell { get; set; }
	}

}
