﻿// 
// AstNode.cs
//
// Author:
//       Mike Krüger <mkrueger@novell.com>
// 
// Copyright (c) 2009 Novell, Inc (http://www.novell.com)
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
using System.Diagnostics;
using System.Linq;

namespace ICSharpCode.NRefactory.CSharp
{
	public abstract class DomNode
	{
		#region Null
		public static readonly DomNode Null = new NullAstNode ();
		
		sealed class NullAstNode : DomNode
		{
			public override NodeType NodeType {
				get {
					return NodeType.Unknown;
				}
			}
			
			public override bool IsNull {
				get {
					return true;
				}
			}
			
			public override S AcceptVisitor<T, S> (DomVisitor<T, S> visitor, T data)
			{
				return default (S);
			}
		}
		#endregion
		
		DomNode parent;
		DomNode prevSibling;
		DomNode nextSibling;
		DomNode firstChild;
		DomNode lastChild;
		Role role;
		
		public abstract NodeType NodeType {
			get;
		}
		
		public virtual bool IsNull {
			get {
				return false;
			}
		}
		
		public virtual DomLocation StartLocation {
			get {
				var child = firstChild;
				if (child == null)
					return DomLocation.Empty;
				return child.StartLocation;
			}
		}
		
		public virtual DomLocation EndLocation {
			get {
				var child = lastChild;
				if (child == null)
					return DomLocation.Empty;
				return child.EndLocation;
			}
		}
		
		public DomNode Parent {
			get { return parent; }
		}
		
		public Role Role {
			get { return role; }
		}
		
		public DomNode NextSibling {
			get { return nextSibling; }
		}
		
		public DomNode PrevSibling {
			get { return prevSibling; }
		}
		
		public DomNode FirstChild {
			get { return firstChild; }
		}
		
		public DomNode LastChild {
			get { return lastChild; }
		}
		
		public IEnumerable<DomNode> Children {
			get {
				DomNode next;
				for (DomNode cur = firstChild; cur != null; cur = next) {
					// Remember next before yielding cur.
					// This allows removing/replacing nodes while iterating through the list.
					next = cur.nextSibling;
					yield return cur;
				}
			}
		}
		
		/// <summary>
		/// Gets the first child with the specified role.
		/// Returns the role's null object if the child is not found.
		/// </summary>
		public T GetChildByRole<T>(Role<T> role) where T : DomNode
		{
			if (role == null)
				throw new ArgumentNullException("role");
			for (var cur = firstChild; cur != null; cur = cur.nextSibling) {
				if (cur.role == role)
					return (T)cur;
			}
			return role.NullObject;
		}
		
		public IEnumerable<T> GetChildrenByRole<T>(Role<T> role) where T : DomNode
		{
			DomNode next;
			for (DomNode cur = firstChild; cur != null; cur = next) {
				// Remember next before yielding cur.
				// This allows removing/replacing nodes while iterating through the list.
				next = cur.nextSibling;
				if (cur.role == role)
					yield return (T)cur;
			}
		}
		
		protected void SetChildByRole<T>(Role<T> role, T newChild) where T : DomNode
		{
			DomNode oldChild = GetChildByRole(role);
			if (oldChild != null)
				oldChild.ReplaceWith(newChild);
			else
				AddChild(newChild, role);
		}
		
		protected void SetChildrenByRole<T>(Role<T> role, IEnumerable<T> newChildren) where T : DomNode
		{
			// Evaluate 'newChildren' first, since it might change when we remove the old children
			// Example: SetChildren(role, GetChildrenByRole(role));
			if (newChildren != null)
				newChildren = newChildren.ToList();
			
			// remove old children
			foreach (DomNode node in GetChildrenByRole(role))
				node.Remove();
			// add new children
			if (newChildren != null) {
				foreach (T node in newChildren) {
					AddChild(node, role);
				}
			}
		}
		
		public void AddChild<T>(T child, Role<T> role) where T : DomNode
		{
			if (role == null)
				throw new ArgumentNullException("role");
			if (child == null || child.IsNull)
				return;
			if (this.IsNull)
				throw new InvalidOperationException("Cannot add children to null nodes");
			if (child.parent != null)
				throw new ArgumentException ("Node is already used in another tree.", "child");
			child.parent = this;
			child.role = role;
			if (firstChild == null) {
				lastChild = firstChild = child;
			} else {
				lastChild.nextSibling = child;
				child.prevSibling = lastChild;
				lastChild = child;
			}
		}
		
		public void InsertChildBefore<T>(DomNode nextSibling, T child, Role<T> role) where T : DomNode
		{
			if (role == null)
				throw new ArgumentNullException("role");
			if (nextSibling == null) {
				AddChild(child, role);
				return;
			}
			
			if (child == null || child.IsNull)
				return;
			if (child.parent != null)
				throw new ArgumentException ("Node is already used in another tree.", "child");
			if (nextSibling.parent != this)
				throw new ArgumentException ("NextSibling is not a child of this node.", "nextSibling");
			// No need to test for "Cannot add children to null nodes",
			// as there isn't any valid nextSibling in null nodes.
			
			child.parent = this;
			child.role = role;
			child.nextSibling = nextSibling;
			child.prevSibling = nextSibling.prevSibling;
			
			if (nextSibling.prevSibling != null) {
				Debug.Assert(nextSibling.prevSibling.nextSibling == nextSibling);
				nextSibling.prevSibling.nextSibling = child;
			} else {
				Debug.Assert(firstChild == nextSibling);
				firstChild = child;
			}
			nextSibling.prevSibling = child;
		}
		
		public void InsertChildAfter<T>(DomNode prevSibling, T child, Role<T> role) where T : DomNode
		{
			InsertChildBefore((prevSibling == null || prevSibling.IsNull) ? firstChild : prevSibling.nextSibling, child, role);
		}
		
		/// <summary>
		/// Removes this node from its parent.
		/// </summary>
		public void Remove()
		{
			if (parent != null) {
				if (prevSibling != null) {
					Debug.Assert(prevSibling.nextSibling == this);
					prevSibling.nextSibling = nextSibling;
				} else {
					Debug.Assert(parent.firstChild == this);
					parent.firstChild = nextSibling;
				}
				if (nextSibling != null) {
					Debug.Assert(nextSibling.prevSibling == this);
					nextSibling.prevSibling = prevSibling;
				} else {
					Debug.Assert(parent.lastChild == this);
					parent.lastChild = prevSibling;
				}
				parent = null;
				prevSibling = null;
				nextSibling = null;
			}
		}
		
		/// <summary>
		/// Replaces this node with the new node.
		/// </summary>
		public void ReplaceWith(DomNode newNode)
		{
			if (newNode == null || newNode.IsNull) {
				Remove();
				return;
			}
			if (newNode.parent != null) {
				// TODO: what if newNode is used within *this* tree?
				// e.g. "parenthesizedExpr.ReplaceWith(parenthesizedExpr.Expression);"
				// We'll probably want to allow that.
				throw new ArgumentException ("Node is already used in another tree.", "newNode");
			}
			// Because this method doesn't statically check the new node's type with the role,
			// we perform a runtime test:
			if (!role.IsValid(newNode)) {
				throw new ArgumentException (string.Format("The new node '{0}' is not valid in the role {1}", newNode.GetType().Name, role.ToString()), "newNode");
			}
			newNode.parent = parent;
			newNode.role = role;
			newNode.prevSibling = prevSibling;
			newNode.nextSibling = nextSibling;
			if (parent != null) {
				if (prevSibling != null) {
					Debug.Assert(prevSibling.nextSibling == this);
					prevSibling.nextSibling = newNode;
				} else {
					Debug.Assert(parent.firstChild == this);
					parent.firstChild = newNode;
				}
				if (nextSibling != null) {
					Debug.Assert(nextSibling.prevSibling == this);
					nextSibling.prevSibling = newNode;
				} else {
					Debug.Assert(parent.lastChild == this);
					parent.lastChild = newNode;
				}
				parent = null;
				prevSibling = null;
				nextSibling = null;
			}
		}
		
		public abstract S AcceptVisitor<T, S> (DomVisitor<T, S> visitor, T data);
		
		public static class Roles
		{
			// some pre defined constants for common roles
			public static readonly Role<Identifier> Identifier = new Role<Identifier>("Identifier", CSharp.Identifier.Null);
			
			public static readonly Role<BlockStatement> Body = new Role<BlockStatement>("Body", CSharp.BlockStatement.Null);
			public static readonly Role<ParameterDeclaration> Parameter = new Role<ParameterDeclaration>("Parameter");
			public static readonly Role<Expression> Argument = new Role<Expression>("Argument", CSharp.Expression.Null);
			public static readonly Role<DomType> Type = new Role<DomType>("Type", CSharp.DomType.Null);
			public static readonly Role<Expression> Expression = new Role<Expression>("Expression", CSharp.Expression.Null);
			public static readonly Role<Expression> TargetExpression = new Role<Expression>("Target", CSharp.Expression.Null);
			public readonly static Role<Expression> Condition = new Role<Expression>("Condition", CSharp.Expression.Null);
			
			public static readonly Role<TypeParameterDeclaration> TypeParameter = new Role<TypeParameterDeclaration>("TypeParameter");
			public static readonly Role<DomType> TypeArgument = new Role<DomType>("TypeArgument", CSharp.DomType.Null);
			public readonly static Role<Constraint> Constraint = new Role<Constraint>("Constraint");
			public static readonly Role<VariableInitializer> Variable = new Role<VariableInitializer>("Variable");
			public static readonly Role<Statement> EmbeddedStatement = new Role<Statement>("EmbeddedStatement", CSharp.Statement.Null);
			
			public static readonly Role<CSharpTokenNode> Keyword = new Role<CSharpTokenNode>("Keyword", CSharpTokenNode.Null);
			public static readonly Role<CSharpTokenNode> InKeyword = new Role<CSharpTokenNode>("InKeyword", CSharpTokenNode.Null);
			
			// some pre defined constants for most used punctuation
			public static readonly Role<CSharpTokenNode> LPar = new Role<CSharpTokenNode>("LPar", CSharpTokenNode.Null);
			public static readonly Role<CSharpTokenNode> RPar = new Role<CSharpTokenNode>("RPar", CSharpTokenNode.Null);
			public static readonly Role<CSharpTokenNode> LBracket = new Role<CSharpTokenNode>("LBracket", CSharpTokenNode.Null);
			public static readonly Role<CSharpTokenNode> RBracket = new Role<CSharpTokenNode>("RBracket", CSharpTokenNode.Null);
			public static readonly Role<CSharpTokenNode> LBrace = new Role<CSharpTokenNode>("LBrace", CSharpTokenNode.Null);
			public static readonly Role<CSharpTokenNode> RBrace = new Role<CSharpTokenNode>("RBrace", CSharpTokenNode.Null);
			public static readonly Role<CSharpTokenNode> LChevron = new Role<CSharpTokenNode>("LChevron", CSharpTokenNode.Null);
			public static readonly Role<CSharpTokenNode> RChevron = new Role<CSharpTokenNode>("RChevron", CSharpTokenNode.Null);
			public static readonly Role<CSharpTokenNode> Comma = new Role<CSharpTokenNode>("Comma", CSharpTokenNode.Null);
			public static readonly Role<CSharpTokenNode> Semicolon = new Role<CSharpTokenNode>("Semicolon", CSharpTokenNode.Null);
			public static readonly Role<CSharpTokenNode> Assign = new Role<CSharpTokenNode>("Assign", CSharpTokenNode.Null);
			
			public static readonly Role<Comment> Comment = new Role<Comment>("Comment");
			
		}
	}
}
