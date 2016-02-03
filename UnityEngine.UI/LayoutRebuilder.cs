using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace UnityEngine.UI
{
	/// <summary>
	///   <para>Wrapper class for managing layout rebuilding of CanvasElement.</para>
	/// </summary>
	public class LayoutRebuilder : ICanvasElement
	{
		private RectTransform m_ToRebuild;

		private int m_CachedHashFromTransform;

		private static ObjectPool<LayoutRebuilder> s_Rebuilders;

		/// <summary>
		///   <para>See ICanvasElement.</para>
		/// </summary>
		public Transform transform
		{
			get
			{
				return this.m_ToRebuild;
			}
		}

		static LayoutRebuilder()
		{
			LayoutRebuilder.s_Rebuilders = new ObjectPool<LayoutRebuilder>(null, delegate(LayoutRebuilder x)
			{
				x.Clear();
			});
			RectTransform.reapplyDrivenProperties += new RectTransform.ReapplyDrivenProperties(LayoutRebuilder.ReapplyDrivenProperties);
		}

		private void Initialize(RectTransform controller)
		{
			this.m_ToRebuild = controller;
			this.m_CachedHashFromTransform = controller.GetHashCode();
		}

		private void Clear()
		{
			this.m_ToRebuild = null;
			this.m_CachedHashFromTransform = 0;
		}

		private static void ReapplyDrivenProperties(RectTransform driven)
		{
			LayoutRebuilder.MarkLayoutForRebuild(driven);
		}

		/// <summary>
		///   <para>Has the native representation of this LayoutRebuilder been destroyed?</para>
		/// </summary>
		public bool IsDestroyed()
		{
			return this.m_ToRebuild == null;
		}

		private static void StripDisabledBehavioursFromList(List<Component> components)
		{
			components.RemoveAll((Component e) => e is Behaviour && !((Behaviour)e).isActiveAndEnabled);
		}

		/// <summary>
		///   <para>Forces an immediate rebuild of the layout element and child layout elements affected by the calculations.</para>
		/// </summary>
		/// <param name="layoutRoot">The layout element to perform the layout rebuild on.</param>
		public static void ForceRebuildLayoutImmediate(RectTransform layoutRoot)
		{
			LayoutRebuilder layoutRebuilder = LayoutRebuilder.s_Rebuilders.Get();
			layoutRebuilder.Initialize(layoutRoot);
			layoutRebuilder.Rebuild(CanvasUpdate.Layout);
			LayoutRebuilder.s_Rebuilders.Release(layoutRebuilder);
		}

		/// <summary>
		///   <para>See ICanvasElement.Rebuild.</para>
		/// </summary>
		/// <param name="executing"></param>
		public void Rebuild(CanvasUpdate executing)
		{
			if (executing == CanvasUpdate.Layout)
			{
				this.PerformLayoutCalculation(this.m_ToRebuild, delegate(Component e)
				{
					(e as ILayoutElement).CalculateLayoutInputHorizontal();
				});
				this.PerformLayoutControl(this.m_ToRebuild, delegate(Component e)
				{
					(e as ILayoutController).SetLayoutHorizontal();
				});
				this.PerformLayoutCalculation(this.m_ToRebuild, delegate(Component e)
				{
					(e as ILayoutElement).CalculateLayoutInputVertical();
				});
				this.PerformLayoutControl(this.m_ToRebuild, delegate(Component e)
				{
					(e as ILayoutController).SetLayoutVertical();
				});
			}
		}

		private void PerformLayoutControl(RectTransform rect, UnityAction<Component> action)
		{
			if (rect == null)
			{
				return;
			}
			List<Component> list = ListPool<Component>.Get();
			rect.GetComponents(typeof(ILayoutController), list);
			LayoutRebuilder.StripDisabledBehavioursFromList(list);
			if (list.Count > 0)
			{
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i] is ILayoutSelfController)
					{
						action(list[i]);
					}
				}
				for (int j = 0; j < list.Count; j++)
				{
					if (!(list[j] is ILayoutSelfController))
					{
						action(list[j]);
					}
				}
				for (int k = 0; k < rect.childCount; k++)
				{
					this.PerformLayoutControl(rect.GetChild(k) as RectTransform, action);
				}
			}
			ListPool<Component>.Release(list);
		}

		private void PerformLayoutCalculation(RectTransform rect, UnityAction<Component> action)
		{
			if (rect == null)
			{
				return;
			}
			List<Component> list = ListPool<Component>.Get();
			rect.GetComponents(typeof(ILayoutElement), list);
			LayoutRebuilder.StripDisabledBehavioursFromList(list);
			if (list.Count > 0)
			{
				for (int i = 0; i < rect.childCount; i++)
				{
					this.PerformLayoutCalculation(rect.GetChild(i) as RectTransform, action);
				}
				for (int j = 0; j < list.Count; j++)
				{
					action(list[j]);
				}
			}
			ListPool<Component>.Release(list);
		}

		/// <summary>
		///   <para>Mark the given RectTransform as needing it's layout to be recalculated during the next layout pass.</para>
		/// </summary>
		/// <param name="rect">Rect to rebuild.</param>
		public static void MarkLayoutForRebuild(RectTransform rect)
		{
			if (rect == null)
			{
				return;
			}
			List<Component> list = ListPool<Component>.Get();
			RectTransform rectTransform = rect;
			while (true)
			{
				RectTransform rectTransform2 = rectTransform.parent as RectTransform;
				if (!LayoutRebuilder.ValidLayoutGroup(rectTransform2, list))
				{
					break;
				}
				rectTransform = rectTransform2;
			}
			if (rectTransform == rect && !LayoutRebuilder.ValidController(rectTransform, list))
			{
				ListPool<Component>.Release(list);
				return;
			}
			LayoutRebuilder.MarkLayoutRootForRebuild(rectTransform);
			ListPool<Component>.Release(list);
		}

		private static bool ValidLayoutGroup(RectTransform parent, List<Component> comps)
		{
			if (parent == null)
			{
				return false;
			}
			parent.GetComponents(typeof(ILayoutGroup), comps);
			LayoutRebuilder.StripDisabledBehavioursFromList(comps);
			return comps.Count > 0;
		}

		private static bool ValidController(RectTransform layoutRoot, List<Component> comps)
		{
			if (layoutRoot == null)
			{
				return false;
			}
			layoutRoot.GetComponents(typeof(ILayoutController), comps);
			LayoutRebuilder.StripDisabledBehavioursFromList(comps);
			return comps.Count > 0;
		}

		private static void MarkLayoutRootForRebuild(RectTransform controller)
		{
			if (controller == null)
			{
				return;
			}
			LayoutRebuilder layoutRebuilder = LayoutRebuilder.s_Rebuilders.Get();
			layoutRebuilder.Initialize(controller);
			if (!CanvasUpdateRegistry.TryRegisterCanvasElementForLayoutRebuild(layoutRebuilder))
			{
				LayoutRebuilder.s_Rebuilders.Release(layoutRebuilder);
			}
		}

		/// <summary>
		///   <para>See ICanvasElement.LayoutComplete.</para>
		/// </summary>
		public void LayoutComplete()
		{
			LayoutRebuilder.s_Rebuilders.Release(this);
		}

		/// <summary>
		///   <para>See ICanvasElement.GraphicUpdateComplete.</para>
		/// </summary>
		public void GraphicUpdateComplete()
		{
		}

		public override int GetHashCode()
		{
			return this.m_CachedHashFromTransform;
		}

		public override bool Equals(object obj)
		{
			return obj.GetHashCode() == this.GetHashCode();
		}

		public override string ToString()
		{
			return "(Layout Rebuilder for) " + this.m_ToRebuild;
		}
	}
}