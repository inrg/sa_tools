﻿using System;
using System.Collections.Generic;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using SonicRetro.SAModel;
using SonicRetro.SAModel.Direct3D;
using SonicRetro.SAModel.SAEditorCommon.DataTypes;
using SonicRetro.SAModel.SAEditorCommon.SETEditing;
using Extensions = SonicRetro.SAModel.Direct3D.Extensions;
using Mesh = Microsoft.DirectX.Direct3D.Mesh;
using Object = SonicRetro.SAModel.Object;

namespace SADXObjectDefinitions.Common
{
	public abstract class ItemBoxBase : ObjectDefinition
	{
		protected Object model;
		protected Mesh[] meshes;
		protected int childindex;

		public override HitResult CheckHit(SETItem item, Vector3 Near, Vector3 Far, Viewport Viewport, Matrix Projection, Matrix View, MatrixStack transform)
		{
			transform.Push();
			transform.NJTranslate(item.Position.ToVector3());
			HitResult result = model.CheckHit(Near, Far, Viewport, Projection, View, transform, meshes);
			transform.Pop();
			return result;
		}

		public override List<RenderInfo> Render(SETItem item, Device dev, EditorCamera camera, MatrixStack transform)
		{
			List<RenderInfo> result = new List<RenderInfo>();
			((BasicAttach)model.Children[childindex].Attach).Material[0].TextureID = itemTexs[Math.Min(Math.Max((int)item.Scale.X, 0), 8)];
			transform.Push();
			transform.NJTranslate(item.Position.ToVector3());
			result.AddRange(model.DrawModelTree(dev, transform, ObjectHelper.GetTextures("OBJ_REGULAR"), meshes));
			if (item.Selected)
				result.AddRange(model.DrawModelTreeInvert(dev, transform, meshes));
			transform.Pop();
			return result;
		}

		internal int[] itemTexs = { 35, 72, 33, 32, 34, 71, 31, 73, 70 };

		internal int[] charTexs = { 31, 0, 4, 0, 0, 1, 3, 2 };

		private PropertySpec[] customProperties = new PropertySpec[] {
			new PropertySpec("Item", typeof(Item), "Extended", null, null, (o) => (Items)Math.Min(Math.Max((int)o.Scale.X, 0), 8), (o, v) => o.Scale.X = (int)v)
		};

		public override PropertySpec[] CustomProperties { get { return customProperties; } }
	}

	public class ItemBox : ItemBoxBase
	{
		public override void Init(ObjectData data, string name, Device dev)
		{
			model = ObjectHelper.LoadModel("Objects/Common/Item Box/Ground.sa1mdl");
			meshes = ObjectHelper.GetMeshes(model, dev);
			childindex = 2;
		}

		public override BoundingSphere GetBounds(SETItem item)
		{
			BoundingSphere bounds = new BoundingSphere(item.Position, model.Attach.Bounds.Radius);

			return bounds;
		}

		public override string Name { get { return "Item Box"; } }
	}

	public class FloatingItemBox : ItemBoxBase
	{
		public override void Init(ObjectData data, string name, Device dev)
		{
			model = ObjectHelper.LoadModel("Objects/Common/Item Box/Air.sa1mdl");
			meshes = ObjectHelper.GetMeshes(model, dev);
			childindex = 1;
		}

		public override BoundingSphere GetBounds(SETItem item)
		{
			BoundingSphere bounds = new BoundingSphere(item.Position, model.Attach.Bounds.Radius);

			return bounds;
		}

		public override string Name { get { return "Floating Item Box"; } }
	}

	public enum Items
	{
		SpeedUp,
		Invincibility,
		FiveRings,
		TenRings,
		RandomRings,
		Barrier,
		ExtraLife,
		Bomb,
		MagneticBarrier
	}
}