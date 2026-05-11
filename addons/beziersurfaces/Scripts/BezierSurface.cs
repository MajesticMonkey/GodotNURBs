using Godot;
using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using BezierSurfaces.Types.Matrix;
using BezierSurfaces.Types.VectorVariants.HalfVector;
using BezierSurfaces.Types.VectorVariants.ByteVector;
using BezierSurfaces.Types.VectorVariants.BitVector;
using BezierSurfaces;

using static System.Math;

namespace BezierSurfaces
{
	#region BezierSurfaceClass
		public partial class BezierSurface : MeshInstance3D
		{
			public Vector2 CNLoc = new Vector2(0, 0);
			
			public Byte LOD = 1;

			readonly BezierSurfaceBuilder parent;

			private bool Loading = false;
			private bool QueuedForReload = false;

			#region Lambda Expressions
				List<List<ControlPoint>> ControlNetwork => parent.ControlNetwork;
				//ShaderMaterial NormalShower => parent.NormalShower;
				ByteVector2 CNSize => parent.CNSize;
				ByteVector2 SVMSize => parent.SVMSize;
				ByteVector2 SSize => parent.SSize;
				Matrix NB => parent.NB;
				Matrix MB => parent.MB;
				Matrix NBD => parent.NBD;
				Matrix MBD => parent.MBD;

				Vector2 ControlPointSpacing => parent.ControlPointSpacing;

				String BezierPrefix => parent.BezierPrefix;
			#endregion
			
			
			public BezierSurface()
			{
				// This constructor is only used for the editor to create a preview of the surface. It should never be used for actual surfaces, and thus doesn't need to be fully initalized.
			}

			public BezierSurface(BezierSurfaceBuilder Parent, Vector2 CNLocation)
			{
				parent = Parent; // this MUST be initalized first. Other values use lambda expressions to hide the use of the parent pointer
				CNLoc = CNLocation;

				InitSelfMeshInstance();

				parent.AddChild(this, false, Node.InternalMode.Front); // Might cause issues that it's in this order.

				if (Engine.IsEditorHint())
				{
					AddTreeOwnership();
				}
				ReloadSurface();
			}
			
			public override void _Notification(int what)
			{
				if (Engine.IsEditorHint())
				{
					if (what == NotificationEditorPreSave)
					{
						SetOwner(null);
					}
					else if (what == NotificationEditorPostSave)
					{
						AddTreeOwnership();
					}
				}
			}
			
			private void AddTreeOwnership(){
				SetOwner(EditorInterface.Singleton.GetEditedSceneRoot());
			}

			public override void _Process(double delta)
			{
				if (Engine.IsEditorHint())
				{
					Godot.Collections.Array<Node> selection = EditorInterface.Singleton.GetSelection().GetSelectedNodes();
					for (int i = 0; i < selection.Count; i++)
					{
						if (selection[i] == this)
						{
							EditorInterface.Singleton.EditNode(GetParent());
						}
					}
				}
			}
				

			public async void ReloadSurface()
			{
				if (Loading)
				{
					QueuedForReload = true;
					GD.Print("Reloading!");
					return;
				}
				else
				{
					Loading = true;
				}

				ArrayMesh arrMesh = await CreateArrayMesh();

				Godot.Collections.Array<Node> children = GetChildren();
				for (int i = 0; i < children.Count; i++)
				{
					children[i].SetOwner(null);
					children[i].QueueFree();
				}

				

				this.SetMesh(arrMesh);

				this.CreateTrimeshCollision();

				children = GetChildren();
				for (int i = 0; i < children.Count; i++)
				{
					if (children[i] is Node3D node3DChild)
					{
						node3DChild.SetOwner(null);
						node3DChild.Hide();
					}
				}


				Loading = false;
				if (QueuedForReload)
				{
					QueuedForReload = false;
					ReloadSurface();
				}
			}

			private Vector3[,] GetControlNodes()
			{
				Vector3[,] CN = new Vector3[CNSize.X, CNSize.Y];
				for (int i = 0; i + CNLoc.X < ControlNetwork.Count && i < CNSize.X; i++)
				{
					for (int j = 0; j + CNLoc.Y < ControlNetwork[i + (int)CNLoc.X].Count && j < CNSize.Y; j++)
					{
						CN[i, j] = ControlNetwork[i + (int)CNLoc.X][j + (int)CNLoc.Y].Position;
					}
				}
				return CN;
			}
			
			public bool IsMyControlNode(Vector2 CPLoc)
			{
				float dx = CPLoc.X - CNLoc.X;
				float dy = CPLoc.Y - CNLoc.Y;
				return ((dx >= 0) && (dy >= 0) && (dx < CNSize.X) && (dy < CNSize.Y));
			}

			private void InitSelfMeshInstance()
			{
				Name = BezierPrefix + CNLoc.X.ToString() + "_" + CNLoc.Y.ToString();

				TopLevel = true;
			}
			
			#region Create Array Mesh
			private async Task<ArrayMesh> CreateArrayMesh()
			{
				var LODedSVMSize = GetLODedSVMSize();

				var tCN = GetControlNodes();

				var tNB = NB;
				var tMB = MB;
				var tNBD = NBD;
				var tMBD = MBD;

				Vector3[,] STM = await Task.Run(() => GetSurfaceTransforms(tNB, tMB, tCN, LODedSVMSize));
				Vector3[,] SNM = await Task.Run(() => GetSurfaceNormals(tNB, tMB, tNBD, tMBD, tCN, LODedSVMSize));

				ArrayMesh ArrMesh = new ArrayMesh();

				var SurfaceArray = new Godot.Collections.Array();

				SurfaceArray.Resize((int)Mesh.ArrayType.Max);

				SurfaceArray[(int)Mesh.ArrayType.Vertex] = WindTriangles(STM);
				SurfaceArray[(int)Mesh.ArrayType.Normal] = WindTriangles(SNM);

				
				ArrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, SurfaceArray);

				//ArrMesh.SurfaceSetMaterial(0, NormalShower);


				return ArrMesh;
			}

			#region Surface Calculations
			private static Vector3[,] GetSurfaceTransforms(Matrix NB, Matrix MB, Vector3[,] CN, ByteVector2 LODedSVMSize)
			{

				Vector3[,] STMForklift = new Vector3[LODedSVMSize.X, LODedSVMSize.Y];
				for (byte u = 0; u < LODedSVMSize.X; u++)
				{
					for (byte v = 0; v < LODedSVMSize.Y; v++)
					{
						STMForklift[u, v] = ComputeVertexVector(u, v, 0, NB, MB, CN, LODedSVMSize);
					}
				}
				return STMForklift;
			}
			
			private static Vector3[,] GetSurfaceNormals(Matrix NB, Matrix MB, Matrix NBD, Matrix MBD, Vector3[,] CN, ByteVector2 LODedSVMSize)
			{

				Vector3[,] SNMForklift = new Vector3[LODedSVMSize.X, LODedSVMSize.Y];				
				
				
				for (byte u = 0; u < LODedSVMSize.X; u++)
				{
					for (byte v = 0; v < LODedSVMSize.Y; v++)
					{
						SNMForklift[u, v] = ComputeVertexNormal(u, v, NB, MB, NBD, MBD, CN, LODedSVMSize);
					}
				}


				return SNMForklift;
			}

			private static Vector3 ComputeVertexNormal(byte u, byte v, Matrix NB, Matrix MB, Matrix NBD, Matrix MBD, Vector3[,] CN, ByteVector2 LODedSVMSize)
			{
				Vector3 TangentA = ComputeVertexVector(u, v, 1, NBD, MB, CN, LODedSVMSize);
				Vector3 TangentB = ComputeVertexVector(u, v, 2, NB, MBD, CN, LODedSVMSize);

				if (CN[0, 0].X < 0)
				{
					TangentA.X = Abs(TangentA.X);
					TangentA.Y = Abs(TangentA.Y);
					TangentA.Z = Abs(TangentA.Z);
				} else if (CN[0, 0].Z < 0)
				{
					TangentB.X = Abs(TangentB.X);
					TangentB.Y = Abs(TangentB.Y);
					TangentB.Z = Abs(TangentB.Z);
				}

				Vector3 Normal = TangentA.Cross(TangentB).Normalized();
				
				return Normal;
			}

			private static Vector3 ComputeVertexVector(byte u, byte v, int NormVer, Matrix tNB, Matrix tMB, Vector3[,] CN, ByteVector2 LODedSVMSize) // Calculates the transform or tangent vector of a given point on the bezier surface
			{
				// This code is going to be hard to read no matter what.
				// I've shorted down many of the names so that its compact, which makes it more readable in my opinion.
				Matrix powerBasisU = BezierSurfaceBuilder.PowerBasis(CN.GetLength(0));
				Matrix powerBasisV = BezierSurfaceBuilder.PowerBasis(CN.GetLength(1));

				float uF = (float)u / (float)(LODedSVMSize.X - 1);
				float vF = (float)v / (float)(LODedSVMSize.Y - 1);

				if (NormVer == 1) { powerBasisU = PowerDiv(powerBasisU); }
				if (NormVer == 2) { powerBasisV = PowerDiv(powerBasisV); }

				Matrix pU = PowsOfI(uF, powerBasisU).Transpose();
				Matrix pV = PowsOfI(vF, powerBasisV);
				
				Matrix cNX = new Matrix(CN.GetLength(0), CN.GetLength(1));
				Matrix cNY = new Matrix(CN.GetLength(0), CN.GetLength(1));
				Matrix cNZ = new Matrix(CN.GetLength(0), CN.GetLength(1));
				for (int i = 0; i < CN.GetLength(0); i++)
				{
					for (int j = 0; j < CN.GetLength(1); j++)
					{
						cNX[i, j] = CN[i, j].X;
						cNY[i, j] = CN[i, j].Y;
						cNZ[i, j] = CN[i, j].Z;
					}
				}

				Vector3 transform = new Vector3(0, 0, 0);
				
				Matrix pUProdTMB = pU.Product(tMB);
				Matrix tNBProdPV = tNB.Product(pV);

				transform.X = pUProdTMB.Product(cNX).Product(tNBProdPV)[0,0];
				transform.Y = pUProdTMB.Product(cNY).Product(tNBProdPV)[0,0];
				transform.Z = pUProdTMB.Product(cNZ).Product(tNBProdPV)[0,0];
				
				return transform;
			}
			#endregion

			private ByteVector2 GetLODedSVMSize()
			{ // Currently defunct method for Generating Different Levels of Detail based on distance.
				return new ByteVector2(SVMSize.X, SVMSize.Y);

				/*Byte X = (byte)Math.Ceiling((float)SVMSize.x / (float)LOD);
				Byte Y = (byte)Math.Ceiling((float)SVMSize.y / (float)LOD);
				ByteVector2 LODedSVMSize = new ByteVector2(X, Y);
				return LODedSVMSize;*/
			}
			#endregion

			#region Triangles
				private Vector3[] WindTriangles(Vector3[,] STM)
				{
					int PackRATLen = ((SVMSize.X - 1) * (SVMSize.Y - 1)) * 6;
					int n = 0;

					Vector3[] PackRAT = new Vector3[PackRATLen]; // Packed Reordered Array of Triangles
					for (int j = 0; j < SVMSize.Y - 1; j++)
					{
						for (int i = 0; i < SVMSize.X - 1; i++)
						{
							PackRAT[n++] = STM[i, j];
							PackRAT[n++] = STM[i+1, j];
							PackRAT[n++] = STM[i+1, j+1];
							
							PackRAT[n++] = STM[i+1, j+1];
							PackRAT[n++] = STM[i, j+1];
							PackRAT[n++] = STM[i, j];
						}
					}
					return PackRAT;
				}
			#endregion

			#region Pows
				static Matrix PowsOfI(float i, Matrix Pows)
				{
					Matrix PowedI = new Matrix(Pows.GetLength(0), 1);
					for (int j = 0; j < Pows.GetLength(0); j++)
					{ 
						PowedI[j, 0] = (float)Math.Pow((double)i, (double)Pows[j, 0]);
					}
					return PowedI;
				}

				static Matrix PowerDiv(Matrix Pows)
				{
					for (int i = 1; i < Pows.GetLength(0); i++)
					{
						Pows[i, 0] = Pows[i - 1, 0];
					}
					return Pows;
				}
			#endregion
		}
		#endregion
}
