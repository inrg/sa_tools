﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using SA_Tools;
using SonicRetro.SAModel.Direct3D;
using SonicRetro.SAModel.SAEditorCommon.UI;

namespace SonicRetro.SAModel.SAEditorCommon.DataTypes
{
	/// <summary>
	/// A linear (non-bezier) spline object that contains a series of knots.
	/// </summary>
	[Serializable]
	public class SplineData : Item
	{
		PathData splineData;

		#region Rendering Variables
		[NonSerialized]
		private CustomVertex.PositionColored[] vertices;
		[NonSerialized]
		private UInt16[] faceIndeces;

		[NonSerialized]
		private BoundingSphere bounds;
		[Browsable(false)]
		public override BoundingSphere Bounds { get	{ return bounds; }	}
		[NonSerialized]
		private Mesh mesh;

		public static NJS_MATERIAL SelectedMaterial { get; set; }
		public static NJS_MATERIAL UnSelectedMaterial { get; set; }

		[NonSerialized]
		private static Mesh vertexHandleMesh;
		[NonSerialized]
		private Sprite textSprite;
		#endregion

		#region Editor Variables
		private int selectedKnot = -1;
		#endregion

		#region Accessors
		public uint Code { get { return splineData.Code; } set { splineData.Code = value; } }
		#endregion

		#region Static and Const Variables
		public static PointHelper vertexHelper;

		private const float splineMeshRadius = 1f;
		#endregion

		#region Construction / Initialization
		public static void Init()
		{
			SelectedMaterial = new NJS_MATERIAL
			{
				DiffuseColor = Color.White,
				SpecularColor = Color.Black,
				UseAlpha = false,
				DoubleSided = true,
				Exponent = 10,
				IgnoreSpecular = false,
				UseTexture = false,
				IgnoreLighting = true
			};

			UnSelectedMaterial = new NJS_MATERIAL
			{
				DiffuseColor = Color.Maroon,
				SpecularColor = Color.Black,
				UseAlpha = false,
				DoubleSided = true,
				Exponent = 10,
				IgnoreSpecular = false,
				UseTexture = false,
				IgnoreLighting = true
			};

			vertexHelper = new PointHelper { HandleSize = 3f };
		}

		public SplineData(EditorItemSelection selectionManager)
			: base (selectionManager)
		{
			splineData = new PathData();

			selectionManager.SelectionChanged += selectionManager_SelectionChanged;
			vertexHelper.PointChanged += vertexHelper_PointChanged;
		}

		public SplineData(PathData splineData, EditorItemSelection selectionManager)
			: base(selectionManager)
		{
			this.splineData = splineData;

			selectionManager.SelectionChanged += selectionManager_SelectionChanged;
			vertexHelper.PointChanged += vertexHelper_PointChanged;
		}
		#endregion

		public void RebuildMesh(Device device)
		{
			List<CustomVertex.PositionColored> vertList = new List<CustomVertex.PositionColored>();
			List<ushort> faceIndexList = new List<ushort>();

			Vector3 up = new Vector3(0, 1, 0);

			#region Segment vert/face creation
			ushort highestFaceIndex = 0;

			for (int i = 0; i < splineData.Path.Count - 1; i++) // don't process the last knot
			{
				Vector3 thisKnot = new Vector3(splineData.Path[i].Position.X, splineData.Path[i].Position.Y, splineData.Path[i].Position.Z);
				Vector3 nextKnot = new Vector3(splineData.Path[i + 1].Position.X, splineData.Path[i + 1].Position.Y, splineData.Path[i + 1].Position.Z);

				Vector3 directionToNextKnot = Vector3.Normalize(nextKnot - thisKnot);
				Vector3 perpendicularDirection = Vector3.Cross(directionToNextKnot, up);

				// verts for knot 1
				CustomVertex.PositionColored vert1_1; // top vert 1 (0)
				CustomVertex.PositionColored vert1_2; // top vert 2 (1)
				CustomVertex.PositionColored vert1_3; // bottom vert 1 (2)
				CustomVertex.PositionColored vert1_4; // bottom vert 2 (3)

				// verts for knot 2
				CustomVertex.PositionColored vert2_1; // top vert 1 (4)
				CustomVertex.PositionColored vert2_2; // top vert 2 (5)
				CustomVertex.PositionColored vert2_3; // bottom vert 1 (6)
				CustomVertex.PositionColored vert2_4; // bottom vert 2 (7)

				// move top verts
				vert1_1 = new CustomVertex.PositionColored((thisKnot + (perpendicularDirection * splineMeshRadius)), Color.White.ToArgb());
				vert1_2 = new CustomVertex.PositionColored((thisKnot + (perpendicularDirection * (splineMeshRadius * -1))), Color.White.ToArgb());

				vert2_1 = new CustomVertex.PositionColored((nextKnot + (perpendicularDirection * splineMeshRadius)), Color.White.ToArgb());
				vert2_2 = new CustomVertex.PositionColored((nextKnot + (perpendicularDirection * (splineMeshRadius * -1))), Color.White.ToArgb());

				// move bottom verts
				vert1_3 = new CustomVertex.PositionColored(vert1_1.Position - (up * splineMeshRadius), Color.White.ToArgb());
				vert1_4 = new CustomVertex.PositionColored(vert1_2.Position - (up * splineMeshRadius), Color.White.ToArgb());

				vert2_3 = new CustomVertex.PositionColored(vert2_1.Position - (up * splineMeshRadius), Color.White.ToArgb());
				vert2_4 = new CustomVertex.PositionColored(vert2_2.Position - (up * splineMeshRadius), Color.White.ToArgb());

				List<ushort> thisKnotFaceIndexes = new List<ushort> 
				{
					// far side
					4,0,6,
					6,2,0,

					// bottom
					6,2,3,
					3,7,6,

					// our side
					7,3,1,
					7,5,1,

					// top
					1,5,4,
					4,0,1
				};

				for (int faceIndx = 0; faceIndx < thisKnotFaceIndexes.Count(); faceIndx++)
				{
					thisKnotFaceIndexes[faceIndx] += (ushort)vertList.Count(); // this is the wrong approach because it's the verts we're indexing, not the faces!
					if (thisKnotFaceIndexes[faceIndx] > highestFaceIndex) highestFaceIndex = thisKnotFaceIndexes[faceIndx];
				}

				// add verts to vert list and faces to face list
				vertList.Add(vert1_1);
				vertList.Add(vert1_2);
				vertList.Add(vert1_3);
				vertList.Add(vert1_4);
				vertList.Add(vert2_1);
				vertList.Add(vert2_2);
				vertList.Add(vert2_3);
				vertList.Add(vert2_4);

				faceIndexList.AddRange(thisKnotFaceIndexes);
			}
			#endregion

			vertices = vertList.ToArray();
			faceIndeces = faceIndexList.ToArray();

			// build bounding sphere
			float radius = Geometry.ComputeBoundingSphere(vertices, CustomVertex.PositionColored.Format, out Vector3 center);
			bounds = new BoundingSphere(center.ToVertex(), radius);

			// build actual mesh from face index array and vbuf
			mesh = new Mesh(faceIndexList.Count() / 3, vertList.Count(), MeshFlags.Managed, CustomVertex.PositionColored.Format, device);

			// Apply the buffers
			mesh.SetVertexBufferData(vertices, LockFlags.None);
			mesh.IndexBuffer.SetData(faceIndeces, 0, LockFlags.None);

			// create a vertexHandle
			if (vertexHandleMesh == null) vertexHandleMesh = Mesh.Box(device, 1, 1, 1);

			textSprite = new Sprite(device); // todo: do we really have to create this so often? Look into storing a cache list statically?
		}

		/// <summary>
		/// Call this after changing a knot's position - it will set the distance of the previous knot.
		/// </summary>
		/// <param name="knotID"></param>
		public void CalcDistance(int knotID)
		{
			if (knotID == 0) return;

			// knot A is the previous knot, and the one we will be applying the distance value to
			Vector3 knotAPos = new Vector3(splineData.Path[knotID - 1].Position.X, splineData.Path[knotID - 1].Position.Y, splineData.Path[knotID - 1].Position.Z);
			Vector3 knotBPos = new Vector3(splineData.Path[knotID].Position.X, splineData.Path[knotID].Position.Y, splineData.Path[knotID].Position.Z);

			float distance = knotAPos.Distance(knotBPos);

			splineData.Path[knotID - 1].Distance = distance;
		}

		/// <summary>
		/// Add a new knot (in-editor)
		/// </summary>
		public void AddKnot()
		{
			PathDataEntry newKnot = new PathDataEntry(splineData.Path[splineData.Path.Count - 1].Position.X + 10, splineData.Path[splineData.Path.Count - 1].Position.Y, splineData.Path[splineData.Path.Count - 1].Position.Z);
			splineData.Path.Add(newKnot);
			CalcDistance(splineData.Path.Count - 1);
		}

		/// <summary>
		/// Add a new knot (in-editor)
		/// </summary>
		public void AddKnot(Vertex position)
		{
			PathDataEntry newKnot = new PathDataEntry(position.X, position.Y, position.Z);
			splineData.Path.Add(newKnot);
			CalcDistance(splineData.Path.Count - 1);
		}

		/// <summary>
		/// Add a pre-defined knot.
		/// </summary>
		/// <param name="knot"></param>
		public void AddKnot(PathDataEntry knot)
		{
			splineData.Path.Add(knot);
			CalcDistance(splineData.Path.Count - 1);
		}

		// override implementations go here!
		public override HitResult CheckHit(Vector3 Near, Vector3 Far, Viewport Viewport, Matrix Projection, Matrix View)
		{
			MatrixStack transform = new MatrixStack();

			#region Checking Vertex Handles
			if (Selected)
			{
				foreach (PathDataEntry splineVertex in splineData.Path)
				{
					transform.Push();

					transform.Translate(splineVertex.Position.X, splineVertex.Position.Y, splineVertex.Position.Z);

					HitResult hitResult = vertexHandleMesh.CheckHit(Near, Far, Viewport, Projection, View, transform);

					transform.Pop();

					if (hitResult.IsHit)
					{
						selectedKnot = splineData.Path.FindIndex(item => item == splineVertex);
						vertexHelper.SetPoint(splineData.Path[selectedKnot].Position);
						return hitResult;
					}
				}
			}
			#endregion

			transform.Push();
			return mesh.CheckHit(Near, Far, Viewport, Projection, View, transform);
		}

		public override List<RenderInfo> Render(Device dev, EditorCamera camera, MatrixStack transform)
		{
			if (!camera.SphereInFrustum(Bounds))
				return EmptyRenderInfo;

			List<RenderInfo> result = new List<RenderInfo>();

			RenderInfo outputInfo = new RenderInfo(mesh, 0, Matrix.Identity, (Selected) ? SelectedMaterial : UnSelectedMaterial, null, EditorOptions.RenderFillMode, Bounds);
			result.Add(outputInfo);

			textSprite.Begin(SpriteFlags.AlphaBlend);

			Matrix view = camera.ToMatrix();
			Matrix projection = Matrix.PerspectiveFovRH(camera.FOV, camera.Aspect, 1, camera.DrawDistance);

			if (Selected)
			{
				for (int vIndx = 0; vIndx < splineData.Path.Count(); vIndx++)
				{
					#region Draw Vertex IDs
					Vector3 screenCoordinates = Vector3.Project(new Vector3(splineData.Path[vIndx].Position.X, splineData.Path[vIndx].Position.Y, splineData.Path[vIndx].Position.Z),
						dev.Viewport, projection, view, Matrix.Identity);
					Vector3 altScrCoord = Vector3.Project(new Vector3(splineData.Path[vIndx].Position.X, splineData.Path[vIndx].Position.Y, splineData.Path[vIndx].Position.Z),
						dev.Viewport, dev.Transform.Projection, dev.Transform.View, Matrix.Identity);

					EditorOptions.OnscreenFont.DrawText(textSprite, vIndx.ToString(), new Point((int)(screenCoordinates.X), (int)(screenCoordinates.Y)), Color.White);
					#endregion

					#region Draw Vertex Handles
					transform.Push();

					transform.Translate(splineData.Path[vIndx].Position.X, splineData.Path[vIndx].Position.Y, splineData.Path[vIndx].Position.Z);

					result.Add(new RenderInfo(vertexHandleMesh, 0, transform.Top, UnSelectedMaterial, null, FillMode.Solid, new BoundingSphere(splineData.Path[vIndx].Position.X,
						splineData.Path[vIndx].Position.Y, splineData.Path[vIndx].Position.Z, 1f)));

					if (vIndx == selectedKnot) result.Add(new RenderInfo(vertexHandleMesh, 0, transform.Top, SelectedMaterial, null, FillMode.WireFrame, new BoundingSphere(splineData.Path[vIndx].Position.X,
	   splineData.Path[vIndx].Position.Y, splineData.Path[vIndx].Position.Z, 1f)));
					transform.Pop();
					#endregion
				}
			}

			textSprite.End();

			return result;
		}

		public override void Delete()
		{
			LevelData.LevelSplines.Remove(this);
		}

		public override void Paste()
		{
			throw new NotImplementedException();
		}

		[Browsable(false)]
		public override Vertex Position
		{
			get
			{
				return bounds.Center;
			}
			set
			{
				base.Position = value;
			}
		}

		public override Rotation Rotation
		{
			get
			{
                return new Rotation();
			}
			set
			{

			}
		}

        void selectionManager_SelectionChanged(EditorItemSelection sender)
		{
			if (sender.ItemCount != 1)
			{
				vertexHelper.Enabled = false;
			}
			else
			{
				if (Selected)
				{
					vertexHelper.Enabled = (selectedKnot != -1);
				}
			}
		}

		void vertexHelper_PointChanged(PointHelper sender)
		{
			if (Selected)
			{
				RebuildMesh(EditorOptions.Direct3DDevice);
			}
		}
	}
}