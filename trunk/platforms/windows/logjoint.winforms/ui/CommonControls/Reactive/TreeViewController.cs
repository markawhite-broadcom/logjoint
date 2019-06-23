﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using LogJoint.UI.Presenters.Reactive;

namespace LogJoint.UI.Windows.Reactive
{
	class TreeViewController : ITreeViewController
	{
		readonly TreeView treeView;
		readonly Dictionary<ITreeNode, ViewNode> nodeToViewNodes = new Dictionary<ITreeNode, ViewNode>();
		ITreeNode currentRoot = EmptyTreeNode.Instance;
		bool updating;

		public TreeViewController(TreeView treeView)
		{
			this.treeView = treeView;
			treeView.BeforeExpand += (s, e) =>
			{
				if (updating)
					return;
				e.Cancel = true;
				OnExpand?.Invoke((e.Node as ViewNode)?.Node);
			};
			treeView.BeforeCollapse += (s, e) =>
			{
				if (updating)
					return;
				e.Cancel = true;
				OnCollapse?.Invoke((e.Node as ViewNode)?.Node);
			};
			treeView.BeforeSelect += (s, e) =>
			{
				if (updating)
					return;
				var n = (e.Node as ViewNode)?.Node;
				OnSelect?.Invoke(n != null ? new[] { n } : new ITreeNode[0]);
			};
		}

		public Action<ITreeNode[]> OnSelect { get; set; }
		public Action<ITreeNode> OnExpand { get; set; }
		public Action<ITreeNode> OnCollapse { get; set; }
		public Action<TreeNode, ITreeNode, ITreeNode> OnUpdateNode { get; set; }

		public void Update(ITreeNode newRoot)
		{
			var finalizeActions = new List<Action>();
			bool ensureSelectedVisible = false;

			bool updateBegun = false;
			void BeginUpdate()
			{
				if (!updateBegun)
				{
					// Call ListView's BeginUpdate/EndUpdate only when needed
					// tree structure changed to avoid flickering when only selection changes.
					treeView.BeginUpdate();
					updateBegun = true;
				}
			}

			updating = true;
			try
			{
				var edits = TreeEdit.GetTreeEdits(currentRoot, newRoot);
				currentRoot = newRoot;

				foreach (var e in edits)
				{
					TreeNode node = e.Node == newRoot ? null : nodeToViewNodes[e.Node];
					TreeNodeCollection nodeChildren = e.Node == newRoot ? treeView.Nodes : node.Nodes;
					switch (e.Type)
					{
						case TreeEdit.EditType.Insert:
							BeginUpdate();
							var insertedNode = CreateViewNode(e.NewChild);
							nodeChildren.Insert(e.ChildIndex, insertedNode);
							break;
						case TreeEdit.EditType.Delete:
							BeginUpdate();
							var deletedNode = nodeChildren[e.ChildIndex];
							nodeChildren.RemoveAt(e.ChildIndex);
							Debug.Assert(deletedNode == nodeToViewNodes[e.OldChild]);
							nodeToViewNodes.Remove(e.OldChild);
							DeleteDescendantsFromMap(deletedNode);
							break;
						case TreeEdit.EditType.Reuse:
							var nodeToReuse = nodeToViewNodes[e.OldChild];
							Rebind(nodeToReuse, e.NewChild);
							UpdateViewNode(nodeToReuse, e.NewChild, e.OldChild);
							break;
						case TreeEdit.EditType.Expand:
							BeginUpdate();
							finalizeActions.Add(node.Expand);
							break;
						case TreeEdit.EditType.Collapse:
							BeginUpdate();
							finalizeActions.Add(node.Collapse);
							break;
						case TreeEdit.EditType.Select:
							if (treeView.SelectedNode != node)
							{
								treeView.SelectedNode = node;
								ensureSelectedVisible = true;
							}
							break;
						case TreeEdit.EditType.Deselect:
							if (treeView.SelectedNode == node)
								treeView.SelectedNode = null;
							break;
					}
				}
			}
			finally
			{
				if (updateBegun)
				{
					treeView.EndUpdate();
				}
				finalizeActions.ForEach(a => a());
				if (treeView.SelectedNode is ViewNode selected && !selected.Node.IsSelected)
				{
					// When no nodes are explicitly selected, the OS selects one for us,
					// and we have to undo that.
					// Another scenario is that a node that was optimistically allowed to be selected in BeforeSelect
					// turned out not be selected.
					treeView.SelectedNode = null;
				}
				updating = false;
			}
			if (ensureSelectedVisible)
			{
				treeView.SelectedNode?.EnsureVisible();
			}
		}

		ViewNode CreateViewNode(ITreeNode node)
		{
			var result = new ViewNode { Node = node };
			UpdateViewNode(result, node, null);
			nodeToViewNodes.Add(node, result);
			return result;
		}

		void Rebind(ViewNode viewNode, ITreeNode newNode)
		{
			nodeToViewNodes.Remove(viewNode.Node);
			viewNode.Node = newNode;
			nodeToViewNodes[newNode] = viewNode;
		}

		void DeleteDescendantsFromMap(TreeNode item)
		{
			foreach (ViewNode c in item.Nodes)
			{
				Debug.Assert(nodeToViewNodes.Remove(c.Node));
				DeleteDescendantsFromMap(c);
			}
		}

		void UpdateViewNode(TreeNode viewNode, ITreeNode newNode, ITreeNode oldNode)
		{
			if (OnUpdateNode != null)
			{
				OnUpdateNode(viewNode, newNode, oldNode);
			}
			else
			{
				var newText = newNode.ToString();
				if (oldNode == null || newText != oldNode.ToString())
					viewNode.Text = newText;
			}
		}

		class ViewNode : TreeNode
		{
			internal ITreeNode Node;
		};
	}
}
