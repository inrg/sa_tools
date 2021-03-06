﻿using System;
using System.Collections.Generic;
using System.IO;

namespace SonicRetro.SAModel.GC
{
	[Serializable]
	public class GCAttach : Attach
	{
		public VertexData VertexData { get; private set; }
		public GeometryData GeometryData { get; private set; }

		public GCAttach()
		{
			Name = "gcattach_" + Extensions.GenerateIdentifier();

			VertexData = new VertexData();
			GeometryData = new GeometryData();
			Bounds = new BoundingSphere();
		}

		public GCAttach(byte[] file, int address, uint imageBase, Dictionary<int, string> labels)
		{
			if (labels.ContainsKey(address))
				Name = labels[address];
			else
				Name = "attach_" + address.ToString("X8");

			// The struct is 36/0x24 bytes long.

			VertexData = new VertexData();
			GeometryData = new GeometryData();

			uint vertex_attribute_offset = ByteConverter.ToUInt32(file, address) - imageBase;
			int unknown_1 = ByteConverter.ToInt32(file, address + 4);
			int opaque_geometry_data_offset = (int)(ByteConverter.ToInt32(file, address + 8) - imageBase);
			int translucent_geometry_data_offset = (int)(ByteConverter.ToInt32(file, address + 12) - imageBase);

			int opaque_geometry_count = ByteConverter.ToInt16(file, address + 16);
			int translucent_geometry_count = ByteConverter.ToInt16(file, address + 18);


			Bounds = new BoundingSphere(file, address + 20);
			VertexData.Load(file, vertex_attribute_offset, imageBase);

			if (opaque_geometry_count > 0)
			{
				GeometryData.Load(file, opaque_geometry_data_offset, imageBase, opaque_geometry_count, GeometryType.Opaque);
			}

			if (translucent_geometry_count > 0)
			{
				GeometryData.Load(file, translucent_geometry_data_offset, imageBase, translucent_geometry_count, GeometryType.Translucent);
			}
		}

		public void ExportOBJ(string file_name)
		{
			StringWriter writer = new StringWriter();

			if (VertexData.CheckAttribute(GXVertexAttribute.Position))
			{
				for (int i = 0; i < VertexData.Positions.Count; i++)
				{
					Vector3 pos = VertexData.Positions[i];
					writer.WriteLine($"v {pos.X} {pos.Y} {pos.Z}");
				}
			}

			if (VertexData.CheckAttribute(GXVertexAttribute.Normal))
			{
				for (int i = 0; i < VertexData.Normals.Count; i++)
				{
					Vector3 nrm = VertexData.Normals[i];
					writer.WriteLine($"vn {nrm.X} {nrm.Y} {nrm.Z}");
				}
			}

			if (VertexData.CheckAttribute(GXVertexAttribute.Tex0))
			{
				for (int i = 0; i < VertexData.TexCoord_0.Count; i++)
				{
					Vector2 tex = VertexData.TexCoord_0[i];
					writer.WriteLine($"vt {tex.X} {tex.Y}");
				}
			}

			int mesh_index = 0;

			foreach (Mesh m in GeometryData.OpaqueMeshes)
			{
				writer.WriteLine($"o mesh_{ mesh_index++ }");
				foreach (Primitive p in m.Primitives)
				{
					List<Vertex> triangles = p.ToTriangles();
					if (triangles == null)
						continue;

					for (int i = 0; i < triangles.Count; i += 3)
					{
						int pos_1 = (int)triangles[i].PositionIndex;
						int pos_2 = (int)triangles[i + 1].PositionIndex;
						int pos_3 = (int)triangles[i + 2].PositionIndex;

						string empty = "";

						int tex_1 = 0; int tex_2 = 0; int tex_3 = 0;
						int nrm_1 = 0; int nrm_2 = 0; int nrm_3 = 0;

						bool has_tex = VertexData.TexCoord_0.Count > 0;
						bool has_nrm = VertexData.Normals.Count > 0;

						if (has_tex)
						{
							tex_1 = (int)triangles[i].UVIndex;
							tex_2 = (int)triangles[i + 1].UVIndex;
							tex_3 = (int)triangles[i + 2].UVIndex;
						}
						if (has_nrm)
						{
							nrm_1 = (int)triangles[i].NormalIndex;
							nrm_2 = (int)triangles[i + 1].NormalIndex;
							nrm_3 = (int)triangles[i + 2].NormalIndex;
						}

						string v1 = $"{pos_1 + 1}{(has_tex ? "/" + tex_1.ToString() : empty) }{(!has_tex ? "/" : empty) + (has_nrm ? "/" + nrm_1.ToString() : empty)}";
						string v2 = $"{pos_2 + 1}{(has_tex ? "/" + tex_2.ToString() : empty) }{(!has_tex ? "/" : empty) + (has_nrm ? "/" + nrm_2.ToString() : empty)}";
						string v3 = $"{pos_3 + 1}{(has_tex ? "/" + tex_3.ToString() : empty) }{(!has_tex ? "/" : empty) + (has_nrm ? "/" + nrm_3.ToString() : empty)}";

						writer.WriteLine($"f { v1 } { v2 } { v3 }");
					}
				}
			}

			foreach (Mesh m in GeometryData.TranslucentMeshes)
			{
				foreach (Primitive p in m.Primitives)
				{
					List<Vertex> triangles = p.ToTriangles();
					if (triangles == null)
						continue;

					for (int i = 0; i < triangles.Count; i += 3)
					{
						int pos_1 = (int)triangles[i].PositionIndex;
						int pos_2 = (int)triangles[i + 1].PositionIndex;
						int pos_3 = (int)triangles[i + 2].PositionIndex;

						writer.WriteLine($"f {pos_1 + 1} {pos_2 + 1} {pos_3 + 1}");
					}
				}
			}

			File.WriteAllText(file_name, writer.ToString());
		}

		public override byte[] GetBytes(uint imageBase, bool DX, Dictionary<string, uint> labels, out uint address)
		{
			byte[] output;

			using (MemoryStream strm = new MemoryStream())
			{
				BinaryWriter gc_file = new BinaryWriter(strm);

				gc_file.Write(0);
				gc_file.Write(0);
				gc_file.Write(0);
				gc_file.Write(0);

				gc_file.Write((short)GeometryData.OpaqueMeshes.Count);
				gc_file.Write((short)GeometryData.TranslucentMeshes.Count);

				gc_file.Write(Bounds.Center.X);
				gc_file.Write(Bounds.Center.Y);
				gc_file.Write(Bounds.Center.Z);
				gc_file.Write(Bounds.Radius);

				VertexData.WriteVertexAttributes(gc_file, imageBase);
				GeometryData.WriteGeometryData(gc_file, imageBase);

				output = strm.ToArray();
			}

			address = 0;
			labels.Add(Name, imageBase);
			return output;
		}

		public override string ToStruct(bool DX)
		{
			throw new System.NotImplementedException();
		}

		public override void ToStructVariables(TextWriter writer, bool DX, List<string> labels, string[] textures = null)
		{
			throw new System.NotImplementedException();
		}

		NJS_MATERIAL cur_mat = new NJS_MATERIAL();
		public override void ProcessVertexData()
		{
			List<MeshInfo> meshInfo = new List<MeshInfo>();
			bool hasUV = VertexData.TexCoord_0.Count != 0;
			bool hasVColor = VertexData.Color_0.Count != 0;
			foreach (Mesh m in GeometryData.OpaqueMeshes)
			{
				meshInfo.Add(ProcessMesh(m, hasUV, hasVColor, false));
				cur_mat = new NJS_MATERIAL(cur_mat);
			}

			foreach (Mesh m in GeometryData.TranslucentMeshes)
			{
				meshInfo.Add(ProcessMesh(m, hasUV, hasVColor, true));
				cur_mat = new NJS_MATERIAL(cur_mat);
			}

			MeshInfo = meshInfo.ToArray();
		}

		private MeshInfo ProcessMesh(Mesh m, bool hasUV, bool hasVColor, bool useAlpha)
		{
			List<SAModel.VertexData> vertData = new List<SAModel.VertexData>();
			List<Poly> polys = new List<Poly>();

			foreach (Parameter param in m.Parameters)
			{
				if (param.ParameterType == ParameterType.Texture)
				{
					TextureParameter tex = param as TextureParameter;
					cur_mat.TextureID = tex.TextureID;
					if (!tex.Tile.HasFlag(TextureParameter.TileMode.MirrorU))
						cur_mat.FlipU = true;
					if (!tex.Tile.HasFlag(TextureParameter.TileMode.MirrorV))
						cur_mat.FlipV = true;
					if (!tex.Tile.HasFlag(TextureParameter.TileMode.WrapU))
						cur_mat.ClampU = true;
					if (!tex.Tile.HasFlag(TextureParameter.TileMode.WrapV))
						cur_mat.ClampV = true;

					cur_mat.ClampU &= tex.Tile.HasFlag(TextureParameter.TileMode.Unk_1);
					cur_mat.ClampV &= tex.Tile.HasFlag(TextureParameter.TileMode.Unk_1);
				}
				else if (param.ParameterType == ParameterType.TexCoordGen)
				{
					TexCoordGenParameter gen = param as TexCoordGenParameter;
					if (gen.TexGenSrc == GXTexGenSrc.Normal)
						cur_mat.EnvironmentMap = true;
					else cur_mat.EnvironmentMap = false;
				}
				else if (param.ParameterType == ParameterType.BlendAlpha)
				{
					BlendAlphaParameter blend = param as BlendAlphaParameter;
					cur_mat.SourceAlpha = blend.SourceAlpha;
					cur_mat.DestinationAlpha = blend.DestinationAlpha;
				}
			}

			foreach (Primitive prim in m.Primitives)
			{
				List<Poly> newPolys = new List<Poly>();
				switch (prim.PrimitiveType)
				{
					case GXPrimitiveType.Triangles:
						for (int i = 0; i < prim.Vertices.Count / 3; i++)
						{
							newPolys.Add(new Triangle());
						}
						break;
					case GXPrimitiveType.TriangleStrip:
						newPolys.Add(new Strip(prim.Vertices.Count, false));
						break;
				}

				for (int i = 0; i < prim.Vertices.Count; i++)
				{
					if (prim.PrimitiveType == GXPrimitiveType.Triangles)
					{
						newPolys[i / 3].Indexes[i % 3] = (ushort)vertData.Count;
					}
					else newPolys[0].Indexes[i] = (ushort)vertData.Count;

					vertData.Add(new SAModel.VertexData(
						VertexData.Positions[(int)prim.Vertices[i].PositionIndex],
						VertexData.Normals.Count > 0 ? VertexData.Normals[(int)prim.Vertices[i].NormalIndex] : new Vector3(0, 1, 0),
						hasVColor ? VertexData.Color_0[(int)prim.Vertices[i].Color0Index] : new GC.Color { R = 1, G = 1, B = 1, A = 1 },
						hasUV ? VertexData.TexCoord_0[(int)prim.Vertices[i].UVIndex] : new Vector2() { X = 0, Y = 0 }));
				}
				polys.AddRange(newPolys);
			}

			cur_mat.UseAlpha = useAlpha;
			var result = new MeshInfo(cur_mat, polys.ToArray(), vertData.ToArray(), hasUV, hasVColor);
			return result;
		}

		public override void ProcessShapeMotionVertexData(NJS_MOTION motion, int frame, int animindex)
		{
			throw new System.NotImplementedException();
		}

		public override Attach Clone()
		{
			throw new System.NotImplementedException();
		}
	}
}
