﻿using System;
using AppKit;
using Foundation;
using LogJoint.UI;

namespace LogJoint.UI.Postprocessing.StateInspector
{
	class PropertiesViewDelegate: NSTableViewDelegate
	{
		public StateInspectorWindowController owner;
		public NSTableView table;
		nint selectedRow = -1;

		public override NSView GetViewForItem (NSTableView tableView, NSTableColumn tableColumn, nint row)
		{
			var data = owner.PropsDataSource.data [(int)row];
			var paintInfo = owner.EventsHandler.OnPropertyCellPaint ((int)row);

			if (tableColumn == owner.PropKeyColumn) {
				var viewId = "name";
				var view = (NSTextField)tableView.MakeView (viewId, this);
				if (view == null)
					view = MakeLabel (viewId);
				if (paintInfo.AddLeftPadding)
					view.StringValue = "  " + data.Key;
				else
					view.StringValue = data.Key;
				return view;
			} else if (tableColumn == owner.PropValueColumn) {
				if (paintInfo.PaintAsLink) {
					var viewId = "link";
					var view = (NSLinkLabel)tableView.MakeView (viewId, this);
					if (view == null)
						view = new NSLinkLabel () {
							Identifier = viewId,
							RespectInteriorBackgroundStyle = true
						};
					view.StringValue = data.Value.ToString();
					view.LinkClicked = (s, e) => 
						owner.EventsHandler.OnPropertyCellClicked((int)row);
					return view;
				} else {
					var viewId = "val";
					var view = (NSTextField)tableView.MakeView (viewId, this);
					if (view == null)
						view = MakeLabel (viewId);
					view.StringValue = data.Value.ToString();
					return view;
				}
			}

			return null;
		}

		static NSTextField MakeLabel(string viewId)
		{
			var ret = new NSTextField () {
				Identifier = viewId,
				Editable = false,
				Selectable = false,
				Bordered = false,
				BackgroundColor = NSColor.Clear
			};
			ret.Cell.LineBreakMode = NSLineBreakMode.TruncatingTail;
			return ret;
		}

		void InvalidateValueCell (nint row)
		{
			if (row >= 0 && row < table.RowCount) {
				var v = table.GetView (1, row, false);
				if (v != null)
					v.NeedsDisplay = true;
			}
		}

		public override void SelectionDidChange (NSNotification notification)
		{
			InvalidateValueCell (table.SelectedRow);
			InvalidateValueCell (selectedRow);
			selectedRow = table.SelectedRow;
		}
	};
}