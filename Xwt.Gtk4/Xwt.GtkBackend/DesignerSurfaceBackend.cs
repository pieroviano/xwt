// 
// DesignerSurfaceBackend.cs
//  
// Author:
//       lluis <${AuthorEmail}>
// 
// Copyright (c) 2011 lluis
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


namespace Xwt.GtkBackend
{
	public class DesignerSurfaceBackend: WidgetBackend, IDesignerSurfaceBackend
	{
		DesignerBox box;
		
		public DesignerSurfaceBackend ()
		{
			box = new DesignerBox ();
			Widget = box;
		}
		
		public void Load (Widget w)
		{
			var wb = (IGtkWidgetBackend) Toolkit.GetBackend (w);
			box.Child = wb.Widget;
		}
	}
	
	/// <summary>
	/// Shows the designed widget with an empty input-catching surface on top of it.
	/// </summary>
	class DesignerBox: Gtk.Overlay
	{
		Gtk.Widget surface;
		
		public DesignerBox ()
		{
			// an empty widget covering the whole content (overlay children fill by default)
			surface = new Gtk.Box (Gtk.Orientation.Horizontal, 0);
			AddOverlay (surface);
		}
	}
}
