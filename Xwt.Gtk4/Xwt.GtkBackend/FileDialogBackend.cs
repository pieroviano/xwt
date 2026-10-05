// 
// FileDialogBackend.cs
//  
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
// 
// Copyright (c) 2012 Xamarin Inc
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
using System.IO;
using Xwt.Backends;
using System.Linq;


namespace Xwt.GtkBackend
{
	/// <summary>
	/// File dialogs on <see cref="Gtk.FileDialog"/>. The GTK 4 dialog is asynchronous; <see cref="Run"/>
	/// waits for the result in a nested main loop.
	/// </summary>
	public class FileDialogBackend: IFileDialogBackend
	{
		Gtk.FileDialog dialog;
		Gtk.FileChooserAction action;
		bool multiselect;
		List<Gtk.FileFilter> gtkFilters = new List<Gtk.FileFilter> ();
		List<FileDialogFilter> filters = new List<FileDialogFilter> ();
		FileDialogFilter activeFilter;
		string currentFolder;
		string[] selectedFiles = new string [0];

		public ApplicationContext ApplicationContext { get; private set; }

		public FileDialogBackend (Gtk.FileChooserAction action)
		{
			this.action = action;
		}

		public void InitializeBackend (object frontend, ApplicationContext context)
		{
			ApplicationContext = context;
		}

		public void EnableEvent (object eventId)
		{
		}

		public void DisableEvent (object eventId)
		{
		}

		#region IFileDialogBackend implementation

		public void Initialize (bool multiselect)
		{
			this.multiselect = multiselect;
			dialog = new Gtk.FileDialog ();
			dialog.Modal = true;
			if (action == Gtk.FileChooserAction.SelectFolder)
				dialog.AcceptLabel = Application.TranslationCatalog.GetString ("Select");
		}

		public void Initialize (IEnumerable<FileDialogFilter> filters, bool multiselect, string initialFileName)
		{
			Initialize (multiselect);

			if (!string.IsNullOrEmpty (initialFileName))
				dialog.InitialName = initialFileName;

			this.filters = filters.ToList ();
			gtkFilters = new List<Gtk.FileFilter> ();

			foreach (var filter in this.filters) {
				var gf = new Gtk.FileFilter ();
				gtkFilters.Add (gf);
				if (!string.IsNullOrEmpty (filter.Name))
					gf.Name = filter.Name;
				if (filter.Patterns != null)
					foreach (var pattern in filter.Patterns)
						gf.AddPattern (pattern);
			}

			if (gtkFilters.Count > 0) {
				var store = new GLib.ListStore (Gtk.FileFilter.GType);
				foreach (var gf in gtkFilters)
					store.Append (gf.Handle);
				dialog.Filters = store;
			}
		}

		public string Title {
			get { return dialog.Title; }
			set { dialog.Title = value; }
		}

		/// <summary>
		/// The filter selected when the dialog opens. GTK 4: GtkFileDialog does not report the
		/// filter the user picked, so after <see cref="Run"/> this is still the initial filter.
		/// </summary>
		public FileDialogFilter ActiveFilter {
			get { return activeFilter; }
			set {
				activeFilter = value;
				int i = value != null ? filters.IndexOf (value) : -1;
				dialog.DefaultFilter = i >= 0 && i < gtkFilters.Count ? gtkFilters [i] : null;
			}
		}

		public bool Run (IWindowFrameBackend parent)
		{
			var p = parent != null ? ApplicationContext.Toolkit.GetNativeWindow (parent) as Gtk.Window : null;
			if (p == null)
				p = MessageService.GetDefaultModalParent ();

			selectedFiles = new string [0];
			var loop = new NestedLoop ();
			GLib.AsyncReadyCallback done = (source, res, data) => {
				try {
					selectedFiles = Finish (res);
				} catch (GLib.GException) {
					// dismissed or failed
					selectedFiles = new string [0];
				} finally {
					loop.Quit ();
				}
			};

			switch (action) {
			case Gtk.FileChooserAction.Save:
				dialog.Save (p, null, done);
				break;
			case Gtk.FileChooserAction.SelectFolder:
				if (multiselect)
					dialog.SelectMultipleFolders (p, null, done);
				else
					dialog.SelectFolder (p, null, done);
				break;
			default:
				if (multiselect)
					dialog.OpenMultiple (p, null, done);
				else
					dialog.Open (p, null, done);
				break;
			}
			loop.Run ();

			if (selectedFiles.Length == 0)
				return false;
			// GTK 4 reports only the chosen files; the current folder is the one containing them
			currentFolder = Path.GetDirectoryName (selectedFiles [0]) ?? selectedFiles [0];
			return true;
		}

		string[] Finish (GLib.IAsyncResult res)
		{
			switch (action) {
			case Gtk.FileChooserAction.Save:
				return ToPaths (dialog.SaveFinish (res));
			case Gtk.FileChooserAction.SelectFolder:
				return multiselect ? ToPaths (dialog.SelectMultipleFoldersFinish (res)) : ToPaths (dialog.SelectFolderFinish (res));
			default:
				return multiselect ? ToPaths (dialog.OpenMultipleFinish (res)) : ToPaths (dialog.OpenFinish (res));
			}
		}

		static string[] ToPaths (GLib.IFile file)
		{
			return file != null && file.Path != null ? new [] { file.Path } : new string [0];
		}

		static string[] ToPaths (GLib.IListModel files)
		{
			var result = new List<string> ();
			if (files == null)
				return result.ToArray ();
			for (uint n = 0; n < files.NItems; n++) {
				var file = GLib.FileAdapter.GetObject (files.GetItem (n), true);
				if (file != null && file.Path != null)
					result.Add (file.Path);
			}
			return result.ToArray ();
		}

		public void Cleanup ()
		{
			dialog?.Dispose ();
			dialog = null;
		}

		protected Gtk.FileDialog Dialog {
			get { return dialog; }
		}

		public string FileName {
			get { return selectedFiles.FirstOrDefault (); }
		}

		public string[] FileNames {
			get { return selectedFiles; }
		}

		public string Folder {
			get { return selectedFiles.FirstOrDefault (); }
		}

		public string[] Folders {
			get { return selectedFiles; }
		}

		public string CurrentFolder {
			get {
				return currentFolder;
			}
			set {
				currentFolder = value;
				if (!string.IsNullOrEmpty (value))
					dialog.InitialFolder = GLib.FileFactory.NewForPath (value);
			}
		}

		#endregion
	}

	public class OpenFileDialogBackend: FileDialogBackend, IOpenFileDialogBackend
	{
		public OpenFileDialogBackend (): base (Gtk.FileChooserAction.Open)
		{
		}
	}

	public class SaveFileDialogBackend: FileDialogBackend, ISaveFileDialogBackend
	{
		public SaveFileDialogBackend (): base (Gtk.FileChooserAction.Save)
		{
		}
	}

	public class SelectFolderDialogBackend: FileDialogBackend, ISelectFolderDialogBackend
	{
		bool canCreateFolders;

		public SelectFolderDialogBackend (): base (Gtk.FileChooserAction.SelectFolder)
		{
		}

		#region ISelectFolderDialogBackend implementation

		/// <summary>GTK 4: the folder dialog always offers folder creation; the value is only remembered.</summary>
		public bool CanCreateFolders {
			get { return canCreateFolders; }
			set { canCreateFolders = value; }
		}

		#endregion
	}
}
